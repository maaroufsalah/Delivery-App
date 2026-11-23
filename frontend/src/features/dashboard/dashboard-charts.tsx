'use client';

import { PARCEL_STATUS_GROUPS, type ParcelStatusGroup } from '@delivery/shared';
import { useFormatter, useTranslations } from 'next-intl';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  LabelList,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ChartFrame } from '@/features/dashboard/chart-frame';
import type { DashboardGranularity } from '@delivery/shared';
import type { DashboardCityPoint, DashboardDayPoint } from '@/lib/api/dashboard';

/**
 * Graphiques du tableau de bord.
 *
 * Les couleurs viennent des tokens de la marque (`var(--chart-N)`), jamais
 * d'une palette écrite dans le composant : changer la couleur primaire dans
 * Paramètres > Société repeint les graphiques.
 *
 * Le rendu serveur passe par `ChartFrame` (C7 bloc 4) : `ResponsiveContainer`
 * ne sait rien mesurer hors du navigateur et rendait deux trous blancs de
 * 256 px. Le cadre pose un squelette de la même hauteur, et le graphique s'y
 * substitue sans saut de mise en page.
 */

/** Une teinte par famille de statut, prise dans les tokens du thème. */
const GROUP_COLORS: Record<ParcelStatusGroup, string> = {
  initial: 'var(--muted-foreground)',
  transit: 'var(--chart-3)',
  pivot: 'var(--chart-1)',
  retry: 'var(--chart-5)',
  failure: 'var(--destructive)',
  return: 'var(--chart-2)',
  terminal: 'var(--chart-4)',
};

export function DailyVolumeChart({
  data,
  granularity,
}: {
  data: DashboardDayPoint[];
  /** Le PAS du serveur : l'étiquette suit, jour · semaine · mois. */
  granularity: DashboardGranularity;
}) {
  const t = useTranslations('dashboard.charts.daily');
  const tGroup = useTranslations('statusGroup');
  const format = useFormatter();

  /*
   * Le point porte le premier jour de son seau ; l'étiquette suit le pas. Au
   * mois, on lit « mars 2026 » ; au jour et à la semaine, « 04 mars » — la
   * semaine étant nommée par son lundi.
   */
  const labelFormat = granularity === 'month' ? 'monthYear' : 'dayMonth';
  const points = data.map((point) => ({
    ...point,
    label: format.dateTime(new Date(`${point.date}T12:00:00Z`), labelFormat),
  }));

  return (
    <Card className="lg:col-span-2">
      <CardHeader>
        <CardTitle>{t('title')}</CardTitle>
        <CardDescription>{t('description')}</CardDescription>
      </CardHeader>
      <CardContent>
        {/* Hauteur fixe : ResponsiveContainer ne peut pas déduire une hauteur
            d'un parent en `auto`, il rendrait un graphique de 0 pixel. */}
        <ChartFrame className="h-64 sm:h-72">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={points} margin={{ top: 8, right: 8, bottom: 0, left: -20 }}>
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
                cursor={{ fill: 'var(--muted)', opacity: 0.4 }}
                contentStyle={{
                  backgroundColor: 'var(--popover)',
                  border: '1px solid var(--border)',
                  borderRadius: 'var(--radius)',
                  fontSize: 12,
                }}
                labelStyle={{ color: 'var(--foreground)', fontWeight: 600 }}
                formatter={(value, name) =>
                  [String(value), tGroup(String(name) as ParcelStatusGroup)] as [string, string]
                }
              />
              {PARCEL_STATUS_GROUPS.map((group) => (
                <Bar key={group} dataKey={group} stackId="parcels" fill={GROUP_COLORS[group]} />
              ))}
            </BarChart>
          </ResponsiveContainer>
        </ChartFrame>

        {/* Légende maison : celle de recharts ne suit pas le sens d'écriture. */}
        <ul className="mt-3 flex flex-wrap gap-x-4 gap-y-1.5">
          {PARCEL_STATUS_GROUPS.map((group) => (
            <li key={group} className="text-muted-foreground flex items-center gap-1.5 text-xs">
              <span
                className="size-2.5 shrink-0 rounded-sm"
                style={{ backgroundColor: GROUP_COLORS[group] }}
                aria-hidden
              />
              {tGroup(group)}
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

export function CityBreakdownChart({ data }: { data: DashboardCityPoint[] }) {
  const t = useTranslations('dashboard.charts.cities');

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('title')}</CardTitle>
        <CardDescription>{t('description')}</CardDescription>
      </CardHeader>
      <CardContent>
        {data.length === 0 ? (
          <p className="text-muted-foreground text-sm">{t('empty')}</p>
        ) : (
          <ChartFrame className="h-64 sm:h-72">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart
                data={data}
                layout="vertical"
                margin={{ top: 0, right: 28, bottom: 0, left: 0 }}
              >
                <XAxis type="number" hide allowDecimals={false} />
                <YAxis
                  type="category"
                  dataKey="cityName"
                  width={92}
                  tickLine={false}
                  axisLine={false}
                  fontSize={11}
                  stroke="var(--muted-foreground)"
                />
                <Bar dataKey="count" radius={4} isAnimationActive={false}>
                  {data.map((point, index) => (
                    <Cell
                      key={point.cityId}
                      // Dégradé d'une seule teinte : la ville la plus servie est
                      // la plus saturée, sans introduire de couleur arbitraire.
                      fill={`color-mix(in oklab, var(--chart-1) ${100 - index * 9}%, var(--muted))`}
                    />
                  ))}
                  <LabelList
                    dataKey="count"
                    position="right"
                    fontSize={11}
                    fill="var(--muted-foreground)"
                  />
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </ChartFrame>
        )}
      </CardContent>
    </Card>
  );
}
