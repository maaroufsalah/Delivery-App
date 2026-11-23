import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { PageHeader } from '@/components/layout/page-header';
import { CourierStockDetailScreen } from '@/features/courier-stock/courier-stock-detail';
import { getCourierStockDetail } from '@/lib/api/courier-stock';
import { ApiError } from '@/lib/http';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courierStock');
  return { title: t('title') };
}

/**
 * Stock d'un livreur — lot M, M2 (D-222). Atteint depuis la liste et depuis
 * la fiche livreur (« Stock »). Hors portée = introuvable (D-082).
 */
export default async function CourierStockDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<SearchParams>;
}) {
  const [{ id }, query] = await Promise.all([params, searchParams]);
  const t = await getTranslations('courierStock');

  const detail = await getCourierStockDetail(id, {
    ...listQueryFrom(query, { sort: 'lastMovementAt', order: 'desc' }),
    statusId: firstParam(query.statusId),
    clientId: firstParam(query.clientId),
    cityId: firstParam(query.cityId),
    routeId: firstParam(query.routeId),
  }).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  return (
    <>
      <PageHeader
        backHref="/courier-stock"
        title={t('detail.title', { name: detail.courier.name })}
        subtitle={t('detail.subtitle', {
          agency: detail.courier.agencyName ?? t('detail.noAgency'),
          phone: detail.courier.phone,
        })}
      />
      <CourierStockDetailScreen detail={detail} />
    </>
  );
}
