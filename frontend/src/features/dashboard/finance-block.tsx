import type { DashboardFinance } from '@delivery/shared';
import {
  AlertTriangle,
  ArrowRight,
  Banknote,
  CheckCircle2,
  Clock,
  FileText,
  HandCoins,
  Receipt,
  Wallet,
} from 'lucide-react';
import { getFormatter, getTranslations } from 'next-intl/server';
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';
import { kpiTrend, type TrendLabels } from '@/lib/kpi-trend';

/**
 * LE BLOC FINANCE — chantier « Dashboard de pilotage », spec § 2 et § 7, D-163.
 *
 * Rendu seulement pour qui porte `finance.read` : la page ne l'appelle pas
 * sinon. Deux étages : la famille de KPI (encaissé, reversé, à remettre,
 * règlements, bons de versement, écart) avec son évolution, puis la « Situation
 * financière » qui dit OÙ est l'argent — chez les livreurs, rentré mais pas
 * reversé, en retard — espèces et chèques séparés, et renvoie vers les écrans
 * où agir (`/finance`, `/remittances`).
 */
export async function FinanceKpis({
  finance,
  previousHint,
  links,
}: {
  finance: DashboardFinance;
  /** « vs 01/02 – 18/02 », déjà formaté ; vide sans période précédente. */
  previousHint: string;
  links: { finance: string; remittances: string; payments: string; transferOrders: string };
}) {
  const [t, tKpi, format] = await Promise.all([
    getTranslations('dashboard.finance'),
    getTranslations('dashboard.kpi'),
    getFormatter(),
  ]);

  const money = (cents: number) => format.number(cents / 100, 'currency');
  const labels: TrendLabels = {
    fresh: tKpi('trendNew'),
    points: tKpi('trendPoints'),
    hint: previousHint,
  };
  const trend = (current: number, previous: number | undefined, invert = false) =>
    finance.previous ? kpiTrend(current, previous, { invert }, format, labels) : undefined;

  const toRemitTotal = finance.toRemit.couriers.total + finance.toRemit.remitted.total;

  return (
    <section aria-label={tKpi('sections.finance')}>
      <h2 className="text-muted-foreground mb-2 text-xs font-medium tracking-wide uppercase">
        {tKpi('sections.finance')}
      </h2>
      <KpiGrid>
        <KpiCard
          label={t('collected')}
          value={money(finance.collected.total)}
          hint={t('split', {
            cash: money(finance.collected.cash),
            cheque: money(finance.collected.cheque),
          })}
          icon={Wallet}
          tone="var(--chart-1)"
          trend={trend(finance.collected.total, finance.previous?.collected)}
          href={links.finance}
        />
        <KpiCard
          label={t('reversed')}
          value={money(finance.reversed)}
          hint={t('reversedHint')}
          icon={HandCoins}
          tone="var(--chart-4)"
          trend={trend(finance.reversed, finance.previous?.reversed)}
          href={links.finance}
        />
        <KpiCard
          label={t('toRemit')}
          value={money(toRemitTotal)}
          hint={t('toRemitHint', {
            couriers: money(finance.toRemit.couriers.total),
            remitted: money(finance.toRemit.remitted.total),
          })}
          icon={Banknote}
          tone="var(--chart-5)"
          href={links.remittances}
        />
        <KpiCard
          label={t('settlements')}
          value={money(finance.settlements.amount)}
          hint={t('count', { count: format.number(finance.settlements.count) })}
          icon={Receipt}
          tone="var(--chart-3)"
          trend={trend(finance.settlements.amount, finance.previous?.settlements)}
          href={links.payments}
        />
        <KpiCard
          label={t('transferOrders')}
          value={money(finance.transferOrders.amount)}
          hint={t('count', { count: format.number(finance.transferOrders.count) })}
          icon={FileText}
          tone="var(--chart-2)"
          trend={trend(finance.transferOrders.amount, finance.previous?.transferOrders)}
          href={links.transferOrders}
        />
        <KpiCard
          label={t('cashVariance')}
          value={money(finance.cashVariance.amount)}
          hint={
            finance.courierBalance === 0
              ? t('noBalance')
              : t('balance', { amount: money(finance.courierBalance) })
          }
          icon={finance.cashVariance.amount === 0 ? CheckCircle2 : AlertTriangle}
          tone={finance.cashVariance.amount === 0 ? 'var(--chart-4)' : 'var(--destructive)'}
          href={links.remittances}
        />
      </KpiGrid>
    </section>
  );
}

export async function FinanceSituation({
  finance,
  links,
}: {
  finance: DashboardFinance;
  links: { finance: string; remittances: string; lateParcels: string };
}) {
  const [t, format] = await Promise.all([getTranslations('dashboard.finance'), getFormatter()]);
  const money = (cents: number) => format.number(cents / 100, 'currency');

  const rows: Array<{
    key: string;
    label: string;
    amount: number;
    sub?: string;
    href: string;
    tone: string;
    icon: typeof Wallet;
  }> = [
    {
      key: 'collected',
      label: t('collected'),
      amount: finance.collected.total,
      sub: t('split', { cash: money(finance.collected.cash), cheque: money(finance.collected.cheque) }),
      href: links.finance,
      tone: 'var(--chart-1)',
      icon: Wallet,
    },
    {
      key: 'reversed',
      label: t('reversed'),
      amount: finance.reversed,
      sub: t('reversedHint'),
      href: links.finance,
      tone: 'var(--chart-4)',
      icon: HandCoins,
    },
    {
      key: 'couriers',
      label: t('situation.couriers'),
      amount: finance.toRemit.couriers.total,
      sub: t('split', {
        cash: money(finance.toRemit.couriers.cash),
        cheque: money(finance.toRemit.couriers.cheque),
      }),
      href: links.remittances,
      tone: 'var(--chart-5)',
      icon: Banknote,
    },
    {
      key: 'remitted',
      label: t('situation.remitted'),
      amount: finance.toRemit.remitted.total,
      sub: t('split', {
        cash: money(finance.toRemit.remitted.cash),
        cheque: money(finance.toRemit.remitted.cheque),
      }),
      href: links.finance,
      tone: 'var(--chart-3)',
      icon: Receipt,
    },
    {
      key: 'late',
      label: t('situation.late', { hours: String(finance.late.thresholdHours) }),
      amount: finance.late.amount,
      sub: t('situation.lateCount', { count: finance.late.count }),
      href: links.lateParcels,
      tone: finance.late.count === 0 ? 'var(--chart-4)' : 'var(--destructive)',
      icon: Clock,
    },
  ];

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-2">
        <div>
          <CardTitle>{t('situation.title')}</CardTitle>
          <CardDescription>{t('situation.description')}</CardDescription>
        </div>
        <Button asChild variant="ghost" size="sm" className="gap-1.5 print:hidden">
          <Link href={links.finance}>
            {t('situation.detail')}
            <ArrowRight className="flip-rtl size-4" aria-hidden />
          </Link>
        </Button>
      </CardHeader>
      <CardContent>
        <ul className="divide-border divide-y">
          {rows.map((row) => (
            <li key={row.key}>
              <Link
                href={row.href}
                className="hover:bg-accent/60 focus-visible:ring-ring -mx-2 flex items-center gap-3 rounded-md px-2 py-2.5 outline-none focus-visible:ring-2"
              >
                <span
                  className="flex size-8 shrink-0 items-center justify-center rounded-lg"
                  style={{
                    backgroundColor: `color-mix(in oklab, ${row.tone} 14%, transparent)`,
                    color: row.tone,
                  }}
                >
                  <row.icon className="size-4" aria-hidden />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-medium">{row.label}</span>
                  {row.sub ? (
                    <span className="text-muted-foreground block truncate text-xs">{row.sub}</span>
                  ) : null}
                </span>
                <span className="text-sm font-semibold tabular-nums">{money(row.amount)}</span>
              </Link>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
