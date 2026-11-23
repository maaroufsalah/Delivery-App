'use client';

import type {
  Agency,
  Client,
  Paginated,
  ProductListItem,
  StorageRequestListItem,
} from '@delivery/shared';
import { Eye, Plus, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useState, useTransition } from 'react';
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
import { StorageRequestForm } from '@/features/warehouse/storage-request-form';
import { useWarehouseError } from '@/features/warehouse/use-warehouse-error';
import { useRouter } from '@/i18n/navigation';
import { deleteStorageRequestAction } from '@/lib/actions/warehouse';

const helper = createDataTableColumnHelper<StorageRequestListItem>();

/**
 * Demandes d'entreposage.
 *
 * **Un seul écran pour deux menus.** Côté staff il s'appelle « Entreposages »
 * et sert à trancher ; côté e-commerçant il s'appelle « Demandes
 * d'entreposage » et sert à suivre. Ce n'est pas la liste qui change, c'est la
 * portée du compte — et la validation, fermée au client par
 * `warehouse.validate`.
 */
export function StorageScreen({
  page,
  clients,
  agencies,
  products,
  lockedClientId,
  /** Rend les libellés côté client : « Demandes d'entreposage ». */
  clientView = false,
}: {
  page: Paginated<StorageRequestListItem>;
  clients: Client[];
  agencies: Agency[];
  products: ProductListItem[];
  lockedClientId?: string;
  clientView?: boolean;
}) {
  const t = useTranslations('warehouse.storage');
  const tStatus = useTranslations('warehouse.storage.status');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();
  const errorMessage = useWarehouseError();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'requestedAt',
    defaultOrder: 'desc',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [creating, setCreating] = useState(false);
  const [, startTransition] = useTransition();

  const remove = (row: StorageRequestListItem) => {
    startTransition(async () => {
      const result = await deleteStorageRequestAction(row.code);
      if (result.ok) toast.success(t('deleted'));
      else toast.error(errorMessage(result));
    });
  };

  const selects: FilterSelect[] = useMemo(() => {
    const list: FilterSelect[] = [
      {
        // D-255 : plusieurs états, clients et agences.
        id: 'status',
        multiple: true,
        label: t('filters.state'),
        value: param('status') ?? ALL_VALUE,
        options: [
          { value: 'pending', label: tStatus('pending') },
          { value: 'validated', label: tStatus('validated') },
          { value: 'refused', label: tStatus('refused') },
        ],
        onChange: (value) => setFilter('status', value === ALL_VALUE ? undefined : value),
      },
    ];

    if (!lockedClientId) {
      list.push({
        id: 'clientId',
        multiple: true,
        label: t('filters.client'),
        value: param('clientId') ?? ALL_VALUE,
        options: clients.map((client) => ({ value: client.id, label: client.name })),
        onChange: (value) => setFilter('clientId', value === ALL_VALUE ? undefined : value),
      });
    }

    list.push({
      id: 'agencyId',
      multiple: true,
      label: t('filters.agency'),
      value: param('agencyId') ?? ALL_VALUE,
      options: agencies.map((agency) => ({ value: agency.id, label: agency.name })),
      onChange: (value) => setFilter('agencyId', value === ALL_VALUE ? undefined : value),
    });

    return list;
  }, [agencies, clients, lockedClientId, param, setFilter, t, tStatus]);

  const columns: DataTableColumns<StorageRequestListItem> = useMemo(
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
          meta: { hideBelow: 'md' },
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),

        helper.accessor('lineCount', {
          header: t('columns.lines'),
          meta: { align: 'end', hideBelow: 'sm' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),

        helper.accessor('totalQuantity', {
          header: t('columns.quantity'),
          meta: { align: 'end' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),

        helper.accessor('decidedAt', {
          header: t('columns.decidedAt'),
          meta: { hideBelow: 'xl' },
          cell: (info) => {
            const value = info.getValue();
            return (
              <CellStack
                label={value ? format.dateTime(new Date(value), 'stamp') : '-'}
                sub={info.row.original.decidedByName ?? undefined}
              />
            );
          },
        }),

        helper.accessor('status', {
          header: t('columns.state'),
          meta: { width: 'w-32' },
          cell: (info) => {
            const status = info.getValue();
            if (status === 'validated') {
              return <CellBadge label={tStatus('validated')} color="var(--chart-4)" />;
            }
            if (status === 'refused') {
              return <CellBadge label={tStatus('refused')} color="var(--destructive)" />;
            }
            return <CellBadge label={tStatus('pending')} color="var(--chart-3)" />;
          },
        }),

        helper.display({
          id: 'actions',
          header: '',
          meta: { isActions: true, align: 'end', width: 'w-16' },
          cell: (info) => {
            const row = info.row.original;

            return (
              <RowActions>
                <DropdownMenuItem onClick={() => router.push(`/storage/${row.code}`)}>
                  <Eye className="size-4" />
                  {t('open')}
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                {/* Une demande tranchée reste : c'est la trace de la décision. */}
                <DropdownMenuItem
                  variant="destructive"
                  disabled={row.status !== 'pending'}
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
        onRowClick={(row) => router.push(`/storage/${row.code}`)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="storage-requests"
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            onReset={() => {
              setSearch('');
              reset();
            }}
          >
            <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
              <Plus className="size-4" />
              <span className="hidden sm:inline">{t('new')}</span>
            </Button>
          </DataTableToolbar>
        }
      />

      {creating ? (
        <StorageRequestForm
          clients={clients}
          agencies={agencies}
          products={products}
          lockedClientId={lockedClientId}
          onDone={(request) => {
            setCreating(false);
            if (request) router.push(`/storage/${request.code}`);
          }}
        />
      ) : null}

      {clientView ? <span className="sr-only">{t('clientSubtitle')}</span> : null}
    </>
  );
}
