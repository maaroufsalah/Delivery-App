import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { ZonesScreen } from '@/features/referential/zones-screen';
import { getAllCities } from '@/lib/api/directory';
import { getZonePage } from '@/lib/api/referential';
import { activeParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export default async function ZonesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const t = await getTranslations('referential.zones');

  const [page, cities] = await Promise.all([
    getZonePage({ ...listQueryFrom(params, { sort: 'name' }), isActive: activeParam(params) }),
    // Toutes les villes, désactivées comprises : une ville inactive peut rester
    // rattachée à une zone, et la décocher par accident serait une régression.
    getAllCities(),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <ZonesScreen page={page} cities={cities} />
    </>
  );
}
