'use client';

import type { Agency, BankAccount, Client, Paginated, PaymentListItem } from '@delivery/shared';
import { Plus, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellMuted, CellNumber, CellStack, RowActions } from '@/components/data-table/cells';
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
import { PaymentForm } from '@/features/finance/payment-form';
import { useFinanceError } from '@/features/finance/use-finance-error';
import { useRouter } from '@/i18n/navigation';
import { deletePaymentAction } from '@/lib/actions/finance';

const helper = createDataTableColumnHelper<PaymentListItem>();

/**
 * Règlements.
 *
 * L'annulation est dans le menu de ligne et nulle part ailleurs : supprimer un
 * règlement peut DÉ-solder une facture et ramener ses colis en arrière. Ce
 * n'est pas un geste de liste, c'est une correction — elle se cherche.
 *
 * L'e-commerçant y voit les siens, en lecture : c'est la trace de ce qu'il a
 * reçu, et elle lui appartient autant qu'à l'agence.
 */
export function PaymentsScreen({
  page,
  clients,
  agencies,
  accounts,
  canPay,
}: {
  page: Paginated<PaymentListItem>;
  clients: Client[];
  agencies: Agency[];
  accounts: BankAccount[];
  canPay: boolean;
}) {
  const t = useTranslations('finance.payments');
  const tMethod = useTranslations('finance.payments.method');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();
  const errorMessage = useFinanceError();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'paidAt',
    defaultOrder: 'desc',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [creating, setCreating] = useState(false);
  const [, startTransition] = useTransition();

  const remove = (row: PaymentListItem) => {
    startTransition(async () => {
      const result = await deletePaymentAction(row.id);
      if (result.ok) toast.success(t('deleted'));
      else toast.error(errorMessage(result));
    });
  };

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        // D-255 : méthode, client et agence à choix multiple (`a,b` dans l'URL).
        id: 'method',
        multiple: true,
        label: t('filters.method'),
        value: param('method') ?? ALL_VALUE,
        options: [
          { value: 'cash', label: tMethod('cash') },
          { value: 'transfer', label: tMethod('transfer') },
          { value: 'check', label: tMethod('check') },
        ],
        onChange: (value) => setFilter('method', value === ALL_VALUE ? undefined : value),
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
    [agencies, clients, param, setFilter, t, tMethod],
  );

  const columns: DataTableColumns<PaymentListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('paidAt', {
          header: t('columns.paidAt'),
          cell: (info) => (
            <CellStack
              label={format.dateTime(new Date(info.getValue()), 'short')}
              sub={info.row.original.agencyName}
            />
          ),
        }),

        helper.accessor('clientName', {
          header: t('columns.client'),
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),

        helper.accessor('invoiceCode', {
          header: t('columns.invoice'),
          meta: { hideBelow: 'sm' },
          cell: (info) => {
            const code = info.getValue();
            // Un règlement sans facture est un acompte : on le dit, plutôt que
            // de laisser une cellule vide qui ressemble à une donnée manquante.
            return code ? <CellStack label={code} mono /> : <CellMuted>{t('onAccount')}</CellMuted>;
          },
        }),

        helper.accessor('method', {
          header: t('columns.method'),
          meta: { hideBelow: 'lg' },
          cell: (info) => (
            <CellStack
              label={tMethod(info.getValue())}
              sub={info.row.original.bankAccountLabel ?? undefined}
            />
          ),
        }),

        helper.accessor('amount', {
          header: t('columns.amount'),
          meta: { align: 'end' },
          cell: (info) => (
            <CellNumber
              value={format.number(info.getValue() / 100, 'currency')}
              sub={info.row.original.reference ?? undefined}
            />
          ),
        }),

        ...(canPay
          ? [
              helper.display({
                id: 'actions',
                header: '',
                meta: { isActions: true, align: 'end', width: 'w-16' },
                cell: (info) => (
                  <RowActions>
                    <DropdownMenuItem
                      variant="destructive"
                      onClick={() => remove(info.row.original)}
                    >
                      <Trash2 className="size-4" />
                      {tCommon('delete')}
                    </DropdownMenuItem>
                  </RowActions>
                ),
              }),
            ]
          : []),
      ]),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [canPay, format, t, tCommon, tMethod],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(row) => {
          if (row.invoiceCode) router.push(`/invoices/${row.invoiceCode}`);
        }}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="payments"
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            onReset={() => {
              setSearch('');
              reset();
            }}
          >
            {canPay ? (
              <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
                <Plus className="size-4" />
                <span className="hidden sm:inline">{t('new')}</span>
              </Button>
            ) : null}
          </DataTableToolbar>
        }
      />

      {creating ? (
        <PaymentForm
          accounts={accounts}
          clients={clients}
          agencies={agencies}
          onDone={() => setCreating(false)}
        />
      ) : null}
    </>
  );
}
