'use client';

import {
  DASHBOARD_COURIER_SORTS,
  type DashboardCourierRow,
  type DashboardCourierSort,
  type Paginated,
} from '@delivery/shared';
import { ArrowRight, Trophy } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo } from 'react';
import { CellMuted, CellNumber, CellStack } from '@/components/data-table/cells';
import { DataTable } from '@/components/data-table/data-table';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { ExportMenu } from '@/features/dashboard/export-menu';
import { RankBadge } from '@/features/dashboard/rank-badge';
import { useRankingTable } from '@/features/dashboard/use-ranking-table';
import { Link, useRouter } from '@/i18n/navigation';

const helper = createDataTableColumnHelper<DashboardCourierRow>();

/**
 * TOP LIVREURS — chantier « Dashboard de pilotage », spec § 4.
 *
 * Le classement arrive TRIÉ et PAGINÉ du serveur ; la carte n'ordonne rien.
 * Le critère (`csort`) et la page (`cpage`) vivent dans l'URL. Un clic sur une
 * ligne ouvre la fiche du livreur ; « Voir tous les livreurs » ouvre la liste.
 * La colonne « livrés » porte l'écart avec la période précédente.
 */
export function TopCouriers({
  page,
  sort,
  hasPrevious,
  exportQuery,
}: {
  page: Paginated<DashboardCourierRow>;
  sort: DashboardCourierSort;
  hasPrevious: boolean;
  /** Période et filtres de l'écran, pour l'export (spec § 16). */
  exportQuery: Record<string, string>;
}) {
  const t = useTranslations('dashboard.couriers');
  const format = useFormatter();
  const router = useRouter();
  const { server, set } = useRankingTable('c', { total: page.total, sort });

  const columns: DataTableColumns<DashboardCourierRow> = useMemo(
    () =>
      helper.columns([
        helper.accessor('rank', {
          header: t('columns.rank'),
          enableSorting: false,
          meta: { width: 'w-14', align: 'center' },
          cell: (info) => <RankBadge rank={info.getValue()} />,
        }),
        helper.accessor('courierName', {
          header: t('columns.courier'),
          enableSorting: false,
          cell: (info) => <CellStack label={info.getValue()} />,
        }),
        helper.accessor('assigned', {
          header: t('columns.assigned'),
          enableSorting: false,
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => <CellNumber value={format.number(info.getValue())} />,
        }),
        helper.accessor('delivered', {
          header: t('columns.delivered'),
          enableSorting: false,
          meta: { align: 'end' },
          cell: (info) => {
            const previous = info.row.original.previousDelivered;
            const delta = previous === null ? null : info.getValue() - previous;
            return (
              <CellStack
                label={format.number(info.getValue())}
                sub={
                  hasPrevious && delta !== null
                    ? `${delta > 0 ? '+' : ''}${format.number(delta)}`
                    : undefined
                }
                className="items-end text-end"
              />
            );
          },
        }),
        helper.accessor('returned', {
          header: t('columns.returned'),
          enableSorting: false,
          meta: { align: 'end', hideBelow: 'sm' },
          cell: (info) => <CellNumber value={format.number(info.getValue())} />,
        }),
        helper.accessor('successRate', {
          header: t('columns.successRate'),
          enableSorting: false,
          meta: { align: 'end' },
          cell: (info) => {
            const value = info.getValue();
            return value === null ? (
              <CellMuted>—</CellMuted>
            ) : (
              <CellNumber value={format.number(value, 'percent')} />
            );
          },
        }),
        helper.accessor('avgDelayHours', {
          header: t('columns.avgDelay'),
          enableSorting: false,
          meta: { align: 'end', hideBelow: 'lg' },
          cell: (info) => {
            const value = info.getValue();
            return value === null ? (
              <CellMuted>—</CellMuted>
            ) : (
              <CellNumber value={t('hours', { count: format.number(value, { minimumFractionDigits: 1, maximumFractionDigits: 1 }) })} />
            );
          },
        }),
        helper.accessor('codCollected', {
          header: t('columns.cod'),
          enableSorting: false,
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => <CellNumber value={format.number(info.getValue() / 100, 'currency')} />,
        }),
      ]),
    [format, hasPrevious, t],
  );

  return (
    <Card className="gap-0 overflow-hidden p-0">
      <CardHeader className="flex flex-col gap-3 px-4 py-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <CardTitle className="flex items-center gap-2">
            <Trophy className="text-primary size-4" aria-hidden />
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
              {DASHBOARD_COURIER_SORTS.map((value) => (
                <SelectItem key={value} value={value}>
                  {t(`sort.${value}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <ExportMenu section="couriers" query={{ ...exportQuery, sort }} filename="livreurs" />
          <Button asChild variant="ghost" size="sm" className="gap-1.5">
            <Link href="/couriers">
              {t('seeAll')}
              <ArrowRight className="flip-rtl size-4" aria-hidden />
            </Link>
          </Button>
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
          onRowClick={(row) => router.push(`/couriers/${row.courierId}`)}
        />
      </CardContent>
    </Card>
  );
}
