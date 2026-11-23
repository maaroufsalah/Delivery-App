import { useRouter } from 'expo-router';
import {
  Banknote,
  CheckCircle2,
  Menu,
  MessageSquare,
  RefreshCw,
  Scale,
  Truck,
} from 'lucide-react-native';
import type { LucideIcon } from 'lucide-react-native';
import { useState } from 'react';
import { Pressable, RefreshControl, ScrollView, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n } from '@/i18n';
import { useAgencies, useAlerts, useDashboard } from '@/lib/staff';
import { RoleMenu } from '@/components/role-menu';
import { Card, Chip, EmptyState, GradientHeader, Skeleton, SkeletonCard } from '@/ui';

/**
 * L'AGENCE, en un écran — M3 bloc 7.
 *
 * Quatre chiffres du jour, puis **ce qui cloche**. C'est l'inverse d'un tableau
 * de bord de back-office : là-bas on explore, ici on traite. Un agent qui ouvre
 * ce téléphone a trente secondes entre deux clients, et la question est « qu'
 * est-ce qui demande mon attention maintenant ».
 *
 * **Une famille d'alertes absente n'est pas vide** (D-080). Un agent sans
 * `finance.read` ne voit pas la carte des écarts de caisse — il ne voit pas
 * « 0 écart », qui lui ferait croire que tout va bien alors qu'il n'a
 * simplement pas le droit de regarder.
 *
 * **Le sélecteur d'agence disparaît quand il n'y a qu'une agence**, et c'est le
 * SERVEUR qui en décide : `GET /directory/agencies` ne rend que la sienne à un
 * compte rattaché. L'écran n'a pas à connaître la règle.
 */
export default function StaffHomeScreen() {
  const { t } = useI18n();
  const router = useRouter();

  const agencies = useAgencies();
  const [agencyId, setAgencyId] = useState<string | undefined>(undefined);
  const [menu, setMenu] = useState(false);

  const kpis = useDashboard(agencyId);
  const alerts = useAlerts(agencyId);

  const pull = usePullToRefresh(kpis, alerts);

  const data = alerts.data;

  /** Vrai quand rien ne réclame d'attention : toutes familles à zéro. */
  const quiet =
    data !== undefined &&
    [
      data.stuckInRetry,
      data.staleRoutes,
      data.pendingNotes,
      data.pendingRemittances,
      data.cashVariances,
      data.openClaims,
    ].every((family) => (family?.count ?? 0) === 0);

  return (
    <View className="flex-1 bg-background">
      <ScrollView
        keyboardShouldPersistTaps="handled"
        contentContainerClassName="pb-8"
        refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
      >
        {/* --- L'en-tête de marque, comme les deux autres espaces — M4 --- */}
        <GradientHeader height={110}>
          <View className="flex-row items-center justify-between">
            <Text className="flex-1 text-[24px] font-bold text-white">{t('staff.home')}</Text>
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={t('menu.title')}
              onPress={() => setMenu(true)}
              className="min-h-11 min-w-11 items-center justify-center rounded-full bg-white/15"
              hitSlop={8}
            >
              <Menu size={22} color="#FFFFFF" />
            </Pressable>
          </View>
        </GradientHeader>

        <View className="gap-3 px-5 pt-4">
          {/* --- Sélecteur d'agence, seulement s'il y a un choix --- */}
          {(agencies.data ?? []).length > 1 ? (
            <ScrollView
              keyboardShouldPersistTaps="handled"
              horizontal
              showsHorizontalScrollIndicator={false}
              contentContainerClassName="gap-2"
            >
              {(agencies.data ?? []).map((agency) => (
                <Chip
                  key={agency.id}
                  label={agency.name}
                  active={agencyId === agency.id}
                  onPress={() => setAgencyId(agencyId === agency.id ? undefined : agency.id)}
                />
              ))}
            </ScrollView>
          ) : null}

          {/* --- Les chiffres du jour --- */}
          {kpis.isLoading ? (
            <SkeletonCard />
          ) : (
            <View className="flex-row gap-3">
              <Figure label={t('staff.created')} value={String(kpis.data?.todayCount ?? 0)} />
              <Figure label={t('staff.delivered')} value={String(kpis.data?.deliveredCount ?? 0)} />
              <Figure
                label={t('staff.successRate')}
                value={`${Math.round((kpis.data?.successRate ?? 0) * 100)} %`}
              />
            </View>
          )}

          <Card className="flex-row items-baseline justify-between gap-3">
            <Text className="text-[15px] text-muted-foreground">{t('staff.codToRemit')}</Text>
            <Text className="text-[20px] font-bold tabular-nums text-foreground">
              {((kpis.data?.codToRemit ?? 0) / 100).toFixed(2)} MAD
            </Text>
          </Card>

          {/* --- Ce qui cloche --- */}
          <Text className="pt-2 text-[18px] font-semibold text-foreground">
            {t('staff.alerts')}
          </Text>

          {alerts.isLoading ? (
            <Skeleton className="h-32" />
          ) : quiet ? (
            <EmptyState
              icon={CheckCircle2}
              title={t('staff.noAlerts')}
              hint={t('staff.noAlertsHint')}
            />
          ) : (
            <>
              <AlertRow
                icon={RefreshCw}
                label={t('staff.stuckInRetry')}
                count={data?.stuckInRetry.count ?? 0}
                lines={(data?.stuckInRetry.items ?? []).map(
                  (item) => `${item.code} · ${t('staff.attempts', { count: item.attempts })}`,
                )}
                onPress={() => router.push('/(staff)/search')}
              />

              <AlertRow
                icon={Truck}
                label={t('staff.staleRoutes')}
                count={data?.staleRoutes.count ?? 0}
                lines={(data?.staleRoutes.items ?? []).map(
                  (item) =>
                    `${item.code} · ${item.courierName} · ${t('staff.hoursOpen', {
                      count: item.hoursOpen,
                    })}`,
                )}
              />

              {/* Les trois suivantes n'existent que si la clé est là (D-080). */}
              {data?.pendingNotes ? (
                <AlertRow
                  icon={Truck}
                  label={t('staff.pendingNotes')}
                  count={data.pendingNotes.count}
                  lines={data.pendingNotes.items.map(
                    (item) => `${item.code} · ${item.clientName} · ${item.parcelCount}`,
                  )}
                  onPress={() => router.push('/(staff)/pickups')}
                />
              ) : null}

              {data?.pendingRemittances ? (
                <AlertRow
                  icon={Banknote}
                  label={t('staff.pendingRemittances')}
                  count={data.pendingRemittances.count}
                  lines={data.pendingRemittances.items.map(
                    (item) => `${item.code} · ${item.courierName}`,
                  )}
                  onPress={() => router.push('/(staff)/remittances')}
                />
              ) : null}

              {data?.cashVariances ? (
                <AlertRow
                  icon={Scale}
                  label={t('staff.cashVariances')}
                  tone="warning"
                  count={data.cashVariances.count}
                  lines={data.cashVariances.items.map(
                    (item) =>
                      `${item.code} · ${(item.variance / 100).toFixed(2)} MAD · ${
                        item.varianceReason ?? ''
                      }`,
                  )}
                  onPress={() => router.push('/(staff)/remittances')}
                />
              ) : null}

              {data?.openClaims ? (
                <AlertRow
                  icon={MessageSquare}
                  label={t('staff.openClaims')}
                  count={data.openClaims.count}
                  lines={data.openClaims.items.map(
                    (item) => `${item.code} · ${item.clientName}${item.isLate ? ' · !' : ''}`,
                  )}
                  onPress={() => router.push('/(staff)/claims')}
                />
              ) : null}
            </>
          )}
        </View>
      </ScrollView>

      <RoleMenu open={menu} onClose={() => setMenu(false)} space="staff" />
    </View>
  );
}

function Figure({ label, value }: { label: string; value: string }) {
  return (
    <Card className="flex-1 gap-1">
      <Text className="text-[22px] font-bold tabular-nums text-foreground">{value}</Text>
      <Text className="text-[13px] text-muted-foreground">{label}</Text>
    </Card>
  );
}

/**
 * Une famille d'alertes : une pastille, un titre, un échantillon.
 *
 * Le COMPTE n'est jamais tronqué avec la liste — la pastille dit 23 quand
 * l'échantillon en montre cinq. Les aligner ferait croire le problème plus
 * petit qu'il n'est, et c'est exactement ce qu'on ne veut pas d'une alerte.
 */
function AlertRow({
  icon: Icon,
  label,
  count,
  lines,
  onPress,
  tone,
}: {
  icon: LucideIcon;
  label: string;
  count: number;
  lines: string[];
  onPress?: () => void;
  tone?: 'warning';
}) {
  if (count === 0) return null;

  const body = (
    <Card className="gap-2">
      <View className="flex-row items-center gap-2">
        <Icon size={18} className={tone === 'warning' ? 'text-warning' : 'text-muted-foreground'} />
        <Text className="flex-1 text-[16px] font-semibold text-foreground">{label}</Text>
        <View
          className={`min-w-7 items-center rounded-full px-2 py-0.5 ${
            tone === 'warning' ? 'bg-warning' : 'bg-muted'
          }`}
        >
          <Text
            className={`text-[14px] font-bold tabular-nums ${
              tone === 'warning' ? 'text-warning-foreground' : 'text-foreground'
            }`}
          >
            {count}
          </Text>
        </View>
      </View>

      {lines.map((line) => (
        <Text key={line} className="text-[14px] text-muted-foreground" numberOfLines={1}>
          {line}
        </Text>
      ))}
    </Card>
  );

  if (!onPress) return body;

  return (
    <Pressable accessibilityRole="button" onPress={onPress}>
      {body}
    </Pressable>
  );
}
