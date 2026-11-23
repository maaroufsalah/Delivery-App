import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { HandoversScreen } from '@/features/returns/handovers-screen';
import { getAgencies, getCouriers } from '@/lib/api/directory';
import { getHandoverPage } from '@/lib/api/returns';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('returns.handovers');
  return { title: t('title') };
}

export default async function HandoversPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('returns.handovers');

  const [page, couriers, agencies] = await Promise.all([
    getHandoverPage({
      ...listQueryFrom(params, { sort: 'createdAt', order: 'desc' }),
      status: firstParam(params.status),
      // L'origine n'était pas transmise : le filtre de l'écran ne filtrait rien.
      origin: firstParam(params.origin),
      courierId: firstParam(params.courierId),
      agencyId: firstParam(params.agencyId),
    }),
    getCouriers(),
    getAgencies(),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <HandoversScreen page={page} couriers={couriers} agencies={agencies} />
    </>
  );
}
