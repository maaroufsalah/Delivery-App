import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { PageHeader } from '@/components/layout/page-header';
import { HandoverDetailScreen } from '@/features/returns/handover-detail';
import { getHandover } from '@/lib/api/returns';
import { ApiError } from '@/lib/http';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ code: string }>;
}): Promise<Metadata> {
  const { code } = await params;
  return { title: decodeURIComponent(code) };
}

export default async function HandoverDetailPage({
  params,
}: {
  params: Promise<{ code: string }>;
}) {
  const { code } = await params;
  const t = await getTranslations('returns.handovers');

  const handover = await getHandover(decodeURIComponent(code)).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  return (
    <>
      <PageHeader backHref="/returns/handovers" title={handover.code} subtitle={t('subtitle')} />
      <HandoverDetailScreen handover={handover} />
    </>
  );
}
