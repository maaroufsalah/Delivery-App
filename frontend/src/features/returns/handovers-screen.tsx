'use client';

import {
  RETURN_HANDOVER_ORIGINS,
  RETURN_HANDOVER_STATUS_COLORS,
  RETURN_HANDOVER_STATUSES,
  type Agency,
  type Courier,
  type Paginated,
  type ReturnHandoverListItem,
} from '@delivery/shared';
import { Eye, Plus, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
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
import { HandoverWizard } from '@/features/returns/handover-wizard';
import { useRouter } from '@/i18n/navigation';
import { deleteHandoverAction } from '@/lib/actions/returns';

const helper = createDataTableColumnHelper<ReturnHandoverListItem>();

/**
 * BONS DE RETOUR COLIS — chantier « Retours, BL Clients Pro », points 1 et 2.
 *
 * **Sept états au filtre, et leurs couleurs viennent du référentiel.** L'écran
 * en affichait deux, codés en dur dans un ternaire : le troisième état l'aurait
 * fait mentir, et il y en a cinq de plus.
 *
 * **L'origine est une colonne ET un filtre.** L'agent qui suit ses demandes ne
 * veut pas voir les bons que les livreurs ont ouverts d'eux-mêmes : les deux
 * piles n'ont ni le même délai d'attente ni le même interlocuteur.
 *
 * **Seul un BROUILLON se supprime.** Les autres bons s'annulent — l'annulation
 * relâche les colis et laisse une ligne au journal, alors qu'une suppression
 * effacerait la demande sans dire qu'elle a existé. L'action reste visible mais
 * grisée : la voir dit la règle mieux que son absence.
 */
export function HandoversScreen({
  page,
  couriers,
  agencies,
}: {
  page: Paginated<ReturnHandoverListItem>;
  couriers: Courier[];
  agencies: Agency[];
}) {
  const t = useTranslations('returns.handovers');
  const tStatus = useTranslations('returns.handovers.status');
  const tOrigin = useTranslations('returns.handovers.origin');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'createdAt',
    defaultOrder: 'desc',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [creating, setCreating] = useState(false);
  const [, startTransition] = useTransition();

  const remove = (row: ReturnHandoverListItem) => {
    startTransition(async () => {
      const result = await deleteHandoverAction(row.code);
      if (result.ok) toast.success(t('deleted'));
      else toast.error(result.error);
    });
  };

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        id: 'status',
        multiple: true,
        label: t('filters.state'),
        value: param('status') ?? ALL_VALUE,
        options: RETURN_HANDOVER_STATUSES.map((status) => ({
          value: status,
          label: tStatus(status),
        })),
        onChange: (value) => setFilter('status', value === ALL_VALUE ? undefined : value),
      },
      {
        id: 'origin',
        label: t('filters.origin'),
        value: param('origin') ?? ALL_VALUE,
        options: RETURN_HANDOVER_ORIGINS.map((origin) => ({
          value: origin,
          label: tOrigin(origin),
        })),
        onChange: (value) => setFilter('origin', value === ALL_VALUE ? undefined : value),
      },
      {
        id: 'courierId',
        multiple: true,
        label: t('filters.courier'),
        value: param('courierId') ?? ALL_VALUE,
        options: couriers.map((courier) => ({
          value: courier.id,
          label: `${courier.firstName} ${courier.lastName}`,
        })),
        onChange: (value) => setFilter('courierId', value === ALL_VALUE ? undefined : value),
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
    [agencies, couriers, param, setFilter, t, tOrigin, tStatus],
  );

  const columns: DataTableColumns<ReturnHandoverListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('code', {
          header: t('columns.code'),
          cell: (info) => (
            <CellStack label={info.getValue()} sub={info.row.original.agencyName} mono />
          ),
        }),

        helper.accessor('courierName', {
          header: t('columns.courier'),
          meta: { hideBelow: 'sm' },
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),

        helper.accessor('parcelCount', {
          header: t('columns.parcels'),
          meta: { align: 'end' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),

        /*
         * LES VENDEURS — point 1, le pluriel est celui des colis. Un bon
         * n'appartient à aucun e-commerçant : il rassemble ce qu'UN livreur
         * rapporte, et cela traverse plusieurs vendeurs.
         */
        helper.accessor('clientNames', {
          header: t('columns.clients'),
          meta: { hideBelow: 'xl' },
          cell: (info) => {
            const names = info.getValue();
            return (
              <CellMuted>
                {names.length === 0
                  ? '-'
                  : names.length <= 2
                    ? names.join(', ')
                    : `${names.slice(0, 2).join(', ')} +${names.length - 2}`}
              </CellMuted>
            );
          },
        }),

        helper.accessor('origin', {
          header: t('columns.origin'),
          meta: { hideBelow: 'lg', width: 'w-28' },
          cell: (info) => <CellMuted>{tOrigin(info.getValue())}</CellMuted>,
        }),

        helper.accessor('confirmedAt', {
          header: t('columns.confirmedAt'),
          meta: { hideBelow: 'lg' },
          cell: (info) => {
            const value = info.getValue();
            return (
              <CellStack
                label={value ? format.dateTime(new Date(value), 'stamp') : '-'}
                sub={info.row.original.confirmedByName ?? undefined}
              />
            );
          },
        }),

        helper.accessor('status', {
          header: t('columns.state'),
          meta: { width: 'w-44' },
          // La couleur vient du référentiel partagé : aucune page ne choisit sa
          // palette, et les trois espaces montrent le même état pareil.
          cell: (info) => {
            const status = info.row.original.status;
            return (
              <CellBadge label={tStatus(status)} color={RETURN_HANDOVER_STATUS_COLORS[status]} />
            );
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
                <DropdownMenuItem onClick={() => router.push(`/returns/handovers/${row.code}`)}>
                  <Eye className="size-4" />
                  {t('open')}
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                {/*
                  Seul un BROUILLON se supprime ; les autres bons s'annulent
                  depuis leur fiche, ce qui relâche leurs colis et laisse une
                  ligne au journal.
                */}
                <DropdownMenuItem
                  variant="destructive"
                  disabled={row.status !== 'draft'}
                  onClick={() => remove(row)}
                >
                  <Trash2 className="size-4" />
                  {tCommon('delete')}
                </DropdownMenuItem>
              </RowActions>
            );
          },
        }),
      ]),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [format, router, t, tCommon, tOrigin, tStatus],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(row) => router.push(`/returns/handovers/${row.code}`)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="return-handovers"
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            onReset={() => {
              setSearch('');
              reset();
            }}
          >
            <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
              <Plus className="size-4" />
              <span className="hidden sm:inline">{t('new')}</span>
            </Button>
          </DataTableToolbar>
        }
      />

      {creating ? (
        <HandoverWizard
          couriers={couriers}
          agencies={agencies}
          onDone={() => setCreating(false)}
          onCreated={(code) => router.push(`/returns/handovers/${code}`)}
        />
      ) : null}
    </>
  );
}
