'use client';

import {
  ROUTE_STATUS_COLORS,
  ROUTE_STATUSES,
  type City,
  type Courier,
  type Paginated,
  type RouteListItem,
  type RouteStats,
  type RouteStatus,
} from '@delivery/shared';
import { CheckCheck, Pencil, Plus, Route as RouteIcon, Trash2, Truck } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useCallback, useMemo, useState } from 'react';
import {
  CellBadge,
  CellMuted,
  CellNumber,
  CellStack,
  RowActions,
} from '@/components/data-table/cells';
import { DropdownMenuItem } from '@/components/ui/dropdown-menu';
import { RouteDraftDialogs } from '@/features/routes/route-draft-actions';
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
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { Button } from '@/components/ui/button';
import { RouteForm } from '@/features/routes/route-form';
import { useRouter } from '@/i18n/navigation';

const helper = createDataTableColumnHelper<RouteListItem>();

/**
 * Liste des circuits — DOMAIN.md §1.13 (`boxes` où `type = 'circuit'`).
 * Le type `boxe` (lot inter-agences) n'est pas repris : 1 ligne en prod,
 * concept mort.
 */
export function RoutesTable({
  page,
  stats,
  cities,
  couriers,
}: {
  page: Paginated<RouteListItem>;
  /** Compteurs de la SÉLECTION, calculés par l'API (C7 bloc 4). */
  stats: RouteStats;
  cities: City[];
  couriers: Courier[];
}) {
  const t = useTranslations('routes');
  const tStatus = useTranslations('routeStatus');
  const tKind = useTranslations('routeKind');
  const format = useFormatter();

  const router = useRouter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    // Date de création, récent d'abord (chantier « Tri par défaut », 11/11/2026).
    defaultSort: 'createdAt',
    defaultOrder: 'desc',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [creating, setCreating] = useState(false);
  const [target, setTarget] = useState<{ route: RouteListItem; mode: 'edit' | 'delete' } | null>(
    null,
  );
  const tCommon = useTranslations('common');

  // `useCallback` : la référence entre dans les dépendances du `useMemo` des
  // filtres ; recréée à chaque rendu, elle le recalculerait à chaque frappe.
  const setParam = useCallback(
    (key: string, value: string) => setFilter(key, value === ALL_VALUE ? undefined : value),
    [setFilter],
  );

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        id: 'status',
        multiple: true,
        label: t('filters.status'),
        value: param('status') ?? ALL_VALUE,
        options: ROUTE_STATUSES.map((status) => ({
          value: status,
          label: tStatus(status),
          color: ROUTE_STATUS_COLORS[status],
        })),
        onChange: (value) => setParam('status', value),
      },
      {
        id: 'cityId',
        multiple: true,
        label: t('filters.city'),
        value: param('cityId') ?? ALL_VALUE,
        options: cities.map((city) => ({ value: city.id, label: city.name })),
        cities,
        onChange: (value) => setParam('cityId', value),
      },
    ],
    [cities, param, setParam, t, tStatus],
  );

  const columns: DataTableColumns<RouteListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('code', {
          header: t('columns.code'),
          meta: { width: 'w-40' },
          cell: (info) => (
            <CellStack mono label={info.getValue()} sub={tKind(info.row.original.kind)} />
          ),
        }),
        helper.accessor('courierName', {
          header: t('columns.courier'),
          cell: (info) => <CellStack label={info.getValue()} sub={info.row.original.cityName} />,
        }),
        helper.accessor('status', {
          header: t('columns.status'),
          cell: (info) => {
            // v9 élargit la valeur d'accesseur à `any` : on la resserre ici,
            // sinon l'indexation de la table de couleurs n'est plus vérifiée.
            const status = info.getValue() as RouteStatus;
            return <CellBadge label={tStatus(status)} color={ROUTE_STATUS_COLORS[status]} />;
          },
        }),
        helper.accessor('confirmedCount', {
          header: t('columns.confirmed'),
          sortFn: 'basic',
          meta: { align: 'end', hideBelow: 'sm' },
          // Le total compte les colis retirés des bons — « 62 / 199 » (Recette 5, point 7).
          cell: (info) => {
            const total = info.row.original.parcelCount + info.row.original.removedCount;
            return (
              <CellNumber
                value={`${info.getValue()} / ${total}`}
                sub={total > 0 ? format.number(info.getValue() / total, 'percent') : undefined}
              />
            );
          },
        }),
        helper.accessor('statusAt', {
          header: t('columns.date'),
          sortFn: 'datetime',
          meta: { hideBelow: 'lg' },
          cell: (info) => (
            <CellMuted>{format.dateTime(new Date(info.getValue()), 'stamp')}</CellMuted>
          ),
        }),

        /*
          MODIFIER / SUPPRIMER — D-173 (lot B, point B2). Sur un BROUILLON
          seulement : un circuit parti n'a ni l'un ni l'autre, et l'API les
          refuserait de toute façon (`route-not-draft`).
        */
        helper.display({
          id: 'actions',
          header: () => <span className="sr-only">{tCommon('actions')}</span>,
          enableSorting: false,
          meta: { align: 'end', isActions: true, width: 'w-12' },
          cell: (info) => {
            const route = info.row.original;
            if (route.status !== 'draft') return null;

            return (
              <RowActions>
                <DropdownMenuItem
                  className="cursor-pointer gap-2"
                  onSelect={() => setTarget({ route, mode: 'edit' })}
                >
                  <Pencil className="size-4" aria-hidden />
                  {t('form.edit')}
                </DropdownMenuItem>
                <DropdownMenuItem
                  className="text-destructive cursor-pointer gap-2"
                  onSelect={() => setTarget({ route, mode: 'delete' })}
                >
                  <Trash2 className="size-4" aria-hidden />
                  {t('form.delete')}
                </DropdownMenuItem>
              </RowActions>
            );
          },
        }),
      ]),
    [t, tCommon, tKind, tStatus, format],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(route) => router.push(`/routes/${route.code}`)}
        kpis={
          <KpiGrid>
            <KpiCard
              label={t('kpi.total')}
              value={format.number(stats.total, 'integer')}
              icon={RouteIcon}
            />
            <KpiCard
              label={t('kpi.open')}
              value={format.number(stats.open, 'integer')}
              icon={Truck}
              tone="var(--chart-3)"
            />
            <KpiCard
              label={t('kpi.parcels')}
              value={format.number(stats.parcels, 'integer')}
              icon={RouteIcon}
              tone="var(--chart-2)"
            />
            <KpiCard
              label={t('kpi.confirmed')}
              value={format.number(stats.confirmed, 'integer')}
              hint={
                stats.parcels > 0
                  ? format.number(stats.confirmed / stats.parcels, 'percent')
                  : undefined
              }
              icon={CheckCheck}
              tone="var(--chart-4)"
            />
          </KpiGrid>
        }
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="routes"
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            onReset={() => {
              setSearch('');
              reset();
            }}
          >
            <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
              <Plus className="size-4" aria-hidden />
              <span className="hidden sm:inline">{t('form.new')}</span>
            </Button>
          </DataTableToolbar>
        }
      />

      {creating ? (
        <RouteForm
          cities={cities}
          couriers={couriers}
          onDone={() => setCreating(false)}
          onCreated={(code) => router.push(`/routes/${code}`)}
        />
      ) : null}

      {target ? (
        <RouteDraftDialogs
          route={target.route}
          cities={cities}
          couriers={couriers}
          locked={target.route.parcelCount > 0}
          editing={target.mode === 'edit'}
          deleting={target.mode === 'delete'}
          onClose={() => setTarget(null)}
          onDeleted={() => router.refresh()}
        />
      ) : null}
    </>
  );
}
