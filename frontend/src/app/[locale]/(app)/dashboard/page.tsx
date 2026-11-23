import {
  DASHBOARD_CITY_SORTS,
  DASHBOARD_CLIENT_RANKS,
  DASHBOARD_COURIER_SORTS,
  DASHBOARD_FAILED_STATUS_IDS,
  DASHBOARD_IN_PROGRESS_STATUS_IDS,
  DASHBOARD_SERIES_RANGES,
  type DashboardCitySort,
  type DashboardClientRank,
  type DashboardCourierSort,
  type DashboardFilters as Filters,
  type DashboardSeriesRange,
} from '@delivery/shared';
import type { Metadata } from 'next';
import { getFormatter, getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { Await } from '@/features/dashboard/await';
import { CityPerformance } from '@/features/dashboard/city-performance';
import { DashboardFilters } from '@/features/dashboard/dashboard-filters';
import { DelaysBlock } from '@/features/dashboard/delays-block';
import { EvolutionChart } from '@/features/dashboard/evolution-chart';
import { PrintDashboardButton } from '@/features/dashboard/export-menu';
import { FinanceKpis, FinanceSituation } from '@/features/dashboard/finance-block';
import { LatestParcels } from '@/features/dashboard/latest-parcels';
import { NewsBanner } from '@/features/dashboard/news-banner';
import { OpsAlerts } from '@/features/dashboard/ops-alerts';
import { ParcelKpis } from '@/features/dashboard/parcel-kpis';
import { RoutesBlock } from '@/features/dashboard/routes-block';
import { CardSkeleton, KpiGridSkeleton, TableSkeleton } from '@/features/dashboard/skeletons';
import { TopClients } from '@/features/dashboard/top-clients';
import { TopCouriers } from '@/features/dashboard/top-couriers';
import { WidgetBoundary } from '@/features/dashboard/widget-boundary';
import { getAnnouncementFeed } from '@/lib/api/announcements';
import {
  getDashboard,
  getDashboardAlerts,
  getDashboardCities,
  getDashboardClients,
  getDashboardCouriers,
  getDashboardDelays,
  getDashboardFinance,
  getDashboardRoutes,
  getDashboardSeries,
} from '@/lib/api/dashboard';
import { getAgencies, getCities, getClients, getCouriers } from '@/lib/api/directory';
import { getCurrentUser } from '@/lib/api/auth';
import {
  DASHBOARD_FILTER_KEYS,
  type DashboardFilterValues,
  effectivePeriod,
  periodSubtitle,
} from '@/lib/dashboard-period';
import {
  readDashboardFilterPreference,
  readPeriodPreference,
} from '@/lib/dashboard-period.server';
import { firstParam as first, type SearchParams } from '@/lib/list-query';
import { MerchantDashboard } from '@/features/merchant/merchant-dashboard';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('nav');
  return { title: t('dashboard') };
}

/** Une valeur d'URL parmi une liste fermée, sinon le défaut. */
function oneOf<T extends string>(value: string | undefined, allowed: readonly T[], fallback: T): T {
  return value && (allowed as readonly string[]).includes(value) ? (value as T) : fallback;
}

/** Une page d'URL : un entier ≥ 1, sinon la première. */
function pageOf(value: string | undefined): number {
  const page = Number(value ?? 1);
  return Number.isFinite(page) && page >= 1 ? Math.floor(page) : 1;
}

/** Ajoute à une requête d'URL les clés renseignées. */
function withFilters(base: URLSearchParams, keys: Record<string, string | undefined>) {
  const next = new URLSearchParams(base);
  for (const [key, value] of Object.entries(keys)) if (value) next.set(key, value);
  return next;
}

/**
 * LE TABLEAU DE BORD DE PILOTAGE — chantier « Dashboard de pilotage »
 * (`docs/chantiers/dashboard.md`). Même URL qu'avant, enrichi EN PLACE, dans
 * l'ordre de la spec § 11 : KPI · évolution + villes · livreurs · clients ·
 * finance · circuits + délais · alertes.
 *
 * **Chaque section se charge seule** (spec § 12). Les promesses sont créées
 * ici, une fois, et attendues DANS leur section (`<Await>` sous
 * `<WidgetBoundary>`) : le serveur diffuse les cartes au fil des agrégats, et
 * une API en panne sur un seul d'entre eux ne rend qu'une carte « indisponible ».
 * Seules la vue d'ensemble (pour l'en-tête) et les listes de filtres sont
 * attendues avant le premier rendu.
 *
 * Le test « chacun son tableau de bord » vit dans `(app)/layout.tsx` (L1) ;
 * le vendeur, lui, reste ici et voit SON contenu (V2 vendeur bloc 2, D-090).
 * Le bloc Finance n'est DEMANDÉ que si le compte porte `finance.read` (D-163).
 */
export default async function DashboardPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;

  const user = await getCurrentUser();
  if (user?.clientId) return <MerchantDashboard />;
  const canReadFinance = user?.permissions.includes('finance.read') ?? false;

  /*
   * LA PÉRIODE EFFECTIVE — chantier « Filtre de période ». L'URL l'emporte
   * (lien partagé, bouton Retour), sinon le dernier choix en cookie, sinon
   * « ce mois ». Résolue AVANT l'appel API pour que le premier pixel affiche la
   * bonne fenêtre.
   */
  const [rememberedPeriod, rememberedFilters] = await Promise.all([
    readPeriodPreference(),
    readDashboardFilterPreference(),
  ]);
  const period = effectivePeriod(first(params.period), rememberedPeriod.period);
  const customFrom = first(params.from) ?? rememberedPeriod.range?.from;
  const customTo = first(params.to) ?? rememberedPeriod.range?.to;

  /*
   * LES FILTRES COMPLÉMENTAIRES : l'URL l'emporte dès qu'elle en porte UN —
   * c'est la barre qui l'écrit, avec l'état complet — sinon la mémoire.
   */
  const urlCarriesFilters = DASHBOARD_FILTER_KEYS.some((key) => first(params[key]));
  const values: DashboardFilterValues = urlCarriesFilters
    ? Object.fromEntries(DASHBOARD_FILTER_KEYS.map((key) => [key, first(params[key])]))
    : rememberedFilters;

  const filters: Filters = {
    period,
    from: period === 'custom' ? customFrom : undefined,
    to: period === 'custom' ? customTo : undefined,
    ...values,
  };

  const range = oneOf<DashboardSeriesRange>(first(params.range), DASHBOARD_SERIES_RANGES, '30d');
  const courierSort = oneOf<DashboardCourierSort>(
    first(params.csort),
    DASHBOARD_COURIER_SORTS,
    'delivered',
  );
  const citySort = oneOf<DashboardCitySort>(first(params.vsort), DASHBOARD_CITY_SORTS, 'total');
  const clientRank = oneOf<DashboardClientRank>(
    first(params.krank),
    DASHBOARD_CLIENT_RANKS,
    'volume',
  );

  /* --- Les promesses, créées une fois, attendues dans leur section ----------- */
  const overview = getDashboard(filters);
  const series = getDashboardSeries(filters, range);
  const couriersPage = getDashboardCouriers(filters, {
    page: pageOf(first(params.cpage)),
    pageSize: 10,
    sort: courierSort,
  });
  const citiesPage = getDashboardCities(filters, {
    page: pageOf(first(params.vpage)),
    pageSize: 10,
    sort: citySort,
  });
  const clientsPage = getDashboardClients(filters, {
    page: pageOf(first(params.kpage)),
    pageSize: 10,
    rank: clientRank,
  });
  const finance = canReadFinance ? getDashboardFinance(filters) : null;
  const routes = getDashboardRoutes(filters);
  const delays = getDashboardDelays(filters);
  const alerts = getDashboardAlerts(filters);
  /* Les délais nourrissent aussi la famille Performance : une panne ne la vide pas. */
  const delaysOrNull = delays.catch(() => null);

  const [header, agencies, cities, couriers, clients, news, t, tWidget, tPeriod, format] =
    await Promise.all([
      /*
       * La vue d'ensemble donne la fenêtre RÉSOLUE à l'en-tête et à la barre de
       * filtres. Si elle échoue, l'en-tête se contente de la période demandée
       * et la section KPI affiche son erreur — le reste de la page vit.
       */
      overview.catch(() => null),
      getAgencies(),
      getCities(),
      getCouriers(),
      getClients(),
      /*
       * Le bandeau « Nouveautés » est un CONFORT : une API muette sur ce point
       * ne doit pas vider le tableau de bord, elle doit juste faire disparaître
       * le bandeau (C7 bloc 3).
       */
      getAnnouncementFeed(3).catch(() => []),
      getTranslations('dashboard'),
      getTranslations('dashboard.widget'),
      getTranslations('dashboard.period'),
      getFormatter(),
    ]);

  const window = header?.window ?? {
    period,
    from: customFrom ?? '',
    to: customTo ?? '',
    granularity: 'day' as const,
  };

  /*
   * LES LIENS DE DÉTAIL reprennent la période et les filtres : ce qu'on voit
   * est ce qu'on ouvre. La liste des colis nomme la ville `destinationCityId`.
   */
  const periodQuery = new URLSearchParams({ period: window.period });
  if (window.period === 'custom' && window.from && window.to) {
    periodQuery.set('from', window.from);
    periodQuery.set('to', window.to);
  }
  const parcelsQuery = withFilters(periodQuery, {
    clientId: values.clientId,
    courierId: values.courierId,
    destinationCityId: values.cityId,
  });
  const parcelsHref = (extra: Record<string, string> = {}) =>
    `/parcels?${withFilters(parcelsQuery, extra).toString()}`;
  const detailQuery = withFilters(periodQuery, {
    agencyId: values.agencyId,
    cityId: values.cityId,
    courierId: values.courierId,
  });
  const financeQuery = withFilters(periodQuery, {
    agencyId: values.agencyId,
    courierId: values.courierId,
  });
  const routesQuery = new URLSearchParams();
  if (values.cityId) routesQuery.set('cityId', values.cityId);
  const routesHref = (status?: string) => {
    const next = new URLSearchParams(routesQuery);
    if (status) next.set('status', status);
    const query = next.toString();
    return query ? `/routes?${query}` : '/routes';
  };
  const lateParcelsHref = parcelsHref({ statusId: '4', financialStatus: 'encaisse' });
  const financeLinks = {
    finance: `/finance?${financeQuery.toString()}`,
    remittances: `/remittances?${financeQuery.toString()}`,
    payments: '/payments',
    transferOrders: '/finance',
    lateParcels: lateParcelsHref,
  };

  const day = (value: string) => format.dateTime(new Date(`${value}T12:00:00Z`), 'date');
  const previousHint = header?.previousWindow
    ? t('kpi.vsPrevious', {
        from: day(header.previousWindow.from),
        to: day(header.previousWindow.to),
      })
    : '';

  /* L'export rejoue EXACTEMENT ce que l'écran a demandé (spec § 16). */
  const exportQuery: Record<string, string> = Object.fromEntries(
    Object.entries({
      period: filters.period,
      from: filters.from,
      to: filters.to,
      agencyId: values.agencyId,
      cityId: values.cityId,
      courierId: values.courierId,
      clientId: values.clientId,
    }).filter((entry): entry is [string, string] => typeof entry[1] === 'string'),
  );

  return (
    <>
      <PageHeader
        title={t('title')}
        subtitle={window.from ? periodSubtitle(window, tPeriod, format) : tPeriod(window.period)}
        actions={<PrintDashboardButton />}
      />

      <div className="flex flex-col gap-6">
        <NewsBanner items={news} />

        <DashboardFilters
          agencies={agencies}
          cities={cities}
          couriers={couriers}
          clients={clients}
          period={window.period}
          from={window.from}
          to={window.to}
          values={values}
        />

        {/* SECTION 1 — KPI principaux : colis, performance, finance */}
        <WidgetBoundary title={tWidget('kpis')} fallback={<KpiGridSkeleton count={8} />}>
          <Await promise={Promise.all([overview, delaysOrNull])}>
            {([data, delaysData]) => (
              <ParcelKpis overview={data} delays={delaysData} parcelsHref={parcelsHref} />
            )}
          </Await>
        </WidgetBoundary>
        {finance ? (
          <WidgetBoundary title={tWidget('finance')} fallback={<KpiGridSkeleton count={6} />}>
            <Await promise={finance}>
              {(data) => <FinanceKpis finance={data} previousHint={previousHint} links={financeLinks} />}
            </Await>
          </WidgetBoundary>
        ) : null}

        {/* SECTION 2 — Évolution des colis + délais, puis performance par ville */}
        <div className="print-stack grid gap-6 lg:grid-cols-3">
          <WidgetBoundary
            title={tWidget('evolution')}
            fallback={<CardSkeleton className="lg:col-span-2" />}
          >
            <Await promise={series}>{(data) => <EvolutionChart series={data} />}</Await>
          </WidgetBoundary>
          <WidgetBoundary title={tWidget('delays')} fallback={<CardSkeleton />}>
            <Await promise={delays}>{(data) => <DelaysBlock delays={data} />}</Await>
          </WidgetBoundary>
        </div>
        <WidgetBoundary
          title={tWidget('cities')}
          fallback={
            <div className="print-stack grid gap-6 lg:grid-cols-3">
              <TableSkeleton className="lg:col-span-2" />
              <CardSkeleton />
            </div>
          }
        >
          <Await promise={Promise.all([citiesPage, overview])}>
            {([page, data]) => (
              <CityPerformance
                page={page}
                sort={citySort}
                breakdown={data.byCity}
                parcelsQuery={parcelsQuery.toString()}
                exportQuery={exportQuery}
              />
            )}
          </Await>
        </WidgetBoundary>

        {/* SECTION 3 — Top livreurs */}
        <WidgetBoundary title={tWidget('couriers')} fallback={<TableSkeleton rows={10} />}>
          <Await promise={couriersPage}>
            {(page) => (
              <TopCouriers
                page={page}
                sort={courierSort}
                hasPrevious={header?.previousWindow !== null}
                exportQuery={exportQuery}
              />
            )}
          </Await>
        </WidgetBoundary>

        {/* SECTION 4 — Top clients */}
        <WidgetBoundary
          title={tWidget('clients')}
          fallback={
            <div className="print-stack grid gap-6 lg:grid-cols-3">
              <TableSkeleton rows={10} className="lg:col-span-2" />
              <CardSkeleton body="h-28" />
            </div>
          }
        >
          <Await promise={clientsPage}>
            {(data) => (
              <TopClients
                data={data}
                detailQuery={detailQuery.toString()}
                exportQuery={exportQuery}
              />
            )}
          </Await>
        </WidgetBoundary>

        {/* SECTIONS 5 à 7 — Finance, circuits, alertes */}
        <div className="print-stack grid gap-6 lg:grid-cols-3">
          {finance ? (
            <WidgetBoundary title={tWidget('situation')} fallback={<CardSkeleton body="h-72" />}>
              <Await promise={finance}>
                {(data) => <FinanceSituation finance={data} links={financeLinks} />}
              </Await>
            </WidgetBoundary>
          ) : null}
          <WidgetBoundary title={tWidget('routes')} fallback={<CardSkeleton body="h-72" />}>
            <Await promise={routes}>
              {(data) => (
                <RoutesBlock
                  routes={data}
                  links={{ open: routesHref(), closed: routesHref('closed') }}
                />
              )}
            </Await>
          </WidgetBoundary>
          <WidgetBoundary title={tWidget('alerts')} fallback={<CardSkeleton body="h-72" />}>
            <Await promise={Promise.all([alerts, overview])}>
              {([data, overviewData]) => (
                <OpsAlerts
                  alerts={data}
                  legacy={overviewData.alerts}
                  links={{
                    parcelsQuery: parcelsQuery.toString(),
                    failedStatusIds: DASHBOARD_FAILED_STATUS_IDS.join(','),
                    openRoutes: routesHref(),
                    stuckParcels: `/parcels?${withFilters(new URLSearchParams({ period: 'all' }), {
                      clientId: values.clientId,
                      courierId: values.courierId,
                      destinationCityId: values.cityId,
                      statusIds: DASHBOARD_IN_PROGRESS_STATUS_IDS.join(','),
                      sort: 'statusAt',
                      order: 'asc',
                    }).toString()}`,
                    codNotRemitted: `/remittances?${financeQuery.toString()}`,
                    cashVariances: `/remittances?${withFilters(financeQuery, { status: 'confirmed' }).toString()}`,
                  }}
                />
              )}
            </Await>
          </WidgetBoundary>
        </div>

        <WidgetBoundary title={tWidget('latest')} fallback={<TableSkeleton rows={10} />}>
          <Await promise={overview}>
            {(data) => <LatestParcels parcels={data.latest} seeAllHref={parcelsHref()} />}
          </Await>
        </WidgetBoundary>
      </div>
    </>
  );
}
