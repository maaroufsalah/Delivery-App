import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { OrdersScreen } from '@/features/warehouse/orders-screen';
import { getCurrentUser } from '@/lib/api/auth';
import { getCities, getClients, getDistricts } from '@/lib/api/directory';
import { getOrderPage, getProductPage } from '@/lib/api/warehouse';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('warehouse.orders');
  return { title: t('title') };
}

/**
 * Commandes — visibles du STAFF comme de l'E-COMMERÇANT.
 *
 * C'est lui qui les saisit et les importe ; la portée serveur les lui borne, et
 * le sélecteur de client disparaît de son écran.
 */
export default async function OrdersPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('warehouse.orders');

  const [page, clients, cities, districts, products, user] = await Promise.all([
    getOrderPage({
      ...listQueryFrom(params, { sort: 'createdAt', order: 'desc' }),
      clientId: firstParam(params.clientId),
      status: firstParam(params.status),
      source: firstParam(params.source),
    }),
    getClients(),
    getCities(),
    getDistricts(),
    getProductPage({ page: 1, pageSize: 200, sort: 'name' }),
    getCurrentUser(),
  ]);

  const merchant = user?.roles.includes('MERCHANT') ?? false;

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <OrdersScreen
        page={page}
        clients={clients}
        cities={cities}
        districts={districts}
        products={products.items}
        lockedClientId={merchant ? (clients[0]?.id ?? undefined) : undefined}
      />
    </>
  );
}
