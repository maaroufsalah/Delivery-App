'use client';

import type { ParcelImportListItem } from '@delivery/shared';
import { AlertTriangle, Download } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useTransition } from 'react';
import { toast } from 'sonner';
import { CellStack, RowActions } from '@/components/data-table/cells';
import { DataTable } from '@/components/data-table/data-table';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { DropdownMenuItem } from '@/components/ui/dropdown-menu';
import { parcelImportReportAction } from '@/lib/actions/parcels';
import { downloadBase64 } from '@/lib/download';

const helper = createDataTableColumnHelper<ParcelImportListItem>();

const XLSX_MIME = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

/**
 * Historique des imports.
 *
 * Un import est un FAIT daté : qui a chargé quel fichier, quand, avec quel
 * résultat. Le legacy n'en gardait aucune trace (DOMAIN.md §4 dette n°19), si
 * bien qu'un lot douteux n'était rattachable à rien. Le rapport d'erreurs se
 * retélécharge d'ici, même des semaines plus tard.
 */
export function ParcelImportHistory({ items }: { items: ParcelImportListItem[] }) {
  const t = useTranslations('parcels.import.history');
  const tMode = useTranslations('parcels.import.mode');
  const format = useFormatter();
  const [pending, startTransition] = useTransition();

  const download = (id: string) => {
    startTransition(async () => {
      const response = await parcelImportReportAction(id);
      if (!response.ok) {
        toast.error(response.error);
        return;
      }
      downloadBase64(response.data, `rapport-import-${id}.xlsx`, XLSX_MIME);
    });
  };

  const columns: DataTableColumns<ParcelImportListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('filename', {
          header: t('filename'),
          cell: (info) => <CellStack label={info.getValue()} sub={info.row.original.clientName} />,
        }),

        helper.accessor('createdAt', {
          header: t('date'),
          meta: { hideBelow: 'sm' },
          cell: (info) => (
            <CellStack
              label={format.dateTime(new Date(info.getValue()), 'stamp')}
              sub={info.row.original.authorName}
            />
          ),
        }),

        helper.accessor('okCount', {
          header: t('rows'),
          meta: { align: 'end' },
          cell: (info) => {
            const row = info.row.original;
            return (
              <span className="inline-flex items-center gap-2 text-sm tabular-nums">
                <span>
                  {row.okCount} / {row.rowCount}
                </span>
                {row.koCount > 0 ? (
                  <span className="text-destructive inline-flex items-center gap-1">
                    <AlertTriangle className="size-3.5" aria-hidden />
                    {row.koCount}
                  </span>
                ) : null}
              </span>
            );
          },
        }),

        helper.accessor('mode', {
          header: t('mode'),
          meta: { hideBelow: 'lg' },
          cell: (info) => (
            <span className="text-muted-foreground text-sm">
              {info.getValue() === 'valid-only' ? tMode('validOnly') : tMode('allOrNothing')}
            </span>
          ),
        }),

        helper.display({
          id: 'actions',
          header: '',
          meta: { isActions: true, align: 'end', width: 'w-12' },
          cell: (info) => (
            <RowActions>
              <DropdownMenuItem disabled={pending} onSelect={() => download(info.row.original.id)}>
                <Download className="size-4" aria-hidden />
                {t('report')}
              </DropdownMenuItem>
            </RowActions>
          ),
        }),
      ]),
    // `pending` change l'état désactivé de l'action : il entre donc dans les
    // dépendances, alors même qu'il ne sert pas au rendu des autres colonnes.
    [t, tMode, format, pending],
  );

  return (
    <DataTable
      data={items}
      columns={columns}
      pageSize={10}
      initialSorting={[{ id: 'createdAt', desc: true }]}
      emptyTitle={t('empty')}
      emptyHint={t('emptyHint')}
    />
  );
}
