'use client';

import {
  JOURNAL_ACTIONS,
  JOURNAL_CATEGORIES,
  type JournalEntry,
  type JournalEntryDetail,
  type Paginated,
} from '@delivery/shared';
import { useFormatter } from 'next-intl';
import { useMemo, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge, CellStack } from '@/components/data-table/cells';
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { journalEntryAction } from '@/lib/actions/audit';
import { JournalDetail } from './journal-detail';
import { useJournalText } from './use-journal-text';

const helper = createDataTableColumnHelper<JournalEntry>();

/** Une couleur par nature de geste. */
const TONE: Record<string, string> = {
  create: 'var(--chart-4)',
  delete: 'var(--destructive)',
  cancel: 'var(--destructive)',
  'login-failed': 'var(--destructive)',
  login: 'var(--chart-2)',
  validate: 'var(--chart-2)',
  pay: 'var(--chart-2)',
  close: 'var(--chart-2)',
  issue: 'var(--chart-2)',
  // D-257 : un geste d'administrateur qui déplace un colis d'un client à l'autre.
  'client-change': 'var(--chart-3)',
};

export const JOURNAL_PERIODS = ['today', 'week', 'month', 'all'] as const;

/**
 * LE JOURNAL LISIBLE — recette SpeedyTrans 4, point 4 (D-133).
 *
 * Chaque ligne répond à « qui a fait quoi, sur quoi, quand » : l'action déduite
 * du changement, l'objet par son type et sa référence, l'auteur par son nom et
 * son rôle. Les connexions ont leur propre vue. L'écran reste en LECTURE SEULE
 * (D-064) : un journal qu'on peut effacer ne prouve rien.
 */
export function JournalScreen({
  page,
  users,
  exportQuery,
  technical = false,
}: {
  page: Paginated<JournalEntry>;
  users: Array<{ id: string; label: string }>;
  /** La période résolue en dates : l'URL ne porte que son nom. */
  exportQuery: Record<string, string>;
  /** « Vue technique » : Nocido en « se connecter en tant que » (point 5 a). */
  technical?: boolean;
}) {
  const { t, tx, object, action, change } = useJournalText();
  const format = useFormatter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'at',
    defaultOrder: 'desc',
  });
  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [entry, setEntry] = useState<JournalEntryDetail | null>(null);
  const [, startTransition] = useTransition();

  const view = param('view') === 'logins' ? 'logins' : 'changes';

  const open = (row: JournalEntry) => {
    startTransition(async () => {
      const result = await journalEntryAction(row.id);
      if (result.ok) setEntry(result.data);
      else toast.error(result.error);
    });
  };

  const selects: FilterSelect[] = useMemo(() => {
    const choose = (key: string) => (next: string) =>
      setFilter(key, next === ALL_VALUE ? undefined : next);

    return [
      {
        id: 'view',
        label: t('filters.view'),
        value: view,
        options: (['changes', 'logins'] as const).map((value) => ({
          value,
          label: t(`view.${value}`),
        })),
        onChange: (next) => setFilter('view', next === 'logins' ? 'logins' : undefined),
      },
      ...(view === 'changes'
        ? [
            /*
             * D-255 : type d'objet, action et auteur à choix multiple. La vue
             * et la période restent uniques : ce sont des MODES (une vue, une
             * fenêtre de temps), pas des valeurs qui s'additionnent.
             */
            {
              id: 'category',
              multiple: true,
              label: t('filters.category'),
              value: param('category') ?? ALL_VALUE,
              options: JOURNAL_CATEGORIES.map((value) => ({
                value,
                label: t(`category.${value}`),
              })),
              onChange: choose('category'),
            },
            {
              id: 'action',
              multiple: true,
              label: t('filters.action'),
              value: param('action') ?? ALL_VALUE,
              options: JOURNAL_ACTIONS.filter((value) => !value.startsWith('login')).map(
                (value) => ({ value, label: t(`action.${value}`) }),
              ),
              onChange: choose('action'),
            },
          ]
        : []),
      {
        id: 'userId',
        multiple: true,
        label: t('filters.user'),
        value: param('userId') ?? ALL_VALUE,
        options: users.map((user) => ({ value: user.id, label: user.label })),
        onChange: choose('userId'),
      },
      {
        id: 'period',
        label: t('filters.period'),
        value: param('period') ?? 'all',
        options: JOURNAL_PERIODS.map((value) => ({ value, label: t(`period.${value}`) })),
        onChange: (next) => setFilter('period', next === 'all' ? undefined : next),
      },
    ];
  }, [param, setFilter, t, users, view]);

  const columns: DataTableColumns<JournalEntry> = useMemo(
    () =>
      helper.columns([
        helper.accessor('at', {
          header: t('columns.at'),
          meta: { width: 'w-40' },
          cell: (info) => (
            <CellStack
              label={format.dateTime(new Date(info.getValue()), 'stamp')}
              sub={tx(`source.${info.row.original.source}`)}
            />
          ),
        }),

        helper.accessor('action', {
          header: t('columns.action'),
          cell: (info) => (
            <div className="flex min-w-0 flex-col items-start gap-1">
              <CellBadge
                label={tx(`action.${info.getValue()}`)}
                color={TONE[info.getValue()] ?? 'var(--chart-1)'}
              />
              {info.row.original.headline ? (
                <span className="text-muted-foreground truncate text-xs">
                  {change(info.row.original)}
                </span>
              ) : null}
            </div>
          ),
        }),

        helper.accessor('object', {
          header: t('columns.object'),
          cell: (info) => (
            <CellStack
              label={
                info.row.original.object.reference || info.row.original.action !== 'login-failed'
                  ? object(info.getValue(), info.row.original.count)
                  : t('unknownAccount')
              }
            />
          ),
        }),

        helper.accessor('user', {
          header: t('columns.user'),
          meta: { hideBelow: 'md' },
          cell: (info) => {
            const user = info.getValue();
            return user ? (
              <CellStack label={user.name} sub={tx(`role.${user.role}`)} />
            ) : (
              <CellStack label={t('system')} />
            );
          },
        }),

        helper.accessor('agency', {
          header: t('columns.agency'),
          meta: { hideBelow: 'lg' },
          cell: (info) => <CellStack label={info.getValue() ?? '—'} />,
        }),
      ]),
    [change, format, object, t, tx],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={open}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="audit/journal"
            exportQuery={exportQuery}
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            onReset={() => {
              setSearch('');
              reset();
            }}
          />
        }
      />

      <Dialog open={entry !== null} onOpenChange={(next) => !next && setEntry(null)}>
        <DialogContent className="sm:max-w-3xl">
          <DialogHeader>
            <DialogTitle>{entry ? action(entry) : ''}</DialogTitle>
            <DialogDescription>
              {entry
                ? [
                    entry.object.reference || entry.action !== 'login-failed'
                      ? object(entry.object, entry.count)
                      : t('unknownAccount'),
                    entry.user
                      ? `${entry.user.name} (${tx(`role.${entry.user.role}`)})`
                      : t('system'),
                    format.dateTime(new Date(entry.at), 'stamp'),
                  ].join(' · ')
                : ''}
            </DialogDescription>
          </DialogHeader>

          {entry ? <JournalDetail entry={entry} technical={technical} /> : null}
        </DialogContent>
      </Dialog>
    </>
  );
}
