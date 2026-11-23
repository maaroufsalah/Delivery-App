import { useRouter } from 'expo-router';
import { MessageSquare } from 'lucide-react-native';
import { FlatList, Pressable, RefreshControl, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n } from '@/i18n';
import { useTokens } from '@/theme/tenant-theme';
import { useClaims } from '@/lib/claims';
import { Card, EmptyState, Screen, SkeletonCard, StatusBadge } from '@/ui';

/**
 * MES RÉCLAMATIONS — M2 bloc 6.
 *
 * Une liste, et un fil par réclamation. Les réponses de l'agence y arrivent, et
 * une notification prévient quand il y en a une (D-081) — parce que personne
 * n'ouvre une application pour vérifier si on lui a répondu.
 *
 * **Les notes INTERNES n'y sont pas.** `ClaimsService` les filtre déjà du fil
 * pour un e-commerçant, et le refuse aussi sur le téléchargement des pièces
 * jointes. Cet écran n'a donc rien à filtrer : il affiche ce qu'on lui donne,
 * et c'est la bonne façon de tenir une règle de confidentialité.
 */
export default function MerchantClaimsScreen() {
  const { t } = useI18n();
  const tokens = useTokens();
  const router = useRouter();

  const { data, isLoading, refetch } = useClaims();
  const pull = usePullToRefresh({ refetch });

  if (isLoading) {
    return (
      <Screen className="gap-2 px-5 pt-4">
        <SkeletonCard />
        <SkeletonCard />
      </Screen>
    );
  }

  return (
    <Screen>
      <Text className="px-5 pb-3 pt-4 text-[22px] font-bold text-foreground">
        {t('claims.title')}
      </Text>

      <FlatList
        keyboardShouldPersistTaps="handled"
        data={data?.items ?? []}
        keyExtractor={(item) => item.id}
        contentContainerClassName="gap-2 px-5 pb-6"
        refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
        ListEmptyComponent={
          <EmptyState icon={MessageSquare} title={t('claims.empty')} hint={t('claims.emptyHint')} />
        }
        renderItem={({ item }) => (
          <Pressable
            accessibilityRole="button"
            onPress={() => router.push(`/(merchant)/claim/${encodeURIComponent(item.code)}`)}
          >
            <Card className="gap-2">
              <View className="flex-row items-start justify-between gap-3">
                <Text className="flex-1 font-mono text-[14px] text-muted-foreground">
                  {item.code}
                </Text>
                {/* En retard : la seule information d'état qu'un vendeur peut
                    exploiter — elle lui dit qu'il a le droit de relancer. */}
                {item.isLate ? <StatusBadge label="!" color={tokens.warning} /> : null}
              </View>

              <Text className="text-[16px] font-semibold text-foreground">{item.subject}</Text>
              <Text className="text-[14px] text-muted-foreground">
                {item.typeLabel}
                {item.parcelCode ? ` · ${item.parcelCode}` : ''}
              </Text>
            </Card>
          </Pressable>
        )}
      />
    </Screen>
  );
}
