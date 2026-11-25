import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { TransferOrdersScreen } from '@/features/finance/transfer-orders-screen';
import { getTransferOrderPage } from '@/lib/api/finance';
import { listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('finance.transferOrders');
  return { title: t('title') };
}

/** Les ordres de virement déjà générés — consultation seule, aucun geste d'écriture. */
export default async function TransferOrdersPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('finance.transferOrders');
  const { page, pageSize, search } = listQueryFrom(params, { sort: 'createdAt', order: 'desc' });

  const orders = await getTransferOrderPage({ page, pageSize, search });

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <TransferOrdersScreen page={orders} />
    </>
  );
}
