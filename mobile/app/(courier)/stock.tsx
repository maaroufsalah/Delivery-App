import {
  findParcelStatus,
  phonesOf,
  routeTargetsSender,
  type CourierDeliveryParcel,
} from '@delivery/shared';
import { useRouter } from 'expo-router';
import { Boxes, PackageCheck, Phone, ScanLine } from 'lucide-react-native';
import { memo, useCallback, useMemo, useState } from 'react';
import { FlatList, Linking, Pressable, RefreshControl, ScrollView, Text, View } from 'react-native';
import { CartonTotal } from '@/components/carton-count';
import { CircuitCard } from '@/components/circuit-card';
import { DeliverPanel } from '@/components/deliver-panel';
import { useI18n } from '@/i18n';
import { useDeliveries, useRoutes } from '@/lib/courier';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useRefetchOnFocus } from '@/lib/use-refetch-on-focus';
import {
  Button,
  Card,
  Chip,
  EmptyState,
  Field,
  Screen,
  SectionTitle,
  Sheet,
  SkeletonCard,
  StatusBadge,
} from '@/ui';

/**
 * LE MENU STOCK : TOUS MES COLIS À LIVRER, EN UNE LISTE — chantier « Livraisons
 * par circuits + menu Stock », qui corrige D-242.
 *
 * **D'où vient cet écran.** D-242 (demande de Badr, SpeedyTrans) avait fait de
 * cette liste unique le menu « Livraisons », À LA PLACE de l'entrée par
 * circuits. La demande avait été mal transmise : la liste ne remplace pas les
 * circuits, elle vit à côté. « Livraisons » est redevenu l'entrée par circuits
 * (`circuits.tsx`) ; cette liste a sa tuile d'accueil, « Stock », et sa route.
 * L'écran lui-même est resté tel quel.
 *
 * **C'est le serveur qui compose la liste** (`GET /courier/me/deliveries`) :
 * un colis y reste tant qu'il n'est pas livré, avec son dernier statut ;
 * « Livré » l'en sort, un bon de retour `RL-` aussi. L'écran ne cache aucun
 * bouton pour faire tenir une règle — il affiche ce qu'on lui rend.
 *
 * **La ligne porte les deux gestes** : « Livrer » (un appui, chèque
 * facultatif) et « Autre statut » — le même `DeliverPanel` que la fiche et la
 * carte du scan, donc les mêmes refus, la même file hors ligne.
 *
 * **Un brouillon n'est pas parti** : ses colis ne sont pas à livrer. Il reste
 * en pied de liste, avec sa carte et son bouton « Démarrer », tant qu'il existe.
 */
export default function CourierStock() {
  const { t } = useI18n();
  const router = useRouter();

  const deliveries = useDeliveries();
  const draft = useRoutes('draft');
  const pull = usePullToRefresh(deliveries, draft);
  // Un circuit clos, un colis posé sur un bon : la liste suit au retour sur l'écran.
  useRefetchOnFocus(deliveries, draft);

  const [search, setSearch] = useState('');
  const [circuit, setCircuit] = useState<string | null>(null);
  const [city, setCity] = useState<string | null>(null);
  /** Le colis dont on rend compte, et par quel bouton on y est entré. */
  const [acting, setActing] = useState<{ parcel: CourierDeliveryParcel; other: boolean } | null>(
    null,
  );

  const all = useMemo(() => deliveries.data?.parcels ?? [], [deliveries.data]);
  const circuits = useMemo(() => [...new Set(all.map((parcel) => parcel.routeCode))], [all]);
  const cities = useMemo(
    () => [...new Set(all.map((parcel) => parcel.destinationCityName))].sort(),
    [all],
  );

  // Un filtre dont le dernier colis vient de sortir ne doit pas vider l'écran.
  const activeCircuit = circuit !== null && circuits.includes(circuit) ? circuit : null;
  const activeCity = city !== null && cities.includes(city) ? city : null;

  const parcels = useMemo(() => {
    const needle = search.trim().toLowerCase();
    const digits = needle.replace(/\D/g, '');

    return all.filter((parcel) => {
      if (activeCircuit && parcel.routeCode !== activeCircuit) return false;
      if (activeCity && parcel.destinationCityName !== activeCity) return false;
      if (!needle) return true;

      return (
        parcel.code.toLowerCase().includes(needle) ||
        (parcel.externalCode ?? '').toLowerCase().includes(needle) ||
        parcel.recipientName.toLowerCase().includes(needle) ||
        // Le téléphone se cherche par ses chiffres : « 06 12 » trouve « 0612… ».
        (digits.length >= 3 && parcel.recipientPhone.replace(/\D/g, '').includes(digits))
      );
    });
  }, [activeCircuit, activeCity, all, search]);

  // Les brouillons de LIVRAISON seulement : ceux de ramassage vivent sous « Ramassage ».
  const drafts = (draft.data ?? []).filter((route) => !routeTargetsSender(route.kind));

  const openParcel = useCallback(
    (code: string) => router.push(`/(courier)/parcel/${encodeURIComponent(code)}`),
    [router],
  );
  const deliver = useCallback(
    (parcel: CourierDeliveryParcel) => setActing({ parcel, other: false }),
    [],
  );
  const other = useCallback(
    (parcel: CourierDeliveryParcel) => setActing({ parcel, other: true }),
    [],
  );

  const renderItem = useCallback(
    ({ item }: { item: CourierDeliveryParcel }) => (
      <DeliveryRow item={item} onOpen={openParcel} onDeliver={deliver} onOther={other} />
    ),
    [deliver, openParcel, other],
  );

  const loading = deliveries.isLoading || draft.isLoading;
  const counter = deliveries.data
    ? t('deliveries.counter', {
        delivered: deliveries.data.deliveredCount,
        total: deliveries.data.totalCount,
      })
    : '';

  return (
    <Screen>
      {/* --- Le titre, « X livrés / Y », et le scanner à UN geste --- */}
      <View className="flex-row items-center justify-between gap-3 px-5 pb-2 pt-4">
        <View className="flex-1 gap-0.5">
          <Text className="text-[22px] font-bold text-foreground">{t('stock.title')}</Text>
          {counter ? (
            <Text className="text-[15px] font-semibold tabular-nums text-muted-foreground">
              {counter}
            </Text>
          ) : null}
          {/* « 12 colis · 19 cartons » — ce qu'il reste à livrer, cartons compris. */}
          <CartonTotal lines={all} />
        </View>

        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('scan.title')}
          onPress={() => router.push('/(courier)/scan?kind=delivery')}
          className="bg-accent min-h-11 min-w-11 items-center justify-center rounded-2xl"
          hitSlop={8}
          style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
        >
          <ScanLine size={22} className="text-accent-foreground" />
        </Pressable>
      </View>

      <View className="gap-2 px-5 pb-2">
        <Field
          label={t('common.search')}
          value={search}
          onChangeText={setSearch}
          placeholder={t('deliveries.searchPlaceholder')}
          autoCapitalize="none"
          autoCorrect={false}
        />

        {/* --- Les filtres : par circuit, par ville. Montrés s'il y a un choix. --- */}
        {circuits.length > 1 ? (
          <ScrollView
            horizontal
            showsHorizontalScrollIndicator={false}
            keyboardShouldPersistTaps="handled"
            contentContainerClassName="gap-2"
          >
            <Chip
              label={t('deliveries.allCircuits')}
              active={activeCircuit === null}
              onPress={() => setCircuit(null)}
            />
            {circuits.map((code) => (
              <Chip
                key={code}
                label={code}
                active={activeCircuit === code}
                onPress={() => setCircuit(code)}
              />
            ))}
          </ScrollView>
        ) : null}
        {cities.length > 1 ? (
          <ScrollView
            horizontal
            showsHorizontalScrollIndicator={false}
            keyboardShouldPersistTaps="handled"
            contentContainerClassName="gap-2"
          >
            <Chip
              label={t('deliveries.allCities')}
              active={activeCity === null}
              onPress={() => setCity(null)}
            />
            {cities.map((name) => (
              <Chip
                key={name}
                label={name}
                active={activeCity === name}
                onPress={() => setCity(name)}
              />
            ))}
          </ScrollView>
        ) : null}
      </View>

      <FlatList
        keyboardShouldPersistTaps="handled"
        data={loading ? [] : parcels}
        keyExtractor={parcelKey}
        contentContainerClassName="gap-2 px-5 pb-8"
        refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
        renderItem={renderItem}
        ListEmptyComponent={
          loading ? (
            <View className="gap-3">
              <SkeletonCard />
              <SkeletonCard />
              <SkeletonCard />
            </View>
          ) : all.length > 0 ? (
            <Text className="py-6 text-center text-[15px] text-muted-foreground">
              {t('deliveries.noMatch')}
            </Text>
          ) : (
            <EmptyState
              art="route"
              icon={Boxes}
              title={t('deliveries.empty')}
              hint={t('scan.hint')}
            />
          )
        }
        ListFooterComponent={
          drafts.length > 0 ? (
            <View className="gap-3 pt-4">
              <SectionTitle label={t('deliveries.toStart')} />
              {drafts.map((route) => (
                <CircuitCard key={route.id} route={route} />
              ))}
            </View>
          ) : null
        }
      />

      {/*
        --- RENDRE COMPTE DEPUIS LA LIGNE ---
        Le même panneau que la fiche et la carte du scan. Fermé, la liste se
        relit par les invalidations de `useReport` : un livré en sort, un autre
        statut y reste avec son nouveau badge.
      */}
      <Sheet
        open={acting !== null}
        onClose={() => setActing(null)}
        title={acting?.parcel.code ?? ''}
      >
        {acting ? (
          <View className="gap-3 px-2 pb-3">
            <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
              {acting.parcel.recipientName} · {acting.parcel.routeCode}
            </Text>
            <DeliverPanel
              key={`${acting.parcel.id}-${acting.other ? 'other' : 'deliver'}`}
              code={acting.parcel.code}
              codToCollect={acting.parcel.codToCollect}
              kind="delivery"
              statusId={acting.parcel.statusId}
              initialPicker={acting.other}
              onDone={() => setActing(null)}
            />
          </View>
        ) : null}
      </Sheet>
    </Screen>
  );
}

const parcelKey = (parcel: CourierDeliveryParcel) => parcel.id;

/**
 * UNE LIGNE : le destinataire, son téléphone, sa ville, le montant, le dernier
 * statut — et les deux gestes. Mémoïsée : ouvrir la feuille ne redessine pas
 * deux cents lignes (lot C, point C3).
 */
const DeliveryRow = memo(function DeliveryRow({
  item,
  onOpen,
  onDeliver,
  onOther,
}: {
  item: CourierDeliveryParcel;
  onOpen: (code: string) => void;
  onDeliver: (parcel: CourierDeliveryParcel) => void;
  onOther: (parcel: CourierDeliveryParcel) => void;
}) {
  const { t, status } = useI18n();
  const definition = findParcelStatus(item.statusId);
  const area = item.destinationDistrictName
    ? `${item.destinationCityName} · ${item.destinationDistrictName}`
    : item.destinationCityName;

  return (
    <Card className="gap-2.5 py-3" onPress={() => onOpen(item.code)}>
      <View className="flex-row items-start gap-3">
        <View className="flex-1 gap-0.5">
          <Text className="text-[16px] font-semibold text-foreground" numberOfLines={1}>
            {item.recipientName}
          </Text>
          <Text className="text-[14px] tabular-nums text-muted-foreground" numberOfLines={1}>
            {item.recipientPhone}
          </Text>
          <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
            {area}
          </Text>
        </View>

        <View className="items-end gap-1">
          <Text className="text-[16px] font-bold tabular-nums text-foreground">
            {money(item.codToCollect)}
          </Text>
          {(item.quantity ?? 1) > 1 ? (
            <Text className="text-[13px] font-bold text-foreground">
              {t('parcel.cartons', { count: item.quantity ?? 1 })}
            </Text>
          ) : null}
        </View>
      </View>

      <View className="flex-row flex-wrap items-center gap-2">
        {definition ? (
          <StatusBadge label={status(definition.key)} color={definition.color} />
        ) : null}
        <Text className="font-mono text-[12px] text-muted-foreground" numberOfLines={1}>
          {item.code} · {item.routeCode}
        </Text>
      </View>

      <View className="flex-row items-center gap-2">
        {/* D-244 : jusqu'à trois numéros, UN bouton par numéro, chacun le sien. */}
        {phonesOf(item.recipientPhone).map((phone, _index, phones) => (
          <Pressable
            key={phone}
            accessibilityRole="button"
            accessibilityLabel={phones.length > 1 ? t('run.callNumber', { phone }) : t('run.call')}
            className="min-h-11 min-w-11 items-center justify-center rounded-xl bg-muted"
            hitSlop={8}
            onPress={() => void Linking.openURL(`tel:${phone}`)}
          >
            <Phone size={20} className="text-foreground" />
          </Pressable>
        ))}
        <View className="flex-1">
          <Button
            label={t('parcelAction.deliver')}
            icon={PackageCheck}
            onPress={() => onDeliver(item)}
            full
          />
        </View>
        <View className="flex-1">
          <Button
            label={t('parcelAction.otherStatus')}
            tone="ghost"
            onPress={() => onOther(item)}
            full
          />
        </View>
      </View>
    </Card>
  );
});

/** Centimes → dirhams, sans dépendance de formatage. */
function money(cents: number): string {
  return `${(cents / 100).toFixed(2)} MAD`;
}
