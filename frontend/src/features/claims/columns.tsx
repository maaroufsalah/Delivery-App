'use client';

import type { ClaimListItem } from '@delivery/shared';
import { AlertTriangle, Eye } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo } from 'react';
import { CellMuted, CellStack, RowActions } from '@/components/data-table/cells';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { DropdownMenuItem } from '@/components/ui/dropdown-menu';
import { ClaimStatusBadge } from '@/features/claims/status-badge';
import { Link } from '@/i18n/navigation';

const helper = createDataTableColumnHelper<ClaimListItem>();

/**
 * Colonnes de la liste des réclamations.
 *
 * `hideBelow` porte la stratégie mobile : sous `sm` ne restent que le numéro,
 * le statut et le retard — les trois choses qu'un agent regarde d'abord.
 */
export function useClaimColumns({
  showClient,
}: {
  /** L'e-commerçant ne voit que les siennes : lui montrer son nom est du bruit. */
  showClient: boolean;
}): DataTableColumns<ClaimListItem> {
  const t = useTranslations('claims.columns');
  const tCommon = useTranslations('common');
  const format = useFormatter();

  return useMemo(
    () =>
      helper.columns([
        helper.accessor('code', {
          header: t('code'),
          meta: { width: 'w-40' },
          cell: (info) => (
            <CellStack mono label={info.getValue()} sub={info.row.original.typeLabel} />
          ),
        }),

        helper.accessor('subject', {
          header: t('subject'),
          cell: (info) => (
            <CellStack
              label={info.getValue()}
              sub={
                info.row.original.parcelCode ?? (showClient ? info.row.original.clientName : '-')
              }
            />
          ),
        }),

        helper.accessor('status', {
          header: t('status'),
          meta: { width: 'w-36' },
          cell: (info) => (
            <div className="flex flex-wrap items-center gap-1.5">
              <ClaimStatusBadge status={info.getValue()} />
              {info.row.original.isLate ? (
                <span className="text-destructive inline-flex items-center gap-1 text-xs font-medium">
                  <AlertTriangle className="size-3.5" aria-hidden />
                  {t('late')}
                </span>
              ) : null}
            </div>
          ),
        }),

        helper.accessor('assigneeName', {
          header: t('assignee'),
          meta: { hideBelow: 'lg' },
          cell: (info) => <CellMuted>{info.getValue() ?? t('unassigned')}</CellMuted>,
        }),

        helper.accessor('createdAt', {
          header: t('openedAt'),
          meta: { hideBelow: 'md' },
          cell: (info) => (
            <CellStack
              label={format.dateTime(new Date(info.getValue()), 'stamp')}
              sub={info.row.original.authorName}
            />
          ),
        }),

        helper.display({
          id: 'actions',
          header: '',
          meta: { isActions: true, align: 'end', width: 'w-12' },
          cell: (info) => (
            <RowActions>
              <DropdownMenuItem asChild>
                <Link href={`/claims/${encodeURIComponent(info.row.original.code)}`}>
                  <Eye className="size-4" aria-hidden />
                  {tCommon('view')}
                </Link>
              </DropdownMenuItem>
            </RowActions>
          ),
        }),
      ]),
    [t, tCommon, format, showClient],
  );
}
