import {
  createScanGate,
  normalizeScannedCode,
  routeTargetsSender,
  type CourierRoute,
  type CourierRouteParcel,
  type ScanRefusal,
  type TransferCityChoice,
} from '@delivery/shared';
import { CameraView, useCameraPermissions } from 'expo-camera';
import * as Haptics from 'expo-haptics';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { Check, ChevronDown, Keyboard, ListOrdered, ScanLine, Trash2 } from 'lucide-react-native';
import { type ComponentRef, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Alert, Pressable, ScrollView, Text, useWindowDimensions, View } from 'react-native';
import Animated, { FadeIn, FadeInDown, FadeOut } from 'react-native-reanimated';
import { useI18n } from '@/i18n';
import { useTokens } from '@/theme/tenant-theme';
import { ApiError } from '@/lib/api';
import { ActionFeedbackLine, type Feedback } from '@/components/action-feedback';
import { DeliverPanel } from '@/components/deliver-panel';
import {
  isDeferred,
  useDispatch,
  useRemoveParcel,
  useRoutes,
  useScan,
  useTransferScan,
} from '@/lib/courier';
import { useRefetchOnFocus } from '@/lib/use-refetch-on-focus';
import { primeScanFeedback, scanFeedback } from '@/lib/scan-feedback';
import { routeStopOf } from '@/lib/route-stop';
import { describeFailure } from '@/lib/failure';
import { ScanResultCard } from '@/components/scan-result-card';
import { Button, Card, Chip, EmptyState, Field, Screen, Sheet, useToast } from '@/ui';

/**
 * LE SCANNER — M1 (D-073), refait par le chantier « Scanner » (D-141).
 *
 * L'écran du matin. Le livreur charge sa moto, passe chaque colis devant la
 * caméra, et part ; puis, en tournée, il scanne le colis qu'il a en main et le
 * livre. Le gabarit est celui de l'ancienne appli Wimo, du haut vers le bas :
 *
 *  1. **le circuit** — UN sélecteur compact (« CIR-2610-0010 · 3/12 livrés »)
 *     et, au toucher, ses circuits ouverts du type du menu. Les puces « Livraison
 *     0 » répétées (vieux brouillons vides) ont disparu ;
 *  2. **la caméra** — 35 % de la hauteur au plus, un viseur simple ;
 *  3. **le dernier colis lu** — sa carte, qui apparaît et s'efface en douceur ;
 *  4. **UNE rangée d'actions** au pied : « Démarrer ma tournée » tant que le
 *     circuit est en préparation, « Livrer » et « Autre statut » quand il est
 *     parti — jamais les deux, jamais l'une sur l'autre.
 *
 * **Le circuit suit le colis** (point 4) : un scan qui tombe sur un autre de
 * SES circuits ouverts y bascule l'écran, et le dit (« Circuit CIR-… »). Le
 * 03/10, l'écran montrait CIR-2610-0008 vide pendant que le colis lu était sur
 * CIR-2610-0010.
 *
 * **Le retour est HAPTIQUE et SONORE avant d'être visuel** : `scanFeedback()`,
 * préparé dès l'ouverture de l'écran (point 1). Les refus sont traduits par
 * MOTIF ; le message de l'API reste le secours.
 */

type Menu = 'delivery' | 'pickup';

const menuOf = (route: { kind: string }): Menu =>
  routeTargetsSender(route.kind) ? 'pickup' : 'delivery';

/**
 * Le dernier type choisi, pour la session : le bouton du scanner de la barre
 * basse ne dit pas d'où l'on vient, et rouvrir le scanner sur l'autre type à
 * chaque passage obligerait à rebasculer.
 */
let rememberedMenu: Menu | null = null;

/** Le colis de ce code sur ces circuits — par son code ou la référence du vendeur. */
function findOnRoutes(
  routes: CourierRoute[] | undefined,
  code: string,
): { route: CourierRoute; parcel: CourierRouteParcel } | undefined {
  for (const route of routes ?? []) {
    const parcel = route.parcels.find(
      (row) =>
        row.code.toUpperCase() === code || (row.externalCode ?? '').trim().toUpperCase() === code,
    );
    if (parcel) return { route, parcel };
  }
  return undefined;
}

const byNewest = (a: CourierRoute, b: CourierRoute) => b.createdAt.localeCompare(a.createdAt);

/**
 * LA CAMÉRA, 35 % de la hauteur au plus (point 5) — 30 % sous 700 points. Sur
 * un 360×640, le compte est serré : barre d'état 24, sélecteur 64, rangée
 * d'actions 68, barre basse 58 ; à 35 % la carte du colis (≈ 240) aurait
 * défilé, à 30 % elle tient entière. Un code-barres se lit très bien dans
 * 190 points de haut.
 */
const cameraHeight = (screen: number) => Math.round(screen * (screen < 700 ? 0.3 : 0.35));

/**
 * Durées du bandeau rouge (lot C, C1) : « Déjà scanné » se lit d'un coup d'œil
 * (~2 s, la consigne) ; un motif de refus est une phrase, il reste un peu plus.
 */
const DUPLICATE_BANNER_MS = 2000;
const REFUSAL_BANNER_MS = 4000;

export default function ScanScreen() {
  const { t } = useI18n();
  const tokens = useTokens();
  const router = useRouter();
  const toast = useToast();
  const { height } = useWindowDimensions();
  const [permission, requestPermission] = useCameraPermissions();
  const params = useLocalSearchParams<{ kind?: string }>();

  const draftRoutes = useRoutes('draft');
  const drafts = draftRoutes.data;
  const activeRoutes = useRoutes('active');
  // Le sélecteur de circuit ne propose plus un circuit clos par l'agence.
  useRefetchOnFocus(draftRoutes, activeRoutes);

  // Le son et le mode audio se préparent AVANT le premier scan (point 1).
  useEffect(() => {
    primeScanFeedback();
  }, []);

  /*
   * LE TYPE DU MENU — Livraison ou Ramassage (Recette 5, point 1). L'entrée
   * par un menu le fixe ; sinon le dernier choisi, sinon celui du brouillon
   * le plus récent, sinon la livraison.
   */
  const requested: Menu | null =
    params.kind === 'pickup' ? 'pickup' : params.kind === 'delivery' ? 'delivery' : null;
  const [menu, setMenuState] = useState<Menu>(
    () => requested ?? rememberedMenu ?? (drafts?.[0] ? menuOf(drafts[0]) : null) ?? 'delivery',
  );
  const [seenRequest, setSeenRequest] = useState(requested);
  if (requested !== seenRequest) {
    setSeenRequest(requested);
    if (requested) setMenuState(requested);
  }
  const setMenu = useCallback((next: Menu) => {
    rememberedMenu = next;
    setMenuState(next);
  }, []);

  /**
   * SES CIRCUITS OUVERTS du type du menu : les brouillons (en préparation)
   * d'abord, puis les circuits partis, les plus récents en tête.
   */
  const circuits = useMemo(
    () => [
      ...(drafts ?? []).filter((route) => menuOf(route) === menu).sort(byNewest),
      ...(activeRoutes.data ?? []).filter((route) => menuOf(route) === menu).sort(byNewest),
    ],
    [activeRoutes.data, drafts, menu],
  );

  /*
   * LE circuit montré. Par défaut : le premier qui porte des colis — un vieux
   * brouillon vide ne doit plus passer devant le circuit du jour.
   */
  const [shown, setShown] = useState<string | null>(null);
  const current =
    circuits.find((route) => route.code === shown) ??
    circuits.find((route) => route.parcelCount > 0) ??
    circuits[0];

  const scan = useScan();
  /** Lot I, I1 (D-211) : prendre le colis d'un autre livreur, après confirmation. */
  const transfer = useTransferScan();
  const dispatch = useDispatch();
  const removeParcel = useRemoveParcel(current?.status === 'draft' ? current.code : '');

  const [feedback, setFeedback] = useState<Feedback | null>(null);
  const [picking, setPicking] = useState(false);
  /**
   * Lot L (D-219), lot N (D-223) : le colis d'un autre — la ville à choisir
   * parmi les siennes ; `currentId`, sa destination quand il la couvre (la
   * garder ne change rien).
   */
  const [reroute, setReroute] = useState<{
    code: string;
    courier: string;
    from: string;
    cities: TransferCityChoice[];
    currentId: string | null;
  } | null>(null);

  /**
   * LE BANDEAU ROUGE DU REFUS — lot C, point C1. Pendant la composition d'un
   * circuit, rien d'autre ne s'affiche sous la caméra que la liste : un colis
   * déjà scanné ou refusé se dit par le son d'erreur et ce bandeau, posé SUR
   * le bas de la caméra (la liste ne saute pas), qui s'efface seul.
   */
  const [banner, setBanner] = useState<{ stamp: number; text: string } | null>(null);
  const bannerTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const flash = useCallback((text: string, ms: number) => {
    if (bannerTimer.current) clearTimeout(bannerTimer.current);
    setBanner({ stamp: Date.now(), text });
    bannerTimer.current = setTimeout(() => setBanner(null), ms);
  }, []);
  useEffect(
    () => () => {
      if (bannerTimer.current) clearTimeout(bannerTimer.current);
    },
    [],
  );

  /** Le colis scanné s'ajoute EN BAS : la liste y descend pour le montrer. */
  const listRef = useRef<ComponentRef<typeof ScrollView>>(null);
  const draftCount = current?.status === 'draft' ? current.parcelCount : 0;
  useEffect(() => {
    if (draftCount > 0) listRef.current?.scrollToEnd({ animated: true });
  }, [draftCount]);

  /** La saisie manuelle — repliée tant qu'on n'en a pas besoin (point 8). */
  const [typing, setTyping] = useState(false);
  const [typed, setTyped] = useState('');

  /**
   * LE VERROU DU SCANNER — Recette 5, point 3 (`createScanGate`) : une lecture
   * à la fois jusqu'à la réponse, 1,5 s de pause, le même code relu dans les
   * 3 s ignoré en silence.
   */
  const gate = useRef(createScanGate()).current;

  /** La carte du dernier colis lu. `stamp` relance son apparition à chaque lecture. */
  const [result, setResult] = useState<{
    stamp: number;
    parcel: CourierRouteParcel;
    kind: string;
    routeCode: string;
    actionable: boolean;
    tone: 'ok' | 'warn';
    message: string;
  } | null>(null);

  /**
   * LE CIRCUIT SUIT LE COLIS — point 4. Le colis lu est sur un autre de ses
   * circuits ouverts : l'écran y passe, et le dit.
   */
  const follow = useCallback(
    (route: { code: string; kind: string }) => {
      const target = menuOf(route);
      if (target !== menu) setMenu(target);
      if (route.code !== current?.code) {
        setShown(route.code);
        toast.show(t('scan.switched', { code: route.code }), 'info');
      }
    },
    [current?.code, menu, setMenu, t, toast],
  );

  const show = useCallback(
    (
      found: { parcel: CourierRouteParcel; route: { code: string; kind: string } },
      actionable: boolean,
      tone: 'ok' | 'warn',
      message: string,
    ) => {
      follow(found.route);
      setFeedback(null);
      setResult({
        stamp: Date.now(),
        parcel: found.parcel,
        kind: found.route.kind,
        routeCode: found.route.code,
        actionable,
        tone,
        message,
      });
    },
    [follow],
  );

  /**
   * UN COLIS DE PLUS DANS LE BROUILLON — lot C, point C1. Plus de grande carte
   * (montant, téléphone, Appeler, adresse : c'est la fiche) : le son, et la
   * ligne au bas de la liste. L'écran va sur le brouillon s'il était ailleurs.
   */
  const added = useCallback(
    (route: { code: string; kind: string }) => {
      follow(route);
      setResult(null);
      setFeedback(null);
      setBanner(null);
    },
    [follow],
  );

  /**
   * « LE PRENDRE DANS MA TOURNÉE » — lot I, I1 (D-211). Confirmé par le livreur :
   * le serveur retire le colis de la tournée de l'autre (qui est prévenu) et le
   * pose sur la sienne — sa tournée en cours, où il se livre tout de suite (la
   * carte s'ouvre), sinon son brouillon (la ligne s'ajoute).
   */
  const taking = useRef(false);
  const takeOver = useCallback(
    (code: string, destinationCityId?: string) => {
      // Un double appui sur « Le prendre » ne part qu'une fois (D-151).
      if (taking.current) return;
      taking.current = true;
      const release = () => {
        taking.current = false;
      };
      transfer
        .mutateAsync({ code, destinationCityId })
        .then((taken) => {
          scanFeedback('accepted');
          toast.show(
            taken.reroutedTo
              ? t('scan.transfer.doneRerouted', {
                  code: taken.added.code,
                  route: taken.route.code,
                  city: taken.reroutedTo,
                })
              : t('scan.transfer.done', { code: taken.added.code, route: taken.route.code }),
          );
          if (taken.route.status === 'active') {
            show({ parcel: taken.added, route: taken.route }, true, 'ok', t('scanCard.onMyRoute'));
          } else {
            added(taken.route);
          }
        })
        .catch((error: unknown) => {
          const failure = describeFailure(error, t);
          scanFeedback('refused');
          setResult(null);
          if (failure.severity === 'business') flash(failure.text, REFUSAL_BANNER_MS);
          else setFeedback(failure);
        })
        .finally(release);
    },
    [added, flash, show, t, toast, transfer],
  );

  const onScanned = useCallback(
    (raw: string) => {
      if (!gate.tryAcquire(raw)) return;
      const code = normalizeScannedCode(raw);
      const release = () => gate.release();

      // « DÉJÀ SCANNÉ » : il est dans un de SES brouillons. Le cache le sait.
      const inDraft = findOnRoutes(drafts, code);
      if (inDraft) {
        scanFeedback('duplicate');
        follow(inDraft.route);
        setResult(null);
        flash(t('scan.duplicateBanner', { code: inDraft.parcel.code }), DUPLICATE_BANNER_MS);
        release();
        return;
      }

      // Sur un de SES circuits PARTIS : c'est le colis qu'il va livrer.
      const onActive = findOnRoutes(activeRoutes.data, code);
      if (onActive) {
        scanFeedback('accepted');
        show(onActive, true, 'ok', t('scanCard.onMyRoute'));
        release();
        return;
      }

      scan
        .mutateAsync(code)
        .then((outcome) => {
          if (isDeferred(outcome)) {
            // Hors ligne : le geste est en file, l'écran avance.
            scanFeedback('queued');
            setResult(null);
            setFeedback({ tone: 'warn', text: t('scan.queued') });
            return;
          }

          scanFeedback('accepted');
          added(outcome.route);
        })
        .catch(async (error: unknown) => {
          const reason = error instanceof ApiError ? error.reason : undefined;

          // Son propre circuit, que le cache ne connaissait pas encore : on le
          // relit et on ouvre la carte (détail `ownRoute`, Recette 5 point 3).
          if (
            reason === 'parcel-other-route' &&
            error instanceof ApiError &&
            error.failure.details?.ownRoute
          ) {
            const fresh = await activeRoutes.refetch();
            const mine = findOnRoutes(fresh.data, code);
            if (mine) {
              scanFeedback('accepted');
              show(mine, true, 'ok', t('scanCard.onMyRoute'));
              return;
            }
          }

          /*
           * LE COLIS D'UN AUTRE LIVREUR, TRANSFÉRABLE — lot I, I1 (D-211). Le
           * refus nomme le porteur ; le téléphone propose de le prendre, et ne
           * le fait que sur confirmation (jamais au passage devant la caméra).
           */
          if (
            reason === 'parcel-other-route' &&
            error instanceof ApiError &&
            error.failure.details?.transferable === true &&
            !error.failure.details?.ownRoute
          ) {
            const holder = error.failure.details as {
              courierName?: string;
              routeCode?: string;
              destinationCityName?: string;
              rerouteCities?: TransferCityChoice[];
              transferCities?: TransferCityChoice[];
              destinationCityId?: string;
              destinationCovered?: boolean;
            };
            setResult(null);
            setFeedback(null);
            /*
             * VERS QUELLE VILLE ? — lot L (D-219), lot N (D-223). Ses villes
             * viennent du serveur (`transferCities`, la liste de la boîte web ;
             * à défaut, celle de D-219). Plusieurs : la feuille, TOUJOURS,
             * sa destination actuelle en tête quand il la couvre. Une seule :
             * la question, qui dit le changement s'il y en a un. Aucune (hors
             * de ses villes et rien de coché) : le refus, sans question.
             */
            const covered = holder.destinationCovered ?? !holder.rerouteCities;
            const cities = holder.transferCities ?? holder.rerouteCities ?? [];
            const from =
              holder.destinationCityName ??
              cities.find((city) => city.id === holder.destinationCityId)?.name ??
              '';
            if (cities.length > 1) {
              const currentId = covered ? (holder.destinationCityId ?? null) : null;
              setReroute({
                code,
                courier: holder.courierName ?? '',
                from,
                // La destination actuelle d'abord : la garder est l'appui le plus court.
                cities: [
                  ...cities.filter((city) => city.id === currentId),
                  ...cities.filter((city) => city.id !== currentId),
                ],
                currentId,
              });
              return;
            }
            // Lot N2 (D-224) : aucune ville cochée — il ne prend rien, même pour la ville de sa
            // fiche. La feuille de refus, sans question (le serveur refuserait `courier-no-city`).
            if (cities.length === 0 && holder.transferCities) {
              scanFeedback('refused');
              Alert.alert(t('scan.transfer.title'), t('scan.refusal.courier-no-city'));
              return;
            }
            // Couvert : rien à changer, rien à envoyer. Sinon, la seule ville où il ira.
            const only = covered ? undefined : cities[0];
            Alert.alert(
              t('scan.transfer.title'),
              t('scan.transfer.body', {
                code,
                courier: holder.courierName ?? '',
                route: holder.routeCode ?? '',
              }) +
                (only
                  ? `\n\n${t('scan.transfer.reroute', {
                      from,
                      to: only.name,
                    })}`
                  : ''),
              [
                { text: t('scan.transfer.cancel'), style: 'cancel' },
                { text: t('scan.transfer.confirm'), onPress: () => takeOver(code, only?.id) },
              ],
            );
            return;
          }

          /*
           * Recette 7, point 3 : un refus MÉTIER (colis inconnu, déjà scanné,
           * hors ville, statut) se lit en orange, sans identifiant ; une panne
           * (réseau, délai, 5xx) en rouge avec sa référence. `describeFailure`
           * traduit le motif connu et classe le reste sur le statut.
           */
          const duplicate = reason === 'parcel-already-scanned';
          const failure = describeFailure(error, t);

          scanFeedback(duplicate ? 'duplicate' : 'refused');
          setResult(null);
          /*
           * Lot C, point C1 : un REFUS (déjà scanné, autre livreur, statut…) est
           * un bandeau rouge qui s'efface ; une PANNE (réseau, 5xx) garde sa
           * ligne et sa référence pour le support (D-150).
           */
          if (duplicate) {
            setFeedback(null);
            flash(t('scan.duplicateBanner', { code }), DUPLICATE_BANNER_MS);
          } else if (failure.severity === 'business') {
            setFeedback(null);
            flash(failure.text, REFUSAL_BANNER_MS);
          } else {
            setFeedback(failure);
          }
        })
        .finally(release);
    },
    [activeRoutes, added, drafts, flash, follow, gate, scan, show, t, takeOver],
  );

  /** Le code TAPÉ rejoint le même chemin que le code LU ; la fenêtre du même code est oubliée. */
  const submitTyped = useCallback(() => {
    const code = typed.trim().toUpperCase();
    if (code.length < 3) return;

    gate.forget();
    onScanned(code);
    setTyped('');
  }, [gate, onScanned, typed]);

  /** Vers l'écran colis de la Recette 5 ; l'écran revient prêt à scanner. */
  const openParcel = (code: string, other = false) => {
    router.push(
      `/(courier)/parcel/${encodeURIComponent(code)}?from=scan${other ? '&other=1' : ''}`,
    );
    setResult(null);
  };

  const start = () => {
    if (!current) return;
    dispatch.mutate(current.code, {
      onSuccess: () => {
        void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
        setResult(null);
        setFeedback({ tone: 'ok', text: t('scan.started') });
      },
      // Aucune action perdue en silence (point 4 a).
      onError: (error) => {
        void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error);
        setResult(null);
        setFeedback(describeFailure(error, t));
      },
    });
  };

  if (!permission?.granted) {
    return (
      <Screen>
        <View className="flex-1 justify-center gap-6 px-8">
          <EmptyState icon={ScanLine} title={t('scan.title')} hint={t('scan.permission')} />
          {/* La permission est demandée AU MOMENT DU BESOIN, pas au lancement. */}
          <Button label={t('scan.permissionAsk')} onPress={() => void requestPermission()} full />
        </View>
      </Screen>
    );
  }

  const isDraft = current?.status === 'draft';
  const counter = current
    ? isDraft
      ? routeTargetsSender(current.kind)
        ? t('scan.counterPickup', { count: current.parcelCount })
        : t('scan.counter', { count: current.parcelCount })
      : t(routeTargetsSender(current.kind) ? 'scan.circuitPicked' : 'scan.circuitDelivered', {
          done: current.parcelCount - current.remainingCount,
          total: current.parcelCount,
        })
    : t('scan.noCircuitHint');

  /*
   * LA RANGÉE DU PIED — une seule. En préparation : « Démarrer ma tournée ».
   * Parti, avec un colis lu de CE circuit : « Livrer » et « Autre statut »
   * (un ramassage se valide sur son BON, pas colis par colis).
   */
  const deliverable =
    !isDraft &&
    result !== null &&
    result.actionable &&
    result.routeCode === current?.code &&
    !routeTargetsSender(result.kind);

  return (
    <Screen>
      {/* --- 1. Le circuit, et la saisie du code ------------------------------ */}
      <View className="flex-row items-center gap-2 px-4 pb-2 pt-2">
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('scan.chooseCircuit')}
          onPress={() => setPicking(true)}
          className="min-h-12 flex-1 flex-row items-center gap-2 rounded-2xl border border-border bg-card px-3 py-1.5"
          style={({ pressed }) => (pressed ? { opacity: 0.7 } : null)}
        >
          <View className="flex-1">
            <Text className="font-mono text-[16px] font-bold text-foreground" numberOfLines={1}>
              {current ? current.code : t('scan.noCircuit')}
            </Text>
            <Text className="text-[13px] text-muted-foreground" numberOfLines={1}>
              {current
                ? `${t(menu === 'pickup' ? 'scan.kindPickup' : 'scan.kindDelivery')} · ${counter}`
                : counter}
            </Text>
          </View>
          <ChevronDown size={20} className="text-muted-foreground" />
        </Pressable>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('scan.manual')}
          accessibilityState={{ selected: typing }}
          onPress={() => setTyping((open) => !open)}
          className={`h-12 w-12 items-center justify-center rounded-2xl border ${
            typing ? 'border-primary bg-primary' : 'border-border bg-card'
          }`}
        >
          <Keyboard size={22} className={typing ? 'text-primary-foreground' : 'text-foreground'} />
        </Pressable>
      </View>

      {/* --- 2. La caméra, compacte ------------------------------------------- */}
      <View
        className="mx-4 overflow-hidden rounded-2xl bg-muted"
        style={{ height: cameraHeight(height) }}
      >
        <CameraView
          style={{ flex: 1 }}
          facing="back"
          barcodeScannerSettings={{
            // Les formats réellement collés sur les colis : moins de faux
            // positifs (un QR de publicité au fond du bac) et une lecture plus
            // rapide.
            barcodeTypes: ['code128', 'code39', 'ean13', 'qr'],
          }}
          onBarcodeScanned={({ data }) => onScanned(data)}
        >
          {/* Un viseur simple : un cadre aux coins arrondis, le centre libre. */}
          <View className="flex-1 items-center justify-center">
            <View className="h-[45%] w-[72%] rounded-2xl border-2 border-primary" />
          </View>
        </CameraView>

        {banner ? (
          <Animated.View
            key={banner.stamp}
            entering={FadeIn.duration(120)}
            exiting={FadeOut.duration(200)}
            accessibilityLiveRegion="assertive"
            className="absolute inset-x-2 bottom-2 rounded-xl bg-destructive px-3 py-2"
          >
            <Text
              className="text-center text-[16px] font-semibold text-destructive-foreground"
              numberOfLines={2}
            >
              {banner.text}
            </Text>
          </Animated.View>
        ) : null}
      </View>

      {/* --- 3. Le dernier colis lu ------------------------------------------- */}
      <ScrollView
        keyboardShouldPersistTaps="handled"
        ref={listRef}
        className="flex-1"
        contentContainerClassName="gap-3 px-4 pb-3 pt-3"
      >
        {typing ? (
          <Animated.View entering={FadeInDown.duration(180)} className="gap-2">
            <Field
              label={t('scan.manual')}
              value={typed}
              onChangeText={setTyped}
              placeholder={t('scan.manualHint')}
              autoCapitalize="characters"
              autoCorrect={false}
              onSubmitEditing={submitTyped}
              returnKeyType="done"
              autoFocus
            />
            <Button label={t('scan.manualAdd')} onPress={submitTyped} full />
          </Animated.View>
        ) : null}

        {result ? (
          <Animated.View
            key={result.stamp}
            entering={FadeInDown.duration(220)}
            exiting={FadeOut.duration(150)}
          >
            <ScanResultCard
              parcel={result.parcel}
              kind={result.kind}
              routeCode={result.routeCode}
              tone={result.tone}
              message={result.message}
              onOpen={() => openParcel(result.parcel.code)}
              onClose={() => setResult(null)}
            />
            {/*
              LIVRER EN UN SEUL APPUI — chantier « Livrer en un appui » (D-156,
              MOB-04). L'action est SUR la carte : « Livrer · X MAD » livre tout
              de suite (chèque facultatif, espèces en direct), « Autre statut »
              ouvre la feuille des issues permises. Plus de détour par la fiche,
              qui reste à un appui sur le code pour consulter.
            */}
            {deliverable ? (
              <View className="pt-3">
                <DeliverPanel
                  key={result.parcel.id}
                  code={result.parcel.code}
                  codToCollect={result.parcel.codToCollect}
                  kind={result.kind}
                  statusId={result.parcel.statusId}
                  onDone={() => setResult(null)}
                />
              </View>
            ) : null}
          </Animated.View>
        ) : feedback || !isDraft || current.parcels.length === 0 ? (
          <Animated.View key={feedback?.text ?? 'ready'} entering={FadeIn.duration(200)}>
            <ActionFeedbackLine feedback={feedback} fallback={t('scan.hint')} center />
          </Animated.View>
        ) : null}

        {/*
          LA LISTE, ET ELLE SEULE — lot C, point C1. Pendant la composition :
          rang, code, destinataire, corbeille ; le dernier scanné en bas. C'est
          l'écran qu'on obtenait en changeant de circuit puis en revenant : la
          carte du dernier scan la masquait (liste rendue « sans carte » seulement), le
          changement de circuit remettait `result` à zéro. Plus de carte ici.
        */}
        {isDraft
          ? current.parcels.map((item) => {
              const stop = routeStopOf(current.kind, item);
              return (
                <Card key={item.id} className="flex-row items-center gap-3 p-3">
                  <View className="h-8 w-8 items-center justify-center rounded-full bg-muted">
                    <Text className="text-[14px] font-bold text-foreground">{item.sequence}</Text>
                  </View>
                  <View className="flex-1">
                    <Text className="font-mono text-[15px] text-foreground">{item.code}</Text>
                    <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
                      {`${stop.name}${stop.area ? ` · ${stop.area}` : ''}`}
                    </Text>
                  </View>
                  <Pressable
                    accessibilityRole="button"
                    accessibilityLabel={`${t('common.cancel')} ${item.code}`}
                    className="min-h-11 min-w-11 items-center justify-center"
                    hitSlop={8}
                    disabled={removeParcel.isPending}
                    onPress={() =>
                      removeParcel.mutate(item.id, {
                        onError: (error) => setFeedback(describeFailure(error, t)),
                      })
                    }
                  >
                    <Trash2 size={20} className="text-destructive" />
                  </Pressable>
                </Card>
              );
            })
          : null}
      </ScrollView>

      {/* --- 4. UNE rangée d'actions ------------------------------------------ */}
      {isDraft ? (
        <Animated.View entering={FadeIn.duration(200)} className="px-4 pb-3 pt-1">
          <Button
            label={t('scan.start')}
            onPress={start}
            disabled={current.parcelCount === 0}
            busy={dispatch.isPending}
            full
          />
        </Animated.View>
      ) : null}

      {/* --- « Le prendre ? » vers une de ses villes (lot L, D-219) ----------- */}
      <Sheet
        open={reroute !== null}
        onClose={() => setReroute(null)}
        title={t('scan.transfer.pickCity')}
      >
        {reroute ? (
          <>
            <Text className="px-2 pb-3 text-[15px] text-foreground">
              {t(
                reroute.currentId
                  ? 'scan.transfer.pickCityBodyCovered'
                  : 'scan.transfer.pickCityBody',
                {
                  code: reroute.code,
                  courier: reroute.courier,
                  from: reroute.from,
                },
              )}
            </Text>
            {reroute.cities.map((city) => {
              const current = city.id === reroute.currentId;
              return (
                <Pressable
                  key={city.id}
                  accessibilityRole="button"
                  accessibilityState={{ selected: current }}
                  onPress={() => {
                    const chosen = reroute;
                    setReroute(null);
                    takeOver(chosen.code, city.id);
                  }}
                  className={`mb-2 min-h-[56px] flex-row items-center gap-2 rounded-2xl border px-3 py-2 ${
                    current ? 'bg-primary/10 border-2 border-primary' : 'border-border'
                  }`}
                  style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
                >
                  {current ? <Check size={20} color={tokens.primary} /> : null}
                  <Text className="flex-1 text-[16px] font-semibold text-foreground">
                    {current ? t('scan.transfer.currentCity', { city: city.name }) : city.name}
                  </Text>
                </Pressable>
              );
            })}
            <Button
              label={t('scan.transfer.cancel')}
              tone="ghost"
              onPress={() => setReroute(null)}
              full
            />
          </>
        ) : null}
      </Sheet>

      {/* --- Le choix du circuit ---------------------------------------------- */}
      <Sheet open={picking} onClose={() => setPicking(false)} title={t('scan.chooseCircuit')}>
        <View className="flex-row gap-2 px-2 pb-3">
          <Chip
            label={t('scan.kindDelivery')}
            active={menu === 'delivery'}
            onPress={() => {
              setMenu('delivery');
              setShown(null);
            }}
          />
          <Chip
            label={t('scan.kindPickup')}
            active={menu === 'pickup'}
            onPress={() => {
              setMenu('pickup');
              setShown(null);
            }}
          />
        </View>

        {circuits.length === 0 ? (
          <Text className="px-2 pb-4 text-[15px] text-muted-foreground">
            {t('scan.noCircuitHint')}
          </Text>
        ) : (
          circuits.map((route) => {
            const selected = route.code === current?.code;
            const done = route.parcelCount - route.remainingCount;
            return (
              <Pressable
                key={route.code}
                accessibilityRole="button"
                accessibilityState={{ selected }}
                onPress={() => {
                  setShown(route.code);
                  setResult(null);
                  setPicking(false);
                }}
                className={`mb-2 min-h-[56px] flex-row items-center gap-3 rounded-2xl border px-3 py-2 ${
                  selected ? 'border-primary' : 'border-border'
                }`}
                style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
              >
                <View className="flex-1">
                  <Text className="font-mono text-[16px] font-bold text-foreground">
                    {route.code}
                  </Text>
                  <Text className="text-[13px] text-muted-foreground">
                    {route.status === 'draft' ? t('scan.draft') : t('scan.active')} ·{' '}
                    {route.status === 'draft'
                      ? t(routeTargetsSender(route.kind) ? 'scan.counterPickup' : 'scan.counter', {
                          count: route.parcelCount,
                        })
                      : t(
                          routeTargetsSender(route.kind)
                            ? 'scan.circuitPicked'
                            : 'scan.circuitDelivered',
                          { done, total: route.parcelCount },
                        )}
                  </Text>
                </View>
              </Pressable>
            );
          })
        )}

        {current ? (
          <Button
            label={t('scan.openCircuit')}
            icon={ListOrdered}
            tone="ghost"
            onPress={() => {
              setPicking(false);
              router.push(`/(courier)/circuit/${encodeURIComponent(current.code)}`);
            }}
            full
          />
        ) : null}
      </Sheet>
    </Screen>
  );
}

/** Les motifs que l'écran sait traduire. Le reste retombe sur le message API. */
export type KnownRefusal = ScanRefusal;
