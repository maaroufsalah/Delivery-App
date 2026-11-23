import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { AgenciesScreen } from '@/features/accounts/agencies-screen';
import { getAgencyPage } from '@/lib/api/accounts';
import { getAllCities } from '@/lib/api/directory';
import { activeParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('accounts.agencies');
  return { title: t('title') };
}

export default async function AgenciesPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('accounts.agencies');

  const [page, cities] = await Promise.all([
    getAgencyPage({ ...listQueryFrom(params, { sort: 'name' }), isActive: activeParam(params) }),
    // Villes désactivées comprises : une agence peut rester rattachée à une
    // ville retirée du référentiel, et la perdre à l'enregistrement serait
    // une régression silencieuse.
    getAllCities(),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <AgenciesScreen page={page} cities={cities} />
    </>
  );
}
