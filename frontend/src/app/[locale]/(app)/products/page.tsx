import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { ProductsScreen } from '@/features/warehouse/products-screen';
import { getCurrentUser } from '@/lib/api/auth';
import { getAgencies, getClients } from '@/lib/api/directory';
import { getProductAttributes, getProductPage } from '@/lib/api/warehouse';
import { activeParam, firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('warehouse.products');
  return { title: t('title') };
}

export default async function ProductsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('warehouse.products');

  const [page, clients, agencies, attributes, user] = await Promise.all([
    getProductPage({
      ...listQueryFrom(params, { sort: 'name' }),
      isActive: activeParam(params),
      clientId: firstParam(params.clientId),
      agencyId: firstParam(params.agencyId),
    }),
    getClients(),
    getAgencies(),
    getProductAttributes(),
    getCurrentUser(),
  ]);

  return (
    <>
      {/* Lot V, V2 : le vendeur lit SON catalogue, pas celui « de chaque e-commerçant ». */}
      <PageHeader
        title={t('title')}
        subtitle={user?.clientId ? t('subtitleMerchant') : t('subtitle')}
      />
      <ProductsScreen
        page={page}
        clients={clients}
        agencies={agencies}
        attributes={attributes}
        // Un compte e-commerçant ne choisit pas de client : la portée l'a fixé.
        lockedClientId={
          user?.roles.includes('MERCHANT') ? (clients[0]?.id ?? undefined) : undefined
        }
      />
    </>
  );
}
