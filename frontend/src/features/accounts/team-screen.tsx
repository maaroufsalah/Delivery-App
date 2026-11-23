'use client';

import type { Paginated, UserListItem } from '@delivery/shared';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo } from 'react';
import { CellBadge, CellMuted, CellStack } from '@/components/data-table/cells';
import { DataTable } from '@/components/data-table/data-table';
import { DataTableToolbar } from '@/components/data-table/data-table-toolbar';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { useServerTable, useUrlSearch } from '@/components/data-table/use-server-table';
import { useRouter } from '@/i18n/navigation';

const helper = createDataTableColumnHelper<UserListItem>();

/**
 * « Mon équipe » — le personnel de l'agence du compte connecté.
 *
 * C'est une vue de CONSULTATION : la gestion des comptes vit dans
 * `/users`, derrière `accounts.write`. Un chef d'agence a besoin de savoir qui
 * est rattaché chez lui et qui s'est connecté ; il n'a pas à inviter ni à
 * révoquer pour autant.
 */
export function TeamScreen({ page }: { page: Paginated<UserListItem> }) {
  const t = useTranslations('accounts.team');
  const tUser = useTranslations('accounts.users');
  const tRole = useTranslations('accounts.roles.names');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'lastName',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));

  const columns: DataTableColumns<UserListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('lastName', {
          header: tUser('columns.name'),
          cell: (info) => (
            <CellStack
              label={`${info.row.original.firstName} ${info.getValue()}`}
              sub={info.row.original.jobTitle ?? info.row.original.email}
            />
          ),
        }),

        helper.accessor('roles', {
          header: tUser('columns.roles'),
          meta: { hideBelow: 'sm' },
          cell: (info) => (
            <div className="flex flex-wrap gap-1">
              {info.row.original.roles.map((role) => (
                <CellBadge key={role} label={tRole(role)} />
              ))}
            </div>
          ),
        }),

        helper.accessor('phone', {
          header: tUser('fields.phone'),
          meta: { hideBelow: 'md' },
          cell: (info) => <CellMuted>{info.getValue() ?? '-'}</CellMuted>,
        }),

        helper.accessor('lastLoginAt', {
          header: tUser('columns.lastLogin'),
          meta: { hideBelow: 'lg' },
          cell: (info) => {
            const value = info.getValue();
            return (
              <CellMuted>
                {value ? format.dateTime(new Date(value), 'stamp') : tUser('never')}
              </CellMuted>
            );
          },
        }),

        helper.accessor('isActive', {
          header: tUser('columns.state'),
          meta: { width: 'w-32' },
          cell: (info) =>
            info.getValue() ? (
              <CellBadge label={tCommon('active')} color="var(--chart-4)" />
            ) : (
              <CellBadge label={tCommon('inactive')} />
            ),
        }),
      ]),
    [format, tCommon, tRole, tUser],
  );

  return (
    <DataTable
      data={page.items}
      columns={columns}
      server={server}
      /*
       * La ligne mène à la fiche du compte — C13 bloc 3.
       *
       * « Mon équipe » n'avait aucune fiche : c'était une liste et rien
       * d'autre. La page est la MÊME que celle de `/users/[id]`, et elle
       * s'ouvre en lecture seule pour qui n'a pas `accounts.write` — un chef
       * d'agence consulte, il ne révoque pas.
       */
      onRowClick={(member) => router.push(`/users/${member.id}`)}
      emptyTitle={t('empty')}
      emptyHint={t('emptyHint')}
      toolbar={
        <DataTableToolbar
          search={search}
          onSearchChange={setSearch}
          exportResource="users"
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
