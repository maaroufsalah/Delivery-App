import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { CitiesScreen } from '@/features/referential/cities-screen';
import { getZones } from '@/lib/api/directory';
import { getCityPage } from '@/lib/api/referential';
import { activeParam, firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

/**
 * Villes — l'URL porte l'état de la liste (page, tri, filtres), et c'est ELLE
 * que le serveur relit pour demander la bonne page à l'API. L'écran est donc
 * partageable par lien et survit au rechargement.
 */
export default async function CitiesPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('referential.cities');

  const [page, zones] = await Promise.all([
    getCityPage({
      ...listQueryFrom(params, { sort: 'name' }),
      isActive: activeParam(params),
      zoneId: firstParam(params.zoneId),
    }),
    getZones(),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <CitiesScreen page={page} zones={zones} />
    </>
  );
}
