'use client';

import type { Paginated, TransferOrderListItem } from '@delivery/shared';
import { Download, Loader2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useState } from 'react';
import { toast } from 'sonner';
import { CellBadge, CellMuted, CellNumber, CellStack } from '@/components/data-table/cells';
import { DataTable } from '@/components/data-table/data-table';
import { DataTableToolbar } from '@/components/data-table/data-table-toolbar';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { useServerTable, useUrlSearch } from '@/components/data-table/use-server-table';
import { Button } from '@/components/ui/button';
import { downloadTransferOrderAction } from '@/lib/actions/finance';
import { downloadBase64 } from '@/lib/download';

const helper = createDataTableColumnHelper<TransferOrderListItem>();

const MIME = {
  pdf: 'application/pdf',
  xlsx: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
} as const;

/**
 * LES ORDRES DE VIREMENT déjà générés — consultation seule.
 *
 * Aucun geste ne crée ni ne modifie un ordre : les deux boutons relisent l'OV
 * gelé par les routes existantes (`/transfer-orders/:code/pdf` et `/xlsx`). La
 * génération reste sur les factures, et seulement là.
 */
export function TransferOrdersScreen({ page }: { page: Paginated<TransferOrderListItem> }) {
  const t = useTranslations('finance.transferOrders');
  const format = useFormatter();
  const [busy, setBusy] = useState<string | null>(null);

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'createdAt',
    defaultOrder: 'desc',
  });
  const [search, setSearch] = useUrlSearch(setFilter, param('search'));

  const download = async (code: string, kind: 'pdf' | 'xlsx') => {
    setBusy(`${code}:${kind}`);
    const result = await downloadTransferOrderAction(code, kind);
    setBusy(null);
    if (result.ok) downloadBase64(result.data, `Ordre-Virement-${code}.${kind}`, MIME[kind]);
    else toast.error(result.error);
  };

  const columns: DataTableColumns<TransferOrderListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('code', {
          header: t('columns.code'),
          enableSorting: false,
          cell: (info) => <CellStack label={info.getValue()} mono />,
        }),
        helper.accessor('createdAt', {
          header: t('columns.createdAt'),
          enableSorting: false,
          cell: (info) => (
            <CellMuted>{format.dateTime(new Date(info.getValue()), 'stamp')}</CellMuted>
          ),
        }),
        helper.accessor('kind', {
          header: t('columns.kind'),
          enableSorting: false,
          meta: { hideBelow: 'sm' },
          cell: (info) => (
            <CellBadge label={t(info.row.original.kind === 'pro' ? 'kind.pro' : 'kind.clients')} />
          ),
        }),
        helper.accessor('clientCount', {
          header: t('columns.clientCount'),
          enableSorting: false,
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => <CellNumber value={format.number(info.getValue())} />,
        }),
        helper.accessor('totalDue', {
          header: t('columns.totalDue'),
          enableSorting: false,
          meta: { align: 'end' },
          cell: (info) => <CellNumber value={format.number(info.getValue() / 100, 'currency')} />,
        }),
        helper.accessor('createdByName', {
          header: t('columns.createdBy'),
          enableSorting: false,
          meta: { hideBelow: 'lg' },
          cell: (info) => <CellMuted>{info.getValue() ?? '—'}</CellMuted>,
        }),
        helper.display({
          id: 'actions',
          header: '',
          meta: { isActions: true, align: 'end' },
          cell: (info) => {
            const code = info.row.original.code;
            return (
              <div className="flex justify-end gap-1.5">
                {(['pdf', 'xlsx'] as const).map((kind) => (
                  <Button
                    key={kind}
                    type="button"
                    size="sm"
                    variant="outline"
                    className="gap-1.5"
                    disabled={busy !== null}
                    onClick={() => void download(code, kind)}
                  >
                    {busy === `${code}:${kind}` ? (
                      <Loader2 className="size-4 animate-spin" />
                    ) : (
                      <Download className="size-4" aria-hidden />
                    )}
                    {t(kind === 'pdf' ? 'pdf' : 'excel')}
                  </Button>
                ))}
              </div>
            );
          },
        }),
      ]),
    [busy, format, t],
  );

  return (
    <DataTable
      data={page.items}
      columns={columns}
      server={server}
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
  );
}
