'use client';

import {
  BL_NOTE_STATUS_COLORS,
  BL_NOTE_STATUSES,
  type BlNoteListItem,
  type Client,
  type Courier,
  type Paginated,
} from '@delivery/shared';
import { Eye, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useTransition } from 'react';
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
import { DropdownMenuItem, DropdownMenuSeparator } from '@/components/ui/dropdown-menu';
import { useRouter } from '@/i18n/navigation';
import { deleteBlNoteAction } from '@/lib/actions/bl';

const helper = createDataTableColumnHelper<BlNoteListItem>();

/**
 * ADMIN → RETOURS → ONGLET BL — chantier « Retours, BL Clients Pro », point 5.
 *
 * « Liste des bons BL, deux actions séparées “Confirmer la réception” et
 * “Confirmer la remise au vendeur”, chacune journalisée. »
 *
 * **Les deux actions vivent sur la FICHE, pas sur la ligne.** Confirmer une
 * réception, c'est compter des feuilles ; confirmer une remise, c'est faire
 * signer quelqu'un. Ni l'un ni l'autre ne se fait depuis un menu contextuel en
 * survolant une liste — et un bouton qui bouge des papiers ne doit pas être à
 * un clic d'un clic ailleurs.
 *
 * **Le filtre « Client Pro » ne propose que des Pro.** Les autres n'ont aucun
 * bon BL, par construction (D-099), et les lister ferait chercher dans le vide.
 */
export function BlNotesScreen({
  page,
  clients,
  couriers,
}: {
  page: Paginated<BlNoteListItem>;
  clients: Client[];
  couriers: Courier[];
}) {
  const t = useTranslations('returns.bl');
  const tStatus = useTranslations('returns.bl.status');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'createdAt',
    defaultOrder: 'desc',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [, startTransition] = useTransition();

  const remove = (row: BlNoteListItem) => {
    startTransition(async () => {
      const result = await deleteBlNoteAction(row.code);
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
        options: BL_NOTE_STATUSES.map((status) => ({ value: status, label: tStatus(status) })),
        onChange: (value) => setFilter('status', value === ALL_VALUE ? undefined : value),
      },
      {
        id: 'clientId',
        multiple: true,
        label: t('filters.client'),
        value: param('clientId') ?? ALL_VALUE,
        // Seuls les Pro : les autres n'ont aucun bon BL, par construction.
        options: clients
          .filter((client) => client.isPro)
          .map((client) => ({ value: client.id, label: client.name })),
        onChange: (value) => setFilter('clientId', value === ALL_VALUE ? undefined : value),
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
    ],
    [clients, couriers, param, setFilter, t, tStatus],
  );

  const columns: DataTableColumns<BlNoteListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('code', {
          header: t('columns.code'),
          cell: (info) => (
            <CellStack label={info.getValue()} sub={info.row.original.agencyName} mono />
          ),
        }),

        helper.accessor('clientName', {
          header: t('columns.client'),
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),

        helper.accessor('courierName', {
          header: t('columns.courier'),
          meta: { hideBelow: 'md' },
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),

        helper.accessor('slipCount', {
          header: t('columns.slips'),
          meta: { align: 'end' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),

        helper.accessor('receivedAt', {
          header: t('columns.receivedAt'),
          meta: { hideBelow: 'lg' },
          cell: (info) => {
            const value = info.getValue();
            return (
              <CellStack
                label={value ? format.dateTime(new Date(value), 'stamp') : '-'}
                sub={info.row.original.returnedToName ?? undefined}
              />
            );
          },
        }),

        helper.accessor('status', {
          header: t('columns.state'),
          meta: { width: 'w-44' },
          cell: (info) => {
            const status = info.row.original.status;
            return <CellBadge label={tStatus(status)} color={BL_NOTE_STATUS_COLORS[status]} />;
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
                <DropdownMenuItem onClick={() => router.push(`/returns/bl/${row.code}`)}>
                  <Eye className="size-4" />
                  {t('open')}
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                {/*
                  Seul un BROUILLON se supprime : passée la remise à
                  l'entrepôt, les papiers sont physiquement là, et effacer le
                  bon ne les ferait pas disparaître.
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
    [format, router, t, tCommon, tStatus],
  );

  return (
    <DataTable
      data={page.items}
      columns={columns}
      server={server}
      onRowClick={(row) => router.push(`/returns/bl/${row.code}`)}
      emptyTitle={t('empty')}
      emptyHint={t('emptyHint')}
      toolbar={
        <DataTableToolbar
          search={search}
          onSearchChange={setSearch}
          searchPlaceholder={t('searchPlaceholder')}
          selects={selects}
          exportResource="bl/notes"
          onReset={() => {
            setSearch('');
            reset();
          }}
        />
      }
    />
  );
}
