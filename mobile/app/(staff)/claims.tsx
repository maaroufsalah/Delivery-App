import { useRouter } from 'expo-router';
import { ArrowLeft, MessageSquare } from 'lucide-react-native';
import { FlatList, Pressable, RefreshControl, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n } from '@/i18n';
import { useTokens } from '@/theme/tenant-theme';
import { useStaffClaims } from '@/lib/staff';
import { Card, EmptyState, Screen, SkeletonCard, StatusBadge } from '@/ui';

/**
 * RÉCLAMATIONS À TRAITER — M3 bloc 8.
 *
 * Les plus anciennes en tête, et celles en retard signalées. C'est l'ordre dans
 * lequel elles se traitent : une réclamation vieillit mal, et l'échéance est
 * déjà calculée côté serveur à partir du réglage de la société.
 */
export default function StaffClaimsScreen() {
  const { t } = useI18n();
  const tokens = useTokens();
  const router = useRouter();

  const { data, isLoading, refetch } = useStaffClaims();
  const pull = usePullToRefresh({ refetch });

  return (
    <Screen>
      <View className="flex-row items-center gap-3 px-5 py-3">
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('common.close')}
          className="min-h-11 min-w-11 items-center justify-center"
          hitSlop={8}
          onPress={() => router.back()}
        >
          <ArrowLeft size={24} className="text-foreground" />
        </Pressable>
        <Text className="flex-1 text-[20px] font-bold text-foreground">
          {t('staff.openClaims')}
        </Text>
      </View>

      {isLoading ? (
        <View className="gap-2 px-5">
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
          ListEmptyComponent={<EmptyState icon={MessageSquare} title={t('staff.noClaims')} />}
          renderItem={({ item }) => (
            <Pressable
              accessibilityRole="button"
              onPress={() => router.push(`/(staff)/claim/${encodeURIComponent(item.code)}`)}
            >
              <Card className="gap-2">
                <View className="flex-row items-start justify-between gap-3">
                  <Text className="flex-1 font-mono text-[14px] text-muted-foreground">
                    {item.code}
                  </Text>
                  {item.isLate ? <StatusBadge label="!" color={tokens.warning} /> : null}
                </View>

                <Text className="text-[16px] font-semibold text-foreground">{item.subject}</Text>
                <Text className="text-[14px] text-muted-foreground">
                  {item.clientName} · {item.typeLabel}
                </Text>
                {item.assigneeName ? (
                  <Text className="text-[13px] text-muted-foreground">{item.assigneeName}</Text>
                ) : null}
              </Card>
            </Pressable>
          )}
        />
      )}
    </Screen>
  );
}
