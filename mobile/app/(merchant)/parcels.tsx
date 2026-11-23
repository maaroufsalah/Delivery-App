import { getParcelStatus, PARCEL_STATUS_GROUPS } from '@delivery/shared';
import { useRouter } from 'expo-router';
import { PackageSearch, Truck } from 'lucide-react-native';
import { useState } from 'react';
import { FlatList, RefreshControl, ScrollView, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n } from '@/i18n';
import { useParcels } from '@/lib/merchant';
import {
  Button,
  Card,
  Chip,
  EmptyState,
  FadeIn,
  Field,
  Screen,
  SkeletonCard,
  StatusBadge,
} from '@/ui';

/**
 * MES COLIS — M2.
 *
 * **Le filtre est par FAMILLE, pas par statut.** Un vendeur pense « en cours »,
 * « livré », « retour » ; lui proposer trente-quatre statuts ferait une liste
 * déroulante plus longue que l'écran, dont il n'utiliserait jamais que quatre
 * entrées. Les familles sont celles du référentiel partagé, donc les mêmes que
 * partout ailleurs.
 *
 * **La recherche accepte un code ou un téléphone**, parce que c'est ce qu'on a
 * sous la main : un client rappelle avec son numéro, pas avec `CAS-RAB-…`. Le
 * serveur cherche déjà sur les deux, plus le nom du destinataire.
 */
export default function MerchantParcelsScreen() {
  const { t, status } = useI18n();
  const router = useRouter();

  const [search, setSearch] = useState('');
  const [group, setGroup] = useState<string | undefined>(undefined);

  const { data, isLoading, refetch } = useParcels({
    search,
    statusGroup: group,
  });
  const pull = usePullToRefresh({ refetch });

  return (
    <Screen>
      <View className="gap-3 px-5 pb-2 pt-4">
        <View className="flex-row items-center justify-between gap-3">
          <Text className="flex-1 text-[22px] font-bold text-foreground">
            {t('merchant.parcels')}
          </Text>
          <Button
            label={t('pickup.title')}
            icon={Truck}
            tone="ghost"
            onPress={() => router.push('/(merchant)/pickup')}
          />
        </View>

        <Field
          label={t('common.search')}
          value={search}
          onChangeText={setSearch}
          placeholder={t('merchant.searchHint')}
          autoCapitalize="characters"
          autoCorrect={false}
          inputMode="search"
        />

        {/*
          Les familles, en une seule rangée qui défile. Une grille de sept
          boutons prendrait le tiers de l'écran sur un téléphone ; la rangée en
          garde un seul, et le pouce y arrive.
        */}
        <ScrollView
          keyboardShouldPersistTaps="handled"
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerClassName="gap-2"
        >
          <Chip label={t('merchant.all')} active={!group} onPress={() => setGroup(undefined)} />
          {PARCEL_STATUS_GROUPS.map((family) => (
            <Chip
              key={family}
              label={t(`statusGroup.${family}`)}
              active={group === family}
              onPress={() => setGroup(family)}
            />
          ))}
        </ScrollView>
      </View>

      {isLoading ? (
        <View className="gap-2 px-5 pt-2">
          <SkeletonCard />
          <SkeletonCard />
          <SkeletonCard />
        </View>
      ) : (
        <FlatList
          keyboardShouldPersistTaps="handled"
          data={data?.items ?? []}
          keyExtractor={(item) => item.id}
          contentContainerClassName="gap-2 px-5 pb-6"
          refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
          ListEmptyComponent={
            <EmptyState
              art="search"
              icon={PackageSearch}
              title={t('merchant.noParcels')}
              hint={t('merchant.noParcelsHint')}
            />
          }
          renderItem={({ item, index }) => {
            const definition = getParcelStatus(item.statusId);

            return (
              <FadeIn index={index}>
                <Card
                  className="gap-2"
                  onPress={() => router.push(`/(merchant)/parcel/${encodeURIComponent(item.code)}`)}
                >
                  <View className="flex-row items-start justify-between gap-3">
                    <Text className="flex-1 font-mono text-[15px] text-muted-foreground">
                      {item.code}
                    </Text>
                    <StatusBadge label={status(definition.key)} color={definition.color} />
                  </View>

                  <Text className="text-[16px] font-semibold text-foreground">
                    {item.recipientName}
                  </Text>
                  <Text className="text-[15px] text-muted-foreground">
                    {item.destinationCityName}
                    {item.destinationDistrictName ? ` · ${item.destinationDistrictName}` : ''}
                  </Text>
                </Card>
              </FadeIn>
            );
          }}
        />
      )}
    </Screen>
  );
}
