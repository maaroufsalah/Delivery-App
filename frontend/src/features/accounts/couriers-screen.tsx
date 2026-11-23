'use client';

import type { Agency, City, CourierListItem, Paginated } from '@delivery/shared';
import { BadgeCheck, Eye, Pencil, Plus, Power, Trash2, UserX } from 'lucide-react';
import { useTranslations } from 'next-intl';
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
import { CourierForm } from '@/features/accounts/courier-form';
import { RejectDialog, ValidateDialog } from '@/features/accounts/validation-dialogs';
import { DeleteDialog } from '@/features/referential/form-sheet';
import { useRouter } from '@/i18n/navigation';
import {
  deleteCourierAction,
  setCourierActiveAction,
  setCourierValidatedAction,
  validateCourierAction,
  rejectCourierAction,
} from '@/lib/actions/accounts';

const helper = createDataTableColumnHelper<CourierListItem>();

/**
 * Livreurs — DOMAIN.md §1.6.
 *
 * La grille de paie par ville vit sur la fiche : elle a autant de lignes que le
 * livreur couvre de villes, et n'a pas sa place dans une colonne. La
 * modification s'y fait aussi — la ligne de liste ne porte ni la CNI ni le RIB.
 */
export function CouriersScreen({
  page,
  cities,
  agencies,
  pendingCount,
}: {
  /** Fiches à valider, pour le bouton mis en avant — C13 bloc 2. */
  pendingCount?: number;
  page: Paginated<CourierListItem>;
  cities: City[];
  agencies: Agency[];
}) {
  const t = useTranslations('accounts.couriers');
  const tValidation = useTranslations('accounts.validation');
  const tVehicle = useTranslations('accounts.couriers.vehicles');
  const tModel = useTranslations('accounts.couriers.payModels');
  const tCommon = useTranslations('common');
  const router = useRouter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'lastName',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [creating, setCreating] = useState(false);
  const [removing, setRemoving] = useState<CourierListItem | null>(null);
  const [, startTransition] = useTransition();
  const [validating, setValidating] = useState<CourierListItem | null>(null);
  const [rejecting, setRejecting] = useState<CourierListItem | null>(null);

  const name = (courier: CourierListItem) => `${courier.firstName} ${courier.lastName}`;

  const toggleActive = (courier: CourierListItem) => {
    startTransition(async () => {
      const result = await setCourierActiveAction(courier.id, !courier.isActive);
      if (result.ok) toast.success(t('toggled', { name: name(courier) }));
      else toast.error(result.error);
    });
  };

  const toggleValidated = (courier: CourierListItem) => {
    startTransition(async () => {
      const result = await setCourierValidatedAction(courier.id, courier.validatedAt === null);
      if (result.ok) toast.success(t('toggled', { name: name(courier) }));
      else toast.error(result.error);
    });
  };

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        // D-255 : plusieurs agences, plusieurs villes ; « validé » et « actif » restent uniques.
        id: 'agencyId',
        multiple: true,
        label: t('filters.agency'),
        value: param('agencyId') ?? ALL_VALUE,
        options: agencies.map((agency) => ({ value: agency.id, label: agency.name })),
        onChange: (value) => setFilter('agencyId', value === ALL_VALUE ? undefined : value),
      },
      {
        id: 'cityId',
        multiple: true,
        label: t('filters.city'),
        value: param('cityId') ?? ALL_VALUE,
        options: cities.map((city) => ({ value: city.id, label: city.name })),
        cities,
        onChange: (value) => setFilter('cityId', value === ALL_VALUE ? undefined : value),
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
    [agencies, cities, param, setFilter, t, tCommon],
  );

  const columns: DataTableColumns<CourierListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('lastName', {
          header: t('columns.name'),
          cell: (info) => (
            <CellStack
              label={
                <span className="inline-flex items-center gap-1.5">
                  {`${info.row.original.firstName} ${info.getValue()}`}
                  {info.row.original.validatedAt ? (
                    <BadgeCheck
                      className="size-3.5 text-[var(--chart-4)]"
                      aria-label={t('validated')}
                    />
                  ) : null}
                </span>
              }
              sub={info.row.original.phone}
            />
          ),
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

        helper.accessor('vehicleType', {
          header: t('columns.vehicle'),
          meta: { hideBelow: 'lg' },
          cell: (info) => <CellMuted>{tVehicle(info.row.original.vehicleType)}</CellMuted>,
        }),

        helper.accessor('payModel', {
          header: t('columns.pay'),
          meta: { hideBelow: 'md' },
          cell: (info) => <CellMuted>{tModel(info.row.original.payModel)}</CellMuted>,
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
                <DropdownMenuItem onClick={() => router.push(`/couriers/${row.id}`)}>
                  <Eye className="size-4" />
                  {t('grid')}
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => router.push(`/couriers/${row.id}`)}>
                  <Pencil className="size-4" />
                  {tCommon('edit')}
                </DropdownMenuItem>
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
    [router, t, tCommon, tModel, tVehicle],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(courier) => router.push(`/couriers/${courier.id}`)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="couriers"
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
        <CourierForm
          courier={null}
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
          label={name(removing)}
          onConfirm={() => deleteCourierAction(removing.id)}
          onDeactivate={() => setCourierActiveAction(removing.id, false)}
        />
      ) : null}

      {validating ? (
        <ValidateDialog
          label={`${validating.firstName} ${validating.lastName}`}
          agencies={agencies}
          onConfirm={(agencyId) => validateCourierAction(validating.id, agencyId)}
          onClose={() => setValidating(null)}
        />
      ) : null}

      {rejecting ? (
        <RejectDialog
          label={`${rejecting.firstName} ${rejecting.lastName}`}
          onConfirm={(reason) => rejectCourierAction(rejecting.id, reason)}
          onClose={() => setRejecting(null)}
        />
      ) : null}
    </>
  );
}
