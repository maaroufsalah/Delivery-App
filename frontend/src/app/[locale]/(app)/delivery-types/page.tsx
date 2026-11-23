import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { DeliveryTypesScreen } from '@/features/referential/delivery-types-screen';
import { getDeliveryTypePage } from '@/lib/api/referential';
import { activeParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export default async function DeliveryTypesPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('referential.deliveryTypes');

  const page = await getDeliveryTypePage({
    ...listQueryFrom(params, { sort: 'name' }),
    isActive: activeParam(params),
  });

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <DeliveryTypesScreen page={page} />
    </>
  );
}
