import {
  DASHBOARD_FAILED_STATUS_IDS,
  DASHBOARD_IN_PROGRESS_STATUS_IDS,
  DASHBOARD_IN_TRANSIT_STATUS_IDS,
  DASHBOARD_RETURN_STATUS_IDS,
  type DashboardDelays,
  type DashboardOverview,
} from '@delivery/shared';
import {
  CheckCircle2,
  Clock,
  Gauge,
  Package,
  PackageCheck,
  RotateCcw,
  Timer,
  TrendingDown,
  TrendingUp,
  Truck,
  XCircle,
} from 'lucide-react';
import { getFormatter, getTranslations } from 'next-intl/server';
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { kpiTrend, type TrendLabels } from '@/lib/kpi-trend';

/** La liste de colis correspondant à un compteur : `statusIds=4,5,…`. */
const ids = (list: readonly number[]) => ({ statusIds: list.join(',') });

/**
 * LES KPI COLIS ET PERFORMANCE — chantier « Dashboard de pilotage », spec § 2.
 *
 * Composant SERVEUR : les nombres arrivent agrégés de l'API, l'évolution est
 * calculée ici une fois (`kpiTrend`), rien n'est recompté dans le navigateur.
 * Les cartes historiques (colis du jour, livrés, taux de réussite, COD à
 * remettre) restent, avec leur libellé ; les nouvelles s'ajoutent.
 *
 * Le COD à remettre et la finance détaillée vivent dans le bloc Finance
 * (`finance.read`) ; la carte historique reste ici parce qu'elle y était.
 */
export async function ParcelKpis({
  overview,
  delays,
  parcelsHref,
}: {
  overview: DashboardOverview;
  /** Les délais (`GET /dashboard/delays`), pour la famille Performance ; absents si l'appel a échoué. */
  delays: DashboardDelays | null;
  /** Le lien de détail d'un compteur : la liste des colis, filtrée comme lui. */
  parcelsHref: (extra?: Record<string, string>) => string;
}) {
  const [t, tDelays, format] = await Promise.all([
    getTranslations('dashboard.kpi'),
    getTranslations('dashboard.delays'),
    getFormatter(),
  ]);
  const { kpis, previous, previousWindow } = overview;

  const hours = (value: number | null) =>
    value === null
      ? '—'
      : tDelays("hours", { count: format.number(value, { minimumFractionDigits: 1, maximumFractionDigits: 1 }) });

  const day = (value: string) => format.dateTime(new Date(`${value}T12:00:00Z`), 'date');
  const labels: TrendLabels = {
    fresh: t('trendNew'),
    points: t('trendPoints'),
    hint: previousWindow
      ? t('vsPrevious', { from: day(previousWindow.from), to: day(previousWindow.to) })
      : '',
  };
  const trend = (
    current: number | null,
    prev: number | null | undefined,
    options: Parameters<typeof kpiTrend>[2] = {},
  ) => (current === null ? undefined : kpiTrend(current, prev, options, format, labels));

  const int = (value: number) => format.number(value, 'integer');
  const pct = (value: number | null) => (value === null ? '—' : format.number(value, 'percent'));

  return (
    <div className="flex flex-col gap-4">
      <section aria-label={t('sections.parcels')}>
        <h2 className="text-muted-foreground mb-2 text-xs font-medium tracking-wide uppercase">
          {t('sections.parcels')}
        </h2>
        <KpiGrid>
          <KpiCard
            label={t('today')}
            value={int(kpis.todayCount)}
            hint={t('todayHint')}
            icon={Package}
            href={parcelsHref({ period: 'today' })}
          />
          <KpiCard
            label={t('total')}
            value={int(kpis.total)}
            hint={t('periodHint')}
            icon={Package}
            tone="var(--chart-1)"
            trend={trend(kpis.total, previous?.total, { neutral: true })}
            href={parcelsHref()}
          />
          <KpiCard
            label={t('delivered')}
            value={int(kpis.delivered)}
            hint={t('deliveredHint')}
            icon={PackageCheck}
            tone="var(--chart-4)"
            trend={trend(kpis.delivered, previous?.delivered)}
            href={parcelsHref({ statusId: '4' })}
          />
          <KpiCard
            label={t('inProgress')}
            value={int(kpis.inProgress)}
            hint={t('inProgressHint')}
            icon={Clock}
            tone="var(--chart-3)"
            trend={trend(kpis.inProgress, previous?.inProgress, { neutral: true })}
            href={parcelsHref(ids(DASHBOARD_IN_PROGRESS_STATUS_IDS))}
          />
          <KpiCard
            label={t('inTransit')}
            value={int(kpis.inTransit)}
            hint={t('inTransitHint')}
            icon={Truck}
            tone="var(--chart-3)"
            trend={trend(kpis.inTransit, previous?.inTransit, { neutral: true })}
            href={parcelsHref(ids(DASHBOARD_IN_TRANSIT_STATUS_IDS))}
          />
          <KpiCard
            label={t('returned')}
            value={int(kpis.returned)}
            hint={t('returnedHint')}
            icon={RotateCcw}
            tone="var(--chart-2)"
            trend={trend(kpis.returned, previous?.returned, { invert: true })}
            href={parcelsHref(ids(DASHBOARD_RETURN_STATUS_IDS))}
          />
          <KpiCard
            label={t('failed')}
            value={int(kpis.failed)}
            hint={t('failedHint')}
            icon={XCircle}
            tone="var(--destructive)"
            trend={trend(kpis.failed, previous?.failed, { invert: true })}
            href={parcelsHref(ids(DASHBOARD_FAILED_STATUS_IDS))}
          />
          <KpiCard
            label={t('closed')}
            value={int(kpis.closed)}
            hint={t('closedHint')}
            icon={CheckCircle2}
            tone="var(--chart-4)"
            trend={trend(kpis.closed, previous?.closed, { neutral: true })}
            href={parcelsHref({ statusGroup: 'terminal' })}
          />
        </KpiGrid>
      </section>

      <section aria-label={t('sections.performance')}>
        <h2 className="text-muted-foreground mb-2 text-xs font-medium tracking-wide uppercase">
          {t('sections.performance')}
        </h2>
        <KpiGrid>
          <KpiCard
            label={t('successRate')}
            value={pct(kpis.successRate)}
            hint={t('successRateHint')}
            icon={TrendingUp}
            tone="var(--chart-4)"
            trend={trend(kpis.successRate, previous?.successRate, { rate: true })}
          />
          <KpiCard
            label={t('returnRate')}
            value={pct(kpis.returnRate)}
            hint={t('returnRateHint')}
            icon={TrendingDown}
            tone="var(--chart-2)"
            trend={trend(kpis.returnRate, previous?.returnRate, { rate: true, invert: true })}
          />
          <KpiCard
            label={tDelays('avg')}
            value={hours(delays?.avgHours ?? null)}
            hint={tDelays('avgHint')}
            icon={Timer}
            tone="var(--chart-3)"
            trend={
              delays ? trend(delays.avgHours, delays.previous?.avgHours, { invert: true }) : undefined
            }
          />
          <KpiCard
            label={tDelays('within24')}
            value={pct(delays?.within24 ?? null)}
            hint={tDelays('shareHint')}
            icon={Gauge}
            tone="var(--chart-4)"
            trend={
              delays ? trend(delays.within24, delays.previous?.within24, { rate: true }) : undefined
            }
          />
          <KpiCard
            label={tDelays('within48')}
            value={pct(delays?.within48 ?? null)}
            hint={tDelays('shareHint')}
            icon={Gauge}
            tone="var(--chart-3)"
            trend={
              delays ? trend(delays.within48, delays.previous?.within48, { rate: true }) : undefined
            }
          />
          <KpiCard
            label={tDelays('over48')}
            value={pct(delays?.over48 ?? null)}
            hint={tDelays('shareHint')}
            icon={Gauge}
            tone="var(--destructive)"
            trend={
              delays
                ? trend(delays.over48, delays.previous?.over48, { rate: true, invert: true })
                : undefined
            }
          />
          {/*
           * « COD à remettre » vit dans la famille Finance (`finance.read`) :
           * la carte historique faisait DOUBLON avec une autre formule
           * (correctif du 05/10) — l'API garde `kpis.codToRemit`, alignée sur
           * celle du bloc Finance.
           */}
        </KpiGrid>
      </section>
    </div>
  );
}
