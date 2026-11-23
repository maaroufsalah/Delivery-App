'use client';

import type { DashboardDelays } from '@delivery/shared';
import { Timer } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
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
import { ChartFrame } from '@/features/dashboard/chart-frame';

/**
 * DÉLAIS DE LIVRAISON — chantier « Dashboard de pilotage », spec § 9.
 *
 * Moyen, médian, parts < 24 h / < 48 h / > 48 h et la courbe du délai moyen par
 * seau. Les parts se calculent sur les colis MESURABLES ; ceux sans horodatage
 * de ramassage sont dits, pas estimés (D-159) — la carte l'écrit sous les
 * chiffres quand il y en a.
 */
export function DelaysBlock({ delays }: { delays: DashboardDelays }) {
  const t = useTranslations('dashboard.delays');
  const format = useFormatter();

  const hours = (value: number | null) =>
    value === null ? '—' : t('hours', { count: format.number(value, { minimumFractionDigits: 1, maximumFractionDigits: 1 }) });
  const pct = (value: number | null) => (value === null ? '—' : format.number(value, 'percent'));

  const labelFormat = delays.granularity === 'month' ? 'monthYear' : 'dayMonth';
  const points = delays.series.map((point) => ({
    ...point,
    label: format.dateTime(new Date(`${point.date}T12:00:00Z`), labelFormat),
  }));
  const hasCurve = delays.series.some((point) => point.avgHours !== null);

  const shares: Array<{ key: 'within24' | 'within48' | 'over48'; tone: string }> = [
    { key: 'within24', tone: 'var(--chart-4)' },
    { key: 'within48', tone: 'var(--chart-3)' },
    { key: 'over48', tone: 'var(--destructive)' },
  ];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Timer className="text-primary size-4" aria-hidden />
          {t('title')}
        </CardTitle>
        <CardDescription>{t('description')}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <dl className="grid grid-cols-2 gap-3">
          <div className="rounded-md border p-3">
            <dt className="text-muted-foreground text-xs">{t('avg')}</dt>
            <dd className="mt-0.5 text-lg font-semibold tabular-nums">{hours(delays.avgHours)}</dd>
          </div>
          <div className="rounded-md border p-3">
            <dt className="text-muted-foreground text-xs">{t('median')}</dt>
            <dd className="mt-0.5 text-lg font-semibold tabular-nums">{hours(delays.medianHours)}</dd>
          </div>
        </dl>

        {/* La répartition : une barre empilée, puis ses trois lectures. */}
        <div>
          <div className="bg-muted flex h-2.5 w-full overflow-hidden rounded-full" aria-hidden>
            {shares.map((share) => {
              const value = delays[share.key];
              return value ? (
                <span
                  key={share.key}
                  style={{ width: `${Math.round(value * 1000) / 10}%`, backgroundColor: share.tone }}
                />
              ) : null;
            })}
          </div>
          <ul className="mt-2 grid grid-cols-3 gap-2">
            {shares.map((share) => (
              <li key={share.key} className="text-xs">
                <span className="flex items-center gap-1.5">
                  <span
                    className="size-2.5 shrink-0 rounded-full"
                    style={{ backgroundColor: share.tone }}
                    aria-hidden
                  />
                  <span className="text-muted-foreground truncate">{t(share.key)}</span>
                </span>
                <span className="block text-sm font-semibold tabular-nums">{pct(delays[share.key])}</span>
              </li>
            ))}
          </ul>
          <p className="text-muted-foreground mt-2 text-xs">
            {t('measured', {
              measured: format.number(delays.measured),
              delivered: format.number(delays.delivered),
            })}
            {delays.withoutPickup > 0
              ? ` · ${t('withoutPickup', { count: delays.withoutPickup })}`
              : ''}
          </p>
        </div>

        {hasCurve ? (
          <ChartFrame className="h-40">
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
                  contentStyle={{
                    backgroundColor: 'var(--popover)',
                    border: '1px solid var(--border)',
                    borderRadius: 'var(--radius)',
                    fontSize: 12,
                  }}
                  labelStyle={{ color: 'var(--foreground)', fontWeight: 600 }}
                  formatter={(value) => [hours(typeof value === 'number' ? value : null), t('avg')]}
                />
                <Line
                  type="monotone"
                  dataKey="avgHours"
                  stroke="var(--chart-1)"
                  strokeWidth={2}
                  dot={points.length <= 31}
                  connectNulls
                  isAnimationActive={false}
                />
              </LineChart>
            </ResponsiveContainer>
          </ChartFrame>
        ) : (
          <p className="text-muted-foreground text-sm">{t('empty')}</p>
        )}
      </CardContent>
    </Card>
  );
}
