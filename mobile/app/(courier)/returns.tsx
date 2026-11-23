import {
  findParcelStatus,
  DELIVERY_SLIP_STATUS_COLORS,
  pickupNoteState,
  RETURN_HANDOVER_STATUS_COLORS,
  type CourierCheque,
  type ReturnHandoverStatus,
} from '@delivery/shared';
import { useRouter } from 'expo-router';
import {
  Camera,
  FileSignature,
  FileText,
  History,
  PackageX,
  Plus,
  Receipt,
  Undo2,
} from 'lucide-react-native';
import { useState } from 'react';
import { Pressable, RefreshControl, ScrollView, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n, type MessageKey } from '@/i18n';
import {
  useCheques,
  useDeliverySlips,
  useHandovers,
  usePickups,
  useReturnRoutes,
  useReturnsPending,
} from '@/lib/courier';
import { useRefetchOnFocus } from '@/lib/use-refetch-on-focus';
import { formatStamp } from '@/lib/datetime';
import { CartonTag, CartonTotal } from '@/components/carton-count';
import { CircuitCard } from '@/components/circuit-card';
import { useTokens } from '@/theme/tenant-theme';
import {
  Button,
  Card,
  Chip,
  EmptyState,
  FadeIn,
  Screen,
  SectionTitle,
  SkeletonCard,
  StatusBadge,
} from '@/ui';

type Volet = 'circuits' | 'parcels' | 'handovers' | 'cheques' | 'notes' | 'history';

/**
 * LES RETOURS, EN TROIS VOLETS — L2 bloc 5, maquettes 8 et 9.
 *
 * Anass met sous un même toit trois choses que le livreur rapporte à l'agence
 * en fin de journée : les **colis** qui n'ont pas pu être livrés, les
 * **chèques** qu'il a en poche, et les **bons de livraison** signés. Ce n'est
 * pas une classification de domaine — ce sont trois tables différentes — c'est
 * une classification de GESTE, et c'est la bonne : il vide son sac une fois.
 *
 * **Trois sources, trois états de déploiement, et l'écran le dit :**
 *
 *  - les **colis** viennent de `GET /returns/pending`, qui tourne aujourd'hui :
 *    le livreur a `returns.read` et la portée le borne aux siens. Ce volet
 *    marche tout de suite ;
 *  - les **chèques** et les **bons** viennent des endpoints de ce chantier, que
 *    la production n'a pas : leurs volets affichent un état vide qui le dit,
 *    jamais une erreur.
 *
 * **Ce que D-024 ne permet pas.** Les circuits de retour se clôturent par le
 * bon signé du client et sont traités par le back-office : l'écran LISTE, il
 * ne valide rien. Le seul geste qu'il ouvre est celui que l'application sait
 * déjà faire — photographier un chèque (C11, D-067).
 *
 * **Lot C, point C2 (D-179)** : le circuit de retour, lui, se rend compte
 * d'ici — onglet « Circuits », en tête, vers l'écran circuit commun.
 */
export default function CourierReturns() {
  const { t } = useI18n();
  /*
   * LES CIRCUITS DE RETOUR EN TÊTE — lot C, point C2 (D-179). Un circuit de
   * retour dispatché n'apparaissait nulle part sur le téléphone ; c'est le
   * travail du jour, il s'ouvre en premier.
   */
  const [volet, setVolet] = useState<Volet>('circuits');
  const returnRoutes = useReturnRoutes();

  return (
    <Screen>
      <Text className="px-5 pb-3 pt-4 text-[22px] font-bold text-foreground">
        {t('returns.title')}
      </Text>

      {/*
        CINQ volets depuis le chantier « Retours, BL Clients Pro » (point 3).
        Ils défilent horizontalement : à 375 px, cinq puces ne tiennent pas sur
        une ligne, et les replier en menu cacherait celle qu'on ouvre le plus.
      */}
      <ScrollView
        keyboardShouldPersistTaps="handled"
        horizontal
        showsHorizontalScrollIndicator={false}
        contentContainerClassName="flex-row gap-2 px-5 pb-3"
      >
        <Chip
          label={t('returns.circuits')}
          count={returnRoutes.data?.length}
          active={volet === 'circuits'}
          onPress={() => setVolet('circuits')}
        />
        <Chip
          label={t('returns.parcels')}
          active={volet === 'parcels'}
          onPress={() => setVolet('parcels')}
        />
        <Chip
          label={t('returns.handovers')}
          active={volet === 'handovers'}
          onPress={() => setVolet('handovers')}
        />
        <Chip
          label={t('returns.cheques')}
          active={volet === 'cheques'}
          onPress={() => setVolet('cheques')}
        />
        <Chip
          label={t('returns.notes')}
          active={volet === 'notes'}
          onPress={() => setVolet('notes')}
        />
        <Chip
          label={t('returns.history')}
          active={volet === 'history'}
          onPress={() => setVolet('history')}
        />
      </ScrollView>

      {volet === 'circuits' ? <CircuitsVolet routes={returnRoutes} /> : null}
      {volet === 'parcels' ? <ParcelsVolet /> : null}
      {volet === 'handovers' ? <HandoversVolet /> : null}
      {volet === 'cheques' ? <ChequesVolet /> : null}
      {volet === 'notes' ? <NotesVolet /> : null}
      {volet === 'history' ? <HistoryVolet /> : null}
    </Screen>
  );
}

/**
 * VOLET 0 — les circuits de retour qu'on lui a confiés (D-179). La même carte
 * que Livraisons ; l'écran circuit est le même aussi : Tout cocher, « Rendre
 * compte » en masse (rendu au vendeur, non remis), clôture automatique.
 */
function CircuitsVolet({ routes }: { routes: ReturnType<typeof useReturnRoutes> }) {
  const { t } = useI18n();
  const pull = usePullToRefresh(routes);
  // Un circuit clos (dernier colis rendu compte) quitte la liste au retour.
  useRefetchOnFocus(routes);

  if (routes.isLoading) {
    return (
      <View className="gap-3 px-5">
        <SkeletonCard />
      </View>
    );
  }

  const list = routes.data ?? [];

  return (
    <ScrollView
      keyboardShouldPersistTaps="handled"
      contentContainerClassName="gap-3 px-5 pb-6"
      refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
    >
      {list.length === 0 ? (
        <EmptyState
          art="route"
          icon={Undo2}
          title={t('returns.noCircuits')}
          hint={t('returns.noCircuitsHint')}
        />
      ) : (
        list.map((route, index) => (
          <FadeIn key={route.id} index={index}>
            <CircuitCard route={route} />
          </FadeIn>
        ))
      )}
    </ScrollView>
  );
}

/**
 * VOLET 1 — les colis à retourner, maquette 8.
 *
 * Le « motif » de la maquette est le STATUT du colis : « client absent »,
 * « refus client », « produit endommagé » sont des statuts du référentiel
 * (DOMAIN.md §2.1), pas un champ libre. Les afficher comme tels garde une
 * seule vérité, et la couleur avec.
 */
function ParcelsVolet() {
  const { t, status } = useI18n();
  const returns = useReturnsPending();
  const pull = usePullToRefresh(returns);

  const parcels = (returns.data ?? []).flatMap((group) => group.parcels);

  if (returns.isLoading) {
    return (
      <View className="gap-3 px-5">
        <SkeletonCard />
        <SkeletonCard />
      </View>
    );
  }

  return (
    <ScrollView
      keyboardShouldPersistTaps="handled"
      contentContainerClassName="gap-3 px-5 pb-6"
      refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
    >
      {/* « 5 colis · 9 cartons » — en tête de la pile à retourner. */}
      <CartonTotal lines={parcels} />
      {parcels.length === 0 ? (
        <EmptyState
          art="box"
          icon={PackageX}
          title={t('returns.noParcels')}
          hint={t('returns.noParcelsHint')}
        />
      ) : (
        parcels.map((parcel, index) => {
          const definition = findParcelStatus(parcel.statusId);

          return (
            <FadeIn key={parcel.id} index={index}>
              <Card className="gap-2">
                <View className="flex-row items-center justify-between gap-3">
                  <Text className="flex-1 font-mono text-[16px] font-bold text-foreground">
                    {parcel.code}
                  </Text>
                  {definition ? (
                    <StatusBadge label={status(definition.key)} color={definition.color} />
                  ) : null}
                </View>

                <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
                  {t('returns.reason')} : {definition ? status(definition.key) : '—'}
                </Text>
                <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
                  {t('parcels.client')} : {parcel.recipientName} · {parcel.destinationCityName}
                </Text>
                {/* « × 4 cartons » : tous à rapporter. */}
                <CartonTag line={parcel} />
              </Card>
            </FadeIn>
          );
        })
      )}
    </ScrollView>
  );
}

/**
 * VOLET 2 — les chèques, maquette 9.
 *
 * **Une ambiguïté assumée, et portée au PO (D-L2-8).** La maquette montre
 * « Client : Société Darna », ce qui se lit comme un chèque REÇU d'un client au
 * comptoir. Le domaine modélise ce chèque-là en `Payment` — et `Payment` ne
 * porte aucun lien vers le livreur : le legacy avait `livreur_id` sur
 * `rembourssements`, NULL sur ses 460 lignes, et V2 ne l'a pas repris. Le
 * servir demanderait une migration ET un état de remise, c'est-à-dire une
 * décision de produit.
 *
 * Ce volet montre donc ce qui EXISTE et qui est déjà un chèque à rendre compte
 * : ceux de ses relevés de rémunération (C11, D-067), avec leur état. Le seul
 * geste ouvert est celui que l'application sait faire — photographier le chèque
 * et en saisir le numéro.
 */
function ChequesVolet() {
  const { t, locale } = useI18n();
  const router = useRouter();
  const cheques = useCheques();
  const pull = usePullToRefresh(cheques);

  const rows = cheques.data ?? [];

  if (cheques.isLoading) {
    return (
      <View className="gap-3 px-5">
        <SkeletonCard />
        <SkeletonCard />
      </View>
    );
  }

  return (
    <ScrollView
      keyboardShouldPersistTaps="handled"
      contentContainerClassName="gap-3 px-5 pb-6"
      refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
    >
      {rows.length === 0 ? (
        <EmptyState
          art="box"
          icon={Receipt}
          title={t(cheques.isError ? 'returns.chequesUnavailable' : 'returns.noCheques')}
          hint={t(cheques.isError ? 'returns.chequesUnavailableHint' : 'returns.noChequesHint')}
        />
      ) : (
        rows.map((cheque, index) => (
          <FadeIn key={cheque.id} index={index}>
            <ChequeCard cheque={cheque} locale={locale} />
          </FadeIn>
        ))
      )}

      {/*
        LE GESTE, une fois, en bas — et pas sur chaque carte.

        « Confirmer la remise » de la maquette suppose un état que le domaine
        ne porte pas encore. Ce que le livreur peut faire aujourd'hui, c'est
        DÉCLARER le chèque qu'il a en main : photo et numéro. C'est le même
        geste, du côté où l'application a prise.
      */}
      <Button
        label={t('cheque.title')}
        icon={Camera}
        tone="ghost"
        onPress={() => router.push('/(courier)/cheque')}
        full
      />
    </ScrollView>
  );
}

function ChequeCard({ cheque, locale }: { cheque: CourierCheque; locale: string }) {
  const { t } = useI18n();
  const tokens = useTokens();

  const date = cheque.issuedOn
    ? new Intl.DateTimeFormat(locale === 'ar' ? 'ar-MA' : 'fr-MA', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
      }).format(new Date(cheque.issuedOn))
    : '—';

  return (
    <Card className="gap-2">
      <View className="flex-row items-center justify-between gap-3">
        <Text className="flex-1 font-mono text-[16px] font-bold text-foreground">
          {cheque.statementCode}
        </Text>
        <StatusBadge
          label={t(cheque.handedOver ? 'returns.handedOver' : 'returns.toHandOver')}
          color={cheque.handedOver ? tokens.success : tokens.warning}
        />
      </View>

      <Text className="text-[14px] text-muted-foreground">
        {t('returns.chequeNumber')} : {cheque.number || '—'}
      </Text>
      <Text className="text-[14px] text-muted-foreground">
        {t('cheque.bank')} : {cheque.bank ?? '—'} · {date}
      </Text>
      <Text className="text-[17px] font-semibold tabular-nums text-foreground">
        {t('returns.amount')} : {(cheque.amountInCents / 100).toFixed(2)} MAD
      </Text>
    </Card>
  );
}

/**
 * VOLET 3 — les bons de livraison, spec §5C.
 *
 * Ce sont les mêmes objets que les ramassages, de type `DROPOFF` : le bon que
 * le livreur fait signer en déposant. Même endpoint, même carte, même règle des
 * trois états — les recopier aurait fait deux vocabulaires pour un seul papier.
 */
function NotesVolet() {
  const { t, locale } = useI18n();
  const router = useRouter();
  const tokens = useTokens();
  /*
   * LOT E, E1 (D-201) : « Bons de livraison » veut dire les BL. Le volet les
   * montre d'abord — ceux que le livreur a récupérés, et où ils en sont — avec
   * l'accès à ses liasses (« Bons BL »). Les bons de DÉPÔT qu'il listait
   * restent en dessous, sous leur vrai nom : rien n'a disparu.
   */
  const slips = useDeliverySlips();
  const notes = usePickups('dropoff');
  const pull = usePullToRefresh(slips, notes);

  const slipRows = slips.data?.items ?? [];
  const rows = notes.data ?? [];

  if (notes.isLoading || slips.isLoading) {
    return (
      <View className="gap-3 px-5">
        <SkeletonCard />
        <SkeletonCard />
      </View>
    );
  }

  return (
    <ScrollView
      keyboardShouldPersistTaps="handled"
      contentContainerClassName="gap-3 px-5 pb-6"
      refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
    >
      <Button
        label={t('bl.notes')}
        icon={FileSignature}
        tone="ghost"
        onPress={() => router.push('/(courier)/bl')}
      />

      {slipRows.length === 0 ? (
        <EmptyState
          art="box"
          icon={FileSignature}
          title={t('returns.noSlips')}
          hint={t('returns.noSlipsHint')}
        />
      ) : (
        slipRows.map((slip, index) => (
          <FadeIn key={slip.id} index={index}>
            <Pressable
              accessibilityRole="button"
              onPress={() => router.push(`/(courier)/parcel/${encodeURIComponent(slip.parcelCode)}`)}
              style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
            >
              <Card className="gap-2">
                <View className="flex-row items-center justify-between gap-3">
                  <Text className="flex-1 font-mono text-[16px] font-bold text-foreground">
                    {slip.parcelCode}
                  </Text>
                  <StatusBadge
                    label={t(`bl.status.${slip.status}` as MessageKey)}
                    color={DELIVERY_SLIP_STATUS_COLORS[slip.status]}
                  />
                </View>
                <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
                  {t('parcels.client')} : {slip.clientName}
                </Text>
                {slip.collectedAt ? (
                  <Text className="text-[13px] text-muted-foreground">
                    {formatStamp(slip.collectedAt, locale)}
                    {slip.reference ? ` · ${slip.reference}` : ''}
                  </Text>
                ) : null}
              </Card>
            </Pressable>
          </FadeIn>
        ))
      )}

      <SectionTitle label={t('returns.dropoffs')} />

      {rows.length === 0 ? (
        <EmptyState
          art="box"
          icon={FileText}
          title={t(notes.isError ? 'returns.notesUnavailable' : 'returns.noNotes')}
          hint={t(notes.isError ? 'returns.notesUnavailableHint' : 'returns.noNotesHint')}
        />
      ) : (
        rows.map((note, index) => {
          const state = pickupNoteState(note);

          return (
            <FadeIn key={note.id} index={index}>
              <Card className="gap-2">
                <View className="flex-row items-center justify-between gap-3">
                  <Text className="flex-1 font-mono text-[16px] font-bold text-foreground">
                    {note.code}
                  </Text>
                  <StatusBadge
                    label={t(
                      state === 'done'
                        ? 'pickups.done'
                        : state === 'in-progress'
                          ? 'pickups.running'
                          : 'pickups.pending',
                    )}
                    color={
                      state === 'done'
                        ? tokens.success
                        : state === 'in-progress'
                          ? tokens.primary
                          : tokens.warning
                    }
                  />
                </View>

                <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
                  {t('parcels.client')} : {note.clientName}
                </Text>
                <Text className="text-[14px] text-muted-foreground" numberOfLines={2}>
                  {t('parcels.addressLabel')} : {note.address}, {note.cityName}
                </Text>
              </Card>
            </FadeIn>
          );
        })
      )}
    </ScrollView>
  );
}

/**
 * VOLET 4 — SES BONS DE RETOUR, chantier « Retours, BL Clients Pro », point 3.
 *
 * « Liste de ses bons de retour (créés par lui et par l'admin). » Les deux
 * piles sont dans la même liste, et la mention d'origine les distingue : un bon
 * de l'admin est une DEMANDE qu'il exécute, un bon à lui est sa propre
 * composition. Deux onglets auraient fait chercher.
 *
 * Les bons clôturés et annulés n'y sont pas : ils vivent dans l'historique, et
 * les mélanger ferait agir sur une archive.
 */
function HandoversVolet() {
  const { t, locale } = useI18n();
  const router = useRouter();
  const handovers = useHandovers();
  const pull = usePullToRefresh(handovers);

  /*
   * CE QUI ATTEND ENCORE QUELQUE CHOSE DE LUI — chantier « Bon de retour
   * livreur », point 2 : « un bon reçu quitte la vue En attente ».
   *
   * Un bon CONFIRMÉ est entre les mains de l'agence. Le laisser ici ferait
   * croire au livreur qu'on attend encore un geste de sa part ; il rejoint
   * l'historique, avec les clôturés et les annulés.
   */
  const rows = (handovers.data?.items ?? []).filter(
    (handover) =>
      handover.status !== 'confirmed' &&
      handover.status !== 'closed' &&
      handover.status !== 'cancelled',
  );

  if (handovers.isLoading) {
    return (
      <View className="gap-3 px-5">
        <SkeletonCard />
        <SkeletonCard />
      </View>
    );
  }

  const statusLabel = (status: ReturnHandoverStatus): string =>
    t(`handovers.status.${status}` as MessageKey);

  return (
    <View className="flex-1">
      <ScrollView
        keyboardShouldPersistTaps="handled"
        contentContainerClassName="gap-3 px-5 pb-28"
        refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
      >
        {rows.length === 0 ? (
          <EmptyState
            art="box"
            icon={Undo2}
            title={t('handovers.empty')}
            hint={t('handovers.emptyHint')}
          />
        ) : (
          rows.map((handover, index) => (
            <FadeIn key={handover.id} index={index}>
              <Card
                className="gap-2"
                onPress={() => router.push(`/(courier)/handover/${handover.code}`)}
              >
                <View className="flex-row items-center justify-between gap-3">
                  <Text className="flex-1 font-mono text-[16px] font-bold text-foreground">
                    {handover.code}
                  </Text>
                  <StatusBadge
                    label={statusLabel(handover.status)}
                    color={RETURN_HANDOVER_STATUS_COLORS[handover.status]}
                  />
                </View>

                {/*
                  Ce que le point 1 demande de voir d'un coup d'œil : la date,
                  le nombre de colis, les vendeurs, l'état et l'ORIGINE.
                */}
                <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
                  {handover.origin === 'admin'
                    ? t('handovers.createdByAdmin')
                    : t('handovers.createdByCourier')}
                  {' · '}
                  {formatStamp(handover.createdAt, locale)}
                  {' · '}
                  {t('handovers.parcels', { count: handover.parcelCount })}
                </Text>

                {handover.clientNames.length > 0 ? (
                  <Text className="text-[13px] text-muted-foreground" numberOfLines={1}>
                    {handover.clientNames.join(', ')}
                  </Text>
                ) : null}
              </Card>
            </FadeIn>
          ))
        )}
      </ScrollView>

      {/*
        LE BOUTON FLOTTANT « + Créer un bon de retour » — point 1.

        **En bas à droite, au-dessus de la liste.** Le livreur ouvre cet onglet
        pour faire UNE chose, et il la fait d'une main. `end-5` et non
        `right-5` : en arabe l'interface se retourne, et le pouce aussi.
      */}
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={t('handovers.new')}
        onPress={() => router.push('/(courier)/handover/new')}
        className="absolute bottom-5 end-5 flex-row items-center gap-2 rounded-full bg-primary px-5 py-4 shadow-lg"
        style={({ pressed }) => (pressed ? { opacity: 0.85 } : null)}
      >
        <Plus size={20} className="text-primary-foreground" />
        <Text className="text-[15px] font-semibold text-primary-foreground">
          {t('handovers.new')}
        </Text>
      </Pressable>
    </View>
  );
}

/**
 * VOLET 5 — L'HISTORIQUE des bons de retour clôturés.
 *
 * **Les circuits passés n'y sont plus** — Recette 5, point 2, validé par
 * Anass : le livreur ne voit que ses circuits OUVERTS, l'historique des
 * circuits reste à l'admin, sur le web. Les RL- clôturés restent : ce sont ses
 * remises signées, pas des circuits.
 *
 * **Lecture seule, et l'écran le montre en ne portant aucun bouton.**
 */
function HistoryVolet() {
  const { t, locale } = useI18n();
  const handovers = useHandovers('closed');
  const pull = usePullToRefresh(handovers);

  const closedHandovers = handovers.data?.items ?? [];

  if (handovers.isLoading) {
    return (
      <View className="gap-3 px-5">
        <SkeletonCard />
        <SkeletonCard />
      </View>
    );
  }

  return (
    <ScrollView
      keyboardShouldPersistTaps="handled"
      contentContainerClassName="gap-3 px-5 pb-6"
      refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
    >
      {closedHandovers.length === 0 ? (
        <EmptyState
          art="box"
          icon={History}
          title={t('handovers.historyEmpty')}
          hint={t('handovers.historyEmptyHint')}
        />
      ) : (
        closedHandovers.map((handover, index) => (
          <FadeIn key={handover.id} index={index}>
            <Card className="gap-1">
              <View className="flex-row items-center justify-between gap-3">
                <Text className="flex-1 font-mono text-[15px] font-bold text-foreground">
                  {handover.code}
                </Text>
                <Text className="text-[12px] text-muted-foreground">
                  {formatStamp(handover.closedAt, locale)}
                </Text>
              </View>
              <Text className="text-[13px] text-muted-foreground" numberOfLines={1}>
                {t('handovers.title')} · {handover.parcelCount} · {handover.agencyName}
              </Text>
            </Card>
          </FadeIn>
        ))
      )}
    </ScrollView>
  );
}
