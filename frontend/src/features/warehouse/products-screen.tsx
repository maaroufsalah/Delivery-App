'use client';

import type {
  Agency,
  Client,
  Paginated,
  ProductAttributeItem,
  ProductDetail,
  ProductListItem,
} from '@delivery/shared';
import { Eye, Pencil, Plus, Trash2, Upload } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useState, useTransition } from 'react';
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
import { DeleteDialog } from '@/features/referential/form-sheet';
import { ProductForm } from '@/features/warehouse/product-form';
import { WarehouseImportDialog } from '@/features/warehouse/import-dialog';
import { useWarehouseError } from '@/features/warehouse/use-warehouse-error';
import { useRouter } from '@/i18n/navigation';
import { deleteProductAction, importProductsAction } from '@/lib/actions/warehouse';

const helper = createDataTableColumnHelper<ProductListItem>();

/**
 * Catalogue produits.
 *
 * Deux colonnes de stock se ressemblent et ne disent pas la même chose : le
 * **stock** affiché est la somme des agences, seule quantité réelle. Le champ
 * `internalStock` du legacy, NULL sur les 745 lignes de production, n'apparaît
 * que dans la fiche, où il peut être nommé pour ce qu'il est.
 */
export function ProductsScreen({
  page,
  clients,
  agencies,
  attributes,
  lockedClientId,
}: {
  page: Paginated<ProductListItem>;
  clients: Client[];
  agencies: Agency[];
  attributes: ProductAttributeItem[];
  lockedClientId?: string;
}) {
  const t = useTranslations('warehouse.products');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();
  const errorMessage = useWarehouseError();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'name',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [editing, setEditing] = useState<ProductDetail | null | undefined>(undefined);
  const [removing, setRemoving] = useState<ProductListItem | null>(null);
  const [importing, setImporting] = useState(false);
  const [, startTransition] = useTransition();

  const selects: FilterSelect[] = useMemo(() => {
    const list: FilterSelect[] = [];

    // Un e-commerçant n'a pas de filtre « client » : il n'en a qu'un.
    if (!lockedClientId) {
      list.push({
        // D-255 : plusieurs clients, plusieurs agences ; « actif » reste un oui/non unique.
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
        id: 'isActive',
        label: t('filters.state'),
        value: param('isActive') ?? ALL_VALUE,
        options: [
          { value: 'true', label: tCommon('active') },
          { value: 'false', label: tCommon('inactive') },
        ],
        onChange: (value) => setFilter('isActive', value === ALL_VALUE ? undefined : value),
      },
    );

    return list;
  }, [agencies, clients, lockedClientId, param, setFilter, t, tCommon]);

  const columns: DataTableColumns<ProductListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('name', {
          header: t('columns.name'),
          cell: (info) => (
            <CellStack label={info.getValue()} sub={info.row.original.reference ?? undefined} />
          ),
        }),

        // E2E-012 (lot W3, D-238) : le vendeur ne voit pas une colonne « E-commerçant » à son nom.
        ...(lockedClientId
          ? []
          : [
              helper.accessor('clientName', {
                header: t('columns.client'),
                meta: { hideBelow: 'md' },
                cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
              }),
            ]),

        helper.accessor('price', {
          header: t('columns.price'),
          meta: { align: 'end', hideBelow: 'sm' },
          cell: (info) => {
            const value = info.getValue();
            return (
              <CellNumber value={value === null ? '-' : format.number(value / 100, 'currency')} />
            );
          },
        }),

        helper.accessor('stockTotal', {
          header: t('columns.stock'),
          meta: { align: 'end' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),

        helper.accessor('isActive', {
          header: t('columns.state'),
          meta: { width: 'w-28' },
          cell: (info) =>
            info.getValue() ? (
              <CellBadge label={tCommon('active')} color="var(--chart-4)" />
            ) : (
              <CellBadge label={tCommon('inactive')} />
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
                <DropdownMenuItem onClick={() => router.push(`/products/${row.id}`)}>
                  <Eye className="size-4" />
                  {t('open')}
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => router.push(`/products/${row.id}`)}>
                  <Pencil className="size-4" />
                  {tCommon('edit')}
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem variant="destructive" onClick={() => setRemoving(row)}>
                  <Trash2 className="size-4" />
                  {tCommon('delete')}
                </DropdownMenuItem>
              </RowActions>
            );
          },
        }),
      ]),
    [format, lockedClientId, router, t, tCommon],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(row) => router.push(`/products/${row.id}`)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="products"
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
              <span className="hidden sm:inline">{tCommon('more')}</span>
            </Button>
            <Button size="sm" className="gap-1.5" onClick={() => setEditing(null)}>
              <Plus className="size-4" />
              <span className="hidden sm:inline">{t('new')}</span>
            </Button>
          </DataTableToolbar>
        }
      />

      {editing !== undefined ? (
        <ProductForm
          product={editing}
          clients={clients}
          attributes={attributes}
          lockedClientId={lockedClientId}
          onDone={() => setEditing(undefined)}
        />
      ) : null}

      {removing ? (
        <DeleteDialog
          open
          onOpenChange={(next) => {
            if (!next) setRemoving(null);
          }}
          label={removing.name}
          onConfirm={async () => {
            const result = await deleteProductAction(removing.id);
            if (result.ok) {
              startTransition(() => router.refresh());
              return result;
            }
            // Le motif métier remonte traduit : « ce produit a encore du
            // stock » se corrige, « échec » n'apprend rien.
            return { ...result, error: errorMessage(result) };
          }}
        />
      ) : null}

      {importing ? (
        <WarehouseImportDialog
          kind="products"
          clients={clients}
          lockedClientId={lockedClientId}
          onClose={() => setImporting(false)}
          onRun={(content, apply, clientId) => importProductsAction(content, apply, clientId)}
        />
      ) : null}
    </>
  );
}
