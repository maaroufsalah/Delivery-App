'use client';

import type { Agency, Client, CodPayoutListItem, Paginated } from '@delivery/shared';
import { Eye, Layers, Plus, Trash2 } from 'lucide-react';
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
import { CodPayoutBulkDialog } from '@/features/finance/cod-payout-bulk-dialog';
import { CodPayoutForm } from '@/features/finance/cod-payout-form';
import { useFinanceError } from '@/features/finance/use-finance-error';
import { useRouter } from '@/i18n/navigation';
import { deleteCodPayoutAction } from '@/lib/actions/finance';

const helper = createDataTableColumnHelper<CodPayoutListItem>();

/**
 * Bons de versement — le COD des e-commerçants PROFESSIONNELS, versé en entier.
 *
 * L'écran n'affiche nulle part de déduction, et ce n'est pas un oubli : c'est
 * ce qui distingue ce document d'une facture (D-061). Les frais sont réclamés
 * ailleurs, par la facture mensuelle.
 *
 * Un bon versé ne se supprime pas : c'est la preuve que l'argent est parti.
 * L'action reste visible mais désactivée — la voir grisée dit la règle mieux
 * que son absence.
 */
export function CodPayoutsScreen({
  page,
  clients,
  agencies,
  canWrite,
  canBulk = false,
  merchant = false,
}: {
  page: Paginated<CodPayoutListItem>;
  /** Seulement les PROFESSIONNELS : les autres sont réglés par leur facture. */
  clients: Client[];
  agencies: Agency[];
  canWrite: boolean;
  /** Lot U (D-233) : « En masse », l'administration seule (`finance.validate`). */
  canBulk?: boolean;
  /** E2E-012 (lot W3, D-238) : l'espace vendeur — ni colonne ni filtre « E-commerçant ». */
  merchant?: boolean;
}) {
  const t = useTranslations('finance.codPayouts');
  const tStatus = useTranslations('finance.codPayouts.status');
  const tMethod = useTranslations('finance.payments.method');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();
  const errorMessage = useFinanceError();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'createdAt',
    defaultOrder: 'desc',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [creating, setCreating] = useState(false);
  const [bulk, setBulk] = useState(false);
  const [, startTransition] = useTransition();

  const remove = (row: CodPayoutListItem) => {
    startTransition(async () => {
      const result = await deleteCodPayoutAction(row.code);
      if (result.ok) toast.success(t('deleted'));
      else toast.error(errorMessage(result));
    });
  };

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        // Deux états seulement (brouillon, versé) : le choix reste unique (D-255).
        id: 'status',
        label: t('filters.state'),
        value: param('status') ?? ALL_VALUE,
        options: [
          { value: 'draft', label: tStatus('draft') },
          { value: 'paid', label: tStatus('paid') },
        ],
        onChange: (value) => setFilter('status', value === ALL_VALUE ? undefined : value),
      },
      ...(merchant
        ? []
        : [
            {
              id: 'clientId',
              multiple: true,
              label: t('filters.client'),
              value: param('clientId') ?? ALL_VALUE,
              options: clients.map((client) => ({ value: client.id, label: client.name })),
              onChange: (value: string) =>
                setFilter('clientId', value === ALL_VALUE ? undefined : value),
            },
          ]),
      {
        id: 'agencyId',
        multiple: true,
        label: t('filters.agency'),
        value: param('agencyId') ?? ALL_VALUE,
        options: agencies.map((agency) => ({ value: agency.id, label: agency.name })),
        onChange: (value) => setFilter('agencyId', value === ALL_VALUE ? undefined : value),
      },
    ],
    [agencies, clients, merchant, param, setFilter, t, tStatus],
  );

  const columns: DataTableColumns<CodPayoutListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('code', {
          header: t('columns.code'),
          cell: (info) => (
            <CellStack label={info.getValue()} sub={info.row.original.agencyName} mono />
          ),
        }),

        ...(merchant
          ? []
          : [
              helper.accessor('clientName', {
                header: t('columns.client'),
                cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
              }),
            ]),

        helper.accessor('parcelCount', {
          header: t('columns.parcels'),
          meta: { align: 'end', hideBelow: 'sm' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),

        helper.accessor('amount', {
          header: t('columns.amount'),
          meta: { align: 'end' },
          cell: (info) => <CellNumber value={format.number(info.getValue() / 100, 'currency')} />,
        }),

        helper.accessor('method', {
          header: t('columns.method'),
          meta: { hideBelow: 'lg' },
          cell: (info) => (
            <CellStack
              label={tMethod(info.getValue())}
              sub={info.row.original.reference ?? undefined}
            />
          ),
        }),

        helper.accessor('paidAt', {
          header: t('columns.paidAt'),
          meta: { hideBelow: 'lg' },
          cell: (info) => {
            const value = info.getValue();
            return (
              <CellStack
                label={value ? format.dateTime(new Date(value), 'short') : '-'}
                sub={info.row.original.paidByName ?? undefined}
              />
            );
          },
        }),

        helper.accessor('status', {
          header: t('columns.state'),
          meta: { width: 'w-32' },
          cell: (info) =>
            info.getValue() === 'paid' ? (
              <CellBadge label={tStatus('paid')} color="var(--chart-4)" />
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
                <DropdownMenuItem onClick={() => router.push(`/cod-payouts/${row.code}`)}>
                  <Eye className="size-4" />
                  {t('open')}
                </DropdownMenuItem>
                {canWrite ? (
                  <>
                    <DropdownMenuSeparator />
                    {/* Un bon versé est une preuve : il reste. */}
                    <DropdownMenuItem
                      variant="destructive"
                      disabled={row.status === 'paid'}
                      onClick={() => remove(row)}
                    >
                      <Trash2 className="size-4" />
                      {tCommon('delete')}
                    </DropdownMenuItem>
                  </>
                ) : null}
              </RowActions>
            );
          },
        }),
      ]),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [canWrite, format, router, t, tCommon, tMethod, tStatus],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(row) => router.push(`/cod-payouts/${row.code}`)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="cod-payouts"
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            onReset={() => {
              setSearch('');
              reset();
            }}
          >
            {/*
              Sans client professionnel, le bouton n'a rien à ouvrir : le
              formulaire proposerait une liste vide. Mieux vaut le masquer et
              laisser l'écran vide expliquer pourquoi.
            */}
            {canBulk ? (
              <Button size="sm" variant="outline" className="gap-1.5" onClick={() => setBulk(true)}>
                <Layers className="size-4" />
                <span className="hidden sm:inline">{t('bulk.open')}</span>
              </Button>
            ) : null}
            {canWrite && clients.length > 0 ? (
              <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
                <Plus className="size-4" />
                <span className="hidden sm:inline">{t('new')}</span>
              </Button>
            ) : null}
          </DataTableToolbar>
        }
      />

      {creating ? (
        <CodPayoutForm
          clients={clients}
          agencies={agencies}
          onDone={() => setCreating(false)}
          onCreated={(code) => router.push(`/cod-payouts/${code}`)}
        />
      ) : null}

      {bulk ? (
        <CodPayoutBulkDialog
          agencies={agencies}
          onDone={() => {
            setBulk(false);
            router.refresh();
          }}
        />
      ) : null}
    </>
  );
}
