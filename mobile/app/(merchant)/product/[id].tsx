import { useLocalSearchParams } from 'expo-router';
import { ArrowLeft } from 'lucide-react-native';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { useGoBack } from '@/lib/go-back';
import { useI18n } from '@/i18n';
import { useProduct } from '@/lib/merchant';
import { useTokens } from '@/theme/tenant-theme';
import { Card, Divider, Screen, SkeletonCard, StatusBadge } from '@/ui';

/**
 * LA FICHE D'UN PRODUIT — V2 vendeur bloc 7, spécification §9.
 *
 * **Le stock PAR AGENCE, et non un total.** Un vendeur qui voit « 40 en
 * stock » et découvre que les quarante sont à Agadir alors que sa commande
 * part de Casablanca n'a rien appris. `GET /products/:id` rend la répartition,
 * avec le drapeau de seuil **posé par le serveur** : le seuil est surchargeable
 * par agence (D-019), et le recalculer ici ferait mentir l'écran dès qu'une
 * agence change sa valeur.
 *
 * La modification d'une fiche reste sur le web : elle demande les dimensions,
 * le poids, les attributs et l'image, c'est-à-dire un formulaire long qu'on ne
 * remplit pas debout. Le téléphone sert à CONSULTER et à créer vite.
 */
export default function MerchantProductDetail() {
  const { t } = useI18n();
  // Une notification ouvre cette fiche sans rien derrière : on se rabat.
  const goBack = useGoBack('/(merchant)/warehouse');
  const tokens = useTokens();
  const { id } = useLocalSearchParams<{ id: string }>();

  const product = useProduct(id);
  const data = product.data;

  return (
    <Screen>
      <View className="flex-row items-center gap-3 px-5 py-3">
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('common.close')}
          className="min-h-11 min-w-11 items-center justify-center"
          hitSlop={8}
          onPress={goBack}
        >
          <ArrowLeft size={24} className="flip-rtl text-foreground" />
        </Pressable>
        <Text className="flex-1 text-[17px] font-semibold text-foreground" numberOfLines={1}>
          {data?.name ?? ''}
        </Text>
      </View>

      {product.isLoading ? (
        <View className="gap-3 px-5">
          <SkeletonCard />
          <SkeletonCard />
        </View>
      ) : !data ? (
        <Text className="px-5 text-[15px] text-muted-foreground">
          {t('warehouse.productNotFound')}
        </Text>
      ) : (
        <ScrollView keyboardShouldPersistTaps="handled" contentContainerClassName="gap-3 px-5 pb-8">
          <Card className="gap-1">
            {data.reference ? (
              <Text className="font-mono text-[13px] text-muted-foreground">{data.reference}</Text>
            ) : null}
            <Text className="text-[28px] font-bold tabular-nums text-foreground">
              {data.stockTotal}
            </Text>
            <Text className="text-[13px] text-muted-foreground">
              {t('warehouse.totalAllAgencies')}
            </Text>
            {data.price !== null ? (
              <Text className="pt-1 text-[15px] text-foreground">{money(data.price)}</Text>
            ) : null}
          </Card>

          <Text className="pt-1 text-[18px] font-semibold text-foreground">
            {t('warehouse.byAgency')}
          </Text>

          {data.stock.length === 0 ? (
            <Text className="text-[15px] text-muted-foreground">{t('warehouse.noStock')}</Text>
          ) : (
            <Card className="gap-1 p-1">
              {data.stock.map((line, index) => (
                <View key={line.agencyId}>
                  {index > 0 ? <Divider /> : null}
                  <View className="min-h-[52px] flex-row items-center gap-3 px-3">
                    <Text className="flex-1 text-[16px] text-foreground" numberOfLines={1}>
                      {line.agencyName}
                    </Text>
                    {line.lowStock ? (
                      <StatusBadge label={t('warehouse.lowStock')} color={tokens.warning} />
                    ) : null}
                    <Text className="text-[16px] tabular-nums text-foreground">
                      {line.quantity}
                    </Text>
                  </View>
                </View>
              ))}
            </Card>
          )}
        </ScrollView>
      )}
    </Screen>
  );
}

/** Les montants voyagent en CENTIMES : aucun flottant n'entre dans le calcul. */
function money(cents: number): string {
  return `${(cents / 100).toFixed(2)} MAD`;
}
