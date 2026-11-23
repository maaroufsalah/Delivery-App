import {
  createScanGate,
  keepEligible,
  locateScannedCode,
  normalizeScannedCode,
} from '@delivery/shared';
import { CameraView, useCameraPermissions } from 'expo-camera';
import * as Haptics from 'expo-haptics';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import { Check, PackageCheck, ScanLine } from 'lucide-react-native';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Alert, FlatList, Pressable, Text, View } from 'react-native';
import { ActionFeedbackLine, type Feedback } from '@/components/action-feedback';
import { CartonTag, CartonTotal } from '@/components/carton-count';
import { useI18n } from '@/i18n';
import { useAction } from '@/lib/use-action';
import { usePickupNote, useRoutes } from '@/lib/courier';
import { useRefetchOnFocus } from '@/lib/use-refetch-on-focus';
import { useRouteGone } from '@/lib/use-route-gone';
import { primeScanFeedback, scanFeedback } from '@/lib/scan-feedback';
import {
  Button,
  Card,
  Chip,
  EmptyState,
  FadeIn,
  Screen,
  ScreenHeader,
  SkeletonCard,
  useToast,
} from '@/ui';

/**
 * VALIDER UN BON DE RAMASSAGE — retouches Anass, point 5.
 *
 * « Sur chaque bon du circuit : nom du vendeur visible ; validation au choix —
 * "Valider par scan" (scan des colis du bon, contrôle qu'ils appartiennent au
 * bon, alerte sinon) ou "Valider manuellement" (cocher les colis). »
 *
 * **Un écran, deux modes, une seule liste.** Ce qui change entre le scan et la
 * case à cocher, c'est la façon de cocher — pas ce qu'on coche, pas ce qu'on
 * envoie, pas ce que le serveur vérifie. Deux écrans auraient donné deux
 * définitions de « ce que j'ai en main », et la seconde aurait divergé.
 *
 * **Le contrôle d'appartenance est fait ICI ET au serveur.** Ici pour que le
 * livreur l'apprenne en une vibration, devant la personne, avant d'avoir chargé
 * le carton. Au serveur parce que c'est lui qui écrit, et qu'un écran qui a
 * raison n'est pas une garantie.
 *
 * **Le mode scan ne valide rien tout seul.** Il coche, comme le doigt : le
 * livreur voit ce qu'il a lu, complète à la main ce que le code-barres refuse
 * de rendre — une étiquette déchirée, ça arrive — et valide une fois. Un scan
 * qui enverrait colis par colis produirait autant d'allers-retours que de
 * cartons, sur la 3G d'un trottoir.
 */

function NotePickupScreenContent() {
  const { id, route: routeCode } = useLocalSearchParams<{ id: string; route: string }>();
  const { t } = useI18n();
  const router = useRouter();

  // Le bip prêt AVANT le premier scan du bon, comme au Scanner (D-141).
  useEffect(() => {
    primeScanFeedback();
  }, []);

  const active = useRoutes('active');
  const pickup = usePickupNote(routeCode ?? '');

  const [permission, requestPermission] = useCameraPermissions();
  const [mode, setMode] = useState<'manual' | 'scan'>('manual');
  const [picked, setPicked] = useState<string[]>([]);
  const [feedback, setFeedback] = useState<Feedback | null>(null);

  const gate = useRef(createScanGate()).current;

  const route = (active.data ?? []).find((row) => row.code === routeCode);
  const note = route?.notes.find((row) => row.id === id);
  // Relu à chaque retour ; circuit clos par l'agence entre-temps → on revient.
  useRefetchOnFocus(active);
  useRouteGone(!route && active.isFetched && !active.isFetching);

  /** Les colis DE CE BON, pris dans ceux de la tournée. */
  const parcels = useMemo(() => {
    if (!route || !note) return [];
    const belongs = new Set(note.parcelIds);
    return route.parcels.filter((parcel) => belongs.has(parcel.id));
  }, [note, route]);

  /*
   * Déjà RAMASSÉS lors d'un passage précédent (18) : cochés d'emblée — ils
   * sont dans la sacoche, les décocher les ferait retirer du bon.
   */
  const alreadyPicked = useMemo(
    () => parcels.filter((parcel) => parcel.statusId === 18).map((parcel) => parcel.id),
    [parcels],
  );
  const [seeded, setSeeded] = useState(false);
  if (!seeded && alreadyPicked.length > 0) {
    setSeeded(true);
    setPicked(alreadyPicked);
  }

  /*
   * CE QUI COMPTE ET CE QUI PART : les cochés ENCORE dans le bon — Recette 7,
   * point 1. « 32 / 6 cochés » venait de 32 identifiants d'un autre bon ; aucun
   * identifiant hors liste n'est plus compté ni envoyé (`keepEligible`).
   */
  const checkedIds = useMemo(
    () =>
      keepEligible(
        picked,
        parcels.map((parcel) => parcel.id),
      ),
    [picked, parcels],
  );
  const allPicked = parcels.length > 0 && checkedIds.length === parcels.length;
  const toggleAll = () => setPicked(allPicked ? [] : parcels.map((parcel) => parcel.id));
  const validation = useAction();
  const toast = useToast();

  const toggle = (parcelId: string) =>
    setPicked((current) =>
      current.includes(parcelId)
        ? current.filter((entry) => entry !== parcelId)
        : [...current, parcelId],
    );

  /**
   * UN CODE LU. Trois issues, et chacune a sa vibration.
   *
   * Le code-barres porte le CODE du colis, pas son identifiant : la
   * correspondance se fait sur la liste du bon, ce qui rend le contrôle
   * d'appartenance immédiat et hors-ligne.
   */
  const onScanned = useCallback(
    (raw: string) => {
      // Le verrou commun (Recette 5, point 3) : une lecture, une pause, et le
      // même code relu dans les 3 s ignoré — sinon un colis gardé devant
      // l'objectif sonnait « déjà coché » 1,2 s après avoir été coché.
      if (!gate.tryAcquire(raw)) return;
      const code = normalizeScannedCode(raw);
      // Le contrôle est local : la réponse est immédiate, la pause commence.
      gate.release();

      // Le code colis, ou la référence commande du vendeur (point 7 du 26/09).
      const found = parcels.find(
        (parcel) =>
          parcel.code.toUpperCase() === code ||
          (parcel.externalCode ?? '').trim().toUpperCase() === code,
      );

      if (!found) {
        /*
         * L'ALERTE. C'est le cas courant du terrain — deux vendeurs voisins,
         * deux piles — et il doit être NOMMÉ : « ce colis n'est pas sur ce
         * bon » se corrige, « erreur » ne se corrige pas. Recette 7, point 3 :
         * ce n'est PAS une panne — orange, pas rouge, aucun identifiant ; et si
         * le colis est sur un autre bon du MÊME circuit, on propose d'y aller.
         */
        scanFeedback('refused');
        const where = route
          ? locateScannedCode(route, note?.id ?? '', code)
          : ({ kind: 'not-on-route' } as const);

        if (where.kind === 'other-note') {
          const other = where.note;
          setFeedback({
            tone: 'warn',
            severity: 'business',
            text: t('notePickup.otherNote', { code, note: other.code }),
            action: {
              label: t('notePickup.openNote', { note: other.code }),
              onPress: () => router.push(`/(courier)/note/${other.id}?route=${routeCode ?? ''}`),
            },
          });
          return;
        }

        setFeedback({ tone: 'warn', severity: 'business', text: t('notePickup.notOnRoute') });
        return;
      }

      if (picked.includes(found.id)) {
        scanFeedback('duplicate');
        setFeedback({ tone: 'warn', text: t('notePickup.already', { code }) });
        return;
      }

      scanFeedback('accepted');
      setFeedback({ tone: 'ok', text: t('notePickup.scanned', { code }) });
      setPicked((current) => [...current, found.id]);
    },
    [gate, note?.id, parcels, picked, route, routeCode, router, t],
  );

  /*
   * VALIDER — Recette 5, point 5 (règle du retrait : point 7, validé par
   * Anass). Seuls les colis COCHÉS sont validés ; les autres sont retirés du
   * bon, et la confirmation le dit avec les nombres avant d'écrire quoi que ce
   * soit : « Valider 150 colis sur 200 ? Les 50 non cochés seront retirés du
   * bon. » Tout coché : pas de question, rien n'est retiré.
   */
  const send = () => {
    if (!note || checkedIds.length === 0) return;
    const count = checkedIds.length;

    void validation.run(
      () => pickup.mutateAsync({ noteId: note.id, parcelIds: checkedIds, removeUnpicked: true }),
      {
        onSuccess: () => {
          void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
          toast.show(t('notePickup.validated', { count, total: parcels.length }));
          router.back();
        },
        onError: (_message, failure) => setFeedback(failure),
      },
    );
  };

  const submit = () => {
    if (!note || checkedIds.length === 0) return;
    const missing = parcels.length - checkedIds.length;
    if (missing <= 0) {
      send();
      return;
    }

    Alert.alert(
      t('notePickup.partialTitle', { count: checkedIds.length, total: parcels.length }),
      t('notePickup.partialBody', { missing, count: checkedIds.length, total: parcels.length }),
      [
        { text: t('common.cancel'), style: 'cancel' },
        { text: t('notePickup.partialConfirm'), style: 'destructive', onPress: send },
      ],
    );
  };

  if (active.isLoading) {
    return (
      <Screen className="gap-3 px-5 pt-4">
        <SkeletonCard />
        <SkeletonCard />
      </Screen>
    );
  }

  if (!note) {
    return (
      <Screen>
        <ScreenHeader title={t('notePickup.title')} backHref={'/(courier)/pickups'} />
        <EmptyState icon={PackageCheck} title={t('notePickup.missing')} />
      </Screen>
    );
  }

  return (
    <Screen>
      {/* LE NOM DU VENDEUR en sous-titre : c'est chez lui qu'on est. */}
      <ScreenHeader title={note.clientName} subtitle={note.code} backHref={'/(courier)/pickups'} />

      <View className="gap-3 px-5 pb-3">
        <Text className="text-[15px] text-muted-foreground">
          {note.pickupAddress || note.cityName}
        </Text>

        <View className="flex-row gap-2">
          <Chip
            label={t('notePickup.manual')}
            active={mode === 'manual'}
            onPress={() => setMode('manual')}
          />
          <Chip
            label={t('notePickup.byScan')}
            active={mode === 'scan'}
            onPress={() => setMode('scan')}
          />
        </View>

        {feedback ? (
          <ActionFeedbackLine feedback={feedback} />
        ) : (
          <Text className="text-[15px] text-muted-foreground">
            {t('notePickup.counter', { done: checkedIds.length, total: parcels.length })}
          </Text>
        )}
        {/* « 3 colis · 7 cartons » — le compteur ci-dessus reste par colis. */}
        <CartonTotal lines={parcels} />
      </View>

      {mode === 'scan' ? (
        permission?.granted ? (
          <View className="mx-5 h-44 overflow-hidden rounded-2xl">
            <CameraView
              style={{ flex: 1 }}
              facing="back"
              barcodeScannerSettings={{
                barcodeTypes: ['code128', 'code39', 'ean13', 'qr'],
              }}
              onBarcodeScanned={({ data }) => onScanned(data)}
            >
              {/* Quatre équerres, comme l'écran de scan du matin (M4 bloc 4). */}
              <View className="flex-1 items-center justify-center">
                <View className="h-24 w-52">
                  <View className="absolute start-0 top-0 h-7 w-7 rounded-ss-2xl border-s-4 border-t-4 border-primary" />
                  <View className="absolute end-0 top-0 h-7 w-7 rounded-se-2xl border-e-4 border-t-4 border-primary" />
                  <View className="absolute bottom-0 start-0 h-7 w-7 rounded-es-2xl border-b-4 border-s-4 border-primary" />
                  <View className="absolute bottom-0 end-0 h-7 w-7 rounded-ee-2xl border-b-4 border-e-4 border-primary" />
                </View>
              </View>
            </CameraView>
          </View>
        ) : (
          <View className="gap-3 px-5 pb-3">
            <EmptyState icon={ScanLine} title={t('scan.title')} hint={t('scan.permission')} />
            <Button label={t('scan.permissionAsk')} onPress={() => void requestPermission()} full />
          </View>
        )
      ) : null}

      <FlatList
        keyboardShouldPersistTaps="handled"
        data={parcels}
        keyExtractor={(parcel) => parcel.id}
        contentContainerClassName="gap-2 px-5 pb-4 pt-3"
        ListEmptyComponent={<EmptyState icon={PackageCheck} title={t('notePickup.noParcels')} />}
        /*
          « TOUT COCHER » et « X / Y cochés » — Recette 5, point 5. Le cas
          courant est « tout est là » : un appui, puis on décoche les absents.
        */
        ListHeaderComponent={
          parcels.length > 0 ? (
            <Pressable
              accessibilityRole="checkbox"
              hitSlop={6}
              accessibilityState={{ checked: allPicked }}
              onPress={toggleAll}
              className="mb-1 min-h-12 flex-row items-center gap-3 rounded-2xl border border-border px-3"
            >
              <View
                className={`size-6 items-center justify-center rounded-md border ${
                  allPicked ? 'border-primary bg-primary' : 'border-border'
                }`}
              >
                {allPicked ? <Check size={14} className="text-primary-foreground" /> : null}
              </View>
              <Text className="flex-1 text-[16px] font-semibold text-foreground">
                {t('notePickup.checkAll')}
              </Text>
              <Text className="text-[15px] tabular-nums text-muted-foreground">
                {t('notePickup.checked', { done: checkedIds.length, total: parcels.length })}
              </Text>
            </Pressable>
          ) : null
        }
        renderItem={({ item, index }) => {
          const checked = picked.includes(item.id);

          return (
            <FadeIn index={index}>
              <Pressable
                accessibilityRole="checkbox"
                hitSlop={6}
                accessibilityState={{ checked }}
                onPress={() => toggle(item.id)}
              >
                <Card
                  className={`flex-row items-center gap-3 ${checked ? 'border border-primary' : ''}`}
                >
                  <View
                    className={`size-6 items-center justify-center rounded-md border ${
                      checked ? 'border-primary bg-primary' : 'border-border'
                    }`}
                  >
                    {checked ? <Check size={14} className="text-primary-foreground" /> : null}
                  </View>

                  <View className="flex-1">
                    <Text className="font-mono text-[14px] text-muted-foreground">{item.code}</Text>
                    <Text className="text-[16px] text-foreground" numberOfLines={1}>
                      {item.recipientName}
                    </Text>
                    {/* « × 4 cartons » : tous à prendre chez le vendeur. */}
                    <CartonTag line={item} />
                  </View>
                </Card>
              </Pressable>
            </FadeIn>
          );
        }}
      />

      <View className="px-5 pb-4">
        <Button
          label={t('notePickup.validate', { count: checkedIds.length })}
          icon={PackageCheck}
          onPress={submit}
          disabled={checkedIds.length === 0}
          busy={validation.pending}
          full
        />
      </View>
    </Screen>
  );
}

/** Coque Recette 7 (point 5) : le contenu repart vide à chaque `id` et à chaque fermeture — `useFreshKey`. */
export default function NotePickupScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <NotePickupScreenContent key={useFreshKey(id)} />;
}
