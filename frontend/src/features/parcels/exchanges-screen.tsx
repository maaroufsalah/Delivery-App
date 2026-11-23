'use client';

import {
  EXCHANGE_STATUS_COLORS,
  EXCHANGE_STATUSES,
  type ExchangeListItem,
  type Paginated,
} from '@delivery/shared';
import { ArrowLeftRight, Eye, Plus } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useState } from 'react';
import {
  CellBadge,
  CellMuted,
  CellNumber,
  CellStack,
  RowActions,
} from '@/components/data-table/cells';
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
import { Button } from '@/components/ui/button';
import { DropdownMenuItem } from '@/components/ui/dropdown-menu';
import { ExchangeForm } from '@/features/parcels/exchange-form';
import { useRouter } from '@/i18n/navigation';

const helper = createDataTableColumnHelper<ExchangeListItem>();

/**
 * LES ÉCHANGES — chantier « Finance », point 6.
 *
 * **Deux colis par ligne, et on les voit tous les deux.** L'ancien et le neuf,
 * avec la flèche entre eux : c'est le seul affichage qui dise en un coup d'œil
 * ce qu'est un échange. Montrer le seul dossier aurait obligé à l'ouvrir pour
 * savoir de quoi il parle.
 *
 * **Le vendeur LIT, il n'ouvre pas.** La mission le demande, et c'est juste :
 * l'échange est un mouvement de transport, décidé par l'agence. Le bouton
 * n'apparaît donc que pour qui peut écrire un colis.
 */
export function ExchangesScreen({
  page,
  canWrite,
}: {
  page: Paginated<ExchangeListItem>;
  canWrite: boolean;
}) {
  const t = useTranslations('exchanges');
  const tStatus = useTranslations('exchanges.status');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();

  const [creating, setCreating] = useState(false);

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'requestedAt',
    defaultOrder: 'desc',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        id: 'status',
        multiple: true,
        label: t('filters.status'),
        value: param('status') ?? ALL_VALUE,
        options: EXCHANGE_STATUSES.map((status) => ({
          value: status,
          label: tStatus(status),
          color: EXCHANGE_STATUS_COLORS[status],
        })),
        onChange: (value) => setFilter('status', value === ALL_VALUE ? undefined : value),
      },
    ],
    [param, setFilter, t, tStatus],
  );

  const columns: DataTableColumns<ExchangeListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('code', {
          header: t('columns.code'),
          meta: { width: 'w-40' },
          cell: (info) => (
            <CellStack mono label={info.getValue()} sub={info.row.original.clientName} />
          ),
        }),

        helper.display({
          id: 'parcels',
          header: t('columns.parcels'),
          cell: (info) => {
            const row = info.row.original;

            return (
              <span className="flex flex-wrap items-center gap-1.5 font-mono text-xs">
                {row.originalParcelCode}
                <ArrowLeftRight className="flip-rtl size-3.5 shrink-0" aria-hidden />
                {row.newParcelCode ?? '—'}
              </span>
            );
          },
        }),

        helper.display({
          id: 'products',
          header: t('columns.products'),
          meta: { hideBelow: 'lg' },
          cell: (info) => (
            <CellStack
              label={info.row.original.returnedProduct}
              sub={info.row.original.newProduct}
            />
          ),
        }),

        helper.accessor('status', {
          header: t('columns.status'),
          sortFn: 'basic',
          cell: (info) => {
            const { status } = info.row.original;

            return <CellBadge label={tStatus(status)} color={EXCHANGE_STATUS_COLORS[status]} />;
          },
        }),

        helper.accessor('amount', {
          header: t('columns.amount'),
          sortFn: 'basic',
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => (
            <CellNumber
              value={format.number(info.getValue() / 100, 'currency')}
              sub={t('fees', {
                amount: format.number(info.row.original.fees / 100, 'currency'),
              })}
            />
          ),
        }),

        helper.accessor('requestedAt', {
          header: t('columns.requestedAt'),
          sortFn: 'datetime',
          meta: { hideBelow: 'xl' },
          cell: (info) => (
            <CellMuted>{format.dateTime(new Date(info.getValue()), 'stamp')}</CellMuted>
          ),
        }),

        helper.display({
          id: 'actions',
          header: () => <span className="sr-only">{tCommon('actions')}</span>,
          enableSorting: false,
          meta: { align: 'end', isActions: true, width: 'w-12' },
          cell: (info) => (
            <RowActions>
              <DropdownMenuItem onClick={() => router.push(`/exchanges/${info.row.original.code}`)}>
                <Eye className="size-4" />
                {tCommon('view')}
              </DropdownMenuItem>
            </RowActions>
          ),
        }),
      ]),
    [format, router, t, tCommon, tStatus],
  );

  return (
    <div className="flex flex-col gap-4">
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            selects={selects}
            onReset={() => {
              setSearch('');
              reset();
            }}
          >
            {canWrite ? (
              <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
                <Plus className="size-4" />
                <span className="hidden sm:inline">{t('new')}</span>
              </Button>
            ) : null}
          </DataTableToolbar>
        }
      />

      {creating ? (
        <ExchangeForm
          onDone={() => setCreating(false)}
          onCreated={(code) => router.push(`/exchanges/${code}`)}
        />
      ) : null}
    </div>
  );
}
