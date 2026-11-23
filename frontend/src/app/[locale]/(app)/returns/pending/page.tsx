import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { MerchantReturnsScreen } from '@/features/returns/merchant-returns-screen';
import { MerchantReturnsTabs } from '@/features/returns/merchant-returns-tabs';
import { MerchantSlipsTab } from '@/features/returns/merchant-slips-tab';
import { PendingScreen } from '@/features/returns/pending-screen';
import { getCurrentUser } from '@/lib/api/auth';
import { getAgencies } from '@/lib/api/directory';
import { getReturnsPending, getSlipPage } from '@/lib/api/returns';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const user = await getCurrentUser();
  const t = await getTranslations(user?.clientId ? 'returns.merchant' : 'returns.pending');
  return { title: t('title') };
}

/**
 * Colis à retourner, groupés par livreur.
 *
 * Pas de pagination : c'est une pile de travail, pas une archive. L'API borne
 * à 500 lignes — au-delà, ce n'est plus un problème d'affichage mais
 * d'exploitation.
 *
 * Le vendeur arrive ici par « Mes retours » : la même route lui rend SES
 * retours (filtre serveur, lot V, D-234), et l'écran lui parle sa langue.
 */
export default async function ReturnsPendingPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const search = firstParam(params.search);
  const user = await getCurrentUser();

  if (user?.clientId) {
    const t = await getTranslations('returns.merchant');

    /*
     * Chantier « Bons de retour vendeur » : deux onglets, et seul celui qui
     * est ouvert est lu. Les bons sont paginés par le serveur et bornés à CE
     * vendeur par sa portée (`slipWhere`) — aucun `clientId` ne part d'ici.
     */
    if (firstParam(params.tab) === 'slips') {
      const page = await getSlipPage({
        ...listQueryFrom(params, { sort: 'createdAt', order: 'desc' }),
        from: firstParam(params.from),
        to: firstParam(params.to),
      });

      return (
        <>
          <PageHeader title={t('title')} subtitle={t('subtitle')} />
          <MerchantReturnsTabs tab="slips" counts={{ slips: page.total }}>
            <MerchantSlipsTab page={page} />
          </MerchantReturnsTabs>
        </>
      );
    }

    const groups = await getReturnsPending({ search });
    const parcels = groups.flatMap((group) => group.parcels);

    return (
      <>
        <PageHeader title={t('title')} subtitle={t('subtitle')} />
        <MerchantReturnsTabs tab="on-the-way" counts={{ 'on-the-way': parcels.length }}>
          <MerchantReturnsScreen parcels={parcels} search={search ?? ''} />
        </MerchantReturnsTabs>
      </>
    );
  }

  const t = await getTranslations('returns.pending');

  const [groups, agencies] = await Promise.all([
    getReturnsPending({ search, agencyId: firstParam(params.agencyId) }),
    getAgencies(),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <PendingScreen groups={groups} agencies={agencies} search={search ?? ''} />
    </>
  );
}
