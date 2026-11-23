import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { PageHeader } from '@/components/layout/page-header';
import { StockDetailScreen } from '@/features/warehouse/stock-detail';
import { getCurrentUser } from '@/lib/api/auth';
import { getAgencies } from '@/lib/api/directory';
import { getProductPage, getStockItem } from '@/lib/api/warehouse';
import { ApiError } from '@/lib/http';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ id: string }>;
}): Promise<Metadata> {
  const { id } = await params;

  try {
    const stock = await getStockItem(id);
    return { title: stock.productName };
  } catch {
    const t = await getTranslations('warehouse.stock');
    return { title: t('title') };
  }
}

export default async function StockDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  const stock = await getStockItem(id).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  const [agencies, products, user] = await Promise.all([
    getAgencies(),
    getProductPage({ page: 1, pageSize: 200, sort: 'name' }),
    getCurrentUser(),
  ]);

  return (
    <>
      <PageHeader backHref="/stock" title={stock.productName} subtitle={stock.agencyName} />
      {/* Lot V, V2 : le vendeur consulte ; l'API lui refuse l'ajustement. */}
      <StockDetailScreen
        stock={stock}
        products={products.items}
        agencies={agencies}
        canAdjust={!user?.clientId}
      />
    </>
  );
}
