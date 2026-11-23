import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { DistrictsScreen } from '@/features/referential/districts-screen';
import { getCities } from '@/lib/api/directory';
import { getDistrictPage } from '@/lib/api/referential';
import { activeParam, firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export default async function DistrictsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('referential.districts');

  const [page, cities] = await Promise.all([
    getDistrictPage({
      ...listQueryFrom(params, { sort: 'name' }),
      isActive: activeParam(params),
      cityId: firstParam(params.cityId),
    }),
    getCities(),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <DistrictsScreen page={page} cities={cities} />
    </>
  );
}
