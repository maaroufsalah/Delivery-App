import { STATUS_GROUP_COLORS } from '@delivery/shared';
import {
  Banknote,
  CheckCircle2,
  Package,
  PackageSearch,
  RotateCcw,
  Wallet,
  XCircle,
} from 'lucide-react-native';
import { useState } from 'react';
import { RefreshControl, ScrollView, Text, View } from 'react-native';
import { usePullToRefresh } from '@/lib/use-pull-to-refresh';
import { useI18n } from '@/i18n';
import { usePerformance } from '@/lib/courier';
import {
  Card,
  Chip,
  EmptyState,
  KpiRow,
  KpiTile,
  ProgressRing,
  Screen,
  ScreenHeader,
  SectionTitle,
  Skeleton,
} from '@/ui';

/**
 * MES STATISTIQUES — L2 bloc 1, la destination du chevron de la maquette 2.
 *
 * C'est aussi ce que le profil (maquette 12) appelle **« Mes statistiques »**
 * et **« Historique »** : les deux lignes mènent au même endroit, parce
 * qu'elles posent la même question — « comment je m'en sors ? » — à deux
 * échelles de temps. Un écran par ligne aurait fait deux fois le même écran.
 *
 * **Le jour ET le mois.** L'accueil ne montre que le jour, parce qu'un livreur
 * qui part le matin veut savoir ce qui l'attend, pas ce qu'il a fait en trois
 * semaines. Ici c'est l'inverse : on vient y chercher une tendance, et une
 * tendance sur huit heures n'en est pas une.
 *
 * **Sans l'endpoint, l'écran le DIT.** Il n'invente pas des chiffres depuis la
 * tournée : l'accueil le fait déjà, et un écran de statistiques qui ne
 * compterait que la journée en cours mentirait sur son titre.
 */
export default function CourierStats() {
  const { t, locale } = useI18n();
  const [period, setPeriod] = useState<'day' | 'month'>('day');

  const performance = usePerformance(period);
  const pull = usePullToRefresh(performance);
  const figures = performance.data;

  const money = (cents: number) =>
    `${new Intl.NumberFormat(locale === 'ar' ? 'ar-MA' : 'fr-MA').format(cents / 100)} MAD`;

  return (
    <Screen>
      <ScreenHeader title={t('stats.title')} />

      <ScrollView
        keyboardShouldPersistTaps="handled"
        contentContainerClassName="gap-5 px-5 pb-8 pt-1"
        refreshControl={<RefreshControl refreshing={pull.refreshing} onRefresh={pull.refresh} />}
      >
        {/* --- La fenêtre : deux puces, pas un sélecteur --- */}
        <View className="flex-row gap-2">
          <Chip label={t('stats.day')} active={period === 'day'} onPress={() => setPeriod('day')} />
          <Chip
            label={t('stats.month')}
            active={period === 'month'}
            onPress={() => setPeriod('month')}
          />
        </View>

        {performance.isLoading ? (
          <View className="gap-3">
            <Skeleton className="h-[140px]" />
            <Skeleton className="h-[86px]" />
            <Skeleton className="h-[86px]" />
          </View>
        ) : !figures ? (
          <Card>
            <EmptyState
              art="route"
              title={t('stats.unavailable')}
              hint={t('stats.unavailableHint')}
            />
          </Card>
        ) : (
          <>
            {/* --- Le taux, en grand : c'est pour lui qu'on vient --- */}
            <Card className="items-center gap-3 py-6">
              <ProgressRing percent={figures.deliveryRate.percent} size={132} thickness={13} />
              <View className="items-center gap-1">
                <Text className="text-[17px] font-semibold text-foreground">
                  {t('home.deliveryRate')}
                </Text>
                <Text className="text-[15px] text-muted-foreground">
                  {t('home.rateCount', {
                    delivered: figures.deliveryRate.delivered,
                    total: figures.deliveryRate.total,
                  })}
                </Text>
              </View>
            </Card>

            <View className="gap-3">
              <SectionTitle label={t('stats.counts')} />
              <KpiRow>
                <KpiTile
                  width="third"
                  label={t('home.toPickup')}
                  value={String(figures.toPickup)}
                  icon={PackageSearch}
                  color={STATUS_GROUP_COLORS.initial}
                />
                <KpiTile
                  width="third"
                  label={t('home.toDeliver')}
                  value={String(figures.toDeliver)}
                  icon={Package}
                  color={STATUS_GROUP_COLORS.transit}
                />
                <KpiTile
                  width="third"
                  label={t('home.delivered')}
                  value={String(figures.delivered)}
                  icon={CheckCircle2}
                  color={STATUS_GROUP_COLORS.terminal}
                />
              </KpiRow>
              <KpiRow>
                <KpiTile
                  label={t('home.returns')}
                  value={String(figures.returning)}
                  icon={RotateCcw}
                  color={STATUS_GROUP_COLORS.return}
                />
                <KpiTile
                  label={t('home.failed')}
                  value={String(figures.failed)}
                  icon={XCircle}
                  color={STATUS_GROUP_COLORS.failure}
                />
              </KpiRow>
            </View>

            <View className="gap-3">
              <SectionTitle label={t('stats.amounts')} />
              <KpiRow>
                <KpiTile
                  label={t('home.collected')}
                  value={money(figures.collectedInCents)}
                  icon={Banknote}
                  tone="success"
                />
                <KpiTile
                  label={t('home.toRemit')}
                  value={money(figures.toRemitInCents)}
                  icon={Wallet}
                  tone="warning"
                />
              </KpiRow>
            </View>
          </>
        )}
      </ScrollView>
    </Screen>
  );
}
