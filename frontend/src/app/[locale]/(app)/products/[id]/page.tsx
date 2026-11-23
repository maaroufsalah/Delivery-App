import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { PageHeader } from '@/components/layout/page-header';
import { ProductDetailScreen } from '@/features/warehouse/product-detail';
import { getCurrentUser } from '@/lib/api/auth';
import { getClients } from '@/lib/api/directory';
import { getProduct, getProductAttributes } from '@/lib/api/warehouse';
import { ApiError } from '@/lib/http';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ id: string }>;
}): Promise<Metadata> {
  const { id } = await params;

  try {
    const product = await getProduct(id);
    return { title: product.name };
  } catch {
    const t = await getTranslations('warehouse.products');
    return { title: t('title') };
  }
}

export default async function ProductDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const t = await getTranslations('warehouse.products');

  const product = await getProduct(id).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  const [clients, attributes, user] = await Promise.all([
    getClients(),
    getProductAttributes(),
    getCurrentUser(),
  ]);

  return (
    <>
      <PageHeader backHref="/products" title={product.name} subtitle={t('subtitle')} />
      <ProductDetailScreen
        product={product}
        clients={clients}
        attributes={attributes}
        lockedClientId={user?.roles.includes('MERCHANT') ? product.clientId : undefined}
      />
    </>
  );
}
