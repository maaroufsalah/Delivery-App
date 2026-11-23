import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { ClaimsScreen } from '@/features/claims/claims-screen';
import { getCurrentUser } from '@/lib/api/auth';
import { getClaimCounters, getClaims, getClaimTypes } from '@/lib/api/claims';
import { getClients } from '@/lib/api/directory';
import { firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('claims');
  return { title: t('title') };
}

/**
 * Réclamations — DOMAIN.md §1.19, C7 bloc 2.
 *
 * L'e-commerçant y voit LES SIENNES et peut en ouvrir ; le staff les traite.
 * La portée est posée côté serveur : le sélecteur de client disparaît de
 * l'écran du marchand, mais ce n'est pas lui qui protège la donnée.
 */
export default async function ClaimsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;

  const [page, counters, types, clients, user, t] = await Promise.all([
    getClaims({
      ...listQueryFrom(params, { sort: 'createdAt', order: 'desc' }),
      status: firstParam(params.status) as never,
      typeId: firstParam(params.typeId),
      clientId: firstParam(params.clientId),
      late: firstParam(params.late) === 'true',
    }),
    getClaimCounters(),
    getClaimTypes(),
    getClients(),
    getCurrentUser(),
    getTranslations('claims'),
  ]);

  const merchant = user?.roles.includes('MERCHANT') ?? false;

  return (
    <>
      <PageHeader title={t('title')} subtitle={merchant ? t('subtitleMerchant') : t('subtitle')} />
      <ClaimsScreen
        page={page}
        counters={counters}
        types={types}
        clients={clients}
        showClient={!merchant}
      />
    </>
  );
}
