import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { PageHeader } from '@/components/layout/page-header';
import { RemittanceDetailScreen } from '@/features/finance/remittance-detail';
import { getCurrentUser } from '@/lib/api/auth';
import { getRemittance, getRemittanceCandidates } from '@/lib/api/finance';
import { ApiError } from '@/lib/http';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ code: string }>;
}): Promise<Metadata> {
  const { code } = await params;
  return { title: decodeURIComponent(code) };
}

export default async function RemittanceDetailPage({
  params,
}: {
  params: Promise<{ code: string }>;
}) {
  const { code } = await params;
  const t = await getTranslations('finance.remittances');

  const remittance = await getRemittance(decodeURIComponent(code)).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  const user = await getCurrentUser();
  const canWrite = user?.permissions.includes('finance.write') ?? false;

  /*
   * La pile n'est chargée que pour un BROUILLON qu'on peut composer, et elle
   * est bornée au livreur ET à la ville de la recette : c'est cette enveloppe
   * qu'on est en train de compter, pas la tournée entière.
   */
  const candidates =
    canWrite && remittance.status === 'draft'
      ? await getRemittanceCandidates({
          courierId: remittance.courierId,
          cityId: remittance.cityId,
        })
      : [];

  return (
    <>
      <PageHeader backHref="/remittances" title={remittance.code} subtitle={t('subtitle')} />
      <RemittanceDetailScreen
        remittance={remittance}
        candidates={candidates}
        canWrite={canWrite}
        canConfirm={user?.permissions.includes('finance.confirm') ?? false}
      />
    </>
  );
}
