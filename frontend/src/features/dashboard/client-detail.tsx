import type { DashboardClientDetail } from '@delivery/shared';
import {
  Banknote,
  Clock,
  HandCoins,
  Package,
  PackageCheck,
  RotateCcw,
  TrendingUp,
} from 'lucide-react';
import { getFormatter, getTranslations } from 'next-intl/server';
import { CellBadge } from '@/components/data-table/cells';
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { EvolutionChart } from '@/features/dashboard/evolution-chart';
import { LatestParcels } from '@/features/dashboard/latest-parcels';
import { kpiTrend, type TrendLabels } from '@/lib/kpi-trend';

/**
 * LE DÉTAIL D'UN CLIENT SUR LA PÉRIODE — chantier « Dashboard de pilotage »,
 * spec § 6 (D-162). Composant SERVEUR : tout arrive agrégé.
 */
export async function ClientDetail({
  detail,
  parcelsHref,
}: {
  detail: DashboardClientDetail;
  /** La liste des colis de ce client, filtres de période compris. */
  parcelsHref: string;
}) {
  const [t, tKpi, format] = await Promise.all([
    getTranslations('dashboard.clientDetail'),
    getTranslations('dashboard.kpi'),
    getFormatter(),
  ]);
  const { totals, previous } = detail;

  const labels: TrendLabels = { fresh: tKpi('trendNew'), points: tKpi('trendPoints'), hint: '' };
  const trend = (current: number, prev: number | null | undefined, invert = false) =>
    kpiTrend(current, prev, { invert }, format, labels);

  const int = (value: number) => format.number(value, 'integer');
  const money = (value: number) => format.number(value / 100, 'currency');

  return (
    <div className="flex flex-col gap-6">
      {detail.isPro ? (
        <div>
          <CellBadge label={t('pro')} color="var(--chart-1)" />
        </div>
      ) : null}

      <KpiGrid>
        <KpiCard
          label={t('total')}
          value={int(totals.total)}
          icon={Package}
          tone="var(--chart-1)"
          trend={previous ? kpiTrend(totals.total, previous.total, { neutral: true }, format, labels) : undefined}
        />
        <KpiCard
          label={t('delivered')}
          value={int(totals.delivered)}
          icon={PackageCheck}
          tone="var(--chart-4)"
          trend={previous ? trend(totals.delivered, previous.delivered) : undefined}
        />
        <KpiCard
          label={t('returned')}
          value={int(totals.returned)}
          icon={RotateCcw}
          tone="var(--chart-2)"
          trend={previous ? trend(totals.returned, previous.returned, true) : undefined}
        />
        <KpiCard
          label={t('successRate')}
          value={totals.successRate === null ? '—' : format.number(totals.successRate, 'percent')}
          hint={t('successRateHint')}
          icon={TrendingUp}
          tone="var(--chart-4)"
        />
        <KpiCard
          label={t('cod')}
          value={money(totals.codCollected)}
          hint={t('codHint')}
          icon={Banknote}
          tone="var(--chart-5)"
        />
        <KpiCard
          label={t('codReversed')}
          value={money(totals.codReversed)}
          hint={t('codReversedHint')}
          icon={HandCoins}
          tone="var(--chart-3)"
        />
        <KpiCard
          label={t('avgDelay')}
          value={
            totals.avgDelayHours === null
              ? '—'
              : t('hours', { count: format.number(totals.avgDelayHours, { minimumFractionDigits: 1, maximumFractionDigits: 1 }) })
          }
          hint={t('avgDelayHint')}
          icon={Clock}
          tone="var(--chart-3)"
        />
      </KpiGrid>

      <div className="print-stack grid gap-6 lg:grid-cols-3">
        <EvolutionChart
          series={{
            range: '30d',
            granularity: detail.granularity,
            from: detail.window.from,
            to: detail.window.to,
            points: detail.series,
          }}
          withRange={false}
        />
        <div className="lg:col-span-1" />
      </div>

      <LatestParcels parcels={detail.latest} seeAllHref={parcelsHref} />
    </div>
  );
}
