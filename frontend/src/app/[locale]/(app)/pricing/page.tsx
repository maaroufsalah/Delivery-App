import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { PricingScreen } from '@/features/referential/pricing-screen';
import { getCities, getZones } from '@/lib/api/directory';
import { getPricingByOrigin } from '@/lib/api/referential';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

/**
 * Grille tarifaire PAR ORIGINE — chantier « Tarifs par origine ».
 *
 * L'URL porte l'origine (`origin`, défaut : la ville du siège), la page, le
 * tri, la recherche et la zone. Une seule origine descend à la fois, une page
 * à la fois : la matrice pleine (325 × 325) n'existe plus côté écran.
 */
export default async function PricingPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('referential.pricing');

  const [page, zones, cities] = await Promise.all([
    getPricingByOrigin({
      ...listQueryFrom(params, { sort: 'name', pageSize: 50 }),
      originCityId: firstParam(params.origin),
      zoneId: firstParam(params.zoneId),
    }),
    getZones(),
    getCities(),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      {/* La clé remonte l'écran à chaque origine : ses modifications en cours
          appartiennent à UNE grille et ne doivent pas glisser sur une autre. */}
      <PricingScreen key={page.origin.id} page={page} zones={zones} cities={cities} />
    </>
  );
}
