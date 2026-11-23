'use client';

import { isLockedStatus, type ParcelListItem } from '@delivery/shared';
import { Eye, Printer, RefreshCw, ShieldAlert } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo } from 'react';
import { CellMuted, CellNumber, CellStack, RowActions } from '@/components/data-table/cells';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { Button } from '@/components/ui/button';
import { DropdownMenuItem, DropdownMenuSeparator } from '@/components/ui/dropdown-menu';
import { FinancialStatusBadge } from '@/features/parcels/financial-status-badge';
import { ParcelStatusBadge } from '@/features/parcels/status-badge';
import { Link } from '@/i18n/navigation';

const helper = createDataTableColumnHelper<ParcelListItem>();

/**
 * Colonnes de la liste des colis.
 *
 * `hideBelow` porte la stratégie mobile-first : sous `sm` ne restent que le
 * code, le statut et le montant — les trois informations qu'un agent cherche
 * sur un téléphone.
 */
export function useParcelColumns({
  onChangeStatus,
  onPrintLabel,
}: {
  onChangeStatus: (parcel: ParcelListItem) => void;
  onPrintLabel: (parcel: ParcelListItem) => void;
}): DataTableColumns<ParcelListItem> {
  const t = useTranslations('parcels.columns');
  const tCommon = useTranslations('common');
  const tDetail = useTranslations('parcels.detail');
  const tLabels = useTranslations('labels');
  /* « Colis », « Retour », « Échange » — point 2. */
  const tKind = useTranslations('parcels.kind');
  const format = useFormatter();

  return useMemo(
    () =>
      helper.columns([
        helper.accessor('code', {
          header: t('code'),
          meta: { width: 'w-44' },
          cell: (info) => (
            <CellStack
              mono
              label={info.getValue()}
              /*
                LA NATURE SOUS LE CODE — chantier « Étiquette orange », point 2.
                « Les deux nouveaux colis avec leur code. » Le préfixe `RET-` /
                `ECH-` le dit déjà à l'œil, le sous-libellé le dit en toutes
                lettres — et il prend la place d'une référence client que ces
                colis-là n'ont jamais.
              */
              sub={
                info.row.original.kind === 'parcel'
                  ? (info.row.original.externalCode ?? '-')
                  : tKind(info.row.original.kind)
              }
            />
          ),
        }),

        helper.accessor('recipientName', {
          header: t('recipient'),
          meta: { hideBelow: 'sm' },
          cell: (info) => (
            <CellStack label={info.getValue()} sub={info.row.original.recipientPhone} />
          ),
        }),

        helper.accessor('destinationCityName', {
          header: t('destination'),
          meta: { hideBelow: 'md' },
          cell: (info) => (
            <CellStack
              label={info.getValue()}
              sub={info.row.original.destinationDistrictName ?? '-'}
            />
          ),
        }),

        helper.accessor('clientName', {
          header: t('client'),
          meta: { hideBelow: 'lg' },
          cell: (info) => (
            <CellStack label={info.getValue()} sub={info.row.original.originCityName} />
          ),
        }),

        helper.accessor('statusId', {
          header: t('status'),
          sortFn: 'basic',
          cell: (info) => <ParcelStatusBadge statusId={info.getValue()} />,
        }),

        /*
          LE SECOND AXE — chantier « Finance », point 1, D-105.

          Il vit à côté du statut logistique, jamais à sa place : on lit « livré »
          ET « pas encore facturé » sur la même ligne, ce qui est exactement la
          question du comptable. `hideBelow: 'lg'` parce que l'agent sur
          téléphone cherche d'abord où est le carton ; l'argent se regarde
          assis.

          L'e-commerçant n'a pas `financialStatus` (D-060) : il lit SON axe,
          `merchantFinancialStatus` — Facturé, Payé… (lot O, D-227).
        */
        helper.accessor('financialStatus', {
          header: t('financial'),
          sortFn: 'basic',
          meta: { hideBelow: 'lg' },
          cell: (info) => (
            <FinancialStatusBadge
              status={info.getValue()}
              merchantStatus={info.row.original.merchantFinancialStatus}
            />
          ),
        }),

        helper.accessor('codAmount', {
          header: t('cod'),
          sortFn: 'basic',
          meta: { align: 'end' },
          cell: (info) => (
            <CellNumber
              value={format.number(info.getValue() / 100, 'currency')}
              sub={
                info.row.original.feeIncluded
                  ? t('feeIncluded')
                  : format.number(info.row.original.deliveryFee / 100, 'currency')
              }
            />
          ),
        }),

        helper.accessor('statusAt', {
          header: t('updated'),
          sortFn: 'datetime',
          meta: { hideBelow: 'xl' },
          cell: (info) => (
            <CellMuted>{format.dateTime(new Date(info.getValue()), 'stamp')}</CellMuted>
          ),
        }),

        /*
          L'ÉTIQUETTE EN UN CLIC — D-176 (lot B, point B5). Le même générateur
          que le bouton de la fiche ; le clic ne doit pas ouvrir la fiche.
        */
        helper.display({
          id: 'label',
          header: () => <span className="sr-only">{tLabels('action')}</span>,
          enableSorting: false,
          meta: { align: 'end', width: 'w-10', hideBelow: 'sm' },
          cell: (info) => (
            <Button
              variant="ghost"
              size="icon"
              className="size-8"
              aria-label={tLabels('action')}
              title={tLabels('action')}
              onClick={(event) => {
                event.stopPropagation();
                onPrintLabel(info.row.original);
              }}
            >
              <Printer className="size-4" aria-hidden />
            </Button>
          ),
        }),

        helper.display({
          id: 'actions',
          header: () => <span className="sr-only">{tCommon('actions')}</span>,
          enableSorting: false,
          meta: { align: 'end', isActions: true, width: 'w-12' },
          cell: (info) => {
            const parcel = info.row.original;
            const locked = isLockedStatus(parcel.statusId);

            return (
              <RowActions>
                <DropdownMenuItem asChild>
                  <Link href={`/parcels/${parcel.code}`} className="cursor-pointer gap-2">
                    <Eye className="size-4" />
                    {tCommon('view')}
                  </Link>
                </DropdownMenuItem>

                <DropdownMenuItem
                  className="cursor-pointer gap-2"
                  onSelect={() => onChangeStatus(parcel)}
                >
                  {locked ? <ShieldAlert className="size-4" /> : <RefreshCw className="size-4" />}
                  {locked ? tDetail('dispute.action') : tDetail('changeStatus.action')}
                </DropdownMenuItem>

                <DropdownMenuSeparator />

                <DropdownMenuItem
                  className="cursor-pointer gap-2"
                  onSelect={() => onPrintLabel(parcel)}
                >
                  <Printer className="size-4" />
                  {tLabels('action')}
                </DropdownMenuItem>
              </RowActions>
            );
          },
        }),
      ]),
    [t, tCommon, tDetail, tKind, tLabels, format, onChangeStatus, onPrintLabel],
  );
}
