'use client';

import {
  DASHBOARD_CITY_SORTS,
  type DashboardCityPoint,
  type DashboardCityRow,
  type DashboardCitySort,
  type Paginated,
} from '@delivery/shared';
import { MapPinned } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo } from 'react';
import { CellMuted, CellNumber, CellStack } from '@/components/data-table/cells';
import { DataTable } from '@/components/data-table/data-table';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { CityBreakdownChart } from '@/features/dashboard/dashboard-charts';
import { ExportMenu } from '@/features/dashboard/export-menu';
import { useRankingTable } from '@/features/dashboard/use-ranking-table';
import { useRouter } from '@/i18n/navigation';

const helper = createDataTableColumnHelper<DashboardCityRow>();

/**
 * PERFORMANCE PAR VILLE — chantier « Dashboard de pilotage », spec § 5.
 *
 * Le tableau (trié en base par `vsort`, paginé par `vpage`) et le graphique de
 * répartition côte à côte : le graphique reprend les huit villes les plus
 * servies de la vue d'ensemble, quel que soit le tri du tableau. Un clic sur
 * une ville ouvre la liste des colis qui y vont.
 */
export function CityPerformance({
  page,
  sort,
  breakdown,
  parcelsQuery,
  exportQuery,
}: {
  page: Paginated<DashboardCityRow>;
  sort: DashboardCitySort;
  breakdown: DashboardCityPoint[];
  /**
   * La requête d'URL de la liste des colis (période, filtres), SANS la ville :
   * le lien se construit ici. Une DONNÉE et non une fonction — une fonction ne
   * traverse pas la frontière serveur → client (correctif du 05/10).
   */
  parcelsQuery: string;
  /** Période et filtres de l'écran, pour l'export (spec § 16). */
  exportQuery: Record<string, string>;
}) {
  const parcelsHref = (cityId: string) => {
    const next = new URLSearchParams(parcelsQuery);
    next.set('destinationCityId', cityId);
    return `/parcels?${next.toString()}`;
  };
  const t = useTranslations('dashboard.cities');
  const format = useFormatter();
  const router = useRouter();
  const { server, set } = useRankingTable('v', { total: page.total, sort });

  const pct = (value: number | null) =>
    value === null ? <CellMuted>—</CellMuted> : <CellNumber value={format.number(value, 'percent')} />;

  const columns: DataTableColumns<DashboardCityRow> = useMemo(
    () =>
      helper.columns([
        helper.accessor('cityName', {
          header: t('columns.city'),
          enableSorting: false,
          cell: (info) => <CellStack label={info.getValue()} />,
        }),
        helper.accessor('total', {
          header: t('columns.total'),
          enableSorting: false,
          meta: { align: 'end' },
          cell: (info) => <CellNumber value={format.number(info.getValue())} />,
        }),
        helper.accessor('delivered', {
          header: t('columns.delivered'),
          enableSorting: false,
          meta: { align: 'end', hideBelow: 'sm' },
          cell: (info) => <CellNumber value={format.number(info.getValue())} />,
        }),
        helper.accessor('returned', {
          header: t('columns.returned'),
          enableSorting: false,
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => <CellNumber value={format.number(info.getValue())} />,
        }),
        helper.accessor('successRate', {
          header: t('columns.successRate'),
          enableSorting: false,
          meta: { align: 'end' },
          cell: (info) => pct(info.getValue()),
        }),
        helper.accessor('returnRate', {
          header: t('columns.returnRate'),
          enableSorting: false,
          meta: { align: 'end', hideBelow: 'lg' },
          cell: (info) => pct(info.getValue()),
        }),
        helper.accessor('codCollected', {
          header: t('columns.cod'),
          enableSorting: false,
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => <CellNumber value={format.number(info.getValue() / 100, 'currency')} />,
        }),
      ]),
    // `pct` se reconstruit à chaque rendu ; c'est `format` qui porte la locale.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [format, t],
  );

  return (
    <div className="print-stack grid gap-6 lg:grid-cols-3">
      <Card className="gap-0 overflow-hidden p-0 lg:col-span-2">
        <CardHeader className="flex flex-col gap-3 px-4 py-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <MapPinned className="text-primary size-4" aria-hidden />
              {t('title')}
            </CardTitle>
            <CardDescription>{t('description')}</CardDescription>
          </div>
          <div className="flex items-center gap-2 print:hidden">
            <Select value={sort} onValueChange={(value) => set('sort', value)}>
              <SelectTrigger className="w-44" aria-label={t('sortLabel')}>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {DASHBOARD_CITY_SORTS.map((value) => (
                  <SelectItem key={value} value={value}>
                    {t(`sort.${value}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <ExportMenu section="cities" query={{ ...exportQuery, sort }} filename="villes" />
          </div>
        </CardHeader>
        <CardContent className="p-0">
          <DataTable
            compact={page.total <= page.pageSize}
            data={page.items}
            columns={columns}
            server={page.total > page.pageSize ? server : undefined}
            emptyTitle={t('empty')}
            emptyHint={t('emptyHint')}
            onRowClick={(row) => router.push(parcelsHref(row.cityId))}
          />
        </CardContent>
      </Card>
      <CityBreakdownChart data={breakdown} />
    </div>
  );
}
