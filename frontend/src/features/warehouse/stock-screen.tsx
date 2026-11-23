'use client';

import type { Agency, Client, Paginated, ProductListItem, StockListItem } from '@delivery/shared';
import { Eye, SlidersHorizontal } from 'lucide-react';
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
import { StockAdjustDialog } from '@/features/warehouse/stock-adjust-dialog';
import { useRouter } from '@/i18n/navigation';

const helper = createDataTableColumnHelper<StockListItem>();

/**
 * Stock par produit × agence.
 *
 * Le filtre « alerte » ne demande pas de seuil : il vient de la configuration
 * (`warehouse.lowStockThreshold`, D-019), parce qu'un entrepôt de meubles et un
 * entrepôt de cosmétiques n'ont pas la même notion de « bientôt vide ». Le
 * serveur traduit le réglage en comparaison ; l'écran n'a qu'à cocher.
 */
export function StockScreen({
  page,
  clients,
  agencies,
  products,
  lockedClientId,
  canAdjust = true,
}: {
  page: Paginated<StockListItem>;
  clients: Client[];
  agencies: Agency[];
  /** Pour l'ajustement : il faut désigner un produit et une agence. */
  products: ProductListItem[];
  lockedClientId?: string;
  /** Lot V, V2 (D-234) : le vendeur consulte ; l'API lui refuse l'ajustement (403). */
  canAdjust?: boolean;
}) {
  const t = useTranslations('warehouse.stock');
  const format = useFormatter();
  const router = useRouter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'quantity',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [adjusting, setAdjusting] = useState(false);

  const selects: FilterSelect[] = useMemo(() => {
    const list: FilterSelect[] = [];

    if (!lockedClientId) {
      list.push({
        // D-255 : plusieurs clients, plusieurs agences ; « stock bas » reste un oui/non unique.
        id: 'clientId',
        multiple: true,
        label: t('filters.client'),
        value: param('clientId') ?? ALL_VALUE,
        options: clients.map((client) => ({ value: client.id, label: client.name })),
        onChange: (value) => setFilter('clientId', value === ALL_VALUE ? undefined : value),
      });
    }

    list.push(
      {
        id: 'agencyId',
        multiple: true,
        label: t('filters.agency'),
        value: param('agencyId') ?? ALL_VALUE,
        options: agencies.map((agency) => ({ value: agency.id, label: agency.name })),
        onChange: (value) => setFilter('agencyId', value === ALL_VALUE ? undefined : value),
      },
      {
        id: 'lowStock',
        label: t('filters.lowStock'),
        value: param('lowStock') ?? ALL_VALUE,
        options: [{ value: 'true', label: t('lowStock') }],
        onChange: (value) => setFilter('lowStock', value === ALL_VALUE ? undefined : value),
      },
    );

    return list;
  }, [agencies, clients, lockedClientId, param, setFilter, t]);

  const columns: DataTableColumns<StockListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('productName', {
          header: t('columns.product'),
          cell: (info) => (
            <CellStack
              label={info.getValue()}
              sub={info.row.original.productReference ?? undefined}
            />
          ),
        }),

        // Un vendeur ne voit que SON stock : la colonne « E-commerçant » est du back-office.
        ...(lockedClientId
          ? []
          : [
              helper.accessor('clientName', {
                header: t('columns.client'),
                meta: { hideBelow: 'lg' },
                cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
              }),
            ]),

        helper.accessor('agencyName', {
          header: t('columns.agency'),
          meta: { hideBelow: 'sm' },
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),

        helper.accessor('quantity', {
          header: t('columns.quantity'),
          meta: { align: 'end' },
          cell: (info) => (
            <span className="inline-flex items-center gap-2">
              {info.row.original.lowStock ? (
                <CellBadge label={t('lowStock')} color="var(--chart-3)" />
              ) : null}
              <CellNumber value={info.getValue()} />
            </span>
          ),
        }),

        helper.accessor('updatedAt', {
          header: t('columns.updatedAt'),
          meta: { hideBelow: 'xl' },
          cell: (info) => (
            <CellMuted>{format.dateTime(new Date(info.getValue()), 'stamp')}</CellMuted>
          ),
        }),

        helper.display({
          id: 'actions',
          header: '',
          meta: { isActions: true, align: 'end', width: 'w-16' },
          cell: (info) => (
            <RowActions>
              <DropdownMenuItem onClick={() => router.push(`/stock/${info.row.original.id}`)}>
                <Eye className="size-4" />
                {t('open')}
              </DropdownMenuItem>
            </RowActions>
          ),
        }),
      ]),
    [format, lockedClientId, router, t],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(row) => router.push(`/stock/${row.id}`)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="stock"
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            onReset={() => {
              setSearch('');
              reset();
            }}
          >
            {canAdjust ? (
              <Button size="sm" className="gap-1.5" onClick={() => setAdjusting(true)}>
                <SlidersHorizontal className="size-4" />
                <span className="hidden sm:inline">{t('adjust')}</span>
              </Button>
            ) : null}
          </DataTableToolbar>
        }
      />

      {canAdjust && adjusting ? (
        <StockAdjustDialog
          products={products}
          agencies={agencies}
          onClose={() => setAdjusting(false)}
          onDone={(stock) => router.push(`/stock/${stock.id}`)}
        />
      ) : null}
    </>
  );
}
