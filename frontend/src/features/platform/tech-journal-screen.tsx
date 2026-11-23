'use client';

import type { AuditLogDetail, AuditLogListItem, Paginated } from '@delivery/shared';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellMuted, CellStack } from '@/components/data-table/cells';
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
import { techAuditEntryAction } from '@/lib/actions/technical';

const helper = createDataTableColumnHelper<AuditLogListItem>();

/**
 * LE JOURNAL TECHNIQUE — recette SpeedyTrans 4, point 5 a (D-134).
 *
 * Le journal BRUT d'une société, tel que l'extension l'écrit : modèle,
 * identifiant, champs changés, auteur, adresse, identifiant de requête, et
 * l'avant/après complet en JSON. C'est la vue de diagnostic de Nocido — le
 * client lit, lui, le journal métier (D-133).
 */
export function TechJournalScreen({
  page,
  tenants,
  companyId,
}: {
  page: Paginated<AuditLogListItem> | null;
  tenants: Array<{ id: string; label: string }>;
  companyId: string | null;
}) {
  const t = useTranslations('platform.techJournal');
  const format = useFormatter();
  const { server, param, setFilter, reset } = useServerTable({
    total: page?.total ?? 0,
    defaultSort: 'at',
    defaultOrder: 'desc',
  });
  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [entry, setEntry] = useState<AuditLogDetail | null>(null);
  const [, startTransition] = useTransition();

  const open = (row: AuditLogListItem) => {
    if (!companyId) return;
    startTransition(async () => {
      const result = await techAuditEntryAction(companyId, row.id);
      if (result.ok) setEntry(result.data);
      else toast.error(result.error);
    });
  };

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        id: 'companyId',
        label: t('tenant'),
        value: companyId ?? ALL_VALUE,
        options: tenants.map((tenant) => ({ value: tenant.id, label: tenant.label })),
        onChange: (value) => setFilter('companyId', value === ALL_VALUE ? undefined : value),
      },
    ],
    [companyId, setFilter, t, tenants],
  );

  const columns: DataTableColumns<AuditLogListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('at', {
          header: t('columns.at'),
          meta: { width: 'w-40' },
          cell: (info) => (
            <CellStack
              label={format.dateTime(new Date(info.getValue()), 'stamp')}
              sub={info.row.original.ip ?? undefined}
            />
          ),
        }),
        helper.accessor('entity', {
          header: t('columns.entity'),
          cell: (info) => (
            <CellStack
              label={`${info.getValue()} · ${info.row.original.action}`}
              sub={info.row.original.entityId}
              mono
            />
          ),
        }),
        helper.accessor('diff', {
          header: t('columns.diff'),
          meta: { hideBelow: 'md' },
          cell: (info) => <CellMuted>{info.getValue().join(', ') || '—'}</CellMuted>,
        }),
        helper.accessor('userEmail', {
          header: t('columns.user'),
          meta: { hideBelow: 'lg' },
          cell: (info) => (
            <CellStack label={info.getValue() ?? '—'} sub={info.row.original.role ?? undefined} />
          ),
        }),
      ]),
    [format, t],
  );

  return (
    <>
      <DataTable
        data={page?.items ?? []}
        columns={columns}
        server={server}
        onRowClick={open}
        emptyTitle={companyId ? t('empty') : t('pickTenant')}
        emptyHint={companyId ? t('emptyHint') : t('pickTenantHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
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
        <DialogContent className="sm:max-w-5xl">
          <DialogHeader>
            <DialogTitle className="font-mono text-sm">
              {entry ? `${entry.entity} ${entry.entityId} · ${entry.action}` : ''}
            </DialogTitle>
            <DialogDescription className="font-mono text-xs">
              {entry
                ? `${entry.userEmail ?? '—'} · ${entry.ip ?? '—'} · ${entry.requestId ?? '—'} · ${entry.userAgent ?? '—'}`
                : ''}
            </DialogDescription>
          </DialogHeader>
          {entry ? (
            <div className="grid max-h-[70dvh] gap-3 overflow-y-auto sm:grid-cols-2">
              {(['before', 'after'] as const).map((side) => (
                <div key={side} className="flex min-w-0 flex-col gap-1">
                  <p className="text-muted-foreground text-xs font-medium">{t(side)}</p>
                  <pre className="bg-muted overflow-x-auto rounded p-2 text-[11px] leading-snug">
                    {JSON.stringify(entry[side], null, 2) ?? 'null'}
                  </pre>
                </div>
              ))}
            </div>
          ) : null}
        </DialogContent>
      </Dialog>
    </>
  );
}
