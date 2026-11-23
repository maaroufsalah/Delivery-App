import type { ClaimStatus } from '@delivery/shared';
import { CLAIM_STATUSES } from '@delivery/shared';
import { useRouter } from 'expo-router';
import { MessageSquare, Plus } from 'lucide-react-native';
import { useState } from 'react';
import { FlatList, Pressable, RefreshControl, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n, type MessageKey } from '@/i18n';
import { useClaims } from '@/lib/claims';
import { useTokens } from '@/theme/tenant-theme';
import { Button, Card, Chip, EmptyState, FadeIn, Screen, SkeletonCard, StatusBadge } from '@/ui';

/** Les quatre couleurs d'état, prises aux jetons du thème — pas au référentiel. */
const TONE: Record<ClaimStatus, 'warning' | 'primary' | 'success' | 'destructive'> = {
  open: 'warning',
  'in-progress': 'primary',
  resolved: 'success',
  rejected: 'destructive',
};

/**
 * MES RÉCLAMATIONS — le complément de L2, pendant mobile de `/me/claims`.
 *
 * **Pourquoi l'écran n'existait pas.** L2 avait écarté l'entrée du tiroir
 * « Plus » avec une raison écrite : le livreur portait les clés depuis B-19,
 * mais aucun des quinze écrans de la spécification d'Anass n'en montrait, et
 * « l'entrée viendra avec son écran, pas avant ». Le web, lui, déclarait
 * l'entrée depuis L1 sans la page — et le déploiement de L2, en accordant la
 * clé, a rendu ce lien mort visible. Les deux côtés reçoivent donc l'écran en
 * même temps.
 *
 * **Ce que le livreur y trouve** : les réclamations qui portent sur SES colis.
 * Une réclamation sans colis — « règlement non reçu », une affaire entre le
 * vendeur et la société — lui reste invisible, et c'est la portée serveur qui
 * le décide (`claimWhere()`), pas cet écran.
 *
 * **Le filtre est LOCAL, contrairement au web.** La page web met le statut dans
 * l'URL parce qu'un lien se partage ; ici il n'y a pas d'URL à partager, et un
 * aller-retour réseau par puce sur une 3G d'agence coûterait plus que le tri
 * d'une liste de vingt-cinq lignes déjà chargée.
 */
export default function CourierClaims() {
  const { t } = useI18n();
  const router = useRouter();
  const tokens = useTokens();

  const claims = useClaims();
  const pull = usePullToRefresh(claims);
  const [status, setStatus] = useState<ClaimStatus | null>(null);

  const rows = (claims.data?.items ?? []).filter(
    (claim) => status === null || claim.status === status,
  );

  const tones = {
    warning: tokens.warning,
    primary: tokens.primary,
    success: tokens.success,
    destructive: tokens.destructive,
  };

  return (
    <Screen>
      <View className="flex-row items-center justify-between gap-3 px-5 pb-3 pt-4">
        <Text className="flex-1 text-[22px] font-bold text-foreground">{t('claims.title')}</Text>

        <Pressable
          accessibilityRole="button"
          accessibilityLabel={t('claims.new')}
          onPress={() => router.push('/(courier)/claim/new')}
          className="bg-accent min-h-11 min-w-11 items-center justify-center rounded-2xl"
          hitSlop={8}
          style={({ pressed }) => (pressed ? { opacity: 0.6 } : null)}
        >
          <Plus size={22} className="text-accent-foreground" />
        </Pressable>
      </View>

      <View className="flex-row flex-wrap gap-2 px-5 pb-3">
        <Chip label={t('claims.all')} active={status === null} onPress={() => setStatus(null)} />
        {CLAIM_STATUSES.map((value) => (
          <Chip
            key={value}
            label={t(`claimStatus.${value}` as MessageKey)}
            active={status === value}
            onPress={() => setStatus(status === value ? null : value)}
          />
        ))}
      </View>

      {claims.isLoading ? (
        <View className="gap-3 px-5">
          <SkeletonCard />
          <SkeletonCard />
        </View>
      ) : (
        <FlatList
          keyboardShouldPersistTaps="handled"
          data={rows}
          keyExtractor={(item) => item.id}
          contentContainerClassName="gap-3 px-5 pb-6"
          refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
          ListEmptyComponent={
            <EmptyState
              icon={MessageSquare}
              art="box"
              title={t('claims.empty')}
              hint={t('claims.courierEmptyHint')}
            />
          }
          renderItem={({ item, index }) => (
            <FadeIn index={index}>
              <Card
                className="gap-2"
                onPress={() => router.push(`/(courier)/claim/${encodeURIComponent(item.code)}`)}
              >
                <View className="flex-row items-center justify-between gap-3">
                  <Text className="flex-1 font-mono text-[14px] text-muted-foreground">
                    {item.code}
                  </Text>

                  <View className="flex-row items-center gap-2">
                    {/* Le retard AVANT l'état : c'est lui qui appelle un geste,
                        et « non traitée depuis six jours » ne se lit pas dans
                        « non traitée ». */}
                    {item.isLate ? (
                      <StatusBadge label={t('claims.late')} color={tokens.destructive} />
                    ) : null}
                    <StatusBadge
                      label={t(`claimStatus.${item.status}` as MessageKey)}
                      color={tones[TONE[item.status]]}
                    />
                  </View>
                </View>

                <Text className="text-[16px] font-semibold text-foreground">{item.subject}</Text>
                <Text className="text-[14px] text-muted-foreground" numberOfLines={1}>
                  {item.typeLabel}
                  {item.parcelCode ? ` · ${item.parcelCode}` : ''}
                </Text>
              </Card>
            </FadeIn>
          )}
        />
      )}

      <View className="px-5 pb-4">
        <Button
          label={t('claims.new')}
          icon={Plus}
          onPress={() => router.push('/(courier)/claim/new')}
          full
        />
      </View>
    </Screen>
  );
}
