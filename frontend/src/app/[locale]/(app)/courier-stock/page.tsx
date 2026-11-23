import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { CourierStockScreen } from '@/features/courier-stock/courier-stock-screen';
import { getCourierStockPage } from '@/lib/api/courier-stock';
import { getAgencies } from '@/lib/api/directory';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courierStock');
  return { title: t('title') };
}

/** Stock Livreur — lot M, M1 (D-222). L'URL porte tout l'état de la liste. */
export default async function CourierStockPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('courierStock');

  const [page, agencies] = await Promise.all([
    getCourierStockPage({
      ...listQueryFrom(params, { sort: 'inHand', order: 'desc' }),
      agencyId: firstParam(params.agencyId),
      all: firstParam(params.all) === 'true' ? 'true' : undefined,
    }),
    getAgencies(),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <CourierStockScreen page={page} agencies={agencies} />
    </>
  );
}
