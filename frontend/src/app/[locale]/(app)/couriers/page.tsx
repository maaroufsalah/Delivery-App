import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { CouriersScreen } from '@/features/accounts/couriers-screen';
import { getCourierPage } from '@/lib/api/accounts';
import { getAgencies, getCities, getPendingCounts } from '@/lib/api/directory';
import { activeParam, firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

/**
 * Le filtre de validation compte TROIS états depuis C13 — à valider, validée,
 * refusée. Une valeur inconnue dans l'URL retombe sur « aucun filtre » plutôt
 * que de partir telle quelle vers l'API.
 */
function validatedParam(value: string | undefined): 'true' | 'false' | 'rejected' | undefined {
  return value === 'true' || value === 'false' || value === 'rejected' ? value : undefined;
}

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('accounts.couriers');
  return { title: t('title') };
}

export default async function CouriersPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('accounts.couriers');

  const [page, cities, agencies, pending] = await Promise.all([
    getCourierPage({
      ...listQueryFrom(params, { sort: 'lastName' }),
      isActive: activeParam(params),
      agencyId: firstParam(params.agencyId),
      cityId: firstParam(params.cityId),
      validated: validatedParam(firstParam(params.validated)),
    }),
    getCities(),
    getAgencies(),
    getPendingCounts().catch(() => null),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <CouriersScreen
        page={page}
        cities={cities}
        agencies={agencies}
        pendingCount={pending?.couriers}
      />
    </>
  );
}
