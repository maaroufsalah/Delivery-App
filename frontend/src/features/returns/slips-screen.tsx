'use client';

import type { Agency, Client, Paginated, ReturnSlipListItem } from '@delivery/shared';
import { Eye, Layers, Plus, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useCallback, useMemo, useState, useTransition } from 'react';
import { toast } from 'sonner';
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
import { DropdownMenuItem, DropdownMenuSeparator } from '@/components/ui/dropdown-menu';
import { SlipBulkDialog } from '@/features/returns/slip-bulk-dialog';
import { SlipForm } from '@/features/returns/slip-form';
import { useRouter } from '@/i18n/navigation';
import { deleteSlipAction } from '@/lib/actions/returns';

const helper = createDataTableColumnHelper<ReturnSlipListItem>();

/**
 * Bons de retour rendus aux e-commerçants.
 *
 * Le total des frais est affiché dès la liste : c'est le chiffre que
 * l'e-commerçant conteste, et le voir sans ouvrir le bon évite d'ouvrir dix
 * bons pour trouver celui dont on parle au téléphone.
 */
export function SlipsScreen({
  page,
  clients,
  agencies,
}: {
  page: Paginated<ReturnSlipListItem>;
  clients: Client[];
  agencies: Agency[];
}) {
  const t = useTranslations('returns.slips');
  const tStatus = useTranslations('returns.slips.status');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'createdAt',
    defaultOrder: 'desc',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [creating, setCreating] = useState(false);
  // Lot E, E2 (D-202) : la création en masse, à côté de « Nouveau bon ».
  const [bulk, setBulk] = useState(false);
  const closeBulk = useCallback(() => setBulk(false), []);
  const [, startTransition] = useTransition();

  const remove = (row: ReturnSlipListItem) => {
    startTransition(async () => {
      const result = await deleteSlipAction(row.code);
      if (result.ok) toast.success(t('deleted'));
      else toast.error(result.error);
    });
  };

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        id: 'status',
        label: t('filters.state'),
        value: param('status') ?? ALL_VALUE,
        options: [
          { value: 'draft', label: tStatus('draft') },
          { value: 'delivered', label: tStatus('delivered') },
        ],
        onChange: (value) => setFilter('status', value === ALL_VALUE ? undefined : value),
      },
      {
        id: 'clientId',
        multiple: true,
        label: t('filters.client'),
        value: param('clientId') ?? ALL_VALUE,
        options: clients.map((client) => ({ value: client.id, label: client.name })),
        onChange: (value) => setFilter('clientId', value === ALL_VALUE ? undefined : value),
      },
      {
        id: 'agencyId',
        multiple: true,
        label: t('filters.agency'),
        value: param('agencyId') ?? ALL_VALUE,
        options: agencies.map((agency) => ({ value: agency.id, label: agency.name })),
        onChange: (value) => setFilter('agencyId', value === ALL_VALUE ? undefined : value),
      },
    ],
    [agencies, clients, param, setFilter, t, tStatus],
  );

  const columns: DataTableColumns<ReturnSlipListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('code', {
          header: t('columns.code'),
          cell: (info) => (
            <CellStack label={info.getValue()} sub={info.row.original.agencyName} mono />
          ),
        }),

        helper.accessor('clientName', {
          header: t('columns.client'),
          meta: { hideBelow: 'sm' },
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),

        helper.accessor('parcelCount', {
          header: t('columns.parcels'),
          meta: { align: 'end' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),

        helper.accessor('totalFees', {
          header: t('columns.fees'),
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => <CellNumber value={format.number(info.getValue() / 100, 'currency')} />,
        }),

        helper.accessor('deliveredAt', {
          header: t('columns.deliveredAt'),
          meta: { hideBelow: 'lg' },
          cell: (info) => {
            const value = info.getValue();
            return (
              <CellStack
                label={value ? format.dateTime(new Date(value), 'stamp') : '-'}
                sub={info.row.original.receivedByName ?? undefined}
              />
            );
          },
        }),

        helper.accessor('status', {
          header: t('columns.state'),
          meta: { width: 'w-32' },
          cell: (info) =>
            info.getValue() === 'delivered' ? (
              <CellBadge label={tStatus('delivered')} color="var(--chart-4)" />
            ) : (
              <CellBadge label={tStatus('draft')} color="var(--chart-3)" />
            ),
        }),

        helper.display({
          id: 'actions',
          header: '',
          meta: { isActions: true, align: 'end', width: 'w-16' },
          cell: (info) => {
            const row = info.row.original;

            return (
              <RowActions>
                <DropdownMenuItem onClick={() => router.push(`/returns/slips/${row.code}`)}>
                  <Eye className="size-4" />
                  {t('open')}
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                {/* Un bon remis est la preuve de la restitution : il reste. */}
                <DropdownMenuItem
                  variant="destructive"
                  disabled={row.status === 'delivered'}
                  onClick={() => remove(row)}
                >
                  <Trash2 className="size-4" />
                  {tCommon('delete')}
                </DropdownMenuItem>
              </RowActions>
            );
          },
        }),
      ]),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [format, router, t, tCommon, tStatus],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(row) => router.push(`/returns/slips/${row.code}`)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="return-slips"
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            onReset={() => {
              setSearch('');
              reset();
            }}
          >
            <Button size="sm" variant="outline" className="gap-1.5" onClick={() => setBulk(true)}>
              <Layers className="size-4" />
              <span className="hidden sm:inline">{t('bulk.open')}</span>
            </Button>
            <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
              <Plus className="size-4" />
              <span className="hidden sm:inline">{t('new')}</span>
            </Button>
          </DataTableToolbar>
        }
      />

      {bulk ? <SlipBulkDialog onDone={closeBulk} /> : null}

      {creating ? (
        <SlipForm
          clients={clients}
          agencies={agencies}
          onDone={() => setCreating(false)}
          onCreated={(code) => router.push(`/returns/slips/${code}`)}
        />
      ) : null}
    </>
  );
}
