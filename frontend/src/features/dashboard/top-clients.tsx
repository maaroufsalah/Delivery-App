'use client';

import {
  DASHBOARD_CLIENT_RANKS,
  type DashboardClientRow,
  type DashboardClients,
} from '@delivery/shared';
import { ArrowRight, Crown, Star } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo } from 'react';
import { CellBadge, CellMuted, CellNumber, CellStack } from '@/components/data-table/cells';
import { DataTable } from '@/components/data-table/data-table';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ExportMenu } from '@/features/dashboard/export-menu';
import { RankBadge } from '@/features/dashboard/rank-badge';
import { useRankingTable } from '@/features/dashboard/use-ranking-table';
import { Link, useRouter } from '@/i18n/navigation';

const helper = createDataTableColumnHelper<DashboardClientRow>();

/**
 * TOP CLIENTS — chantier « Dashboard de pilotage », spec § 6.
 *
 * Trois classements (volume, COD, taux de réussite) par onglets — le choix vit
 * dans l'URL (`krank`), la page aussi (`kpage`) — et la vignette « client le
 * plus actif » avec sa comparaison à la période précédente. Un clic sur une
 * ligne ouvre le détail du client SUR LA PÉRIODE (D-162), pas sa fiche
 * administrative.
 */
export function TopClients({
  data,
  detailQuery,
  exportQuery,
}: {
  data: DashboardClients;
  /**
   * La requête d'URL du détail (période, filtres) : le lien se construit ici.
   * Une DONNÉE et non une fonction — une fonction ne traverse pas la frontière
   * serveur → client (correctif du 05/10).
   */
  detailQuery: string;
  /** Période et filtres de l'écran, pour l'export (spec § 16). */
  exportQuery: Record<string, string>;
}) {
  const detailHref = (clientId: string) =>
    detailQuery ? `/dashboard/clients/${clientId}?${detailQuery}` : `/dashboard/clients/${clientId}`;
  const t = useTranslations('dashboard.clients');
  const format = useFormatter();
  const router = useRouter();
  const { server, set } = useRankingTable('k', { total: data.total, sort: data.rank });

  const columns: DataTableColumns<DashboardClientRow> = useMemo(
    () =>
      helper.columns([
        helper.accessor('rank', {
          header: t('columns.rank'),
          enableSorting: false,
          meta: { width: 'w-14', align: 'center' },
          cell: (info) => <RankBadge rank={info.getValue()} />,
        }),
        helper.accessor('clientName', {
          header: t('columns.client'),
          enableSorting: false,
          cell: (info) => (
            <span className="flex items-center gap-2">
              <CellStack label={info.getValue()} />
              {info.row.original.isPro ? <CellBadge label={t('pro')} color="var(--chart-1)" /> : null}
            </span>
          ),
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
          cell: (info) => {
            const value = info.getValue();
            return value === null ? (
              <CellMuted>—</CellMuted>
            ) : (
              <CellNumber value={format.number(value, 'percent')} />
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
    [format, t],
  );

  const active = data.mostActive;
  const delta =
    active && active.previous !== null ? active.current - active.previous : null;

  return (
    <div className="print-stack grid gap-6 lg:grid-cols-3">
      <Card className="gap-0 overflow-hidden p-0 lg:col-span-2">
        <CardHeader className="flex flex-col gap-3 px-4 py-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <Crown className="text-primary size-4" aria-hidden />
              {t('title')}
            </CardTitle>
            <CardDescription>{t('description')}</CardDescription>
          </div>
          <div className="flex flex-wrap items-center gap-2 print:hidden">
            <Tabs value={data.rank} onValueChange={(value) => set('rank', value)}>
              <TabsList aria-label={t('rankLabel')}>
                {DASHBOARD_CLIENT_RANKS.map((rank) => (
                  <TabsTrigger key={rank} value={rank}>
                    {t(`rank.${rank}`)}
                  </TabsTrigger>
                ))}
              </TabsList>
            </Tabs>
            <ExportMenu
              section="clients"
              query={{ ...exportQuery, sort: data.rank }}
              filename="clients"
            />
            <Button asChild variant="ghost" size="sm" className="gap-1.5">
              <Link href="/clients">
                {t('seeAll')}
                <ArrowRight className="flip-rtl size-4" aria-hidden />
              </Link>
            </Button>
          </div>
        </CardHeader>
        <CardContent className="p-0">
          <DataTable
            compact={data.total <= data.pageSize}
            data={data.items}
            columns={columns}
            server={data.total > data.pageSize ? server : undefined}
            emptyTitle={t('empty')}
            emptyHint={t('emptyHint')}
            onRowClick={(row) => router.push(detailHref(row.clientId))}
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Star className="text-primary size-4" aria-hidden />
            {t('mostActive.title')}
          </CardTitle>
          <CardDescription>{t('mostActive.description')}</CardDescription>
        </CardHeader>
        <CardContent>
          {active ? (
            <Link
              href={detailHref(active.clientId)}
              className="hover:bg-accent/60 focus-visible:ring-ring block rounded-lg border p-4 outline-none focus-visible:ring-2"
            >
              <p className="truncate text-lg font-semibold">{active.clientName}</p>
              <p className="mt-2 text-3xl font-semibold tracking-tight tabular-nums">
                {format.number(active.current)}
              </p>
              <p className="text-muted-foreground mt-1 text-sm">
                {active.previous === null
                  ? t('mostActive.noPrevious')
                  : t('mostActive.vsPrevious', {
                      previous: format.number(active.previous),
                      delta: `${delta !== null && delta > 0 ? '+' : ''}${format.number(delta ?? 0)}`,
                    })}
              </p>
            </Link>
          ) : (
            <p className="text-muted-foreground text-sm">{t('mostActive.empty')}</p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
