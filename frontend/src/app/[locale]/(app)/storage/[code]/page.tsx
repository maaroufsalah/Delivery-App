import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { PageHeader } from '@/components/layout/page-header';
import { StorageDetailScreen } from '@/features/warehouse/storage-detail';
import { getCurrentUser } from '@/lib/api/auth';
import { getStorageRequest } from '@/lib/api/warehouse';
import { ApiError } from '@/lib/http';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ code: string }>;
}): Promise<Metadata> {
  const { code } = await params;
  return { title: decodeURIComponent(code) };
}

export default async function StorageDetailPage({ params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;
  const t = await getTranslations('warehouse.storage');

  const request = await getStorageRequest(decodeURIComponent(code)).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  const user = await getCurrentUser();

  return (
    <>
      <PageHeader backHref="/storage" title={request.code} subtitle={t('subtitle')} />
      <StorageDetailScreen
        request={request}
        // Le bouton n'apparaît que si l'API l'accepterait : la permission est
        // la même que celle du garde `warehouse.validate`.
        canDecide={user?.permissions.includes('warehouse.validate') ?? false}
      />
    </>
  );
}
