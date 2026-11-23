import type {
  DashboardAlerts as DashboardAlertsPayload,
  DashboardCityPoint,
  DashboardCityRow,
  DashboardCitySort,
  DashboardClientDetail,
  DashboardClientRank,
  DashboardClients,
  DashboardCourierRow,
  DashboardCourierSort,
  DashboardDayPoint,
  DashboardDelays,
  DashboardFilters,
  DashboardFinance,
  DashboardOverview,
  DashboardOverviewAlerts,
  DashboardRoutes,
  DashboardSeries,
  DashboardSeriesRange,
  Paginated,
} from '@delivery/shared';
import { apiFetch } from '@/lib/http';

/**
 * Tableau de bord — chantier « Dashboard de pilotage ».
 *
 * UNE fonction par endpoint, toutes bornées par les mêmes filtres globaux
 * (`DashboardFilters`). Les contrats vivent dans `@delivery/shared`
 * (`types/dashboard.ts`) : l'API les produit, l'écran les lit, aucun des deux
 * ne les redéclare.
 */

export type { DashboardCityPoint, DashboardDayPoint, DashboardOverview as Dashboard };
export type DashboardAlerts = DashboardOverviewAlerts;

/** Les filtres, tels que l'URL les porte : rien d'autre n'est envoyé. */
function filterQuery(filters: DashboardFilters): Record<string, string | undefined> {
  return {
    period: filters.period,
    from: filters.period === 'custom' ? filters.from : undefined,
    to: filters.period === 'custom' ? filters.to : undefined,
    agencyId: filters.agencyId,
    cityId: filters.cityId,
    courierId: filters.courierId,
    clientId: filters.clientId,
  };
}

/** `GET /dashboard` — KPI, comparaison, série, villes, derniers colis, alertes. */
export async function getDashboard(filters: DashboardFilters): Promise<DashboardOverview> {
  return apiFetch<DashboardOverview>('/dashboard', { query: filterQuery(filters) });
}

/** `GET /dashboard/series?range=` — l'évolution des colis, ancrée sur la période. */
export async function getDashboardSeries(
  filters: DashboardFilters,
  range: DashboardSeriesRange,
): Promise<DashboardSeries> {
  return apiFetch<DashboardSeries>('/dashboard/series', {
    query: { ...filterQuery(filters), range },
  });
}

/** Une page d'un classement : la table est paginée et triée EN BASE. */
export interface RankingPage {
  page: number;
  pageSize: number;
}

/** `GET /dashboard/couriers` — Top livreurs. */
export async function getDashboardCouriers(
  filters: DashboardFilters,
  paging: RankingPage & { sort: DashboardCourierSort },
): Promise<Paginated<DashboardCourierRow>> {
  return apiFetch<Paginated<DashboardCourierRow>>('/dashboard/couriers', {
    query: {
      ...filterQuery(filters),
      page: String(paging.page),
      pageSize: String(paging.pageSize),
      sort: paging.sort,
    },
  });
}

/** `GET /dashboard/cities` — performance par ville de destination. */
export async function getDashboardCities(
  filters: DashboardFilters,
  paging: RankingPage & { sort: DashboardCitySort },
): Promise<Paginated<DashboardCityRow>> {
  return apiFetch<Paginated<DashboardCityRow>>('/dashboard/cities', {
    query: {
      ...filterQuery(filters),
      page: String(paging.page),
      pageSize: String(paging.pageSize),
      sort: paging.sort,
    },
  });
}

/** `GET /dashboard/clients` — Top clients, trois classements, le plus actif. */
export async function getDashboardClients(
  filters: DashboardFilters,
  paging: RankingPage & { rank: DashboardClientRank },
): Promise<DashboardClients> {
  return apiFetch<DashboardClients>('/dashboard/clients', {
    query: {
      ...filterQuery(filters),
      page: String(paging.page),
      pageSize: String(paging.pageSize),
      rank: paging.rank,
    },
  });
}

/** `GET /dashboard/finance` — gardé par `finance.read` (D-163) : n'appeler que si la clé est portée. */
export async function getDashboardFinance(filters: DashboardFilters): Promise<DashboardFinance> {
  return apiFetch<DashboardFinance>('/dashboard/finance', { query: filterQuery(filters) });
}

/** `GET /dashboard/routes` — performance des circuits de livraison. */
export async function getDashboardRoutes(filters: DashboardFilters): Promise<DashboardRoutes> {
  return apiFetch<DashboardRoutes>('/dashboard/routes', { query: filterQuery(filters) });
}

/** `GET /dashboard/delays` — délais ramassé → livré. */
export async function getDashboardDelays(filters: DashboardFilters): Promise<DashboardDelays> {
  return apiFetch<DashboardDelays>('/dashboard/delays', { query: filterQuery(filters) });
}

/** `GET /dashboard/alerts` — les sept familles d'alertes et leurs seuils. */
export async function getDashboardAlerts(filters: DashboardFilters): Promise<DashboardAlertsPayload> {
  return apiFetch<DashboardAlertsPayload>('/dashboard/alerts', { query: filterQuery(filters) });
}

/** `GET /dashboard/clients/:id` — le détail d'un client sur la période. */
export async function getDashboardClient(
  clientId: string,
  filters: DashboardFilters,
): Promise<DashboardClientDetail> {
  return apiFetch<DashboardClientDetail>(`/dashboard/clients/${encodeURIComponent(clientId)}`, {
    query: filterQuery(filters),
  });
}
