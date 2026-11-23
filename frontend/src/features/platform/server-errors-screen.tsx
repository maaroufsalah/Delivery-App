'use client';

import type { Paginated, ServerErrorGroup, ServerErrorOccurrence } from '@delivery/shared';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge, CellMuted, CellNumber, CellStack } from '@/components/data-table/cells';
import { DataTable } from '@/components/data-table/data-table';
import { DataTableToolbar } from '@/components/data-table/data-table-toolbar';
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
import { serverErrorOccurrencesAction } from '@/lib/actions/technical';

const helper = createDataTableColumnHelper<ServerErrorGroup>();

/**
 * LES ERREURS SERVEUR — recette SpeedyTrans 4, point 5 c (D-134).
 *
 * Les 500 de l'API et les échecs des tâches de fond, sur 30 jours, groupés par
 * route et message : combien, depuis quand, la dernière fois. La recherche
 * prend la « Référence » qu'un client lit sur sa page d'erreur ; une ligne
 * ouvre ses occurrences, pile comprise.
 */
export function ServerErrorsScreen({ page }: { page: Paginated<ServerErrorGroup> }) {
  const t = useTranslations('platform.serverErrors');
  const format = useFormatter();
  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'lastAt',
    defaultOrder: 'desc',
  });
  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [open, setOpen] = useState<{
    group: ServerErrorGroup;
    rows: ServerErrorOccurrence[];
  } | null>(null);
  const [, startTransition] = useTransition();

  const show = (group: ServerErrorGroup) => {
    if (!group.lastRequestId) return;
    startTransition(async () => {
      const result = await serverErrorOccurrencesAction(group.lastRequestId!);
      if (result.ok) setOpen({ group, rows: result.data });
      else toast.error(result.error);
    });
  };

  const columns: DataTableColumns<ServerErrorGroup> = useMemo(
    () =>
      helper.columns([
        helper.accessor('route', {
          header: t('columns.route'),
          cell: (info) => (
            <CellStack label={info.getValue()} sub={info.row.original.message} mono />
          ),
        }),
        helper.accessor('status', {
          header: t('columns.status'),
          meta: { width: 'w-24' },
          cell: (info) => (
            <CellBadge
              label={info.row.original.source === 'task' ? t('task') : String(info.getValue())}
              color="var(--destructive)"
            />
          ),
        }),
        helper.accessor('count', {
          header: t('columns.count'),
          meta: { width: 'w-24' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),
        helper.accessor('lastAt', {
          header: t('columns.last'),
          cell: (info) => (
            <CellStack
              label={format.dateTime(new Date(info.getValue()), 'stamp')}
              sub={t('first', {
                at: format.dateTime(new Date(info.row.original.firstAt), 'stamp'),
              })}
            />
          ),
        }),
        helper.accessor('lastRequestId', {
          header: t('columns.requestId'),
          meta: { hideBelow: 'lg' },
          cell: (info) => <CellMuted>{info.getValue() ?? '—'}</CellMuted>,
        }),
      ]),
    [format, t],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={show}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            searchPlaceholder={t('searchPlaceholder')}
            onReset={() => {
              setSearch('');
              reset();
            }}
          />
        }
      />

      <Dialog open={open !== null} onOpenChange={(next) => !next && setOpen(null)}>
        <DialogContent className="sm:max-w-4xl">
          <DialogHeader>
            <DialogTitle className="font-mono text-sm">{open?.group.route}</DialogTitle>
            <DialogDescription>{open?.group.message}</DialogDescription>
          </DialogHeader>
          <div className="flex max-h-[70dvh] flex-col gap-4 overflow-y-auto text-xs">
            {open?.rows.map((row) => (
              <div key={row.id} className="flex flex-col gap-2 rounded-md border p-3">
                <p className="font-mono">
                  {format.dateTime(new Date(row.at), 'stamp')} · {row.method ?? ''} {row.status} ·{' '}
                  {row.durationMs ?? '—'} ms · {row.requestId ?? '—'}
                </p>
                <p className="text-muted-foreground font-mono">
                  {t('who', {
                    user: row.userId ?? '—',
                    role: row.role ?? '—',
                    tenant: row.tenantId ?? '—',
                    ip: row.ip ?? '—',
                  })}
                </p>
                {row.stack ? (
                  <pre className="bg-muted overflow-x-auto rounded p-2 text-[11px] leading-snug whitespace-pre">
                    {row.stack}
                  </pre>
                ) : null}
              </div>
            ))}
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}
