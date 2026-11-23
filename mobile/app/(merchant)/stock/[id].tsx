import { useLocalSearchParams } from 'expo-router';
import { ArrowLeft } from 'lucide-react-native';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { useGoBack } from '@/lib/go-back';
import { useI18n } from '@/i18n';
import { useStockLine } from '@/lib/merchant';
import { useTokens } from '@/theme/tenant-theme';
import { Card, Divider, Screen, SkeletonCard, StatusBadge } from '@/ui';

/**
 * L'HISTORIQUE D'UNE LIGNE DE STOCK — V2 vendeur bloc 7, spécification §9.
 *
 * **Chaque mouvement porte sa quantité SIGNÉE et le solde APRÈS, figé.** C'est
 * ce qui permet de relire l'historique sans le rejouer : on voit qu'une sortie
 * de douze a laissé trois, sans additionner quinze lignes de tête. Le solde
 * est écrit au moment du mouvement et ne se recalcule jamais — un recalcul
 * rétroactif réécrirait le passé au premier ajustement.
 *
 * **Le vendeur CONSULTE, il n'ajuste pas.** `POST /stock/adjust` demande
 * `warehouse.write`, qu'il porte — mais un ajustement est une correction
 * d'inventaire faite par l'agence qui détient physiquement la marchandise, et
 * la lui offrir depuis son téléphone ferait diverger le stock déclaré du stock
 * réel sans que personne s'en aperçoive. Les entrées et sorties viennent des
 * entreposages et des colis, qui les écrivent eux-mêmes.
 */
export default function MerchantStockLine() {
  const { t } = useI18n();
  // Une notification ouvre cette fiche sans rien derrière : on se rabat.
  const goBack = useGoBack('/(merchant)/warehouse');
  const tokens = useTokens();
  const { id } = useLocalSearchParams<{ id: string }>();

  const line = useStockLine(id);
  const data = line.data;

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
          {data?.productName ?? ''}
        </Text>
        {data?.lowStock ? (
          <StatusBadge label={t('warehouse.lowStock')} color={tokens.warning} />
        ) : null}
      </View>

      {line.isLoading ? (
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
            <Text className="text-[15px] text-muted-foreground">{data.agencyName}</Text>
            <Text className="text-[28px] font-bold tabular-nums text-foreground">
              {data.quantity}
            </Text>
            <Text className="text-[13px] text-muted-foreground">
              {t('warehouse.quantity', { count: data.quantity })}
            </Text>
          </Card>

          <Text className="pt-1 text-[18px] font-semibold text-foreground">
            {t('warehouse.movements')}
          </Text>

          {data.movements.length === 0 ? (
            <Text className="text-[15px] text-muted-foreground">{t('warehouse.noMovements')}</Text>
          ) : (
            <Card className="gap-1 p-1">
              {data.movements.map((movement, index) => (
                <View key={movement.id}>
                  {index > 0 ? <Divider /> : null}
                  <View className="min-h-[56px] justify-center gap-0.5 px-3 py-2">
                    <View className="flex-row items-center justify-between gap-3">
                      {/* Le SIGNE porte l'information : une entrée et une
                          sortie de douze ne disent pas la même chose. */}
                      <Text
                        className={`text-[16px] font-semibold tabular-nums ${
                          movement.quantity < 0 ? 'text-destructive' : 'text-foreground'
                        }`}
                      >
                        {movement.quantity > 0 ? `+${movement.quantity}` : movement.quantity}
                      </Text>
                      <Text className="text-[15px] tabular-nums text-muted-foreground">
                        {movement.balance}
                      </Text>
                    </View>
                    <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
                      {movement.reason ??
                        movement.parcelCode ??
                        movement.storageRequestCode ??
                        movement.authorName ??
                        ''}
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
