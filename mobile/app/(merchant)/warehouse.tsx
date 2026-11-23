import { useRouter } from 'expo-router';
import { Boxes } from 'lucide-react-native';
import { useState } from 'react';
import { FlatList, RefreshControl, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n } from '@/i18n';
import { useProducts, useStock } from '@/lib/merchant';
import { useTokens } from '@/theme/tenant-theme';
import { Card, Chip, EmptyState, FadeIn, Field, Screen, SkeletonCard, StatusBadge } from '@/ui';

/**
 * LE WAREHOUSE DU VENDEUR — V2 vendeur bloc 1, spécification §9.
 *
 * **Deux vues, une question chacune.** « Produits » répond à « qu'est-ce que je
 * vends », « Stock » à « combien m'en reste-t-il, et où ». Les fondre en une
 * seule liste aurait mélangé un catalogue et un inventaire : un produit existe
 * une fois, son stock existe par agence.
 *
 * **La recherche est faite par le SERVEUR.** La spécification demande que
 * l'écran reste fluide à plusieurs centaines de produits (§17) ; filtrer une
 * page de vingt-cinq lignes déjà chargée ne trouverait jamais la deux-centième.
 *
 * **« Stock faible » vient du serveur, pas d'un seuil écrit ici.** Le seuil est
 * un `Setting` surchargeable par agence (`warehouse.lowStockThreshold`, D-019) :
 * un entrepôt de meubles et un entrepôt de cosmétiques n'ont pas la même notion
 * de « bientôt vide ». Le recalculer côté téléphone ferait mentir l'écran dès
 * qu'une agence change sa valeur.
 *
 * L'ajout et la modification d'un produit arrivent au bloc 7.
 */
export default function MerchantWarehouse() {
  const { t } = useI18n();
  const router = useRouter();
  const tokens = useTokens();

  const [view, setView] = useState<'products' | 'stock'>('products');
  const [search, setSearch] = useState('');

  const products = useProducts(search);
  const stock = useStock(search);
  const pull = usePullToRefresh(products, stock);

  const query = view === 'products' ? products : stock;

  return (
    <Screen>
      <Text className="px-5 pb-3 pt-4 text-[22px] font-bold text-foreground">
        {t('warehouse.title')}
      </Text>

      <View className="flex-row gap-2 px-5 pb-3">
        <Chip
          label={t('warehouse.products')}
          active={view === 'products'}
          onPress={() => setView('products')}
        />
        <Chip
          label={t('warehouse.stock')}
          active={view === 'stock'}
          onPress={() => setView('stock')}
        />
      </View>

      <View className="px-5 pb-3">
        <Field
          label={t('common.search')}
          placeholder={t('warehouse.searchHint')}
          value={search}
          onChangeText={setSearch}
        />
      </View>

      {query.isLoading ? (
        <View className="gap-3 px-5">
          <SkeletonCard />
          <SkeletonCard />
        </View>
      ) : view === 'products' ? (
        <FlatList
          keyboardShouldPersistTaps="handled"
          data={products.data?.items ?? []}
          keyExtractor={(item) => item.id}
          contentContainerClassName="gap-3 px-5 pb-6"
          refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
          ListEmptyComponent={
            <EmptyState
              icon={Boxes}
              art="box"
              title={t('warehouse.noProducts')}
              hint={t('warehouse.noProductsHint')}
            />
          }
          renderItem={({ item, index }) => (
            <FadeIn index={index}>
              <Card
                className="gap-1"
                onPress={() => router.push(`/(merchant)/product/${encodeURIComponent(item.id)}`)}
              >
                <Text className="text-[16px] font-semibold text-foreground" numberOfLines={1}>
                  {item.name}
                </Text>
                {item.reference ? (
                  <Text className="font-mono text-[13px] text-muted-foreground">
                    {item.reference}
                  </Text>
                ) : null}
                <Text className="text-[14px] text-muted-foreground">
                  {t('warehouse.quantity', { count: item.stockTotal })}
                </Text>
              </Card>
            </FadeIn>
          )}
        />
      ) : (
        <FlatList
          keyboardShouldPersistTaps="handled"
          data={stock.data?.items ?? []}
          keyExtractor={(item) => item.id}
          contentContainerClassName="gap-3 px-5 pb-6"
          refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
          ListEmptyComponent={
            <EmptyState
              icon={Boxes}
              art="box"
              title={t('warehouse.noStock')}
              hint={t('warehouse.noStockHint')}
            />
          }
          renderItem={({ item, index }) => (
            <FadeIn index={index}>
              <Card
                className="gap-1"
                onPress={() => router.push(`/(merchant)/stock/${encodeURIComponent(item.id)}`)}
              >
                <View className="flex-row items-center justify-between gap-3">
                  <Text
                    className="flex-1 text-[16px] font-semibold text-foreground"
                    numberOfLines={1}
                  >
                    {item.productName}
                  </Text>
                  {/* L'alerte AVANT le chiffre : c'est elle qui appelle un
                      geste, et « 2 » ne dit pas tout seul qu'il faut agir. */}
                  {item.lowStock ? (
                    <StatusBadge label={t('warehouse.lowStock')} color={tokens.warning} />
                  ) : null}
                </View>
                <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
                  {item.agencyName}
                </Text>
                <Text className="text-[14px] text-foreground">
                  {t('warehouse.quantity', { count: item.quantity })}
                </Text>
              </Card>
            </FadeIn>
          )}
        />
      )}
    </Screen>
  );
}
