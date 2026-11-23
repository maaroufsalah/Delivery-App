import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { PaymentsScreen } from '@/features/finance/payments-screen';
import { getCurrentUser } from '@/lib/api/auth';
import { getAgencies, getClients } from '@/lib/api/directory';
import { getBankAccounts, getPaymentPage } from '@/lib/api/finance';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('finance.payments');
  return { title: t('title') };
}

export default async function PaymentsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('finance.payments');

  const [page, clients, agencies, user] = await Promise.all([
    getPaymentPage({
      ...listQueryFrom(params, { sort: 'paidAt', order: 'desc' }),
      method: firstParam(params.method),
      clientId: firstParam(params.clientId),
      agencyId: firstParam(params.agencyId),
    }),
    getClients(),
    getAgencies(),
    getCurrentUser(),
  ]);

  const canPay = user?.permissions.includes('finance.pay') ?? false;

  // Les comptes bancaires ne servent qu'au formulaire de saisie : inutile de
  // les charger pour un e-commerçant qui vient lire ses règlements.
  const accounts = canPay ? await getBankAccounts() : [];

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <PaymentsScreen
        page={page}
        clients={clients}
        agencies={agencies}
        accounts={accounts}
        canPay={canPay}
      />
    </>
  );
}
