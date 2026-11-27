'use client';

import {
  type Agency,
  type Client,
  type InvoiceClientKind,
  type InvoiceDaySummary,
  INVOICE_DRAFT_DAY,
  type InvoiceListItem,
  type Paginated,
} from '@delivery/shared';
import { Layers, CalendarRange, Eye, Landmark, Plus, Trash2 } from 'lucide-react';
import { useFormatter, useTimeZone, useTranslations } from 'next-intl';
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
import { BulkInvoiceDialog } from '@/features/finance/bulk-invoice-dialog';
import { InvoiceDayHeader } from '@/features/finance/invoice-day-header';
import { InvoiceForm } from '@/features/finance/invoice-form';
import { InvoiceMonthlyForm } from '@/features/finance/invoice-monthly-form';
import { useFinanceError } from '@/features/finance/use-finance-error';
import { useRouter } from '@/i18n/navigation';
import { deleteInvoiceAction } from '@/lib/actions/finance';
import { invoiceDayOf } from '@/lib/invoice-day';

const helper = createDataTableColumnHelper<InvoiceListItem>();

/** Trois états, trois couleurs, toujours les mêmes d'un écran à l'autre. */
const STATUS_TONE: Record<InvoiceListItem['status'], string> = {
  draft: 'var(--chart-3)',
  issued: 'var(--chart-1)',
  paid: 'var(--chart-4)',
  // Chantier « Report des factures négatives » : le net vit sur la facture suivante.
  'carried-over': 'var(--chart-5)',
};

/**
 * Factures.
 *
 * `canWrite` décide de tout ce qui n'est pas de la lecture : l'e-commerçant
 * arrive ici avec `finance.read` seul, et la portée serveur ne lui montre que
 * SES factures. Il consulte et imprime ; il ne compose pas, ne supprime pas.
 * Cacher les gestes plutôt que les laisser échouer en 403 est la règle du
 * menu, appliquée à l'intérieur de l'écran.
 */
export function InvoicesScreen({
  page,
  days,
  clients,
  agencies,
  canWrite,
  kind = 'normal',
  showClient = true,
}: {
  page: Paginated<InvoiceListItem>;
  /** Les en-têtes des jours de la page (D-254), aux mêmes filtres que la liste. */
  days: InvoiceDaySummary[];
  /** Déjà filtrés par la page sur son type (point 4). */
  clients: Client[];
  agencies: Agency[];
  canWrite: boolean;
  /**
   * « Factures clients » ou « Factures clients pro » — recette 3, point 4.
   * Il choisit les routes de l'API (liste, export, générations, candidats de
   * l'ordre de virement) ; le rendu, lui, est le même : ce sont les mêmes
   * factures.
   */
  kind?: InvoiceClientKind;
  /**
   * Lot V, V2 : le vendeur ne lit que SES factures — une colonne et un filtre
   * « E-commerçant » à une seule valeur étaient le vocabulaire du back-office.
   */
  showClient?: boolean;
}) {
  const t = useTranslations('finance.invoices');
  const tBulk = useTranslations('finance.invoices.bulk');
  const tTransfer = useTranslations('finance.invoices.transfer');
  const tStatus = useTranslations('finance.invoices.status');
  const tDays = useTranslations('finance.invoices.days');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();
  const errorMessage = useFinanceError();

  const timeZone = useTimeZone();
  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    // D-254 : la liste se range par jour d'émission, brouillons en tête.
    defaultSort: 'issuedAt',
    defaultOrder: 'desc',
  });

  /*
   * LES JOURS — D-254. Déplié par défaut : le jour le plus récent de la page
   * et les brouillons (le travail en cours) ; un clic l'emporte sur ce défaut.
   */
  const daysByKey = useMemo(() => new Map(days.map((summary) => [summary.day, summary])), [days]);
  const latestDay = useMemo(
    () =>
      page.items
        .map((invoice) => invoiceDayOf(invoice, timeZone))
        .filter((day) => day !== INVOICE_DRAFT_DAY)
        .sort()
        .at(-1),
    [page.items, timeZone],
  );
  const [folded, setFolded] = useState<Record<string, boolean>>({});
  const isOpen = (day: string) =>
    folded[day] ?? (day === INVOICE_DRAFT_DAY || day === latestDay);

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [creating, setCreating] = useState(false);
  const [bulk, setBulk] = useState(false);
  const [transfer, setTransfer] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [, startTransition] = useTransition();

  /** Les seuls que « la facture du mois » sait facturer — D-061. */
  const professionalClients = useMemo(
    () => clients.filter((client) => client.billingType === 'professional'),
    [clients],
  );

  const remove = (row: InvoiceListItem) => {
    startTransition(async () => {
      const result = await deleteInvoiceAction(row.code);
      if (result.ok) toast.success(t('deleted'));
      else toast.error(errorMessage(result));
    });
  };

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        // D-255 : choix multiple ; l'URL porte `a,b`, l'API la lit telle quelle.
        id: 'status',
        multiple: true,
        label: t('filters.state'),
        value: param('status') ?? ALL_VALUE,
        options: [
          { value: 'draft', label: tStatus('draft') },
          { value: 'issued', label: tStatus('issued') },
          { value: 'paid', label: tStatus('paid') },
          { value: 'carried-over', label: tStatus('carried-over') },
        ],
        onChange: (value) => setFilter('status', value === ALL_VALUE ? undefined : value),
      },
      ...(showClient
        ? [
            {
              id: 'clientId',
              multiple: true,
              label: t('filters.client'),
              value: param('clientId') ?? ALL_VALUE,
              options: clients.map((client) => ({ value: client.id, label: client.name })),
              onChange: (value: string) =>
                setFilter('clientId', value === ALL_VALUE ? undefined : value),
            },
          ]
        : []),
      {
        id: 'agencyId',
        multiple: true,
        label: t('filters.agency'),
        value: param('agencyId') ?? ALL_VALUE,
        options: agencies.map((agency) => ({ value: agency.id, label: agency.name })),
        onChange: (value) => setFilter('agencyId', value === ALL_VALUE ? undefined : value),
      },
    ],
    [agencies, clients, param, setFilter, showClient, t, tStatus],
  );

  const columns: DataTableColumns<InvoiceListItem> = useMemo(
    () =>
      helper.columns([
        /*
         * D-254 : la liste groupée ne se trie que par jour d'émission (sens
         * montant ou descendant) ; trier une autre colonne casserait les
         * groupes en morceaux. Les autres colonnes ne proposent plus de tri.
         */
        helper.accessor('code', {
          header: t('columns.code'),
          enableSorting: false,
          cell: (info) => (
            <CellStack label={info.getValue()} sub={info.row.original.agencyName} mono />
          ),
        }),

        ...(showClient
          ? [
              helper.accessor('clientName', {
                header: t('columns.client'),
                enableSorting: false,
                cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
              }),
            ]
          : []),

        helper.accessor('parcelCount', {
          header: t('columns.parcels'),
          enableSorting: false,
          meta: { align: 'end', hideBelow: 'sm' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),

        helper.accessor('totalDue', {
          header: t('columns.due'),
          enableSorting: false,
          meta: { align: 'end' },
          cell: (info) => {
            const row = info.row.original;
            const balance = row.totalDue - Math.min(row.totalPaid, Math.abs(row.totalDue));

            return (
              <CellNumber
                value={format.number(info.getValue() / 100, 'currency')}
                // Le reste à régler, sous le total : c'est lui qu'on vient
                // chercher sur cette liste, pas le montant d'origine.
                sub={
                  row.status === 'paid' || row.status === 'carried-over'
                    ? tStatus(row.status)
                    : t('remaining', { amount: format.number(balance / 100, 'currency') })
                }
              />
            );
          },
        }),

        helper.accessor('issuedAt', {
          header: t('columns.issuedAt'),
          meta: { hideBelow: 'lg' },
          cell: (info) => {
            const value = info.getValue();
            return (
              <CellStack
                label={value ? format.dateTime(new Date(value), 'stamp') : '-'}
                sub={
                  info.row.original.dueAt
                    ? t('dueOn', {
                        date: format.dateTime(new Date(info.row.original.dueAt), 'short'),
                      })
                    : undefined
                }
              />
            );
          },
        }),

        helper.accessor('status', {
          header: t('columns.state'),
          enableSorting: false,
          meta: { width: 'w-32' },
          cell: (info) => {
            const status = info.getValue() as InvoiceListItem['status'];
            return <CellBadge label={tStatus(status)} color={STATUS_TONE[status]} />;
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
                <DropdownMenuItem onClick={() => router.push(`/invoices/${row.code}`)}>
                  <Eye className="size-4" />
                  {t('open')}
                </DropdownMenuItem>
                {canWrite ? (
                  <>
                    <DropdownMenuSeparator />
                    {/* Une facture émise est une pièce comptable : elle reste. */}
                    <DropdownMenuItem
                      variant="destructive"
                      disabled={row.status !== 'draft'}
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
    [canWrite, format, router, showClient, t, tCommon, tStatus],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        groups={{
          keyOf: (row) => invoiceDayOf(row, timeZone),
          isOpen,
          onToggle: (day) => setFolded((current) => ({ ...current, [day]: !isOpen(day) })),
          toggleLabel: (day, open) =>
            tDays(open ? 'collapse' : 'expand', {
              date:
                day === INVOICE_DRAFT_DAY
                  ? tDays('drafts')
                  : format.dateTime(new Date(`${day}T12:00:00Z`), 'date'),
            }),
          header: (day) => (
            <InvoiceDayHeader
              day={day}
              summary={daysByKey.get(day)}
              kind={kind}
              search={search}
              canWrite={canWrite}
            />
          ),
        }}
        onRowClick={(row) => router.push(`/invoices/${row.code}`)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource={kind === 'pro' ? 'invoices-pro' : 'invoices'}
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            onReset={() => {
              setSearch('');
              reset();
            }}
          >
            {/*
              Sans client professionnel, le geste n'a personne à facturer : le
              formulaire proposerait une liste vide. Il n'apparaît que là où il
              a un sens (D-061).
            */}
            {canWrite && professionalClients.length > 0 ? (
              <Button
                size="sm"
                variant="outline"
                className="gap-1.5"
                onClick={() => setGenerating(true)}
              >
                <CalendarRange className="size-4" />
                <span className="hidden sm:inline">{t('monthly')}</span>
              </Button>
            ) : null}

            {/*
              LA FACTURATION EN MASSE — chantier « Finance », point 5. À côté
              de la mensuelle et pas à sa place : la mensuelle couvre une
              PÉRIODE pour un client professionnel, celle-ci ramasse tout ce
              dont le cash est rentré, pour autant de clients qu'on coche.
            */}
            {canWrite ? (
              <Button size="sm" variant="outline" className="gap-1.5" onClick={() => setBulk(true)}>
                <Layers className="size-4" />
                <span className="hidden sm:inline">{tBulk('action')}</span>
              </Button>
            ) : null}

            {/*
              L'ORDRE DE VIREMENT sans facture — chantier « Caisse, OV », point
              2b. La même liste que la génération en masse, rien n'est facturé.
            */}
            {canWrite ? (
              <Button
                size="sm"
                variant="outline"
                className="gap-1.5"
                onClick={() => setTransfer(true)}
              >
                <Landmark className="size-4" />
                <span className="hidden sm:inline">{tTransfer('action')}</span>
              </Button>
            ) : null}

            {canWrite ? (
              <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
                <Plus className="size-4" />
                <span className="hidden sm:inline">{t('new')}</span>
              </Button>
            ) : null}
          </DataTableToolbar>
        }
      />

      {bulk ? <BulkInvoiceDialog kind={kind} onDone={() => setBulk(false)} /> : null}
      {transfer ? (
        <BulkInvoiceDialog kind={kind} mode="transfer" onDone={() => setTransfer(false)} />
      ) : null}

      {creating ? (
        <InvoiceForm
          kind={kind}
          clients={clients}
          agencies={agencies}
          onDone={() => setCreating(false)}
          onCreated={(code) => router.push(`/invoices/${code}`)}
        />
      ) : null}

      {generating ? (
        <InvoiceMonthlyForm
          kind={kind}
          clients={professionalClients}
          agencies={agencies}
          onDone={() => setGenerating(false)}
          onCreated={(code) => router.push(`/invoices/${code}`)}
        />
      ) : null}
    </>
  );
}
