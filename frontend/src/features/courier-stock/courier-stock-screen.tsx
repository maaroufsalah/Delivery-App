'use client';

import type { Agency, CourierStockPage, CourierStockRow } from '@delivery/shared';
import { Backpack, Banknote, HandCoins, PackageCheck } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo } from 'react';
import { CellMuted, CellNumber, CellStack } from '@/components/data-table/cells';
import { DataTable } from '@/components/data-table/data-table';
import {
  ALL_VALUE,
  DataTableToolbar,
  type FilterSelect,
} from '@/components/data-table/data-table-toolbar';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { useServerTable, useUrlSearch } from '@/components/data-table/use-server-table';
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { StockPdfButton } from '@/features/courier-stock/stock-pdf-button';
import { useRouter } from '@/i18n/navigation';

const helper = createDataTableColumnHelper<CourierStockRow>();

/**
 * STOCK LIVREUR — lot M, M1 (D-222).
 *
 * Une ligne par livreur qui détient quelque chose ; « Afficher tous les
 * livreurs » ajoute ceux à zéro. Tri, pagination, filtres : le serveur (URL).
 * Les cartes et le pied de tableau portent les totaux de TOUTE la liste
 * filtrée, que l'export et le PDF global reprennent à l'identique (D4).
 */
export function CourierStockScreen({
  page,
  agencies,
}: {
  page: CourierStockPage;
  agencies: Agency[];
}) {
  const t = useTranslations('courierStock');
  const format = useFormatter();
  const router = useRouter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'inHand',
    defaultOrder: 'desc',
  });
  const [search, setSearch] = useUrlSearch(setFilter, param('search'));

  const money = (cents: number) => format.number(cents / 100, 'currency');
  const totals = page.totals;

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        id: 'agencyId',
        multiple: true,
        label: t('agency'),
        value: param('agencyId') ?? ALL_VALUE,
        options: agencies.map((agency) => ({ value: agency.id, label: agency.name })),
        onChange: (value) => setFilter('agencyId', value === ALL_VALUE ? undefined : value),
      },
    ],
    [agencies, param, setFilter, t],
  );

  const columns: DataTableColumns<CourierStockRow> = useMemo(
    () =>
      helper.columns([
        helper.accessor('courierName', {
          header: t('columns.courier'),
          cell: (info) => (
            <CellStack label={info.getValue()} sub={info.row.original.courierPhone} />
          ),
        }),
        helper.accessor('agencyName', {
          header: t('columns.agency'),
          meta: { hideBelow: 'lg' },
          cell: (info) => <CellMuted>{info.getValue() ?? '—'}</CellMuted>,
        }),
        helper.accessor('inHand', {
          header: t('columns.inHand'),
          meta: { align: 'end' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),
        helper.accessor('pickedUp', {
          header: t('columns.pickedUp'),
          meta: { align: 'end', hideBelow: 'xl' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),
        helper.accessor('awaiting', {
          header: t('columns.awaiting'),
          meta: { align: 'end', hideBelow: 'xl' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),
        helper.accessor('outForDelivery', {
          header: t('columns.outForDelivery'),
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),
        helper.accessor('failedOrReturning', {
          header: t('columns.failedOrReturning'),
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),
        helper.accessor('deliveredUnremitted', {
          header: t('columns.deliveredUnremitted'),
          meta: { align: 'end', hideBelow: 'sm' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),
        helper.accessor('collectedUnremittedInCents', {
          header: t('columns.collected'),
          meta: { align: 'end' },
          cell: (info) => <CellNumber value={money(info.getValue())} />,
        }),
        helper.accessor('toCollectInCents', {
          header: t('columns.toCollect'),
          meta: { align: 'end', hideBelow: 'lg' },
          cell: (info) => <CellNumber value={money(info.getValue())} />,
        }),
        helper.accessor('lastMovementAt', {
          header: t('columns.lastMovement'),
          meta: { hideBelow: 'lg' },
          cell: (info) => {
            const value = info.getValue();
            return <CellMuted>{value ? format.dateTime(new Date(value), 'stamp') : '—'}</CellMuted>;
          },
        }),
      ]),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [format, t],
  );

  return (
    <DataTable
      data={page.items}
      columns={columns}
      server={server}
      onRowClick={(row) => router.push(`/courier-stock/${row.courierId}`)}
      emptyTitle={t('empty')}
      emptyHint={t('emptyHint')}
      footer={{
        courierName: (
          <span>
            {t('total')} · {t('count', { count: page.total })}
          </span>
        ),
        inHand: totals.inHand,
        pickedUp: totals.pickedUp,
        awaiting: totals.awaiting,
        outForDelivery: totals.outForDelivery,
        failedOrReturning: totals.failedOrReturning,
        deliveredUnremitted: totals.deliveredUnremitted,
        collectedUnremittedInCents: money(totals.collectedUnremittedInCents),
        toCollectInCents: money(totals.toCollectInCents),
      }}
      kpis={
        <KpiGrid>
          <KpiCard
            label={t('kpis.inHand')}
            value={String(totals.inHand)}
            hint={t('count', { count: page.total })}
            icon={Backpack}
          />
          <KpiCard
            label={t('kpis.deliveredUnremitted')}
            value={String(totals.deliveredUnremitted)}
            icon={PackageCheck}
            tone="var(--chart-4)"
          />
          <KpiCard
            label={t('kpis.collected')}
            value={money(totals.collectedUnremittedInCents)}
            hint={t('kpis.collectedHint')}
            icon={Banknote}
            tone="var(--chart-3)"
          />
          <KpiCard
            label={t('kpis.toCollect')}
            value={money(totals.toCollectInCents)}
            hint={t('kpis.toCollectHint')}
            icon={HandCoins}
          />
        </KpiGrid>
      }
      toolbar={
        <DataTableToolbar
          search={search}
          onSearchChange={setSearch}
          searchPlaceholder={t('searchPlaceholder')}
          selects={selects}
          highlight={{
            label: t('showAll'),
            active: param('all') === 'true',
            onToggle: (next) => setFilter('all', next ? 'true' : undefined),
          }}
          exportResource="courier-stock"
          onReset={() => {
            setSearch('');
            reset();
          }}
        >
          <StockPdfButton
            courierId={null}
            rows={totals.inHand + totals.deliveredUnremitted}
            label={t('pdf.global')}
          />
        </DataTableToolbar>
      }
    />
  );
}
