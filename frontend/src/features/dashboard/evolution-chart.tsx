'use client';

import {
  DASHBOARD_SERIES_RANGES,
  type DashboardSeries,
  type DashboardSeriesRange,
} from '@delivery/shared';
import { useFormatter, useTranslations } from 'next-intl';
import { useSearchParams } from 'next/navigation';
import { useTransition } from 'react';
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ChartFrame } from '@/features/dashboard/chart-frame';
import { usePathname, useRouter } from '@/i18n/navigation';

/** Une teinte par ligne, prise dans les tokens du thème — jamais une palette locale. */
const LINE_COLORS: Record<keyof Omit<DashboardSeries['points'][number], 'date'>, string> = {
  delivered: 'var(--chart-4)',
  returns: 'var(--chart-2)',
  inProgress: 'var(--chart-3)',
  failures: 'var(--destructive)',
};

const LINES = ['delivered', 'returns', 'inProgress', 'failures'] as const;

/**
 * L'ÉVOLUTION DES COLIS — chantier « Dashboard de pilotage », spec § 3.
 *
 * Quatre lignes, trois plages. La plage vit dans l'URL (`range`) : le serveur
 * recalcule la série, l'écran ne recompte rien. La plage est ANCRÉE sur la
 * borne haute de la période globale (D-157) — le sous-titre le dit avec les
 * dates réelles.
 *
 * Le rendu serveur passe par `ChartFrame` (C7 bloc 4) pour ne pas laisser un
 * trou blanc le temps de l'hydratation.
 */
export function EvolutionChart({
  series,
  withRange = true,
}: {
  series: DashboardSeries;
  /** Faux sur le détail d'un client : la fenêtre est celle de la période, sans onglets. */
  withRange?: boolean;
}) {
  const t = useTranslations('dashboard.evolution');
  const format = useFormatter();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [, startTransition] = useTransition();

  const setRange = (value: string) => {
    const next = new URLSearchParams(params.toString());
    next.set('range', value as DashboardSeriesRange);
    startTransition(() => router.replace(`${pathname}?${next.toString()}`, { scroll: false }));
  };

  const labelFormat = series.granularity === 'month' ? 'monthYear' : 'dayMonth';
  const points = series.points.map((point) => ({
    ...point,
    label: format.dateTime(new Date(`${point.date}T12:00:00Z`), labelFormat),
  }));
  const day = (value: string) => format.dateTime(new Date(`${value}T12:00:00Z`), 'date');
  const empty = series.points.every(
    (point) => point.delivered + point.returns + point.inProgress + point.failures === 0,
  );

  return (
    <Card className="lg:col-span-2">
      <CardHeader className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <CardTitle>{t('title')}</CardTitle>
          <CardDescription>
            {day(series.from)} – {day(series.to)}
          </CardDescription>
        </div>
        {withRange ? (
          <Tabs value={series.range} onValueChange={setRange} className="print:hidden">
            <TabsList aria-label={t('rangeLabel')}>
              {DASHBOARD_SERIES_RANGES.map((range) => (
                <TabsTrigger key={range} value={range}>
                  {t(`range.${range}`)}
                </TabsTrigger>
              ))}
            </TabsList>
          </Tabs>
        ) : null}
      </CardHeader>
      <CardContent>
        {empty ? (
          <p className="text-muted-foreground flex h-64 items-center justify-center text-sm sm:h-72">
            {t('empty')}
          </p>
        ) : (
          <ChartFrame className="h-64 sm:h-72">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={points} margin={{ top: 8, right: 8, bottom: 0, left: -20 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="var(--border)" />
                <XAxis
                  dataKey="label"
                  tickLine={false}
                  axisLine={false}
                  fontSize={11}
                  stroke="var(--muted-foreground)"
                  interval="preserveStartEnd"
                />
                <YAxis
                  tickLine={false}
                  axisLine={false}
                  fontSize={11}
                  stroke="var(--muted-foreground)"
                  allowDecimals={false}
                />
                <Tooltip
                  cursor={{ stroke: 'var(--muted-foreground)', strokeDasharray: '3 3' }}
                  contentStyle={{
                    backgroundColor: 'var(--popover)',
                    border: '1px solid var(--border)',
                    borderRadius: 'var(--radius)',
                    fontSize: 12,
                  }}
                  labelStyle={{ color: 'var(--foreground)', fontWeight: 600 }}
                  formatter={(value, name) =>
                    [String(value), t(`series.${String(name) as (typeof LINES)[number]}`)] as [
                      string,
                      string,
                    ]
                  }
                />
                {LINES.map((line) => (
                  <Line
                    key={line}
                    type="monotone"
                    dataKey={line}
                    stroke={LINE_COLORS[line]}
                    strokeWidth={2}
                    dot={points.length <= 31}
                    activeDot={{ r: 4 }}
                    isAnimationActive={false}
                  />
                ))}
              </LineChart>
            </ResponsiveContainer>
          </ChartFrame>
        )}

        {/* Légende maison : celle de recharts ne suit pas le sens d'écriture. */}
        <ul className="mt-3 flex flex-wrap gap-x-4 gap-y-1.5">
          {LINES.map((line) => (
            <li key={line} className="text-muted-foreground flex items-center gap-1.5 text-xs">
              <span
                className="size-2.5 shrink-0 rounded-full"
                style={{ backgroundColor: LINE_COLORS[line] }}
                aria-hidden
              />
              {t(`series.${line}`)}
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
