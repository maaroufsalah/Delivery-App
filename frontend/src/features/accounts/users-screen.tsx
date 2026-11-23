'use client';

import {
  type AgencyListItem,
  type Paginated,
  TENANT_ROLES,
  type UserListItem,
} from '@delivery/shared';
import { Pencil, Plus, Power, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge, CellMuted, CellStack, RowActions } from '@/components/data-table/cells';
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
import { DeleteDialog, FormSheet } from '@/features/referential/form-sheet';
import { AccountForm } from '@/features/accounts/account-form';
import { createUserAction, deleteUserAction, setUserActiveAction } from '@/lib/actions/accounts';
import { useRouter } from '@/i18n/navigation';

const helper = createDataTableColumnHelper<UserListItem>();

/**
 * Comptes — DOMAIN.md §1.4.
 *
 * La VUE GLOBALE : tous les comptes de la société, quelle que soit la fiche à
 * laquelle ils sont liés. Elle crée, elle filtre, elle supprime — et elle
 * renvoie vers `/users/[id]` pour le reste, qui est exactement ce qu'offre
 * l'onglet « Compte » d'une fiche client ou livreur (C13 bloc 3).
 *
 * L'écran saisissait autrefois un mot de passe temporaire qu'il montrait une
 * fois. D-086 y a mis fin : le mot de passe se tape, et il n'y a plus de secret
 * à afficher après coup.
 */
export function UsersScreen({
  page,
  agencies,
  currentUserId,
}: {
  page: Paginated<UserListItem>;
  agencies: AgencyListItem[];
  /** Le compte connecté : certaines actions ne s'appliquent pas à soi-même. */
  currentUserId: string;
}) {
  const t = useTranslations('accounts.users');
  const tRole = useTranslations('accounts.roles.names');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'lastName',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [creating, setCreating] = useState(false);
  const [removing, setRemoving] = useState<UserListItem | null>(null);
  const [, startTransition] = useTransition();

  const toggleActive = (user: UserListItem) => {
    startTransition(async () => {
      const result = await setUserActiveAction(user.id, !user.isActive);
      if (result.ok) toast.success(t('toggled', { name: user.lastName }));
      else toast.error(result.error);
    });
  };

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        // D-255 : plusieurs rôles, plusieurs agences ; « actif » reste un oui/non unique.
        id: 'role',
        multiple: true,
        label: t('filters.role'),
        value: param('role') ?? ALL_VALUE,
        options: TENANT_ROLES.map((role) => ({ value: role, label: tRole(role) })),
        onChange: (value) => setFilter('role', value === ALL_VALUE ? undefined : value),
      },
      {
        id: 'agencyId',
        multiple: true,
        label: t('filters.agency'),
        value: param('agencyId') ?? ALL_VALUE,
        options: agencies.map((agency) => ({ value: agency.id, label: agency.name })),
        onChange: (value) => setFilter('agencyId', value === ALL_VALUE ? undefined : value),
      },
      {
        id: 'isActive',
        label: t('filters.state'),
        value: param('isActive') ?? ALL_VALUE,
        options: [
          { value: 'true', label: tCommon('active') },
          { value: 'false', label: tCommon('inactive') },
        ],
        onChange: (value) => setFilter('isActive', value === ALL_VALUE ? undefined : value),
      },
    ],
    [agencies, param, setFilter, t, tCommon, tRole],
  );

  const columns: DataTableColumns<UserListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('lastName', {
          header: t('columns.name'),
          cell: (info) => (
            <CellStack
              label={`${info.row.original.firstName} ${info.getValue()}`}
              sub={info.row.original.jobTitle ?? info.row.original.email}
            />
          ),
        }),

        helper.accessor('email', {
          header: t('columns.email'),
          meta: { hideBelow: 'lg' },
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),

        helper.accessor('roles', {
          header: t('columns.roles'),
          meta: { hideBelow: 'sm' },
          cell: (info) => (
            <div className="flex flex-wrap gap-1">
              {/* `row.original` plutôt que `getValue()` : l'accesseur d'une
                  colonne de tableau ne garde pas le type de l'élément. */}
              {info.row.original.roles.map((role) => (
                <CellBadge key={role} label={tRole(role)} />
              ))}
            </div>
          ),
        }),

        helper.accessor('agencyName', {
          header: t('columns.agency'),
          meta: { hideBelow: 'md' },
          cell: (info) => <CellMuted>{info.getValue() ?? '-'}</CellMuted>,
        }),

        helper.accessor('lastLoginAt', {
          header: t('columns.lastLogin'),
          meta: { hideBelow: 'xl' },
          cell: (info) => {
            const value = info.getValue();
            return (
              <CellMuted>
                {value ? format.dateTime(new Date(value), 'stamp') : t('never')}
              </CellMuted>
            );
          },
        }),

        helper.accessor('isActive', {
          header: t('columns.state'),
          meta: { width: 'w-36' },
          cell: (info) => {
            const row = info.row.original;
            // « Invité » l'emporte sur « actif » : le compte existe mais n'a
            // pas encore de mot de passe choisi, et c'est ce qui compte ici.
            if (row.mustChangePassword) {
              return <CellBadge label={t('pending')} color="var(--chart-3)" />;
            }
            return info.getValue() ? (
              <CellBadge label={tCommon('active')} color="var(--chart-4)" />
            ) : (
              <CellBadge label={tCommon('inactive')} />
            );
          },
        }),

        helper.display({
          id: 'actions',
          header: '',
          meta: { isActions: true, align: 'end', width: 'w-16' },
          cell: (info) => {
            const row = info.row.original;
            const isSelf = row.id === currentUserId;

            return (
              <RowActions>
                {/* « Ouvrir » et non « Modifier » : la page porte l'identité,
                    le mot de passe et l'activation — C13 bloc 3. */}
                <DropdownMenuItem onClick={() => router.push(`/users/${row.id}`)}>
                  <Pencil className="size-4" />
                  {tCommon('open')}
                </DropdownMenuItem>
                {/* Un compte ne se désactive ni ne se supprime lui-même :
                    l'API le refuse, l'écran ne le propose donc pas. */}
                <DropdownMenuItem onClick={() => toggleActive(row)} disabled={isSelf}>
                  <Power className="size-4" />
                  {row.isActive ? tCommon('deactivate') : tCommon('activate')}
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  variant="destructive"
                  disabled={isSelf}
                  onClick={() => setRemoving(row)}
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
    [currentUserId, format, t, tCommon, tRole],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(user) => router.push(`/users/${user.id}`)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="users"
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

      {creating ? <CreateUserSheet agencies={agencies} onDone={() => setCreating(false)} /> : null}

      {removing ? (
        <DeleteDialog
          open
          onOpenChange={(next) => {
            if (!next) setRemoving(null);
          }}
          label={`${removing.firstName} ${removing.lastName}`}
          onConfirm={() => deleteUserAction(removing.id)}
          onDeactivate={() => setUserActiveAction(removing.id, false)}
        />
      ) : null}
    </>
  );
}

/**
 * La création, dans un tiroir — C13 bloc 3.
 *
 * La MODIFICATION, elle, a quitté cet écran : elle vit sur `/users/[id]`, avec
 * le mot de passe et l'activation, c'est-à-dire au même endroit que sur une
 * fiche client ou livreur. Deux formulaires pour le même compte selon la porte
 * d'entrée, c'était la garantie qu'un champ ajouté n'arrive que d'un côté.
 */
function CreateUserSheet({ agencies, onDone }: { agencies: AgencyListItem[]; onDone: () => void }) {
  const t = useTranslations('accounts.users');

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={t('new')}
      description={t('formHint')}
      hideFooter
    >
      <AccountForm
        agencies={agencies}
        submitLabel={t('new')}
        onCancel={onDone}
        onSubmit={async (values) => {
          const result = await createUserAction(values);
          if (result.ok) {
            toast.success(t('created', { name: values.lastName }));
            onDone();
          }
          return result;
        }}
      />
    </FormSheet>
  );
}
