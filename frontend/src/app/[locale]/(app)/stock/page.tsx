import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { StockScreen } from '@/features/warehouse/stock-screen';
import { getCurrentUser } from '@/lib/api/auth';
import { getAgencies, getClients } from '@/lib/api/directory';
import { getProductPage, getStockPage } from '@/lib/api/warehouse';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('warehouse.stock');
  return { title: t('title') };
}

export default async function StockPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const t = await getTranslations('warehouse.stock');
  const lowStock = firstParam(params.lowStock);

  const [page, clients, agencies, products, user] = await Promise.all([
    getStockPage({
      ...listQueryFrom(params, { sort: 'quantity' }),
      clientId: firstParam(params.clientId),
      agencyId: firstParam(params.agencyId),
      lowStock: lowStock === 'true' || lowStock === 'false' ? lowStock : undefined,
    }),
    getClients(),
    getAgencies(),
    // Les produits alimentent le dialogue d'ajustement : il faut pouvoir
    // désigner un produit qui n'a PAS encore de ligne de stock ici.
    getProductPage({ page: 1, pageSize: 200, sort: 'name' }),
    getCurrentUser(),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <StockScreen
        page={page}
        clients={clients}
        agencies={agencies}
        products={products.items}
        lockedClientId={
          user?.roles.includes('MERCHANT') ? (clients[0]?.id ?? undefined) : undefined
        }
        // Lot V, V2 : le stock physique est chez le transporteur, le vendeur consulte.
        canAdjust={!user?.clientId}
      />
    </>
  );
}
