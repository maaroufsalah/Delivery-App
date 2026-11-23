import type { DashboardFilters as Filters } from '@delivery/shared';
import type { Metadata } from 'next';
import { getFormatter, getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { BackButton } from '@/components/back-button';
import { PageHeader } from '@/components/layout/page-header';
import { Button } from '@/components/ui/button';
import { ClientDetail } from '@/features/dashboard/client-detail';
import { Link } from '@/i18n/navigation';
import { getDashboardClient } from '@/lib/api/dashboard';
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
import { ApiError } from '@/lib/http';
import { firstParam as first, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('dashboard.clientDetail');
  return { title: t('title') };
}

/**
 * LE DÉTAIL D'UN CLIENT, VU DU TABLEAU DE BORD — chantier « Dashboard de
 * pilotage », D-162. Une page et non un modal : D-018 interdit à un composant
 * client d'appeler l'API, et une page porte ses filtres dans son URL — les
 * mêmes que le tableau de bord, lus de la même façon (URL, sinon mémoire).
 */
export default async function DashboardClientPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<SearchParams>;
}) {
  const [{ id }, query] = await Promise.all([params, searchParams]);

  const [rememberedPeriod, rememberedFilters] = await Promise.all([
    readPeriodPreference(),
    readDashboardFilterPreference(),
  ]);
  const period = effectivePeriod(first(query.period), rememberedPeriod.period);
  const urlCarriesFilters = DASHBOARD_FILTER_KEYS.some((key) => first(query[key]));
  const values: DashboardFilterValues = urlCarriesFilters
    ? Object.fromEntries(DASHBOARD_FILTER_KEYS.map((key) => [key, first(query[key])]))
    : rememberedFilters;

  const filters: Filters = {
    period,
    from: period === 'custom' ? (first(query.from) ?? rememberedPeriod.range?.from) : undefined,
    to: period === 'custom' ? (first(query.to) ?? rememberedPeriod.range?.to) : undefined,
    ...values,
    // Le client de la page l'emporte sur le filtre global : c'est LUI qu'on lit.
    clientId: id,
  };

  const detail = await getDashboardClient(id, filters).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  const [t, tPeriod, format] = await Promise.all([
    getTranslations('dashboard.clientDetail'),
    getTranslations('dashboard.period'),
    getFormatter(),
  ]);

  // Les filtres de période repris tels quels sur les liens sortants.
  const search = new URLSearchParams();
  if (period) search.set('period', period);
  if (filters.from) search.set('from', filters.from);
  if (filters.to) search.set('to', filters.to);
  const back = search.toString();

  const parcels = new URLSearchParams(search);
  parcels.set('clientId', id);
  if (values.cityId) parcels.set('destinationCityId', values.cityId);

  return (
    <>
      <PageHeader
        title={detail.clientName}
        subtitle={`${t('subtitle')} · ${periodSubtitle(detail.window, tPeriod, format)}`}
        actions={
          <div className="flex items-center gap-2">
            <Button asChild variant="outline" size="sm">
              <Link href={`/clients/${id}`}>{t('adminSheet')}</Link>
            </Button>
            <BackButton fallbackHref={back ? `/dashboard?${back}` : '/dashboard'} />
          </div>
        }
      />
      <ClientDetail detail={detail} parcelsHref={`/parcels?${parcels.toString()}`} />
    </>
  );
}
