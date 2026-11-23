import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { RoutesTable } from '@/features/routes/routes-table';
import { getCities, getCouriers } from '@/lib/api/directory';
import { getRouteStats, getRoutes } from '@/lib/api/routes';
import { firstParam as first, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('routes');
  return { title: t('title') };
}

export default async function RoutesPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;

  // La page demandée, et les compteurs de la sélection entière (B-5, C7 bloc 4).
  const filters = {
    search: first(params.search),
    status: first(params.status),
    cityId: first(params.cityId),
  };

  const [page, stats, cities, couriers, t] = await Promise.all([
    // Les plus récents en premier (chantier « Tri par défaut », 11/11/2026).
    getRoutes({
      ...listQueryFrom(params, { sort: 'createdAt', order: 'desc' }),
      ...filters,
      recent: 'true',
    }),
    getRouteStats(filters),
    getCities(),
    getCouriers(),
    getTranslations('routes'),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <RoutesTable page={page} stats={stats} cities={cities} couriers={couriers} />
    </>
  );
}
