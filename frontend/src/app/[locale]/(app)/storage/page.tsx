import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { StorageScreen } from '@/features/warehouse/storage-screen';
import { getCurrentUser } from '@/lib/api/auth';
import { getAgencies, getClients } from '@/lib/api/directory';
import { getProductPage, getStorageRequestPage } from '@/lib/api/warehouse';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('warehouse.storage');
  return { title: t('title') };
}

/** Vue STAFF : la même liste que `/storage-requests`, avec la décision. */
export default async function StoragePage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('warehouse.storage');
  const status = firstParam(params.status);

  const [page, clients, agencies, products, user] = await Promise.all([
    getStorageRequestPage({
      ...listQueryFrom(params, { sort: 'requestedAt', order: 'desc' }),
      clientId: firstParam(params.clientId),
      agencyId: firstParam(params.agencyId),
      status,
    }),
    getClients(),
    getAgencies(),
    getProductPage({ page: 1, pageSize: 200, sort: 'name' }),
    getCurrentUser(),
  ]);

  const merchant = user?.roles.includes('MERCHANT') ?? false;

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <StorageScreen
        page={page}
        clients={clients}
        agencies={agencies}
        products={products.items}
        lockedClientId={merchant ? (clients[0]?.id ?? undefined) : undefined}
      />
    </>
  );
}
