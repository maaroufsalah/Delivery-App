import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { SlipsScreen } from '@/features/returns/slips-screen';
import { getAgencies, getClients } from '@/lib/api/directory';
import { getSlipPage } from '@/lib/api/returns';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('returns.slips');
  return { title: t('title') };
}

export default async function SlipsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const t = await getTranslations('returns.slips');

  const [page, clients, agencies] = await Promise.all([
    getSlipPage({
      ...listQueryFrom(params, { sort: 'createdAt', order: 'desc' }),
      status: firstParam(params.status),
      clientId: firstParam(params.clientId),
      agencyId: firstParam(params.agencyId),
    }),
    getClients(),
    getAgencies(),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <SlipsScreen page={page} clients={clients} agencies={agencies} />
    </>
  );
}
