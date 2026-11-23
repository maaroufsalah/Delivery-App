import { findParcelStatus } from '@delivery/shared';
import { useRouter } from 'expo-router';
import { ChevronRight, Package, ScanLine, Search } from 'lucide-react-native';
import { useState } from 'react';
import { FlatList, Pressable, ScrollView, Text, View } from 'react-native';
import { useI18n } from '@/i18n';
import { useParcelSearch } from '@/lib/courier';
import { Card, Chip, EmptyState, Field, FadeIn, Screen, SkeletonCard, StatusBadge } from '@/ui';

/**
 * Les puces de la maquette 3, moins « Livrés » (retirée le 11/11/2026) :
 * **Tous**, **À livrer**, **En retour**.
 *
 * Deux sont des statuts (13 « en cours de livraison », 4 « livré »), la
 * troisième est une **famille** — le référentiel compte plusieurs statuts de
 * retour, et un livreur les vit tous comme « ça repart ». Filtrer sur un seul
 * en cacherait les autres.
 *
 * C'est aussi ce qui remplace le quatrième filtre de M4, « reporté » : il
 * n'est pas dans la maquette, et un report reste un colis à livrer — la puce
 * « À livrer » le montre déjà.
 */
const FILTERS = [
  // 11/11/2026 : plus de puce « Livrés ». Ce menu ne montre que les colis à
  // TRAITER — le serveur retire les « Livré » de la liste d'un livreur
  // (`ParcelsService.where`), la puce n'aurait ouvert qu'une liste vide.
  { key: 'parcels.toDeliver', statusId: 13 },
  { key: 'parcels.returning', statusGroup: 'return' },
] as const;

/**
 * L'ÉCRAN COLIS — M4 bloc 3, repris par L2 bloc 2 sur la maquette 3.
 *
 * **Il cherche dans SES colis, et le serveur s'en porte garant.**
 * `GET /parcels` porte `parcels.read`, que le rôle `COURIER` possède, et
 * `ParcelsService.scoped()` borne la lecture à `courierId = le sien`. Aucun
 * filtre n'est posé ici pour cela — un filtre d'écran serait une garantie de
 * façade, que le premier appel direct à l'API contournerait (**D-082**).
 *
 * **Aucun endpoint neuf**, donc aucune attente de déploiement.
 *
 * **La recherche part à chaque frappe**, sans bouton : trois lettres suffisent
 * à trouver, et le résultat précédent reste affiché pendant la frappe
 * (`placeholderData`) — sans quoi la liste clignoterait entre deux lettres.
 *
 * **Ce que L2 change** : le code du colis passe en tête de carte, comme sur la
 * maquette — c'est lui qu'on lit sur l'étiquette et qu'on cherche des yeux —
 * et les deux lignes portent leur étiquette, « Client : » et « Adresse : ».
 * Une adresse sans étiquette se confond avec un nom de quartier.
 */
export default function CourierParcels() {
  const { t, status } = useI18n();
  const router = useRouter();

  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<number | null>(null);

  const chosen = filter === null ? null : FILTERS[filter];

  const query = useParcelSearch({
    search: search.trim() || undefined,
    statusId: chosen && 'statusId' in chosen ? chosen.statusId : undefined,
    statusGroup: chosen && 'statusGroup' in chosen ? chosen.statusGroup : undefined,
  });

  const rows = query.data?.items ?? [];
  const searching = search.trim().length > 0 || filter !== null;

  return (
    <Screen>
      <View className="gap-3 px-5 pb-3 pt-2">
        <View className="flex-row items-center justify-between gap-3">
          <Text className="flex-1 text-[22px] font-bold text-foreground">{t('parcels.title')}</Text>

          {/* Le scanner reste à UN geste depuis l'écran des colis, comme sur
              la maquette : c'est d'ici qu'on part chercher un colis en main. */}
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={t('scan.title')}
            onPress={() => router.push('/(courier)/scan')}
            className="bg-accent min-h-11 min-w-11 items-center justify-center rounded-2xl"
            hitSlop={8}
            style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
          >
            <ScanLine size={22} className="text-accent-foreground" />
          </Pressable>
        </View>

        <Field
          placeholder={t('parcels.search')}
          value={search}
          onChangeText={setSearch}
          autoCapitalize="characters"
          autoCorrect={false}
          returnKeyType="search"
          accessibilityLabel={t('parcels.search')}
        />
      </View>

      {/*
        LES PUCES, qui défilent — celles de la maquette et pas les trente-huit
        statuts du référentiel : un livreur cherche ce qu'il lui reste, ce
        qu'il a livré, ce qui repart. Une liste déroulante de trente-huit
        entrées sur un téléphone serait un formulaire, pas un filtre.
      */}
      <ScrollView
        keyboardShouldPersistTaps="handled"
        horizontal
        showsHorizontalScrollIndicator={false}
        contentContainerClassName="gap-2 px-5 pb-3"
      >
        <Chip label={t('parcels.all')} active={filter === null} onPress={() => setFilter(null)} />
        {FILTERS.map((entry, index) => (
          <Chip
            key={entry.key}
            label={t(entry.key)}
            active={filter === index}
            onPress={() => setFilter(filter === index ? null : index)}
          />
        ))}
      </ScrollView>

      {query.isLoading ? (
        <View className="gap-3 px-5">
          <SkeletonCard />
          <SkeletonCard />
          <SkeletonCard />
        </View>
      ) : (
        <FlatList
          keyboardShouldPersistTaps="handled"
          data={rows}
          keyExtractor={(row) => row.id}
          contentContainerClassName="gap-3 px-5 pb-6"
          ListEmptyComponent={
            <EmptyState
              art={searching ? 'search' : 'box'}
              icon={Search}
              title={t(searching ? 'parcels.noMatch' : 'parcels.empty')}
              hint={t(searching ? 'parcels.noMatchHint' : 'parcels.emptyHint')}
            />
          }
          renderItem={({ item, index }) => {
            const definition = findParcelStatus(item.statusId);

            return (
              <FadeIn index={index}>
                <Card onPress={() => router.push(`/(courier)/parcel/${item.code}`)}>
                  <View className="flex-row items-center gap-3">
                    <View className="bg-accent h-11 w-11 items-center justify-center rounded-2xl">
                      <Package size={20} className="text-accent-foreground" />
                    </View>

                    <View className="flex-1 gap-1">
                      <View className="flex-row items-center justify-between gap-2">
                        <Text className="font-mono text-[16px] font-bold text-foreground">
                          {item.code}
                        </Text>
                        {definition ? (
                          <StatusBadge label={status(definition.key)} color={definition.color} />
                        ) : null}
                      </View>

                      {item.externalCode ? (
                        <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
                          {t('parcel.reference')} : {item.externalCode}
                        </Text>
                      ) : null}
                      <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
                        {t('parcels.client')} : {item.recipientName}
                      </Text>
                      <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
                        {t('parcels.addressLabel')} : {item.deliveryAddress},{' '}
                        {item.destinationCityName}
                      </Text>
                    </View>

                    <ChevronRight size={20} className="flip-rtl text-muted-foreground" />
                  </View>
                </Card>
              </FadeIn>
            );
          }}
        />
      )}
    </Screen>
  );
}
