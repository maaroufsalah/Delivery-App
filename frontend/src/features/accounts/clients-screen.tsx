'use client';

import type { Agency, City, ClientListItem, Paginated } from '@delivery/shared';
import { BadgeCheck, Eye, Pencil, Plus, Power, Trash2, UserX } from 'lucide-react';
import { useTranslations } from 'next-intl';
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
import { ClientForm } from '@/features/accounts/client-form';
import { DeleteDialog } from '@/features/referential/form-sheet';
import { RejectDialog, ValidateDialog } from '@/features/accounts/validation-dialogs';
import { useRouter } from '@/i18n/navigation';
import {
  deleteClientAction,
  setClientActiveAction,
  setClientValidatedAction,
  validateClientAction,
  rejectClientAction,
} from '@/lib/actions/accounts';

const helper = createDataTableColumnHelper<ClientListItem>();

/**
 * E-commerçants — DOMAIN.md §1.5.
 *
 * La fiche et le COMPTE sont deux choses : le legacy confondait la personne
 * morale et son identifiant de connexion, si bien qu'un client sans accès web
 * n'existait pas. Ici la fiche vit seule, et le compte MERCHANT se crée depuis
 * elle quand — et seulement quand — il est demandé.
 *
 * La MODIFICATION n'a pas lieu ici mais sur la fiche : la ligne de liste ne
 * porte ni l'adresse ni les notes internes, et enregistrer un formulaire qui ne
 * les connaît pas les effacerait sans le dire.
 */
export function ClientsScreen({
  page,
  cities,
  agencies,
  pendingCount,
}: {
  /** Fiches à valider, pour le bouton mis en avant — C13 bloc 2. */
  pendingCount?: number;
  page: Paginated<ClientListItem>;
  cities: City[];
  agencies: Agency[];
}) {
  const t = useTranslations('accounts.clients');
  const tCommon = useTranslations('common');
  const tValidation = useTranslations('accounts.validation');
  const router = useRouter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'name',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [creating, setCreating] = useState(false);
  const [removing, setRemoving] = useState<ClientListItem | null>(null);
  const [, startTransition] = useTransition();
  const [validating, setValidating] = useState<ClientListItem | null>(null);
  const [rejecting, setRejecting] = useState<ClientListItem | null>(null);

  const toggleActive = (client: ClientListItem) => {
    startTransition(async () => {
      const result = await setClientActiveAction(client.id, !client.isActive);
      if (result.ok) toast.success(t('toggled', { name: client.name }));
      else toast.error(result.error);
    });
  };

  const toggleValidated = (client: ClientListItem) => {
    startTransition(async () => {
      const result = await setClientValidatedAction(client.id, client.validatedAt === null);
      if (result.ok) toast.success(t('toggled', { name: client.name }));
      else toast.error(result.error);
    });
  };

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        // D-255 : plusieurs agences ; « validé » et « actif » restent des oui/non uniques.
        id: 'agencyId',
        multiple: true,
        label: t('filters.agency'),
        value: param('agencyId') ?? ALL_VALUE,
        options: agencies.map((agency) => ({ value: agency.id, label: agency.name })),
        onChange: (value) => setFilter('agencyId', value === ALL_VALUE ? undefined : value),
      },
      {
        id: 'validated',
        label: t('filters.validated'),
        value: param('validated') ?? ALL_VALUE,
        options: [
          { value: 'true', label: t('validated') },
          { value: 'false', label: t('notValidated') },
          { value: 'rejected', label: t('rejected') },
        ],
        onChange: (value) => setFilter('validated', value === ALL_VALUE ? undefined : value),
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
    [agencies, param, setFilter, t, tCommon],
  );

  const columns: DataTableColumns<ClientListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('name', {
          header: t('columns.name'),
          cell: (info) => (
            <CellStack
              label={
                <span className="inline-flex items-center gap-1.5">
                  {info.getValue()}
                  {info.row.original.validatedAt ? (
                    <BadgeCheck
                      className="size-3.5 text-[var(--chart-4)]"
                      aria-label={t('validated')}
                    />
                  ) : null}
                  {/*
                    CLIENT PRO — point 4 du chantier « Retours, BL Clients Pro ».
                    Une pastille et non une colonne : elle ne concerne qu'une
                    poignée de fiches, et une colonne vide sur quatre-vingt-dix
                    pour cent des lignes coûte de la largeur à 375 px.
                  */}
                  {info.row.original.isPro ? (
                    <span className="bg-primary/10 text-primary rounded-full px-1.5 py-0.5 text-[10px] font-medium">
                      {t('proBadge')}
                    </span>
                  ) : null}
                </span>
              }
              sub={info.row.original.legalName ?? undefined}
            />
          ),
        }),

        helper.accessor('phone', {
          header: t('columns.contact'),
          meta: { hideBelow: 'md' },
          cell: (info) => <CellStack label={info.getValue()} sub={info.row.original.email} />,
        }),

        helper.accessor('cityName', {
          header: t('columns.city'),
          meta: { hideBelow: 'sm' },
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),

        helper.accessor('agencyName', {
          header: t('columns.agency'),
          meta: { hideBelow: 'xl' },
          cell: (info) => <CellMuted>{info.getValue() ?? '-'}</CellMuted>,
        }),

        helper.accessor('parcelCount', {
          header: t('columns.parcels'),
          meta: { align: 'end', hideBelow: 'lg' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),

        helper.accessor('isActive', {
          header: t('columns.state'),
          meta: { width: 'w-32' },
          cell: (info) =>
            info.getValue() ? (
              <CellBadge label={tCommon('active')} color="var(--chart-4)" />
            ) : (
              <CellBadge label={tCommon('inactive')} />
            ),
        }),

        helper.display({
          id: 'actions',
          header: '',
          meta: { isActions: true, align: 'end', width: 'w-16' },
          cell: (info) => {
            const row = info.row.original;

            return (
              <RowActions>
                <DropdownMenuItem onClick={() => router.push(`/clients/${row.id}`)}>
                  <Eye className="size-4" />
                  {t('open')}
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => router.push(`/clients/${row.id}`)}>
                  <Pencil className="size-4" />
                  {tCommon('edit')}
                </DropdownMenuItem>
                {/* Valider et Refuser sont DEUX gestes distincts — C13
                    bloc 2 — et ils ne s'offrent qu'à une fiche qui attend.
                    « Dévalider » reste pour revenir sur une validation, ce qui
                    n'est pas la même chose que refuser une demande. */}
                {row.validatedAt ? (
                  <DropdownMenuItem onClick={() => toggleValidated(row)}>
                    <BadgeCheck className="size-4" />
                    {t('unvalidate')}
                  </DropdownMenuItem>
                ) : (
                  <>
                    <DropdownMenuItem onClick={() => setValidating(row)}>
                      <BadgeCheck className="size-4" />
                      {t('validate')}
                    </DropdownMenuItem>
                    <DropdownMenuItem onClick={() => setRejecting(row)}>
                      <UserX className="size-4" />
                      {tValidation('reject')}
                    </DropdownMenuItem>
                  </>
                )}
                <DropdownMenuItem onClick={() => toggleActive(row)}>
                  <Power className="size-4" />
                  {row.isActive ? tCommon('deactivate') : tCommon('activate')}
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem variant="destructive" onClick={() => setRemoving(row)}>
                  <Trash2 className="size-4" />
                  {tCommon('delete')}
                </DropdownMenuItem>
              </RowActions>
            );
          },
        }),
      ]),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [router, t, tCommon],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(client) => router.push(`/clients/${client.id}`)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="clients"
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            highlight={{
              label: t('notValidated'),
              count: pendingCount,
              active: param('validated') === 'false',
              onToggle: (next) => setFilter('validated', next ? 'false' : undefined),
            }}
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
        <ClientForm
          client={null}
          cities={cities}
          agencies={agencies}
          onDone={() => setCreating(false)}
        />
      ) : null}

      {removing ? (
        <DeleteDialog
          open
          onOpenChange={(next) => {
            if (!next) setRemoving(null);
          }}
          label={removing.name}
          onConfirm={() => deleteClientAction(removing.id)}
          onDeactivate={() => setClientActiveAction(removing.id, false)}
        />
      ) : null}

      {validating ? (
        <ValidateDialog
          label={validating.name}
          agencies={agencies}
          onConfirm={(agencyId) => validateClientAction(validating.id, agencyId)}
          onClose={() => setValidating(null)}
        />
      ) : null}

      {rejecting ? (
        <RejectDialog
          label={rejecting.name}
          onConfirm={(reason) => rejectClientAction(rejecting.id, reason)}
          onClose={() => setRejecting(null)}
        />
      ) : null}
    </>
  );
}
