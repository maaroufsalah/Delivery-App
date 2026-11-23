import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { ExchangesScreen } from '@/features/parcels/exchanges-screen';
import { getCurrentUser } from '@/lib/api/auth';
import { getExchanges } from '@/lib/api/exchanges';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('exchanges');
  return { title: t('title') };
}

export default async function ExchangesPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('exchanges');

  const [page, user] = await Promise.all([
    getExchanges({
      ...listQueryFrom(params, { sort: 'requestedAt', order: 'desc' }),
      status: firstParam(params.status),
      search: firstParam(params.search),
    }),
    getCurrentUser(),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <ExchangesScreen
        page={page}
        canWrite={user?.permissions.includes('parcels.write') ?? false}
      />
    </>
  );
}
