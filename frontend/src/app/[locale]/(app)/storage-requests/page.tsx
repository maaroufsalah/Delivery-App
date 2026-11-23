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
  return { title: t('clientTitle') };
}

/**
 * Vue CLIENT de la même liste — menu Demandes.
 *
 * Ni la route d'API ni l'écran ne changent : c'est la portée du compte qui
 * borne les lignes, et `warehouse.validate` qui ferme la décision. Dupliquer
 * l'écran aurait fait diverger les deux au premier ajout de colonne.
 */
export default async function StorageRequestsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('warehouse.storage');

  const [page, clients, agencies, products, user] = await Promise.all([
    getStorageRequestPage({
      ...listQueryFrom(params, { sort: 'requestedAt', order: 'desc' }),
      agencyId: firstParam(params.agencyId),
      status: firstParam(params.status),
    }),
    getClients(),
    getAgencies(),
    getProductPage({ page: 1, pageSize: 200, sort: 'name' }),
    getCurrentUser(),
  ]);

  const merchant = user?.roles.includes('MERCHANT') ?? false;

  return (
    <>
      <PageHeader title={t('clientTitle')} subtitle={t('clientSubtitle')} />
      <StorageScreen
        page={page}
        clients={clients}
        agencies={agencies}
        products={products.items}
        lockedClientId={merchant ? (clients[0]?.id ?? undefined) : undefined}
        clientView
      />
    </>
  );
}
