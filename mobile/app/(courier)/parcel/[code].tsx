import {
  DELIVERED as DELIVERED_STATUS,
  findParcelStatus,
  isLockedStatus,
  phonesOf,
  routeTargetsSender,
  type CourierRouteParcel,
  type ParcelDetail,
} from '@delivery/shared';
import { CameraView, useCameraPermissions } from 'expo-camera';
import * as Haptics from 'expo-haptics';
import { useFocusEffect, useLocalSearchParams, useRouter } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import {
  Camera,
  ChevronRight,
  MessageCircle,
  FileSignature,
  MessageSquareWarning,
  Navigation,
  Phone,
} from 'lucide-react-native';
import { type ReactNode, useCallback, useRef, useState } from 'react';
import { Linking, Modal, Pressable, ScrollView, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { DeliverPanel } from '@/components/deliver-panel';
import { useI18n, type MessageKey } from '@/i18n';
import { ApiError } from '@/lib/api';
import {
  useCollectBl,
  useOutcomes,
  useParcelBl,
  useParcelDetail,
  useProof,
  useReturnRoutes,
  useRoutes,
} from '@/lib/courier';
import { useGoBack } from '@/lib/go-back';
import { routeStopOf, waNumber } from '@/lib/route-stop';
import { formatStamp } from '@/lib/datetime';
import {
  Button,
  Card,
  Divider,
  Field,
  ScreenHeader,
  SectionTitle,
  StatusBadge,
  Timeline,
  type TimelineEntry,
} from '@/ui';

/** Une date d'événement : jour, heure, minute et seconde, comme sur le web (D-254). */
function formatMoment(iso: string, locale: string): string {
  return formatStamp(iso, locale);
}

/**
 * Un `ParcelDetail` d'API vu comme un colis de tournée — M4 bloc 3.
 *
 * Les deux portent les mêmes faits sous deux noms, parce que l'un a été
 * dessiné pour le terrain et l'autre pour le back-office. Traduire ici, une
 * fois, évite de faire connaître les deux formes à l'écran — et l'écran n'a
 * pas à savoir d'où vient ce qu'il affiche.
 *
 * Ce qui MANQUE est aussi juste : `sequence` (le rang n'existe que dans une
 * tournée), `confirmed` (idem) et `courierCommission` — une commission se fige
 * à la course, et l'annoncer hors tournée serait une promesse sans course.
 */
function fromDetail(detail: ParcelDetail | undefined): CourierRouteParcel | undefined {
  if (!detail) return undefined;

  return {
    id: detail.id,
    code: detail.code,
    externalCode: detail.externalCode,
    sequence: 0,
    recipientName: detail.recipientName,
    recipientPhone: detail.recipientPhone,
    deliveryAddress: detail.deliveryAddress,
    destinationCityName: detail.destinationCityName,
    destinationDistrictName: detail.destinationDistrictName,
    senderName: detail.clientName,
    senderPhone: detail.recipientPhone,
    pickupAddress: detail.pickupAddress ?? '',
    originCityName: detail.originCityName,
    statusId: detail.statusId,
    codToCollect: detail.codToCollect,
    quantity: detail.quantity ?? 1,
    courierCommission: null,
    confirmed: false,
    latitude: null,
    longitude: null,
  };
}

/**
 * LA FICHE COLIS — M1, docs/DECISIONS.md D-074.
 *
 * Ce qu'un livreur regarde devant une porte : à qui, où, combien encaisser. Et
 * ce qu'il fait ensuite : appeler, rapporter, photographier.
 *
 * **Les issues viennent du SERVEUR**, pas d'une liste écrite ici
 * (`GET /outcomes`). La machine à états vit dans `packages/shared` et le filtre
 * de rôle dans l'API ; les recopier sur le téléphone aurait fait une troisième
 * table qui diverge au premier ajustement — et un bouton qui échoue devant un
 * client est pire que pas de bouton.
 *
 * **Le montant encaissé est PRÉ-REMPLI** avec le dû, et modifiable : le cas
 * courant est qu'il corresponde, le cas réel est qu'il arrive qu'il diffère.
 *
 * **La position est prise SI elle est disponible**, jamais demandée en bloquant :
 * une permission refusée ne doit pas empêcher de rapporter une livraison.
 */
function ParcelScreenContent() {
  const {
    code,
    report: reportParam,
    other: otherParam,
  } = useLocalSearchParams<{ code: string; report?: string; other?: string }>();
  const { t, status, locale } = useI18n();
  const router = useRouter();

  const activeRoutes = useRoutes('active');
  // Un colis d'un circuit de retour (lot C, C2, D-179) se lit chez le vendeur.
  const returnRoutes = useReturnRoutes();
  const routes =
    activeRoutes.data || returnRoutes.data
      ? [...(activeRoutes.data ?? []), ...(returnRoutes.data ?? [])]
      : undefined;

  /*
   * La TOURNÉE autant que le colis — C12 bloc 4.
   *
   * C'est elle qui dit si l'on va chez le destinataire ou chez l'expéditeur.
   * L'écran ne cherchait que le colis, et affichait donc le destinataire même
   * sur un ramassage.
   */
  const found = routes
    ?.flatMap((route) => route.parcels.map((row) => ({ route, parcel: row })))
    .find((row) => row.parcel.code === code);

  /*
   * DEUX SOURCES, et c'est M4 qui ajoute la seconde.
   *
   * La tournée active d'abord : elle porte le rang, la commission figée et le
   * montant à encaisser, et elle est déjà en cache — la fiche s'ouvre sans
   * attendre le réseau, ce qui compte devant une porte.
   *
   * `GET /parcels/:code` ensuite — il sert DEUX choses : la fiche quand la
   * tournée ne la porte pas, et l'historique dans tous les cas. Depuis l'écran
   * Colis,
   * on ouvre des colis d'hier, d'une tournée close, ou jamais scannés. Sans ce
   * repli, la fiche restait sur « Chargement… » pour toujours — le défaut que
   * la recherche aurait introduit.
   *
   * Le serveur borne la lecture à `courierId` (D-082) : cette seconde source
   * n'élargit rien de ce que le livreur peut voir.
   */
  const detail = useParcelDetail(code);

  const parcel = found?.parcel ?? fromDetail(detail.data);
  const kind = found?.route.kind ?? 'delivery';

  // Le statut connu donne les issues tout de suite (lot C, C3) ; l'API confirme.
  const outcomesQuery = useOutcomes(code, parcel?.statusId);
  const outcomes = outcomesQuery.data;

  /*
   * FRAIS À CHAQUE RETOUR SUR L'ÉCRAN — retours recette 26/09, point 6.
   *
   * Le cache global ne redemande rien au focus (`refetchOnWindowFocus:
   * false`, une 3G d'agence) ; cette fiche, elle, DOIT l'être : entre deux
   * passages, le circuit est parti, le bon a été validé, l'agence a changé le
   * statut — et les issues qu'on peut rapporter avec. Le web livreur relit le
   * serveur à chaque navigation ; le mobile fait désormais pareil, ici.
   */
  useFocusEffect(
    useCallback(() => {
      void activeRoutes.refetch();
      void detail.refetch();
      void outcomesQuery.refetch();
      // Les trois requêtes sont stables sur la durée de vie de l'écran.
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [code]),
  );

  /*
   * TROIS RAISONS DE N'AVOIR AUCUNE ISSUE, et elles ne se disent pas pareil.
   *
   *  - l'appel a ÉCHOUÉ (réseau, session expirée) : on ne sait rien, on
   *    réessaie — c'est ce qui est arrivé sur le téléphone d'Anass le 26/09,
   *    après une modification de son compte ;
   *  - 404 `parcel-not-mine` : le colis n'est sur aucune de ses tournées
   *    ouvertes — « pas en main » ;
   *  - 200 et liste vide : il est en main, mais ce statut n'a aucune issue
   *    livreur (18 « Ramassé par le livreur » attend la validation du bon par
   *    l'agence). Le web n'affiche rien dans ce cas ; le mobile disait « pas
   *    en main », ce qui était faux.
   */
  const outcomesFailure = outcomesQuery.error;
  const notMine =
    outcomesFailure instanceof ApiError && outcomesFailure.reason === 'parcel-not-mine';
  const proof = useProof(code ?? '');

  /*
   * La feuille s'ouvre D'EMBLÉE quand on arrive par le bouton « Rapporter » de
   * la tournée (`?report=1`) — M4 bloc 4. L'état initial suffit : ouvrir dans
   * un effet ferait voir la fiche une fraction de seconde avant que la feuille
   * ne la recouvre, ce qui ressemble à un défaut d'affichage.
   */
  /*
   * L'ÉCRAN SIMPLIFIÉ — Recette 5, point 4 b. Il remplace « statut + Mettre à
   * jour » (L2 bloc 2), jugé trop complexe sur le terrain :
   *
   *  - un GRAND bouton « Livrer », qui envoie en un geste (le montant est sur
   *    le bouton) ;
   *  - le chèque, facultatif, au-dessus ; les espèces se calculent en direct ;
   *  - « Autre statut » ouvre la liste des SEULES issues que le serveur
   *    autorise (`GET /outcomes`), Livré mis à part, avec le commentaire et la
   *    date de report que l'existant prévoit.
   *
   * L'entrée par la tournée (`?report=1`) ou par la carte du scan
   * (`?other=1`) ouvre directement la liste.
   *
   * Depuis le chantier « Livrer en un appui » (D-156), tout ce bloc — chèque,
   * espèces, « Livrer », « Autre statut » et sa feuille — vit dans
   * `<DeliverPanel>`, le MÊME panneau que la carte du scan et la ligne du
   * circuit. La fiche le rend ; elle ne le refait pas.
   */
  const initialPicker = reportParam === '1' || otherParam === '1';
  const [history, setHistory] = useState(false);
  const goBack = useGoBack('/(courier)/parcels');
  const [note, setNote] = useState<string | null>(null);

  const [camera, setCamera] = useState(false);
  const [permission, requestPermission] = useCameraPermissions();
  const cameraRef = useRef<CameraView>(null);

  if (!parcel) {
    return (
      <SafeAreaView className="flex-1 items-center justify-center gap-2 bg-background px-8">
        <Text className="text-center text-[16px] text-muted-foreground">
          {detail.isLoading ? t('common.loading') : t('parcel.notFound')}
        </Text>
        {detail.isLoading ? null : (
          <Button label={t('common.close')} tone="ghost" onPress={() => router.back()} />
        )}
      </SafeAreaView>
    );
  }

  const definition = findParcelStatus(parcel.statusId);
  const stop = routeStopOf(kind, parcel);

  const timeline: TimelineEntry[] = (detail.data?.events ?? []).flatMap((event) => {
    const target = findParcelStatus(event.statusId);
    if (!target) return [];

    return [
      {
        id: event.id,
        label: status(target.key),
        color: target.color,
        at: formatMoment(event.createdAt, locale),
        author: event.authorName,
        comment: event.comment,
      },
    ];
  });
  const toSender = routeTargetsSender(kind);
  /** « Livrer » ou « Autre statut » n'existent que si le serveur autorise une issue. */
  const hasOutcomes = (outcomes ?? []).length > 0;

  const capture = async () => {
    const photo = await cameraRef.current?.takePictureAsync({
      // Compressée : une photo de porte n'a pas besoin de huit mégaoctets, et
      // l'envoyer en base64 sur une 3G d'agence ferait abandonner.
      quality: 0.4,
      base64: true,
      // LE réglage de conformité : sans lui, la photo embarque les
      // coordonnées EXIF du livreur, qu'on n'a pas demandé à collecter.
      exif: false,
    });

    setCamera(false);
    if (!photo?.base64) return;

    proof.mutate(
      { name: `${parcel.code}.jpg`, mimeType: 'image/jpeg', content: photo.base64 },
      {
        onSuccess: () => {
          void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
          setNote(t('parcel.photoTaken'));
        },
      },
    );
  };

  return (
    <SafeAreaView className="flex-1 bg-background" edges={['top']}>
      <ScreenHeader title={t('parcel.detailTitle')} backHref={'/(courier)/parcels'} />

      <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-3 px-5 pb-6">
        {/* Le code et le statut sur une ligne : ce qu'on vérifie d'abord. */}
        <View className="flex-row items-center justify-between gap-3">
          <Text className="flex-1 font-mono text-[18px] font-bold text-foreground">
            {parcel.code}
          </Text>
          {definition ? (
            <StatusBadge label={status(definition.key)} color={definition.color} />
          ) : null}
        </View>
        {/* La référence commande du vendeur, quand il en a une — point 7 du 26/09. */}
        {parcel.externalCode ? (
          <Text className="text-[14px] text-muted-foreground">
            {t('parcel.reference')} : {parcel.externalCode}
          </Text>
        ) : null}
        {/* Lot G (D-208) : « × 4 cartons » — un code, plusieurs cartons à remettre. */}
        {(parcel.quantity ?? 1) > 1 ? (
          <Text className="text-[16px] font-bold text-foreground">
            {t('parcel.cartons', { count: parcel.quantity ?? 1 })}
          </Text>
        ) : null}

        {/*
          LE BANDEAU BL — chantier « Retours, BL Clients Pro », point 5.

          « Livreur, sur un colis blRequested : bandeau visible 📄 BL à
          récupérer : OUI. » Il est AU-DESSUS de tout le reste, avant le nom du
          destinataire : c'est ce qu'il faut savoir avant de sonner, pas après
          avoir tendu le colis.
        */}
        <BlBanner code={parcel.code} statusId={parcel.statusId} />

        <Card className="gap-3">
          <SectionTitle label={toSender ? t('parcel.senderInfo') : t('parcel.clientInfo')} />

          {/*
            Trois lignes ÉTIQUETÉES, comme sur la maquette. M4 posait le nom en
            gros et l'adresse en dessous ; devant une porte, on épelle un nom et
            on lit une adresse, et les confondre coûte un appel. L'étiquette
            vaut les deux lignes qu'elle prend.
          */}
          <InfoRow label={t('parcel.name')} value={stop.name} />
          <InfoRow
            label={t('parcel.phone')}
            value={stop.phone}
            action={
              // D-244 : jusqu'à trois numéros, UN bouton par numéro, chacun le sien.
              <View className="flex-row gap-2">
                {phonesOf(stop.phone).map((phone, _index, phones) => (
                  <Pressable
                    key={phone}
                    accessibilityRole="button"
                    accessibilityLabel={
                      phones.length > 1 ? t('run.callNumber', { phone }) : t('run.call')
                    }
                    onPress={() => void Linking.openURL(`tel:${phone}`)}
                    className="bg-accent min-h-11 min-w-11 items-center justify-center rounded-2xl"
                    hitSlop={8}
                    style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
                  >
                    <Phone size={20} className="text-accent-foreground" />
                  </Pressable>
                ))}
              </View>
            }
          />
          <InfoRow
            label={toSender ? t('parcel.pickupAddress') : t('parcel.address')}
            value={stop.area ? `${stop.address}\n${stop.area}` : stop.address}
          />

          {/*
            Les TROIS gestes du terrain, sur la personne qu'on va voir.

            WhatsApp par `Linking`, sans bibliothèque : `wa.me` ouvre
            l'application si elle est installée et le navigateur sinon — c'est
            le comportement qu'on veut, et aucun paquet natif ne le ferait
            mieux. Au Maroc, c'est souvent le seul canal auquel un destinataire
            répond.
          */}
          <View className="flex-row gap-2">
            <Button
              label={t('parcel.whatsapp')}
              icon={MessageCircle}
              tone="ghost"
              // WhatsApp : le PREMIER numéro (D-244).
              onPress={() =>
                void Linking.openURL(`https://wa.me/${waNumber(phonesOf(stop.phone)[0] ?? '')}`)
              }
            />
            <Button
              label={t('run.route')}
              icon={Navigation}
              tone="ghost"
              onPress={() => void Linking.openURL(stop.directionsUrl)}
            />
          </View>
        </Card>

        {/* Un ramassage n'encaisse rien : la carte du montant n'a pas lieu
            d'être, et un « 0,00 MAD » en grands chiffres serait un contresens. */}
        {toSender ? null : (
          <Card className="gap-3">
            {/*
              LE MONTANT EN VERT, les frais en gris — maquette 4.

              Les deux se lisent ensemble mais ne se valent pas : le premier est
              ce qu'on réclame à la porte, le second explique pourquoi. Les
              mettre au même poids ferait hésiter devant un client.
            */}
            <AmountRow
              label={t('parcel.collectLong')}
              value={`${(parcel.codToCollect / 100).toFixed(2)} MAD`}
              strong
            />

            {/*
              LE PAIEMENT ATTENDU d'un colis Pro — recette 3, point 3. Une
              INFORMATION : l'encaissement se saisit comme avant, espèces ou
              chèque selon ce que tend le destinataire.
            */}
            {detail.data?.expectedPayment ? (
              <InfoRow
                label={t('parcel.expectedPayment')}
                value={
                  detail.data.expectedPayment === 'cheque'
                    ? t('expectedPayment.cheque')
                    : t('expectedPayment.cash')
                }
              />
            ) : null}

            {/*
              LES FRAIS viennent de la fiche serveur, pas de la tournée : le
              contrat livreur (D-072) ne les porte pas, et c'est `feeIncluded`
              qui dit s'ils sont DANS le montant ou EN PLUS (DOMAIN.md §3.8).
              Sans cette mention, « 750 » et « 20 » se lisent comme 770 un jour
              sur deux. La ligne disparaît tant que la fiche n'a pas répondu.
            */}
            {detail.data ? (
              <>
                <Divider />
                <AmountRow
                  label={t('parcel.deliveryFee')}
                  value={`${(detail.data.deliveryFee / 100).toFixed(2)} MAD`}
                  hint={t(detail.data.feeIncluded ? 'parcel.feeIncluded' : 'parcel.feeExtra')}
                />
              </>
            ) : null}

            {parcel.courierCommission !== null ? (
              <>
                <Divider />
                <AmountRow
                  label={t('parcel.commission')}
                  value={`${(parcel.courierCommission / 100).toFixed(2)} MAD`}
                />
              </>
            ) : null}
          </Card>
        )}

        {toSender && parcel.courierCommission !== null ? (
          <Card className="gap-1">
            <Text className="text-[13px] text-muted-foreground">{t('parcel.commission')}</Text>
            <Text className="text-[19px] font-semibold tabular-nums text-foreground">
              {(parcel.courierCommission / 100).toFixed(2)} MAD
            </Text>
          </Card>
        ) : null}

        {/*
          LE STATUT, SUR LA PAGE — L2 bloc 2, maquette 4.

          M4 cachait le changement de statut derrière un bouton de bas d'écran,
          « Rapporter une issue », qui ouvrait une feuille où tout se passait.
          La maquette d'Anass le pose à plat : une liste déroulante qui montre
          l'état courant, un bouton « Mettre à jour » dessous. La différence
          n'est pas cosmétique — la feuille ne disait pas quel était le statut
          AVANT, et le livreur choisissait sans point de comparaison.

          **Les issues restent celles du SERVEUR** (`GET /outcomes`). La machine
          à états vit dans `packages/shared` et le filtre de rôle dans l'API ;
          les recopier ici aurait fait une troisième table, qui diverge au
          premier ajustement — et un bouton qui échoue devant un client est
          pire que pas de bouton.
        */}
        <Card className="gap-3">
          {/*
            LIVRER — Recette 5, point 4 b. Le chèque est FACULTATIF ; les
            espèces sont la différence, en direct (chantier « Finance »,
            point 8) ; un chèque au-delà du dû est refusé ici comme à l'API.
          */}
          {hasOutcomes ? (
            <DeliverPanel
              code={parcel.code}
              codToCollect={parcel.codToCollect}
              kind={kind}
              statusId={parcel.statusId}
              initialPicker={initialPicker}
              // Retour là d'où l'on vient — la liste, le circuit ou le scanner —
              // dont les compteurs se relisent (invalidations de `useReport`).
              onDone={goBack}
            />
          ) : null}

          {/*
            DEUX RAISONS DE N'AVOIR AUCUN BOUTON, et elles ne se disent pas
            pareil — retouches Anass, point 6.

            « Livré » posé, le statut est VERROUILLÉ côté livreur : seule
            l'agence peut y revenir, et elle doit motiver (DOMAIN.md §2.2). Le
            dire explicitement évite qu'un livreur cherche un bouton qui
            n'existera pas, ou rappelle l'agence pour le demander.

            « Pas en main » est l'autre cas : le colis est en agence, en stock,
            ou sur la tournée de quelqu'un d'autre. Le même message pour les
            deux laissait croire à un défaut de l'application.
          */}
          {outcomesFailure && !notMine ? (
            <View className="items-center gap-2">
              <Text className="text-center text-[14px] text-destructive">
                {t('parcel.outcomesError')}
              </Text>
              <Button
                label={t('common.retry')}
                tone="ghost"
                onPress={() => void outcomesQuery.refetch()}
              />
            </View>
          ) : outcomesQuery.isPending ? (
            <Text className="text-center text-[14px] text-muted-foreground">
              {t('common.loading')}
            </Text>
          ) : (outcomes ?? []).length === 0 ? (
            <Text className="text-center text-[14px] text-muted-foreground">
              {notMine
                ? t('parcel.noOutcome')
                : parcel && isLockedStatus(parcel.statusId)
                  ? t('parcel.locked')
                  : parcel?.statusId === 18
                    ? t('parcel.awaitingNoteValidation')
                    : t('parcel.noOutcomeAtStatus')}
            </Text>
          ) : null}
        </Card>

        {/*
          L'HISTORIQUE — M4 bloc 3, replié par L2.

          Il vient de `GET /parcels/:code`, qui rend `events` déjà trié du plus
          récent au plus ancien : rien à réordonner ici, et donc pas de second
          tri à maintenir. La section est SILENCIEUSE tant que l'appel n'a pas
          répondu, et disparaît s'il échoue — un livreur devant une porte n'a
          rien à faire d'un message d'erreur sur un historique.
        */}
        {timeline.length > 0 ? (
          <Card className="gap-3">
            <Pressable
              accessibilityRole="button"
              accessibilityState={{ expanded: history }}
              onPress={() => setHistory(!history)}
              className="min-h-11 flex-row items-center justify-between gap-3"
              style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
            >
              <Text className="flex-1 text-[16px] font-semibold text-foreground">
                {t('parcel.operations')}
              </Text>
              <ChevronRight
                size={20}
                className={
                  history ? 'rotate-90 text-muted-foreground' : 'flip-rtl text-muted-foreground'
                }
              />
            </Pressable>

            {history ? <Timeline entries={timeline} /> : null}
          </Card>
        ) : null}

        {note ? <Text className="text-[16px] text-muted-foreground">{note}</Text> : null}

        {/*
          SIGNALER UN PROBLÈME — le complément de L2.

          Le chemin réel d'une réclamation : on l'ouvre DEVANT le colis, et le
          code part avec. Demander au livreur de le recopier depuis l'écran
          précédent, debout, serait une invitation à se tromper.
        */}
        <Button
          label={t('claims.report')}
          icon={MessageSquareWarning}
          tone="ghost"
          onPress={() =>
            router.push(`/(courier)/claim/new?parcelCode=${encodeURIComponent(parcel.code)}`)
          }
          full
        />

        <Button
          label={t('parcel.photo')}
          icon={Camera}
          tone="ghost"
          busy={proof.isPending}
          onPress={() => {
            if (!permission?.granted) {
              void requestPermission();
              return;
            }
            setCamera(true);
          }}
          full
        />
      </ScrollView>

      {/* --- La caméra de preuve --- */}
      <Modal visible={camera} animationType="slide" onRequestClose={() => setCamera(false)}>
        <View className="flex-1 bg-black">
          <CameraView ref={cameraRef} style={{ flex: 1 }} facing="back" />
          <View className="gap-3 bg-black px-5 pb-10 pt-4">
            <Button label={t('parcel.photo')} onPress={() => void capture()} full />
            <Button label={t('common.cancel')} tone="ghost" onPress={() => setCamera(false)} full />
          </View>
        </View>
      </Modal>
    </SafeAreaView>
  );
}

/**
 * Une ligne ÉTIQUETÉE de la carte « Informations client » — maquette 4.
 *
 * L'étiquette au-dessus, la valeur en dessous, l'action à droite. Sur un
 * téléphone tenu d'une main, mettre l'étiquette à gauche de la valeur écrase
 * les deux : un nom marocain complet ne tient pas sur la moitié d'un écran de
 * 375 points.
 */
function InfoRow({ label, value, action }: { label: string; value: string; action?: ReactNode }) {
  return (
    <View className="flex-row items-center gap-3">
      <View className="flex-1 gap-0.5">
        <Text className="text-[13px] text-muted-foreground">{label}</Text>
        <Text className="text-[16px] text-foreground">{value}</Text>
      </View>
      {action}
    </View>
  );
}

/**
 * Une ligne de MONTANT. `strong` porte celui qu'on réclame à la porte.
 *
 * Le vert n'est pas décoratif : c'est de l'argent qui rentre, et il se
 * distingue du gris des frais, qui expliquent seulement le calcul.
 */
function AmountRow({
  label,
  value,
  hint,
  strong,
}: {
  label: string;
  value: string;
  hint?: string;
  strong?: boolean;
}) {
  return (
    <View className="flex-row items-start justify-between gap-3">
      <View className="flex-1 gap-0.5">
        <Text className="text-[15px] text-muted-foreground">{label}</Text>
        {hint ? <Text className="text-[13px] text-muted-foreground">{hint}</Text> : null}
      </View>
      <Text
        className={
          strong
            ? 'text-[22px] font-bold tabular-nums text-success'
            : 'text-[17px] font-semibold tabular-nums text-foreground'
        }
      >
        {value}
      </Text>
    </View>
  );
}

/**
 * LE BANDEAU BL, et la question qui va avec — point 5.
 *
 * **Trois états, trois rendus :** rien du tout si le colis ne demande aucun BL
 * (« aucune question pour un client normal ») ; le bandeau et les deux boutons
 * tant que le papier n'est pas enregistré ; l'état du BL une fois qu'il l'est.
 *
 * **« Non, pas cette fois » n'écrit rien**, et le dit. Le destinataire n'avait
 * pas le papier, ou l'a gardé : la question se reposera à la prochaine
 * ouverture de l'écran. Enregistrer un « non » aurait fait un BL qui n'a jamais
 * existé, que l'admin aurait cherché.
 */
function BlBanner({ code, statusId }: { code: string; statusId: number }) {
  const { t } = useI18n();
  const tracking = useParcelBl(code);
  const collect = useCollectBl(code);
  const [reference, setReference] = useState('');
  const [skipped, setSkipped] = useState(false);

  const data = tracking.data;
  if (!data || (!data.requested && !data.slip)) return null;

  /*
   * AVANT LA LIVRAISON — lot E, E1.1 (D-201). Le BL se récupère EN livrant :
   * la case « BL récupéré » est dans le panneau « Livrer », juste en dessous.
   * Le bandeau prévient, il ne pose pas encore la question (l'API la
   * refuserait sur un colis qui n'est pas livré).
   */
  if (!data.slip && statusId !== DELIVERED_STATUS) {
    return (
      <Card className="gap-2">
        <View className="flex-row items-center gap-2">
          <FileSignature size={18} className="text-primary" />
          <Text className="flex-1 text-[15px] font-bold text-foreground">{t('bl.banner')}</Text>
        </View>
        <Text className="text-[13px] text-muted-foreground">{t('bl.bannerBeforeDelivery')}</Text>
      </Card>
    );
  }

  if (data.slip) {
    return (
      <Card className="gap-1">
        <Text className="text-[13px] text-muted-foreground">{t('bl.tracking')}</Text>
        <Text className="text-[15px] font-semibold text-foreground">
          {t(`bl.status.${data.slip.status}` as MessageKey)}
        </Text>
        {data.slip.reference ? (
          <Text className="font-mono text-[13px] text-muted-foreground">{data.slip.reference}</Text>
        ) : null}
      </Card>
    );
  }

  return (
    <Card className="gap-3">
      <View className="flex-row items-center gap-2">
        <FileSignature size={18} className="text-primary" />
        <Text className="flex-1 text-[15px] font-bold text-foreground">{t('bl.banner')}</Text>
      </View>

      <Text className="text-[13px] text-muted-foreground">{t('bl.bannerHint')}</Text>

      {skipped ? (
        <Text className="text-[13px] text-muted-foreground">{t('bl.skipped')}</Text>
      ) : null}

      <Field label={t('bl.reference')} value={reference} onChangeText={setReference} />

      <Button
        label={t('bl.yes')}
        busy={collect.isPending}
        onPress={() => collect.mutate({ collected: true, reference })}
      />
      <Button
        label={t('bl.no')}
        tone="ghost"
        disabled={collect.isPending}
        onPress={() => setSkipped(true)}
      />
    </Card>
  );
}

/** Coque Recette 7 (point 5) : le contenu repart vide à chaque `code` et à chaque fermeture — `useFreshKey`. */
export default function ParcelScreen() {
  const { code } = useLocalSearchParams<{ code: string }>();
  return <ParcelScreenContent key={useFreshKey(code)} />;
}
