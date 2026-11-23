'use client';

import {
  CLAIM_STATUSES,
  type ClaimCounters,
  type ClaimListItem,
  type ClaimType,
  type Client,
  type Paginated,
} from '@delivery/shared';
import { AlertTriangle, CircleDot, Clock, Plus } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { DataTable } from '@/components/data-table/data-table';
import {
  ALL_VALUE,
  DataTableToolbar,
  type FilterSelect,
} from '@/components/data-table/data-table-toolbar';
import { useServerTable, useUrlSearch } from '@/components/data-table/use-server-table';
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { Button } from '@/components/ui/button';
import { useClaimColumns } from '@/features/claims/columns';
import { Link, useRouter } from '@/i18n/navigation';

/**
 * Liste des réclamations — C7 bloc 2.
 *
 * Paginée par le SERVEUR dès le premier jour (B-5) : une liste de demandes
 * grossit sans limite, et charger cent lignes pour en montrer vingt-cinq était
 * déjà l'arbitrage qu'on regrette ailleurs.
 */
export function ClaimsScreen({
  page,
  counters,
  types,
  clients,
  showClient,
}: {
  page: Paginated<ClaimListItem>;
  counters: ClaimCounters;
  types: ClaimType[];
  clients: Client[];
  /** Faux pour un e-commerçant : il ne voit que les siennes. */
  showClient: boolean;
}) {
  const t = useTranslations('claims');
  const tStatus = useTranslations('claims.status');
  const router = useRouter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'createdAt',
    defaultOrder: 'desc',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const columns = useClaimColumns({ showClient });

  const selects: FilterSelect[] = [
    {
      id: 'status',
      multiple: true,
      label: t('filters.status'),
      value: param('status') ?? ALL_VALUE,
      options: CLAIM_STATUSES.map((status) => ({ value: status, label: tStatus(status) })),
      onChange: (value) => setFilter('status', value === ALL_VALUE ? undefined : value),
    },
    {
      id: 'typeId',
      multiple: true,
      label: t('filters.type'),
      value: param('typeId') ?? ALL_VALUE,
      options: types.map((type) => ({ value: type.id, label: type.label })),
      onChange: (value) => setFilter('typeId', value === ALL_VALUE ? undefined : value),
    },
  ];

  // Le filtre par e-commerçant n'a de sens que pour le staff : la portée
  // serveur borne déjà l'e-commerçant à ses propres dossiers.
  if (showClient) {
    selects.push({
      id: 'clientId',
      multiple: true,
      label: t('filters.client'),
      value: param('clientId') ?? ALL_VALUE,
      options: clients.map((client) => ({ value: client.id, label: client.name })),
      onChange: (value) => setFilter('clientId', value === ALL_VALUE ? undefined : value),
    });
  }

  return (
    <DataTable
      data={page.items}
      columns={columns}
      server={server}
      onRowClick={(row) => router.push(`/claims/${encodeURIComponent(row.code)}`)}
      kpis={
        <KpiGrid>
          <KpiCard
            label={t('kpi.open')}
            value={String(counters.open)}
            icon={CircleDot}
            tone="var(--chart-2)"
          />
          <KpiCard
            label={t('kpi.inProgress')}
            value={String(counters.inProgress)}
            icon={Clock}
            tone="var(--chart-1)"
          />
          <KpiCard
            label={t('kpi.late')}
            value={String(counters.late)}
            icon={AlertTriangle}
            tone={counters.late > 0 ? 'var(--destructive)' : undefined}
          />
          <KpiCard label={t('kpi.total')} value={String(page.total)} icon={CircleDot} />
        </KpiGrid>
      }
      toolbar={
        <DataTableToolbar
          search={search}
          onSearchChange={setSearch}
          searchPlaceholder={t('filters.searchPlaceholder')}
          selects={selects}
          onReset={reset}
          exportResource="claims"
        >
          {/* Le filtre « en retard » est un interrupteur, pas une liste : il
              n'a que deux états et mérite d'être atteignable en un clic. */}
          <Button
            type="button"
            variant={param('late') ? 'default' : 'outline'}
            size="sm"
            className="gap-1.5"
            onClick={() => setFilter('late', param('late') ? undefined : 'true')}
          >
            <AlertTriangle className="size-4" aria-hidden />
            {t('filters.late')}
          </Button>

          <Button asChild size="sm" className="gap-1.5">
            <Link href="/claims/new">
              <Plus className="size-4" aria-hidden />
              <span className="hidden sm:inline">{t('new')}</span>
            </Link>
          </Button>
        </DataTableToolbar>
      }
      emptyTitle={t('empty')}
      emptyHint={t('emptyHint')}
    />
  );
}
