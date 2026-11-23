import {
  courierOutcomesFor,
  findParcelStatus,
  phonesOf,
  routeParcelAwaitsOutcome,
  routeTargetsSender,
  type CourierRouteParcel,
} from '@delivery/shared';
import * as Haptics from 'expo-haptics';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useFreshKey } from '@/lib/use-fresh-key';
import {
  Check,
  CircleCheck,
  ClipboardList,
  Navigation,
  PackageCheck,
  Phone,
  Play,
  Truck,
} from 'lucide-react-native';
import { memo, useCallback, useMemo, useState } from 'react';
import { FlatList, Linking, Pressable, RefreshControl, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { CartonTotal } from '@/components/carton-count';
import { DeliverPanel } from '@/components/deliver-panel';
import { useI18n } from '@/i18n';
import { useBulkJob, useBulkOutcome, useDispatch, useReturnRoutes, useRoutes } from '@/lib/courier';
import { isBulkJobAccepted } from '@delivery/shared';
import { useRefetchOnFocus } from '@/lib/use-refetch-on-focus';
import { useRouteGone } from '@/lib/use-route-gone';
import { routeStopOf } from '@/lib/route-stop';
import { describeActionError } from '@/lib/use-action';
import {
  Button,
  Card,
  EmptyState,
  Field,
  Screen,
  ScreenHeader,
  Sheet,
  SheetItem,
  SkeletonCard,
  StatusBadge,
  useToast,
} from '@/ui';

const DELIVERED = 4;
const PICKED_UP_BY_COURIER = 18;

/**
 * LE DÉTAIL D'UN CIRCUIT — M1 « Ma tournée », L2 bloc 3, allégé par le chantier
 * « Sélection en masse sur les circuits » (D-146).
 *
 * **L'en-tête ne dit que trois choses** : le code, « X sur Y traités », la
 * recherche. Les onglets Liste / Carte, la barre et le montant total ont quitté
 * l'en-tête : la « carte » n'était qu'une liste d'itinéraires, et l'itinéraire
 * du prochain arrêt reste au pied de l'écran.
 *
 * **Une ligne, le minimum** (capture de CIR-2610-0011 : vendeur deux fois,
 * « Référence commande », une pastille « 0 » — le rang d'un colis venu d'un
 * bon). Ramassage : code ou référence, vendeur, quartier, statut, une action.
 * Livraison : code, destinataire, quartier, montant, statut, appel, une action.
 * Le reste est dans la fiche, au toucher.
 *
 * **Tout cocher, puis UNE action** — ramassage : « ramassés par le livreur »
 * (18), la clôture fait le reste ; livraison : un résultat commun, « Livré » en
 * espèces seulement. L'API rejoue l'action unitaire, en une transaction.
 *
 * **Un seul bouton au pied** : l'action en masse quand des colis sont cochés,
 * sinon « Ramassage / Livraison en cours » vers le prochain arrêt, ou
 * « Démarrer » sur un brouillon.
 */
function CircuitScreenContent() {
  const { code } = useLocalSearchParams<{ code: string }>();
  const { t, status } = useI18n();
  const router = useRouter();
  const toast = useToast();

  const active = useRoutes('active');
  // Les circuits de retour, demandés à part (lot C, C2, D-179) : même écran.
  const returns = useReturnRoutes();
  const pull = usePullToRefresh(active, returns);
  const drafts = useRoutes('draft');
  const dispatch = useDispatch();
  const bulk = useBulkOutcome(code ?? '');
  /*
   * Lot W2 (D-239) : la tâche de masse en cours, suivie jusqu'au bout. Sa fin
   * se dit une fois ; l'écran se relit (`useBulkJob`).
   */
  const [jobId, setJobId] = useState<string | null>(null);
  const job = useBulkJob(jobId);
  const jobData = job.data;
  if (jobData && (jobData.status === 'done' || jobData.status === 'failed') && jobId) {
    setJobId(null);
    toast.show(
      jobData.status === 'done'
        ? t('circuits.bulkJobDone', { count: jobData.processed - jobData.failures.length })
        : t('circuits.bulkJobFailed'),
      jobData.status === 'done' && jobData.failures.length === 0 ? undefined : 'error',
    );
  }


  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [asking, setAsking] = useState(false);
  /** Le colis dont on rend compte DEPUIS la liste — Livrer en un appui (D-156). */
  const [acting, setActing] = useState<CourierRouteParcel | null>(null);
  /**
   * Entré par « Autre statut » : la feuille des issues s'ouvre d'emblée
   * (chantier « Livraisons par circuits »). Un booléen à part, pour que `onAct`
   * reste `setActing` — un rappel stable, hors des dépendances de la ligne.
   */
  const [otherFirst, setOtherFirst] = useState(false);
  const closeActing = () => {
    setActing(null);
    setOtherFirst(false);
  };

  // Relu à chaque retour sur la fiche : l'agence a pu clore ce circuit.
  useRefetchOnFocus(active, drafts, returns);

  /*
   * LE CIRCUIT DEMANDÉ, ET LUI SEUL — chantier « Mobile : Retour + circuits
   * clôturés ». Le repli `?? active.data?.[0]` montrait silencieusement un
   * AUTRE circuit quand celui-ci n'existait plus : le livreur rendait compte
   * sur la mauvaise tournée sans le savoir. Disparu = on le dit, et on revient.
   */
  const route = [...(active.data ?? []), ...(drafts.data ?? []), ...(returns.data ?? [])].find(
    (row) => row.code === code,
  );
  useRouteGone(
    !route &&
      active.isFetched &&
      drafts.isFetched &&
      returns.isFetched &&
      !active.isFetching &&
      !returns.isFetching,
  );

  const kind = route?.kind ?? 'delivery';
  const toSender = routeTargetsSender(kind);
  const pickup = toSender && kind === 'pickup';
  const isReturn = kind === 'return';
  // Le retour se consulte depuis le menu Retours : c'est là qu'on revient.
  const backHref = isReturn ? '/(courier)/returns' : '/(courier)/circuits';

  const parcels = useMemo(() => {
    // 11/11/2026 : sur une LIVRAISON, un colis « Livré » n'est plus à traiter —
    // il quitte la liste (ni grisé, ni en bas). La progression du circuit, elle,
    // les compte toujours : elle vient du serveur.
    const all = (route?.parcels ?? []).filter(
      (parcel) => kind !== 'delivery' || parcel.statusId !== DELIVERED,
    );
    const needle = search.trim().toLowerCase();

    // On cherche celui qu'on va VOIR, pas l'autre bout du trajet.
    const matching = needle
      ? all.filter((parcel) => {
          const stop = routeStopOf(kind, parcel);

          return (
            parcel.code.toLowerCase().includes(needle) ||
            (parcel.externalCode ?? '').toLowerCase().includes(needle) ||
            stop.name.toLowerCase().includes(needle) ||
            stop.address.toLowerCase().includes(needle)
          );
        })
      : all;

    // Les traités en bas : ce qui reste à faire doit être sous le pouce.
    return [...matching].sort(
      (a, b) => Number(isSettled(kind, a)) - Number(isSettled(kind, b)),
    );
  }, [kind, route, search]);

  const started = route ? route.status !== 'draft' : false;

  const toggle = useCallback(
    (id: string) =>
      setSelected((current) => {
        const nextSet = new Set(current);
        if (nextSet.has(id)) nextSet.delete(id);
        else nextSet.add(id);
        return nextSet;
      }),
    [],
  );
  const openParcel = useCallback(
    (parcelCode: string) => router.push(`/(courier)/parcel/${encodeURIComponent(parcelCode)}`),
    [router],
  );

  /*
   * LES LIGNES NE SE RE-RENDENT PLUS À L'OUVERTURE D'UNE FEUILLE — lot C,
   * point C3. `renderItem` était une fonction neuve à chaque rendu : ouvrir
   * « Rendre compte » (un `setAsking`) faisait re-rendre les ~200 lignes du
   * circuit — icônes SVG, classes NativeWind — AVANT que la feuille ne monte.
   * Le Modal arrivait tard, le second appui tombait sur son voile et le
   * refermait. Désormais : rappel stable, ligne mémoïsée ; seule la ligne
   * cochée ou décochée se redessine.
   */
  const renderItem = useCallback(
    ({ item }: { item: CourierRouteParcel }) => (
      <ParcelRow
        item={item}
        kind={kind}
        pickup={pickup}
        toSender={toSender}
        started={started}
        canSelect={started && routeParcelAwaitsOutcome(kind, item)}
        isSelected={selected.has(item.id)}
        onToggle={toggle}
        onAct={setActing}
        onOtherFirst={setOtherFirst}
        onOpen={openParcel}
      />
    ),
    [kind, openParcel, pickup, selected, started, toSender, toggle],
  );

  if (active.isLoading || drafts.isLoading || returns.isLoading) {
    return (
      <Screen className="gap-3 px-5 pt-4">
        <SkeletonCard />
        <SkeletonCard />
        <SkeletonCard />
      </Screen>
    );
  }

  if (!route) {
    return (
      <Screen>
        <ScreenHeader title={t('circuits.title')} backHref={backHref} />
        <EmptyState art="route" icon={Truck} title={t('circuits.empty')} hint={t('scan.hint')} />
      </Screen>
    );
  }

  const done = route.parcelCount - route.remainingCount;

  /** Le prochain arrêt : le premier colis dont l'issue n'est pas rapportée. */
  const next = [...route.parcels]
    .filter((parcel) => !parcel.confirmed)
    .sort((a, b) => a.sequence - b.sequence)[0];

  // La sélection ne porte que sur les colis qui ATTENDENT encore l'action,
  // visibles (recette 6, point 2 : ramassage = pas encore 18, livraison = sans
  // résultat). Un colis traité entre-temps sort de la sélection.
  const awaits = (parcel: (typeof route.parcels)[number]) =>
    started && routeParcelAwaitsOutcome(kind, parcel);
  const selectable = parcels.filter(awaits);
  const chosen = route.parcels.filter((parcel) => selected.has(parcel.id) && awaits(parcel));
  const noneLeft = started && route.parcels.length > 0 && !route.parcels.some(awaits);
  const allSelected =
    selectable.length > 0 && selectable.every((parcel) => selected.has(parcel.id));

  // Livraison : les issues que le livreur peut poser sur TOUS les cochés, « Livré » en tête.
  const outcomes =
    chosen
      .reduce<number[] | null>((acc, parcel) => {
        const allowed = courierOutcomesFor(parcel.statusId);
        return acc === null ? allowed : acc.filter((id) => allowed.includes(id));
      }, null)
      ?.sort((a, b) => Number(b === DELIVERED) - Number(a === DELIVERED)) ?? [];
  const cash = chosen.reduce((sum, parcel) => sum + parcel.codToCollect, 0);

  const statusLabel = (id: number) => {
    const definition = findParcelStatus(id);
    return definition ? status(definition.key) : `#${id}`;
  };

  const apply = (statusId: number) => {
    setAsking(false);
    bulk.mutate(
      { parcelIds: chosen.map((parcel) => parcel.id), statusId },
      {
        onSuccess: (result) => {
          void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success);
          setSelected(new Set());
          // Lot W2 (D-239) : plus de 300 colis — en cours en arrière-plan, l'écran suit.
          if (isBulkJobAccepted(result)) {
            setJobId(result.job.id);
            toast.show(t('circuits.bulkRunning', { count: result.job.total }));
            return;
          }
          toast.show(
            pickup
              ? t('circuits.bulkDonePicked', { count: result.count })
              : t('circuits.bulkDone', { count: result.count, status: statusLabel(statusId) }),
          );
        },
        onError: (error) => {
          toast.show(describeActionError(error, t), 'error');
          // « Circuit clôturé » pendant qu'on y travaillait : la relecture le
          // retire, et `useRouteGone` ramène à la liste.
          void active.refetch();
          void returns.refetch();
        },
      },
    );
  };

  return (
    <Screen>
      {/* --- L'en-tête : le code, « X sur Y traités », la recherche. Rien d'autre. --- */}
      <ScreenHeader
        backHref={backHref}
        title={route.code}
        subtitle={
          // « 10 livrés · 2 à retourner (1 bon en attente) » sur une livraison —
          // Recette 7, point 8 (D-155) : le circuit se clôt tout seul à la fin.
          // Ramassage et retour : « X sur Y traités ».
          pickup || isReturn
            ? t('circuits.processed', { done, total: route.parcelCount })
            : `${t('circuits.autoProgress', {
                delivered: route.deliveredCount,
                toReturn: route.toReturnCount,
              })}${
                route.pendingHandoverCount > 0
                  ? ` (${t('circuits.autoPending', { count: route.pendingHandoverCount })})`
                  : ''
              }`
        }
      />

      <View className="gap-2 px-5 pb-2">
        {/* « 12 colis · 19 cartons » — ramassage, livraison, retour : le même total. */}
        <CartonTotal lines={route.parcels} />
        {/*
          LE BANDEAU « À DÉMARRER » — un circuit de livraison en brouillon n'est
          pas parti : ses lignes n'ont ni « Livrer » ni « Autre statut », et le
          bouton du pied le démarre. Le ramassage garde son écran d'avant.
        */}
        {!started && kind === 'delivery' ? (
          <View className="bg-accent flex-row items-center gap-2 rounded-xl px-3 py-2">
            <Play size={16} className="text-accent-foreground" />
            <Text className="text-accent-foreground text-[14px] font-semibold">
              {t('deliveries.toStart')}
            </Text>
          </View>
        ) : null}
        <Field
          label={t('common.search')}
          value={search}
          onChangeText={setSearch}
          placeholder={t('common.search')}
          autoCapitalize="none"
          autoCorrect={false}
        />

        {/*
          LES BONS DE LA TOURNÉE — retouches Anass, point 5 : un vendeur, un bon,
          une validation (partielle si besoin, D-139). Une ligne chacun.
        */}
        {route.notes.map((note) => {
          const picked = note.status === 'picked-up-by-courier' || note.status === 'validated';
          return (
            <Card
              key={note.id}
              className={`flex-row items-center gap-3 py-2.5 ${picked ? 'opacity-50' : ''}`}
              onPress={() =>
                router.push(
                  `/(courier)/note/${encodeURIComponent(note.id)}?route=${encodeURIComponent(route.code)}`,
                )
              }
            >
              {picked ? (
                <PackageCheck size={18} className="text-success" />
              ) : (
                <ClipboardList size={18} className="text-primary" />
              )}
              <Text className="flex-1 text-[15px] text-foreground" numberOfLines={1}>
                {note.code} · {t('circuits.noteParcels', { count: note.parcelCount })}
              </Text>
              {picked ? (
                <Text className="text-[13px] text-success">{t('circuits.notePickedUp')}</Text>
              ) : null}
            </Card>
          );
        })}

        {/* --- Tout cocher, et le compteur --- */}
        {selectable.length > 0 ? (
          <Pressable
            accessibilityRole="checkbox"
            hitSlop={6}
            accessibilityState={{ checked: allSelected }}
            onPress={() =>
              setSelected(allSelected ? new Set() : new Set(selectable.map((parcel) => parcel.id)))
            }
            className="min-h-11 flex-row items-center gap-3"
          >
            <Box checked={allSelected} />
            <Text className="flex-1 text-[15px] font-medium text-foreground">
              {t('circuits.selectAll')}
            </Text>
            <Text className="text-[14px] tabular-nums text-muted-foreground">
              {t('circuits.selected', { count: chosen.length, total: selectable.length })}
            </Text>
          </Pressable>
        ) : noneLeft ? (
          <Text className="text-[14px] text-muted-foreground">
            {pickup ? t('circuits.nonePicked') : t('circuits.noneReport')}
          </Text>
        ) : null}
      </View>

      <FlatList
        keyboardShouldPersistTaps="handled"
        data={parcels}
        keyExtractor={parcelKey}
        contentContainerClassName="gap-2 px-5 pb-4"
        refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
        renderItem={renderItem}
      />

      {/* --- UN bouton au pied — jamais sous le bouton Scanner (la barre réserve sa place). --- */}
      <View className="px-5 pb-4 pt-1">
        {!started ? (
          <Button
            key="start"
            label={t('circuits.start')}
            icon={Play}
            busy={dispatch.isPending}
            disabled={route.parcelCount === 0}
            onPress={() =>
              dispatch.mutate(route.code, {
                onSuccess: () =>
                  void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success),
                // Aucune action perdue en silence (Recette 5, point 4 a).
                onError: (error) => toast.show(describeActionError(error, t), 'error'),
              })
            }
            full
          />
        ) : chosen.length > 0 ? (
          <Button
            label={
              pickup
                ? t('circuits.bulkPicked', { count: chosen.length })
                : t('circuits.bulkReport', { count: chosen.length })
            }
            key="bulk"
            icon={PackageCheck}
            busy={bulk.isPending}
            onPress={() => setAsking(true)}
            full
          />
        ) : next ? (
          <Button
            key="next"
            label={
              pickup
                ? t('circuits.pickupInProgress')
                : isReturn
                  ? t('circuits.returnInProgress')
                  : t('circuits.inProgress')
            }
            icon={Navigation}
            tone="soft"
            onPress={() => router.push(`/(courier)/navigate/${encodeURIComponent(next.code)}`)}
            full
          />
        ) : null}
      </View>

      {/* --- La confirmation de l'action en masse --- */}
      <Sheet
        open={asking}
        onClose={() => setAsking(false)}
        title={
          pickup
            ? t('circuits.bulkPickedTitle', { count: chosen.length })
            : t('circuits.bulkReportTitle', { count: chosen.length })
        }
      >
        {pickup ? (
          <View className="gap-3 px-2 pb-3">
            <Text className="text-[15px] text-muted-foreground">
              {t('circuits.bulkPickedHint')}
            </Text>
            <Button
              label={t('circuits.bulkConfirm')}
              onPress={() => apply(PICKED_UP_BY_COURIER)}
              full
            />
          </View>
        ) : outcomes.length === 0 ? (
          <Text className="px-2 pb-4 text-[15px] text-muted-foreground">
            {t('circuits.bulkNoCommon')}
          </Text>
        ) : (
          <View className="gap-1 pb-2">
            {outcomes.map((id) => (
              <SheetItem
                key={id}
                icon={id === DELIVERED ? PackageCheck : CircleCheck}
                label={statusLabel(id)}
                hint={
                  id === DELIVERED
                    ? `${t('circuits.bulkCash', { amount: money(cash) })} · ${t('circuits.bulkChequeNote')}`
                    : undefined
                }
                onPress={() => apply(id)}
              />
            ))}
          </View>
        )}
      </Sheet>

      {/*
        --- RENDRE COMPTE D'UN COLIS DEPUIS LA LISTE — « Livrer en un appui » ---
        Le même panneau que la carte du scan et la fiche : « Livrer · X MAD »,
        chèque facultatif, « Autre statut ». Fermé, la liste se relit par les
        invalidations de `useReport` et ses compteurs suivent.
      */}
      <Sheet open={acting !== null} onClose={closeActing} title={acting?.code ?? ''}>
        {acting ? (
          <View className="gap-3 px-2 pb-3">
            <Pressable
              accessibilityRole="button"
              onPress={() => {
                const code = acting.code;
                closeActing();
                router.push(`/(courier)/parcel/${encodeURIComponent(code)}`);
              }}
              hitSlop={6}
              className="min-h-10 justify-center"
            >
              <Text className="text-[14px] text-muted-foreground underline">
                {`${routeStopOf(route.kind, acting).name} · ${t('scanCard.open')}`}
              </Text>
            </Pressable>
            <DeliverPanel
              key={`${acting.id}-${otherFirst ? 'other' : 'deliver'}`}
              code={acting.code}
              codToCollect={acting.codToCollect}
              kind={route.kind}
              statusId={acting.statusId}
              initialPicker={otherFirst}
              onDone={closeActing}
            />
          </View>
        ) : null}
      </Sheet>
    </Screen>
  );
}

const parcelKey = (parcel: CourierRouteParcel) => parcel.id;

/**
 * CE COLIS EST-IL « RÉGLÉ » SUR CETTE LIGNE — chantier « Menu Livraison : liste
 * unique ». Une issue rapportée (`confirmed`) grisait la ligne et retirait son
 * bouton, livrée ou non : un « reporté » ne pouvait plus se rendre compte d'ici.
 * Sur une LIVRAISON, c'est désormais le serveur qui le dit par sa règle : réglé
 * = plus aucune issue ouverte au livreur (livré, ou repris par l'agence).
 * Ramassage et retour gardent `confirmed`.
 */
function isSettled(kind: string, parcel: CourierRouteParcel): boolean {
  if (kind !== 'delivery') return parcel.confirmed;
  return courierOutcomesFor(parcel.statusId, kind).length === 0;
}

/**
 * UNE LIGNE DU CIRCUIT — mémoïsée (lot C, point C3) : elle ne se redessine que
 * si SON colis, sa case ou le circuit changent, jamais parce qu'une feuille
 * s'ouvre au-dessus de la liste.
 */
const ParcelRow = memo(function ParcelRow({
  item,
  kind,
  pickup,
  toSender,
  started,
  canSelect,
  isSelected,
  onToggle,
  onAct,
  onOtherFirst,
  onOpen,
}: {
  item: CourierRouteParcel;
  kind: string;
  pickup: boolean;
  toSender: boolean;
  started: boolean;
  canSelect: boolean;
  isSelected: boolean;
  onToggle: (id: string) => void;
  onAct: (parcel: CourierRouteParcel) => void;
  /** « Autre statut » : la feuille des issues d'emblée (`true`), ou « Livrer » (`false`). */
  onOtherFirst: (other: boolean) => void;
  onOpen: (code: string) => void;
}) {
  const { t, status } = useI18n();
  const definition = findParcelStatus(item.statusId);
  const stop = routeStopOf(kind, item);
  const settled = isSettled(kind, item);
  const canReport = started && !settled;
  // LIVRAISON : les deux gestes écrits en toutes lettres, sous la ligne.
  const twoButtons = canReport && kind === 'delivery';

  return (
    <Card
      className={`gap-2.5 py-3 ${settled ? 'opacity-50' : ''} ${
        isSelected ? 'border border-primary' : ''
      }`}
      onPress={() => onOpen(item.code)}
    >
      <View className="flex-row items-center gap-3">
        {canSelect ? (
          <Pressable
            accessibilityRole="checkbox"
            accessibilityState={{ checked: isSelected }}
            accessibilityLabel={item.code}
            onPress={() => onToggle(item.id)}
            className="min-h-11 min-w-8 items-center justify-center"
            hitSlop={8}
          >
            <Box checked={isSelected} />
          </Pressable>
        ) : null}

        <View className="flex-1 gap-0.5">
          <Text className="font-mono text-[14px] text-muted-foreground" numberOfLines={1}>
            {pickup && item.externalCode ? item.externalCode : item.code}
          </Text>
          <Text className="text-[16px] font-semibold text-foreground" numberOfLines={1}>
            {stop.name}
          </Text>
          <View className="flex-row flex-wrap items-center gap-2">
            {stop.area ? (
              <Text className="text-[13px] text-muted-foreground" numberOfLines={1}>
                {stop.area}
              </Text>
            ) : null}
            {definition ? (
              <StatusBadge label={status(definition.key)} color={definition.color} />
            ) : null}
            {/* Lot G (D-208) : plusieurs cartons sous un code — tous à prendre. */}
            {(item.quantity ?? 1) > 1 ? (
              <Text className="text-[13px] font-bold text-foreground">
                {t('parcel.cartons', { count: item.quantity ?? 1 })}
              </Text>
            ) : null}
          </View>
        </View>

        {toSender ? null : (
          <Text className="text-[16px] font-bold tabular-nums text-foreground">
            {money(item.codToCollect)}
          </Text>
        )}

        {/* D-244 : jusqu'à trois numéros, UN bouton par numéro, chacun le sien. */}
        {toSender
          ? null
          : phonesOf(stop.phone).map((phone, _index, phones) => (
              <Pressable
                key={phone}
                accessibilityRole="button"
                accessibilityLabel={
                  phones.length > 1 ? t('run.callNumber', { phone }) : t('run.call')
                }
                className="min-h-11 min-w-11 items-center justify-center rounded-xl bg-muted"
                hitSlop={8}
                onPress={() => void Linking.openURL(`tel:${phone}`)}
              >
                <Phone size={20} className="text-foreground" />
              </Pressable>
            ))}

        {/*
          L'action UNE : rendre compte ICI, en feuille — « Livrer en un
          appui » (D-156, MOB-04). L'icône ouvrait la fiche, où il fallait
          appuyer encore ; la ligne elle-même reste la porte de la fiche.
          Ramassage et retour gardent cette icône ; la livraison a ses deux
          boutons, plus bas.
        */}
        {canReport && !twoButtons ? (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={t('parcel.report')}
            className="min-h-11 min-w-11 items-center justify-center rounded-xl bg-primary"
            hitSlop={8}
            onPress={() => onAct(item)}
          >
            <CircleCheck size={20} className="text-primary-foreground" />
          </Pressable>
        ) : null}
      </View>

      {/*
        « LIVRER » ET « AUTRE STATUT » SUR CHAQUE COLIS À LIVRER — chantier
        « Livraisons par circuits + menu Stock ». Les mêmes deux boutons que la
        liste du Stock, le même `DeliverPanel` derrière. Tant que le colis
        n'est pas « Livré », ils restent : la règle est celle du serveur (D-242).
      */}
      {twoButtons ? (
        <View className="flex-row items-center gap-2">
          <View className="flex-1">
            <Button
              label={t('parcelAction.deliver')}
              icon={PackageCheck}
              onPress={() => {
                onOtherFirst(false);
                onAct(item);
              }}
              full
            />
          </View>
          <View className="flex-1">
            <Button
              label={t('parcelAction.otherStatus')}
              tone="ghost"
              onPress={() => {
                onOtherFirst(true);
                onAct(item);
              }}
              full
            />
          </View>
        </View>
      ) : null}
    </Card>
  );
});

/** Une case à cocher, dessinée avec les jetons du tenant. */
function Box({ checked }: { checked: boolean }) {
  return (
    <View
      className={`size-6 items-center justify-center rounded-md border ${
        checked ? 'border-primary bg-primary' : 'border-border'
      }`}
    >
      {checked ? <Check size={14} className="text-primary-foreground" /> : null}
    </View>
  );
}

/** Centimes → dirhams, sans dépendance de formatage. */
function money(cents: number): string {
  return `${(cents / 100).toFixed(2)} MAD`;
}

/** Coque Recette 7 (point 5) : le contenu repart vide à chaque `code` et à chaque fermeture — `useFreshKey`. */
export default function CircuitScreen() {
  const { code } = useLocalSearchParams<{ code: string }>();
  return <CircuitScreenContent key={useFreshKey(code)} />;
}
