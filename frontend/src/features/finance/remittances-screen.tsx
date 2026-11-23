'use client';

import {
  REMITTANCE_STAGES,
  type Agency,
  type City,
  type Courier,
  type Paginated,
  type RemittanceListItem,
} from '@delivery/shared';
import { Eye, Plus, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useSearchParams } from 'next/navigation';
import { useMemo, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellMuted, CellNumber, CellStack, RowActions } from '@/components/data-table/cells';
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
import { RemittanceForm } from '@/features/finance/remittance-form';
import { RemittanceStageBadge } from '@/features/finance/remittance-stage';
import { useFinanceError } from '@/features/finance/use-finance-error';
import { useRouter } from '@/i18n/navigation';
import { deleteRemittanceAction } from '@/lib/actions/finance';

const helper = createDataTableColumnHelper<RemittanceListItem>();

/**
 * Retours de fonds — le cash que le livreur rapporte.
 *
 * Une recette confirmée ne se supprime pas : c'est la preuve que l'argent est
 * arrivé à l'agence. L'action reste visible mais désactivée — la voir grisée
 * dit la règle mieux que son absence.
 */
export function RemittancesScreen({
  page,
  couriers,
  cities,
  agencies,
  canWrite,
}: {
  page: Paginated<RemittanceListItem>;
  couriers: Courier[];
  cities: City[];
  agencies: Agency[];
  canWrite: boolean;
}) {
  const t = useTranslations('finance.remittances');
  const tStage = useTranslations('finance.remittances.stage');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();
  const errorMessage = useFinanceError();
  const searchParams = useSearchParams();

  /*
   * « Caisse fermée » se lit de la plus ANCIENNE à la plus récente (D-243,
   * décision 7) : c'est la file de ce que l'agence doit réceptionner. Le même
   * défaut est posé côté serveur, dans la page : les deux doivent s'accorder,
   * sinon l'en-tête de colonne annonce un ordre que les lignes ne suivent pas.
   */
  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'createdAt',
    defaultOrder: searchParams.get('stage') === 'closed' ? 'asc' : 'desc',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [creating, setCreating] = useState(false);
  const [, startTransition] = useTransition();

  const remove = (row: RemittanceListItem) => {
    startTransition(async () => {
      const result = await deleteRemittanceAction(row.code);
      if (result.ok) toast.success(t('deleted'));
      else toast.error(errorMessage(result));
    });
  };

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        /*
          LE STATUT AFFICHÉ — D-243. Le filtre porte sur `stage`, servi par
          l'API, et non plus sur l'état en base : « ouverte » et « fermée »
          sont toutes deux des brouillons, que `status` ne distingue pas.

          Annulée : écrite par la seule migration de reprise (D-115), jamais
          par un geste d'écran. Elle se filtre parce qu'elle se lit.

          Un ancien lien `?status=…` reste honoré par la page tant qu'aucun
          statut n'est choisi ici ; dès qu'il l'est, la page ne lit plus que
          `stage`, pour que deux filtres ne se contredisent pas.

          Choix multiple (D-255) : plusieurs statuts affichés, le OU de leurs
          conditions côté API. Livreur, ville et agence de même.
        */
        id: 'stage',
        multiple: true,
        label: t('filters.stage'),
        value: param('stage') ?? ALL_VALUE,
        options: REMITTANCE_STAGES.map((stage) => ({ value: stage, label: tStage(stage) })),
        onChange: (value) => setFilter('stage', value === ALL_VALUE ? undefined : value),
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
        id: 'cityId',
        multiple: true,
        label: t('filters.city'),
        value: param('cityId') ?? ALL_VALUE,
        options: cities.map((city) => ({ value: city.id, label: city.name })),
        cities,
        onChange: (value) => setFilter('cityId', value === ALL_VALUE ? undefined : value),
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
    [agencies, cities, couriers, param, setFilter, t, tStage],
  );

  const columns: DataTableColumns<RemittanceListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('code', {
          header: t('columns.code'),
          cell: (info) => (
            <CellStack label={info.getValue()} sub={info.row.original.cityName} mono />
          ),
        }),

        helper.accessor('courierName', {
          header: t('columns.courier'),
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),

        helper.accessor('parcelCount', {
          header: t('columns.parcels'),
          meta: { align: 'end', hideBelow: 'sm' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),

        /*
          ESPÈCES ET CHÈQUES, SÉPARÉMENT — chantier « Chèques », point 1.

          Une seule colonne « montant » mélangeait les deux, et l'agence ne
          pouvait pas savoir combien de billets elle allait compter avant
          d'ouvrir le bon.
        */
        helper.accessor('totalCash', {
          header: t('columns.cash'),
          sortFn: 'basic',
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => <CellNumber value={format.number(info.getValue() / 100, 'currency')} />,
        }),

        helper.accessor('totalCheque', {
          header: t('columns.cheque'),
          sortFn: 'basic',
          meta: { align: 'end', hideBelow: 'lg' },
          cell: (info) => {
            const amount = info.getValue();
            if (amount === 0) return <CellMuted>—</CellMuted>;

            return (
              <CellStack
                label={format.number(amount / 100, 'currency')}
                sub={t('chequeCount', { count: String(info.row.original.chequeCount) })}
              />
            );
          },
        }),

        helper.accessor('amount', {
          header: t('columns.amount'),
          meta: { align: 'end' },
          cell: (info) => <CellNumber value={format.number(info.getValue() / 100, 'currency')} />,
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

        /*
          Quatre statuts de caisse, plus « annulée » — D-243. Le badge lit
          `stage` tel que l'API le sert : l'écran n'en déduit aucun, et la
          liste, la fiche et l'espace livreur passent par le même composant.
          La colonne garde l'identifiant `status` : c'est la clé de tri que
          l'API connaît.
        */
        helper.accessor('status', {
          header: t('columns.state'),
          meta: { width: 'w-36' },
          cell: (info) => <RemittanceStageBadge stage={info.row.original.stage} />,
        }),

        helper.display({
          id: 'actions',
          header: '',
          meta: { isActions: true, align: 'end', width: 'w-16' },
          cell: (info) => {
            const row = info.row.original;

            return (
              <RowActions>
                <DropdownMenuItem onClick={() => router.push(`/remittances/${row.code}`)}>
                  <Eye className="size-4" />
                  {t('open')}
                </DropdownMenuItem>
                {canWrite ? (
                  <>
                    <DropdownMenuSeparator />
                    {/* Une recette confirmée est une preuve : elle reste. Un
                        bon annulé aussi — il porte sa ligne de journal. */}
                    <DropdownMenuItem
                      variant="destructive"
                      disabled={row.status !== 'draft'}
                      onClick={() => remove(row)}
                    >
                      <Trash2 className="size-4" />
                      {tCommon('delete')}
                    </DropdownMenuItem>
                  </>
                ) : null}
              </RowActions>
            );
          },
        }),
      ]),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [canWrite, format, router, t, tCommon],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(row) => router.push(`/remittances/${row.code}`)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="remittances"
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            onReset={() => {
              setSearch('');
              reset();
            }}
          >
            {canWrite ? (
              <Button size="sm" className="gap-1.5" onClick={() => setCreating(true)}>
                <Plus className="size-4" />
                <span className="hidden sm:inline">{t('new')}</span>
              </Button>
            ) : null}
          </DataTableToolbar>
        }
      />

      {creating ? (
        <RemittanceForm
          couriers={couriers}
          cities={cities}
          agencies={agencies}
          onDone={() => setCreating(false)}
          onCreated={(code) => router.push(`/remittances/${code}`)}
        />
      ) : null}
    </>
  );
}
