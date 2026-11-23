import type { PettyCashMovement, PettyCashStatus } from '@delivery/shared';
import { useRouter } from 'expo-router';
import { ArrowLeft, Plus, Wallet } from 'lucide-react-native';
import { Alert, FlatList, Pressable, RefreshControl, Text, View } from 'react-native';
import { type MessageKey, useI18n } from '@/i18n';
import { useDeletePettyCash, usePettyCash } from '@/lib/petty-cash';
import { describeActionError } from '@/lib/use-action';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useRefetchOnFocus } from '@/lib/use-refetch-on-focus';
import { useTokens } from '@/theme/tenant-theme';
import { Button, Card, EmptyState, FadeIn, Screen, SkeletonCard, StatusBadge } from '@/ui';

/**
 * MA PETITE CAISSE — D-168.
 *
 * Ses dépenses pour la société : date, libellé, catégorie, montant, statut
 * coloré, motif d'un refus. Un brouillon se corrige ou se retire ; une dépense
 * tranchée par l'administration est figée (l'API répond 403 de toute façon).
 * Ni solde ni total de caisse : il déclare, il ne gère pas la caisse.
 */
export default function CourierPettyCash() {
  const { t, locale } = useI18n();
  const router = useRouter();
  const tokens = useTokens();

  const pettyCash = usePettyCash();
  const remove = useDeletePettyCash();
  const pull = usePullToRefresh(pettyCash);
  useRefetchOnFocus(pettyCash);

  const colors: Record<PettyCashStatus, string> = {
    draft: tokens.warning,
    validated: tokens.success,
    refused: tokens.destructive,
  };

  const money = (cents: number) =>
    `${new Intl.NumberFormat(locale === 'ar' ? 'ar-MA' : 'fr-MA', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(cents / 100)} DH`;
  const day = (date: string) => date.split('-').reverse().join('/');

  const confirm = (row: PettyCashMovement) =>
    Alert.alert(t('pettyCash.deleteTitle'), row.label ?? '', [
      { text: t('common.cancel'), style: 'cancel' },
      {
        text: t('pettyCash.delete'),
        style: 'destructive',
        onPress: () =>
          remove.mutate(row.id, {
            onError: (error) => Alert.alert(describeActionError(error, t)),
          }),
      },
    ]);

  return (
    <Screen>
      <View className="flex-row items-center gap-3 px-5 pb-3 pt-4">
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('common.close')}
          className="min-h-11 min-w-11 items-center justify-center"
          hitSlop={8}
          onPress={() => router.back()}
        >
          <ArrowLeft size={24} className="flip-rtl text-foreground" />
        </Pressable>
        <View className="flex-1">
          <Text className="text-[22px] font-bold text-foreground">{t('pettyCash.title')}</Text>
          <Text className="text-[13px] text-muted-foreground">
            {t('pettyCash.pending', { count: pettyCash.data?.drafts ?? 0 })}
          </Text>
        </View>
      </View>

      <View className="px-5 pb-3">
        <Button
          label={t('pettyCash.new')}
          icon={Plus}
          onPress={() => router.push('/(courier)/petty-cash-form')}
          full
        />
      </View>

      {pettyCash.isLoading ? (
        <View className="gap-3 px-5">
          <SkeletonCard />
          <SkeletonCard />
        </View>
      ) : (
        <FlatList
          keyboardShouldPersistTaps="handled"
          data={pettyCash.data?.items ?? []}
          keyExtractor={(item) => item.id}
          contentContainerClassName="gap-3 px-5 pb-6"
          refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
          ListEmptyComponent={
            <EmptyState
              icon={Wallet}
              art="box"
              title={t('pettyCash.empty')}
              hint={t('pettyCash.emptyHint')}
            />
          }
          renderItem={({ item, index }) => (
            <FadeIn index={index}>
              <Card className="gap-2">
                <View className="flex-row items-start justify-between gap-3">
                  <View className="flex-1">
                    <Text className="text-[16px] font-semibold text-foreground" numberOfLines={1}>
                      {item.label}
                    </Text>
                    <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
                      {day(item.date)} · {item.category}
                    </Text>
                  </View>
                  <Text className="text-[16px] font-bold text-foreground">
                    {money(item.amount)}
                  </Text>
                </View>

                <StatusBadge
                  label={t(`pettyCash.status.${item.status}` as MessageKey)}
                  color={colors[item.status]}
                />
                {item.status === 'refused' && item.refusalReason ? (
                  <Text className="text-[14px] text-muted-foreground">
                    {t('pettyCash.reason', { reason: item.refusalReason })}
                  </Text>
                ) : null}

                {item.status === 'draft' ? (
                  <View className="flex-row gap-2">
                    <View className="flex-1">
                      <Button
                        label={t('pettyCash.edit')}
                        tone="soft"
                        onPress={() =>
                          router.push({
                            pathname: '/(courier)/petty-cash-form',
                            params: { id: item.id },
                          })
                        }
                        full
                      />
                    </View>
                    <View className="flex-1">
                      <Button
                        label={t('pettyCash.delete')}
                        tone="danger"
                        busy={remove.isPending && remove.variables === item.id}
                        onPress={() => confirm(item)}
                        full
                      />
                    </View>
                  </View>
                ) : null}
              </Card>
            </FadeIn>
          )}
        />
      )}
    </Screen>
  );
}
