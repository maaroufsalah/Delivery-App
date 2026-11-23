import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { PageHeader } from '@/components/layout/page-header';
import { MerchantSlipDetail } from '@/features/returns/merchant-slip-detail';
import { getCurrentUser } from '@/lib/api/auth';
import { getSlip } from '@/lib/api/returns';
import { ApiError } from '@/lib/http';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ code: string }>;
}): Promise<Metadata> {
  const { code } = await params;
  return { title: decodeURIComponent(code) };
}

/**
 * Un bon de retour, côté VENDEUR — chantier « Bons de retour vendeur ».
 *
 * Sous `/returns/pending`, déjà ouvert au vendeur (`MERCHANT_ROUTES`) :
 * `/returns/slips`, l'écran de l'agence, lui reste fermé. Le bon d'un autre
 * vendeur répond 404 à l'API (`slipWhere`), donc 404 ici.
 *
 * Réservée au vendeur : un compte interne a sa propre fiche, avec ses gestes.
 */
export default async function MerchantSlipPage({ params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;
  const user = await getCurrentUser();
  if (!user?.clientId) notFound();

  const t = await getTranslations('returns.merchant');

  const slip = await getSlip(decodeURIComponent(code)).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  return (
    <>
      <PageHeader backHref="/returns/pending?tab=slips" title={slip.code} subtitle={t('title')} />
      <MerchantSlipDetail slip={slip} />
    </>
  );
}
