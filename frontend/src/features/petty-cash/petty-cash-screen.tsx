'use client';

import type {
  PettyCashDaysPage,
  PettyCashMeta,
  PettyCashMovement,
  PettyCashPage,
  PettyCashStatus,
} from '@delivery/shared';
import {
  ArrowDownCircle,
  ArrowUpCircle,
  CheckCircle2,
  FileText,
  Hourglass,
  Loader2,
  Lock,
  Paperclip,
  Pencil,
  Tags,
  Trash2,
  Wallet,
  XCircle,
} from 'lucide-react';
import { useFormatter, useTimeZone, useTranslations } from 'next-intl';
import { useSearchParams } from 'next/navigation';
import { useMemo, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge, CellNumber, CellStack, RowActions } from '@/components/data-table/cells';
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
import { Button } from '@/components/ui/button';
import { DropdownMenuItem, DropdownMenuSeparator } from '@/components/ui/dropdown-menu';
import { PeriodFilter } from '@/features/dashboard/period-filter';
import { PettyCashCategoriesDialog } from '@/features/petty-cash/petty-cash-categories-dialog';
import { type AddMovement, DayBanner, DayGroupHeader } from '@/features/petty-cash/petty-cash-days';
import { PettyCashForm } from '@/features/petty-cash/petty-cash-form';
import { PettyCashRefuseDialog } from '@/features/petty-cash/petty-cash-refuse-dialog';
import {
  deletePettyCashMovementAction,
  downloadPettyCashPdfAction,
  downloadPettyCashProofAction,
  validatePettyCashMovementAction,
} from '@/lib/actions/petty-cash';
import { downloadBase64 } from '@/lib/download';
import { localToday } from '@/lib/local-day';

const helper = createDataTableColumnHelper<PettyCashMovement>();

/** D-168 — brouillon orange, validée verte, refusée rouge : les jetons du thème. */
const STATUS_COLORS: Record<PettyCashStatus, string> = {
  draft: 'var(--warning)',
  validated: 'var(--success)',
  refused: 'var(--destructive)',
};

/**
 * Ce que le formulaire ouvre : un nouveau mouvement d'un type, daté de la
 * journée visée (D-249), ou un existant.
 */
type Editing =
  { kind: 'supply' | 'expense'; date: string } | { movement: PettyCashMovement } | null;

/**
 * LA PETITE CAISSE — chantier « Caisse, OV, Petite caisse », point 3.
 *
 * Le bandeau (solde à ce jour, approvisionné et dépensé sur la période), puis
 * les mouvements. Une dépense s'affiche en négatif, un approvisionnement en
 * positif : la colonne se lit comme un relevé.
 *
 * Au bureau, pas de workflow : on saisit, on corrige, on supprime — et chaque
 * geste est journalisé côté API, avant/après compris. La dépense d'un LIVREUR
 * arrive en brouillon (D-168) : Valider / Refuser sur sa ligne, et seules les
 * validées entrent dans le bandeau, l'Excel et le PDF.
 *
 * D-252, ordre de la page (Badr, 14/11 : un seul tableau) : le bandeau du
 * jour, les quatre cartes, la barre de filtres, puis LE tableau des
 * mouvements, rangés par journée sous un en-tête repliable (chiffres et gestes
 * de la journée). La journée du jour et celles qui ont des brouillons à
 * valider s'ouvrent dépliées.
 */
export function PettyCashScreen({
  page,
  meta,
  canWrite,
  days,
  agencyId,
}: {
  page: PettyCashPage;
  meta: PettyCashMeta;
  canWrite: boolean;
  days: PettyCashDaysPage;
  agencyId: string | undefined;
}) {
  const t = useTranslations('finance.pettyCash');
  const tCommon = useTranslations('common');
  const tDays = useTranslations('finance.pettyCash.days');
  const format = useFormatter();

  /** D-249 — le refus d'une journée clôturée, dans la langue de l'agent. */
  const failure = (result: { error: string; reason?: string }) =>
    toast.error(result.reason === 'petty-cash-day-closed' ? tDays('closedError') : result.error);
  // Le jour du nom de fichier : celui de la SOCIÉTÉ (D-135).
  const timeZone = useTimeZone();
  const searchParams = useSearchParams();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'date',
    defaultOrder: 'desc',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [editing, setEditing] = useState<Editing>(null);
  const [categories, setCategories] = useState(false);
  const [refusing, setRefusing] = useState<PettyCashMovement | null>(null);
  const [pdfPending, setPdfPending] = useState(false);
  const [, startTransition] = useTransition();

  const money = (cents: number) => format.number(cents / 100, 'currency');

  /*
   * La période RÉSOLUE par le serveur, passée aux deux exports : sans elle, une
   * page ouverte sur la période mémorisée (cookie, pas d'URL) exporterait « ce
   * mois » au lieu de ce qu'elle affiche.
   */
  const exportQuery = useMemo(
    () => ({ period: page.period, from: page.from, to: page.to }),
    [page.from, page.period, page.to],
  );

  const downloadPdf = async () => {
    const query: Record<string, string> = { ...exportQuery };
    for (const [key, value] of searchParams.entries()) {
      if (key !== 'page') query[key] = value;
    }
    if (search) query.search = search;

    setPdfPending(true);
    const result = await downloadPettyCashPdfAction(query);
    setPdfPending(false);

    if (!result.ok) {
      toast.error(result.error);
      return;
    }

    downloadBase64(result.data, `Petite-Caisse-${localToday(timeZone)}.pdf`, 'application/pdf');
  };

  const downloadProof = async (row: PettyCashMovement) => {
    const result = await downloadPettyCashProofAction(row.id);
    if (!result.ok) {
      toast.error(result.error);
      return;
    }
    downloadBase64(result.data, row.proofName ?? 'justificatif', 'application/octet-stream');
  };

  const remove = (row: PettyCashMovement) => {
    startTransition(async () => {
      const result = await deletePettyCashMovementAction(row.id);
      if (result.ok) toast.success(t('deleted'));
      else failure(result);
    });
  };

  const validate = (row: PettyCashMovement) => {
    startTransition(async () => {
      const result = await validatePettyCashMovementAction(row.id);
      if (result.ok) toast.success(t('validatedToast'));
      else failure(result);
    });
  };

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        id: 'agencyId',
        label: t('filters.agency'),
        value: param('agencyId') ?? ALL_VALUE,
        // Une seule agence dans la portée : le filtre n'apprendrait rien.
        options:
          meta.agencies.length > 1
            ? meta.agencies.map((agency) => ({ value: agency.id, label: agency.name }))
            : [],
        onChange: (value) => setFilter('agencyId', value === ALL_VALUE ? undefined : value),
      },
      {
        id: 'kind',
        label: t('filters.kind'),
        value: param('kind') ?? ALL_VALUE,
        options: [
          { value: 'supply', label: t('kind.supply') },
          { value: 'expense', label: t('kind.expense') },
        ],
        onChange: (value) => setFilter('kind', value === ALL_VALUE ? undefined : value),
      },
      /*
       * D-255 : catégorie, acheteur et statut à choix multiple. L'agence reste
       * unique (le solde, les journées et la clôture portent sur UNE caisse),
       * le type aussi (deux valeurs : les cocher toutes deux ne filtre rien).
       */
      {
        id: 'category',
        multiple: true,
        label: t('filters.category'),
        value: param('category') ?? ALL_VALUE,
        options: meta.categories.map((category) => ({ value: category, label: category })),
        onChange: (value) => setFilter('category', value === ALL_VALUE ? undefined : value),
      },
      {
        id: 'buyerId',
        multiple: true,
        label: t('filters.buyer'),
        value: param('buyerId') ?? ALL_VALUE,
        options: meta.buyers.map((buyer) => ({ value: buyer.id, label: buyer.name })),
        onChange: (value) => setFilter('buyerId', value === ALL_VALUE ? undefined : value),
      },
      {
        id: 'status',
        multiple: true,
        label: t('filters.status'),
        value: param('status') ?? ALL_VALUE,
        options: (['draft', 'validated', 'refused'] as const).map((status) => ({
          value: status,
          label: t(`status.${status}`),
        })),
        onChange: (value) => setFilter('status', value === ALL_VALUE ? undefined : value),
      },
    ],
    [meta, param, setFilter, t],
  );

  const columns: DataTableColumns<PettyCashMovement> = useMemo(
    () =>
      helper.columns([
        helper.accessor('date', {
          header: t('columns.date'),
          meta: { width: 'w-28' },
          cell: (info) => (
            <CellStack
              label={format.dateTime(new Date(`${info.getValue()}T12:00:00Z`), 'short')}
              sub={info.row.original.agencyName}
            />
          ),
        }),

        helper.accessor('label', {
          header: t('columns.label'),
          cell: (info) => {
            const row = info.row.original;
            return (
              <CellStack
                label={row.kind === 'supply' ? t('kind.supply') : (row.label ?? '')}
                sub={
                  row.status === 'refused' && row.refusalReason
                    ? t('refusalReason', { reason: row.refusalReason })
                    : (row.note ?? undefined)
                }
              />
            );
          },
        }),

        helper.accessor('category', {
          header: t('columns.category'),
          meta: { hideBelow: 'sm' },
          cell: (info) => {
            const row = info.row.original;
            return row.kind === 'supply' ? (
              <CellBadge label={t('kind.supply')} color="var(--chart-2)" />
            ) : (
              <CellBadge label={row.category ?? ''} color="var(--chart-3)" />
            );
          },
        }),

        helper.accessor('status', {
          header: t('columns.status'),
          meta: { hideBelow: 'md' },
          cell: (info) => {
            const status = info.row.original.status;
            return <CellBadge label={t(`status.${status}`)} color={STATUS_COLORS[status]} />;
          },
        }),

        helper.accessor('buyerName', {
          header: t('columns.buyer'),
          meta: { hideBelow: 'lg' },
          cell: (info) => (
            <CellStack
              label={info.getValue() ?? '-'}
              sub={info.row.original.proofName ? t('proof') : undefined}
            />
          ),
        }),

        helper.accessor('amount', {
          header: t('columns.amount'),
          meta: { align: 'end' },
          cell: (info) => {
            const row = info.row.original;
            const signed = row.kind === 'expense' ? -row.amount : row.amount;
            return <CellNumber value={money(signed)} />;
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
                {row.proofName ? (
                  <DropdownMenuItem onClick={() => downloadProof(row)}>
                    <Paperclip className="size-4" />
                    {t('proof')}
                  </DropdownMenuItem>
                ) : null}
                {/* D-249 — journée clôturée : rien ne se change avant « Corriger ». */}
                {row.dayStatus === 'closed' ? (
                  <DropdownMenuItem disabled>
                    <Lock className="size-4" />
                    {tDays('frozen')}
                  </DropdownMenuItem>
                ) : null}
                {canWrite && row.dayStatus !== 'closed' && row.status === 'draft' ? (
                  <>
                    <DropdownMenuItem onClick={() => validate(row)}>
                      <CheckCircle2 className="size-4" />
                      {t('validate')}
                    </DropdownMenuItem>
                    <DropdownMenuItem onClick={() => setRefusing(row)}>
                      <XCircle className="size-4" />
                      {t('refuse')}
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                  </>
                ) : null}
                {canWrite && row.dayStatus !== 'closed' ? (
                  <>
                    <DropdownMenuItem onClick={() => setEditing({ movement: row })}>
                      <Pencil className="size-4" />
                      {t('edit')}
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem variant="destructive" onClick={() => remove(row)}>
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
    [canWrite, format, t, tCommon, tDays],
  );

  const addMovement: AddMovement = (kind, date) => setEditing({ kind, date });

  /*
   * D-252 — les journées, par date (somme des agences sans filtre, l'agence
   * filtrée sinon : la même portée que la liste). Repli : l'écran décide, la
   * table applique. Par défaut, la journée du jour et celles qui ont des
   * brouillons à valider sont dépliées ; un clic l'emporte sur ce défaut.
   */
  const daysByDate = useMemo(
    () => new Map(days.items.map((summary) => [summary.day, summary])),
    [days.items],
  );
  const [folded, setFolded] = useState<Record<string, boolean>>({});
  const openByDefault = (day: string) =>
    day === days.today || (daysByDate.get(day)?.pendingDrafts ?? 0) > 0;
  const isOpen = (day: string) => folded[day] ?? openByDefault(day);

  return (
    <div className="flex flex-col gap-6">
      {/* 1. Le bandeau du jour, même sans journée encore : solde actuel et ajouts. */}
      <DayBanner
        page={days}
        balance={page.totals.balance}
        agencyId={agencyId}
        canWrite={canWrite}
        onAdd={addMovement}
      />

      {/* 2. Les quatre cartes de chiffres. */}
      <KpiGrid>
        <KpiCard
          label={t('kpi.balance')}
          value={money(page.totals.balance)}
          hint={t('kpi.balanceHint')}
          icon={Wallet}
          tone={page.totals.balance < 0 ? 'var(--destructive)' : 'var(--chart-1)'}
        />
        <KpiCard
          label={t('kpi.supplied')}
          value={money(page.totals.supplied)}
          hint={t('kpi.periodHint')}
          icon={ArrowDownCircle}
          tone="var(--chart-2)"
        />
        <KpiCard
          label={t('kpi.spent')}
          value={money(page.totals.spent)}
          hint={t('kpi.periodHint')}
          icon={ArrowUpCircle}
          tone="var(--chart-3)"
        />
        <KpiCard
          label={t('kpi.drafts')}
          value={String(page.totals.drafts)}
          hint={t('kpi.draftsHint')}
          icon={Hourglass}
          tone="var(--warning)"
        />
      </KpiGrid>

      {/* 3. LE tableau : les mouvements, rangés par journée, filtres au-dessus. */}
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        groups={{
          keyOf: (row) => row.date,
          isOpen,
          onToggle: (day) => setFolded((current) => ({ ...current, [day]: !isOpen(day) })),
          toggleLabel: (day, open) =>
            tDays(open ? 'collapse' : 'expand', {
              date: format.dateTime(new Date(`${day}T12:00:00Z`), 'date'),
            }),
          header: (day) => (
            <DayGroupHeader
              day={day}
              summary={daysByDate.get(day)}
              today={days.today}
              agencyId={agencyId}
              canWrite={canWrite}
              onAdd={addMovement}
            />
          ),
        }}
        onRowClick={
          canWrite
            ? (row) =>
                row.dayStatus === 'closed'
                  ? toast.info(tDays('closedError'))
                  : setEditing({ movement: row })
            : undefined
        }
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="petty-cash"
            exportQuery={exportQuery}
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            onReset={() => {
              setSearch('');
              reset();
            }}
          >
            <PeriodFilter period={page.period} from={page.from} to={page.to} />

            <Button
              size="sm"
              variant="outline"
              className="gap-1.5"
              onClick={downloadPdf}
              disabled={pdfPending}
            >
              {pdfPending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <FileText className="size-4" />
              )}
              <span className="hidden sm:inline">{t('pdf')}</span>
            </Button>

            {/* Les ajouts sont dans le bandeau du jour et dans la journée choisie. */}
            {canWrite ? (
              <Button
                size="sm"
                variant="outline"
                className="gap-1.5"
                onClick={() => setCategories(true)}
              >
                <Tags className="size-4" />
                <span className="hidden sm:inline">{t('categories')}</span>
              </Button>
            ) : null}
          </DataTableToolbar>
        }
      />

      {editing ? (
        <PettyCashForm
          meta={meta}
          kind={'movement' in editing ? editing.movement.kind : editing.kind}
          movement={'movement' in editing ? editing.movement : null}
          defaultDate={'movement' in editing ? undefined : editing.date}
          defaultAgencyId={agencyId}
          onDone={() => setEditing(null)}
        />
      ) : null}

      {refusing ? (
        <PettyCashRefuseDialog movement={refusing} onDone={() => setRefusing(null)} />
      ) : null}

      {categories ? (
        <PettyCashCategoriesDialog
          categories={meta.categories}
          onDone={() => setCategories(false)}
        />
      ) : null}
    </div>
  );
}
