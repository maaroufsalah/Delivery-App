'use client';

import type {
  City,
  Client,
  District,
  OrderListItem,
  Paginated,
  ProductListItem,
} from '@delivery/shared';
import { Eye, Plus, Trash2, Upload } from 'lucide-react';
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
import { WarehouseImportDialog } from '@/features/warehouse/import-dialog';
import { OrderForm } from '@/features/warehouse/order-form';
import { useWarehouseError } from '@/features/warehouse/use-warehouse-error';
import { useRouter } from '@/i18n/navigation';
import { deleteOrderAction, importOrdersAction } from '@/lib/actions/warehouse';

const helper = createDataTableColumnHelper<OrderListItem>();

/**
 * Commandes — DOMAIN.md §4 dette n°19.
 *
 * La colonne « référence » est celle de la boutique du client, et c'est elle
 * qui rend l'import rejouable : `(client, référence)` est unique, donc
 * réimporter le même fichier ne crée pas de doublon. Le legacy lisait les
 * commandes PrestaShop en direct et n'en gardait aucune trace.
 */
export function OrdersScreen({
  page,
  clients,
  cities,
  districts,
  products,
  lockedClientId,
}: {
  page: Paginated<OrderListItem>;
  clients: Client[];
  cities: City[];
  districts: District[];
  products: ProductListItem[];
  lockedClientId?: string;
}) {
  const t = useTranslations('warehouse.orders');
  const tStatus = useTranslations('warehouse.orders.status');
  const tSource = useTranslations('warehouse.orders.sources');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();
  const errorMessage = useWarehouseError();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'createdAt',
    defaultOrder: 'desc',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [creating, setCreating] = useState(false);
  const [importing, setImporting] = useState(false);
  const [, startTransition] = useTransition();

  const remove = (row: OrderListItem) => {
    startTransition(async () => {
      const result = await deleteOrderAction(row.code);
      if (result.ok) toast.success(t('deleted'));
      else toast.error(errorMessage(result));
    });
  };

  const selects: FilterSelect[] = useMemo(() => {
    const list: FilterSelect[] = [
      {
        // D-255 : plusieurs états, plusieurs clients ; la source (deux valeurs) reste unique.
        id: 'status',
        multiple: true,
        label: t('filters.state'),
        value: param('status') ?? ALL_VALUE,
        options: [
          { value: 'draft', label: tStatus('draft') },
          { value: 'confirmed', label: tStatus('confirmed') },
          { value: 'parcel-created', label: tStatus('parcel-created') },
          { value: 'cancelled', label: tStatus('cancelled') },
        ],
        onChange: (value) => setFilter('status', value === ALL_VALUE ? undefined : value),
      },
      {
        id: 'source',
        label: t('filters.source'),
        value: param('source') ?? ALL_VALUE,
        options: [
          { value: 'manual', label: tSource('manual') },
          { value: 'excel', label: tSource('excel') },
        ],
        onChange: (value) => setFilter('source', value === ALL_VALUE ? undefined : value),
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

    return list;
  }, [clients, lockedClientId, param, setFilter, t, tSource, tStatus]);

  const columns: DataTableColumns<OrderListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('code', {
          header: t('columns.code'),
          cell: (info) => (
            <CellStack
              label={info.getValue()}
              sub={info.row.original.externalRef ?? undefined}
              mono
            />
          ),
        }),

        helper.accessor('clientName', {
          header: t('columns.client'),
          meta: { hideBelow: 'xl' },
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),

        helper.accessor('recipientName', {
          header: t('columns.recipient'),
          meta: { hideBelow: 'sm' },
          cell: (info) => (
            <CellStack label={info.getValue()} sub={info.row.original.destinationCityName} />
          ),
        }),

        helper.accessor('totalAmount', {
          header: t('columns.total'),
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => <CellNumber value={format.number(info.getValue() / 100, 'currency')} />,
        }),

        helper.accessor('status', {
          header: t('columns.state'),
          meta: { width: 'w-36' },
          cell: (info) => {
            const status = info.getValue();
            if (status === 'parcel-created') {
              return <CellBadge label={tStatus('parcel-created')} color="var(--chart-4)" />;
            }
            if (status === 'confirmed') {
              return <CellBadge label={tStatus('confirmed')} color="var(--chart-1)" />;
            }
            if (status === 'cancelled') {
              return <CellBadge label={tStatus('cancelled')} />;
            }
            return <CellBadge label={tStatus('draft')} color="var(--chart-3)" />;
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
                <DropdownMenuItem onClick={() => router.push(`/orders/${row.code}`)}>
                  <Eye className="size-4" />
                  {t('open')}
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                {/* Une commande devenue colis ne se supprime pas : le colis la
                    cite, et l'historique doit rester lisible. */}
                <DropdownMenuItem
                  variant="destructive"
                  disabled={row.status === 'parcel-created'}
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
        onRowClick={(row) => router.push(`/orders/${row.code}`)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="orders"
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            onReset={() => {
              setSearch('');
              reset();
            }}
          >
            <Button
              size="sm"
              variant="outline"
              className="gap-1.5"
              onClick={() => setImporting(true)}
            >
              <Upload className="size-4" />
              <span className="hidden sm:inline">{t('import')}</span>
            </Button>
            <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
              <Plus className="size-4" />
              <span className="hidden sm:inline">{t('new')}</span>
            </Button>
          </DataTableToolbar>
        }
      />

      {creating ? (
        <OrderForm
          clients={clients}
          cities={cities}
          districts={districts}
          products={products}
          lockedClientId={lockedClientId}
          onDone={(order) => {
            setCreating(false);
            if (order) router.push(`/orders/${order.code}`);
          }}
        />
      ) : null}

      {importing ? (
        <WarehouseImportDialog
          kind="orders"
          clients={clients}
          lockedClientId={lockedClientId}
          onClose={() => setImporting(false)}
          onRun={(content, apply, clientId) => importOrdersAction(content, apply, clientId)}
        />
      ) : null}
    </>
  );
}
