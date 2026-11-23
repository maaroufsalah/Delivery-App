import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { PageHeader } from '@/components/layout/page-header';
import { CodPayoutDetailScreen } from '@/features/finance/cod-payout-detail';
import { getCurrentUser } from '@/lib/api/auth';
import { getBankAccounts, getCodPayout, getCodPayoutCandidates } from '@/lib/api/finance';
import { ApiError } from '@/lib/http';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ code: string }>;
}): Promise<Metadata> {
  const { code } = await params;
  return { title: decodeURIComponent(code) };
}

export default async function CodPayoutDetailPage({
  params,
}: {
  params: Promise<{ code: string }>;
}) {
  const { code } = await params;
  const t = await getTranslations('finance.codPayouts');

  const payout = await getCodPayout(decodeURIComponent(code)).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  const user = await getCurrentUser();
  const canWrite = user?.permissions.includes('finance.write') ?? false;
  const canPay = user?.permissions.includes('finance.pay') ?? false;

  /*
   * La pile n'est chargée que pour un BROUILLON qu'on peut composer. Elle est
   * bornée au client du bon, et l'API n'y met que des colis livrés ET
   * remboursés par le livreur (D-060) : on ne verse pas de l'argent qui dort
   * encore en tournée.
   */
  const [candidates, accounts] = await Promise.all([
    canWrite && payout.status === 'draft'
      ? getCodPayoutCandidates({ clientId: payout.clientId })
      : Promise.resolve([]),
    canPay ? getBankAccounts() : Promise.resolve([]),
  ]);

  return (
    <>
      <PageHeader backHref="/cod-payouts" title={payout.code} subtitle={t('subtitle')} />
      <CodPayoutDetailScreen
        payout={payout}
        candidates={candidates}
        accounts={accounts}
        canWrite={canWrite}
        canPay={canPay}
      />
    </>
  );
}
