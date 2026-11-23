'use client';

import {
  type CourierStockDetail,
  type CourierStockParcel,
  findParcelStatus,
} from '@delivery/shared';
import { Backpack, Banknote, HandCoins, PackageCheck } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo } from 'react';
import { Card } from '@/components/ui/card';
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
import { ParcelStatusBadge } from '@/features/parcels/status-badge';
import { Link, useRouter } from '@/i18n/navigation';

const helper = createDataTableColumnHelper<CourierStockParcel>();

/**
 * « STOCK DE {LIVREUR} » — lot M, M2 (D-222).
 *
 * Les compteurs, le récapitulatif par statut, la liste et l'export suivent
 * les MÊMES filtres (D4) : l'API calcule tout sur la sélection filtrée. Les
 * menus (J2, avec recherche) ne proposent que ce que le stock contient.
 */
export function CourierStockDetailScreen({ detail }: { detail: CourierStockDetail }) {
  const t = useTranslations('courierStock');
  const tStatus = useTranslations('status');
  const format = useFormatter();
  const router = useRouter();

  const { server, param, setFilter, reset } = useServerTable({
    total: detail.parcels.total,
    defaultSort: 'lastMovementAt',
    defaultOrder: 'desc',
  });
  const [search, setSearch] = useUrlSearch(setFilter, param('search'));

  const money = (cents: number) => format.number(cents / 100, 'currency');
  const date = (value: string | null) => (value ? format.dateTime(new Date(value), 'stamp') : '—');
  const counters = detail.counters;
  const statusName = (statusId: number) => {
    const status = findParcelStatus(statusId);
    return status ? tStatus(status.key) : `#${statusId}`;
  };

  const selects: FilterSelect[] = useMemo(() => {
    const select = (
      id: string,
      label: string,
      options: FilterSelect['options'],
      multiple = true,
    ) => ({
      id,
      multiple,
      label,
      value: param(id) ?? ALL_VALUE,
      options,
      onChange: (value: string) => setFilter(id, value === ALL_VALUE ? undefined : value),
    });

    return [
      /*
       * Le statut reste à valeur unique (D-255) : le PDF en arrière-plan le
       * relit en nombre (`courierStockPdfJobSchema`). Les autres menus sont multiples.
       */
      select(
        'statusId',
        t('detail.filters.status'),
        detail.options.statuses.map((statusId) => ({
          value: String(statusId),
          label: statusName(statusId),
          color: findParcelStatus(statusId)?.color,
        })),
        false,
      ),
      select('clientId', t('detail.filters.client'), detail.options.clients),
      select('cityId', t('detail.filters.city'), detail.options.cities),
      select('routeId', t('detail.filters.route'), detail.options.routes),
    ];
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [detail.options, param, setFilter, t]);

  const columns: DataTableColumns<CourierStockParcel> = useMemo(
    () =>
      helper.columns([
        helper.accessor('code', {
          header: t('detail.columns.code'),
          cell: (info) => (
            <Link
              href={`/parcels/${encodeURIComponent(info.getValue())}`}
              title={t('detail.open')}
              className="hover:underline"
              onClick={(event) => event.stopPropagation()}
            >
              <CellStack
                label={info.getValue()}
                sub={t(`blocks.${info.row.original.block}`)}
                mono
              />
            </Link>
          ),
        }),
        helper.accessor('recipientName', {
          header: t('detail.columns.recipient'),
          cell: (info) => (
            <CellStack label={info.getValue()} sub={info.row.original.recipientPhone} />
          ),
        }),
        helper.accessor('destinationCityName', {
          header: t('detail.columns.city'),
          meta: { hideBelow: 'md' },
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),
        helper.accessor('clientName', {
          header: t('detail.columns.client'),
          meta: { hideBelow: 'lg' },
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),
        helper.accessor('codInCents', {
          header: t('detail.columns.cod'),
          meta: { align: 'end' },
          cell: (info) => <CellNumber value={money(info.getValue())} />,
        }),
        helper.accessor('statusId', {
          header: t('detail.columns.status'),
          cell: (info) => <ParcelStatusBadge statusId={info.getValue()} />,
        }),
        helper.accessor('routeCode', {
          header: t('detail.columns.route'),
          meta: { hideBelow: 'lg' },
          cell: (info) => <CellMuted>{info.getValue() ?? '—'}</CellMuted>,
        }),
        helper.accessor('assignedAt', {
          header: t('detail.columns.assignedAt'),
          meta: { hideBelow: 'xl' },
          cell: (info) => <CellMuted>{date(info.getValue())}</CellMuted>,
        }),
        helper.accessor('lastMovementAt', {
          header: t('detail.columns.lastMovement'),
          meta: { hideBelow: 'md' },
          cell: (info) => <CellMuted>{date(info.getValue())}</CellMuted>,
        }),
      ]),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [format, t, tStatus],
  );

  const blocks = [
    ['picked-up', counters.pickedUp],
    ['awaiting', counters.awaiting],
    ['out-for-delivery', counters.outForDelivery],
    ['failed-or-returning', counters.failedOrReturning],
  ] as const;

  return (
    <div className="flex flex-col gap-4">
      <KpiGrid>
        <KpiCard
          label={t('kpis.inHand')}
          value={String(counters.inHand)}
          hint={blocks.map(([block, count]) => `${t(`blocks.${block}`)} ${count}`).join(' · ')}
          icon={Backpack}
        />
        <KpiCard
          label={t('kpis.deliveredUnremitted')}
          value={String(counters.deliveredUnremitted)}
          icon={PackageCheck}
          tone="var(--chart-4)"
        />
        <KpiCard
          label={t('kpis.collected')}
          value={money(counters.collectedUnremittedInCents)}
          hint={t('kpis.collectedHint')}
          icon={Banknote}
          tone="var(--chart-3)"
        />
        <KpiCard
          label={t('kpis.toCollect')}
          value={money(counters.toCollectInCents)}
          hint={t('kpis.toCollectHint')}
          icon={HandCoins}
        />
      </KpiGrid>

      {detail.byStatus.length > 0 ? (
        <Card className="gap-2 p-4">
          <p className="text-muted-foreground text-xs font-medium tracking-wide uppercase">
            {t('detail.byStatus')}
          </p>
          <ul className="flex flex-wrap gap-2">
            {detail.byStatus.map((line) => (
              <li key={line.statusId}>
                <button
                  type="button"
                  onClick={() =>
                    setFilter(
                      'statusId',
                      param('statusId') === String(line.statusId)
                        ? undefined
                        : String(line.statusId),
                    )
                  }
                  className="hover:bg-muted inline-flex items-center gap-2 rounded-full border px-3 py-1 text-sm"
                >
                  <span
                    className="size-2 rounded-full"
                    style={{ backgroundColor: findParcelStatus(line.statusId)?.color }}
                    aria-hidden
                  />
                  <span>{statusName(line.statusId)}</span>
                  <span className="font-semibold tabular-nums">{line.count}</span>
                  <span className="text-muted-foreground tabular-nums">
                    {money(line.codInCents)}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </Card>
      ) : null}

      <DataTable
        data={detail.parcels.items}
        columns={columns}
        server={server}
        onRowClick={(row) => router.push(`/parcels/${encodeURIComponent(row.code)}`)}
        emptyTitle={t('detail.empty')}
        emptyHint={t('detail.emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            searchPlaceholder={t('detail.searchPlaceholder')}
            selects={selects}
            exportResource="courier-stock/parcels"
            exportQuery={{ courierId: detail.courier.id }}
            onReset={() => {
              setSearch('');
              reset();
            }}
          >
            <span className="text-muted-foreground self-center text-sm tabular-nums">
              {t('detail.count', { count: detail.parcels.total })}
            </span>
            <StockPdfButton
              courierId={detail.courier.id}
              rows={detail.parcels.total}
              label={t('pdf.global')}
            />
          </DataTableToolbar>
        }
      />
    </div>
  );
}
