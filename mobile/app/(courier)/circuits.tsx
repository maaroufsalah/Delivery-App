import { routeTargetsSender } from '@delivery/shared';
import { useRouter } from 'expo-router';
import { Map as MapIcon, Navigation, ScanLine, Truck } from 'lucide-react-native';
import { Linking, Pressable, RefreshControl, ScrollView, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n } from '@/i18n';
import { useRoutes } from '@/lib/courier';
import { useRefetchOnFocus } from '@/lib/use-refetch-on-focus';
import { routeStopOf } from '@/lib/route-stop';
import { CircuitCard } from '@/components/circuit-card';
import { Button, Card, EmptyState, FadeIn, Screen, SectionTitle, SkeletonCard } from '@/ui';

/**
 * LE MENU LIVRAISONS : PAR CIRCUITS — L2 bloc 3, maquette 5, rétabli par le
 * chantier « Livraisons par circuits + menu Stock ».
 *
 * **Pourquoi cet écran est revenu.** D-242 l'avait remplacé par une liste
 * unique de tous les colis à livrer. La demande de Badr avait été mal
 * transmise : la liste unique ne remplace PAS les circuits, elle vit à côté,
 * sous sa propre tuile d'accueil (« Stock », `stock.tsx`). Ici, on retrouve
 * l'entrée d'avant : les circuits de livraison OUVERTS du livreur, une carte
 * chacun ; on entre dans un circuit pour voir ses colis, avec « Livrer » et
 * « Autre statut » sur chaque ligne (`circuit/[code].tsx`).
 *
 * **Ce qui reste de D-242** : la règle du serveur — un colis reste révisable
 * tant qu'il n'est pas « Livré » — et les colis livrés masqués dans le circuit.
 *
 * **Les brouillons à part, sous « À démarrer »** : un circuit composé au scan
 * et pas encore parti n'est pas une tournée en cours. Sa carte garde la porte
 * vers son écran, où se trouve « Démarrer ».
 *
 * **Pas de carte embarquée.** `react-native-maps` n'est pas lié dans l'APK, et
 * l'OTA ne peut pas ajouter de module natif : « Voir sur la carte » confie
 * l'itinéraire à l'application de cartes du téléphone (B-18).
 */
export default function CourierCircuits() {
  const { t } = useI18n();
  const router = useRouter();

  const active = useRoutes('active');
  const draft = useRoutes('draft');
  const pull = usePullToRefresh(active, draft);
  // Un circuit clos par l'agence quitte la liste au retour sur l'écran.
  useRefetchOnFocus(active, draft);

  /*
   * LIVRAISON SEULEMENT — Recette 5, point 1. Les circuits de ramassage vivent
   * sous « Ramassage » (`pickups.tsx`). L'API n'a pas de paramètre de type : le
   * filtre est ici, sur la même règle que partout (`routeTargetsSender`).
   */
  const routes = (active.data ?? []).filter((route) => !routeTargetsSender(route.kind));
  const drafts = (draft.data ?? []).filter((route) => !routeTargetsSender(route.kind));
  const loading = active.isLoading || draft.isLoading;

  /*
   * LE PROCHAIN ARRÊT, tous circuits PARTIS confondus : le premier colis dont
   * l'issue n'est pas rapportée, dans l'ordre du sac. C'est lui que « Voir sur
   * la carte » ouvre — un itinéraire à huit points que personne n'a optimisé
   * n'aiderait pas, et l'optimisation est au BACKLOG (B-18).
   */
  const next = routes
    .flatMap((route) => route.parcels.map((parcel) => ({ route, parcel })))
    .filter((row) => !row.parcel.confirmed)
    .sort((a, b) => a.parcel.sequence - b.parcel.sequence)[0];

  return (
    <Screen>
      <View className="flex-row items-center justify-between gap-3 px-5 pb-3 pt-4">
        <Text className="flex-1 text-[22px] font-bold text-foreground">{t('circuits.title')}</Text>

        {/* Le scanner reste à UN geste : c'est d'ici qu'un circuit se compose. */}
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

      <ScrollView
        keyboardShouldPersistTaps="handled"
        contentContainerClassName="gap-4 px-5 pb-8"
        refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
      >
        {loading ? (
          <View className="gap-3">
            <SkeletonCard />
            <SkeletonCard />
          </View>
        ) : routes.length === 0 && drafts.length === 0 ? (
          <EmptyState art="route" icon={Truck} title={t('circuits.empty')} hint={t('scan.hint')} />
        ) : (
          <>
            {/* --- Les circuits de livraison ouverts : une carte chacun --- */}
            {routes.length > 0 ? (
              <View className="gap-3">
                {routes.map((route, index) => (
                  <FadeIn key={route.id} index={index}>
                    <CircuitCard route={route} />
                  </FadeIn>
                ))}
              </View>
            ) : null}

            {/* --- Le bandeau « À démarrer » : les brouillons, pas encore partis --- */}
            {drafts.length > 0 ? (
              <View className="gap-3">
                <SectionTitle label={t('deliveries.toStart')} />
                {drafts.map((route, index) => (
                  <FadeIn key={route.id} index={routes.length + index}>
                    <CircuitCard route={route} />
                  </FadeIn>
                ))}
              </View>
            ) : null}
          </>
        )}

        {/* --- L'itinéraire, confié au téléphone --- */}
        {next ? (
          <View className="gap-3">
            <SectionTitle label={t('circuits.mapTitle')} />
            <Card className="gap-3">
              <View className="flex-row items-center gap-3">
                <View className="bg-accent h-11 w-11 items-center justify-center rounded-2xl">
                  <MapIcon size={22} className="text-accent-foreground" />
                </View>
                <View className="flex-1 gap-0.5">
                  <Text className="text-[15px] font-semibold text-foreground" numberOfLines={1}>
                    {t('circuits.nextStop')} · {routeStopOf(next.route.kind, next.parcel).name}
                  </Text>
                  <Text className="text-[14px] text-muted-foreground" numberOfLines={2}>
                    {routeStopOf(next.route.kind, next.parcel).address}
                  </Text>
                </View>
              </View>

              <Button
                label={t('circuits.map')}
                icon={Navigation}
                onPress={() =>
                  void Linking.openURL(routeStopOf(next.route.kind, next.parcel).directionsUrl)
                }
                full
              />

              <Text className="text-[13px] text-muted-foreground">{t('circuits.mapHint')}</Text>
            </Card>
          </View>
        ) : null}
      </ScrollView>
    </Screen>
  );
}
