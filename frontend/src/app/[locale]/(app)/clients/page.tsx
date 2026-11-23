import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { ClientsScreen } from '@/features/accounts/clients-screen';
import { getClientPage } from '@/lib/api/accounts';
import { getAgencies, getCities, getPendingCounts } from '@/lib/api/directory';
import { activeParam, firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

/**
 * Le filtre de validation compte TROIS états depuis C13 — à valider, validée,
 * refusée. Une valeur inconnue dans l'URL retombe sur « aucun filtre » plutôt
 * que de partir telle quelle vers l'API.
 */
function validatedParam(value: string | undefined): 'true' | 'false' | 'rejected' | undefined {
  return value === 'true' || value === 'false' || value === 'rejected' ? value : undefined;
}

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('accounts.clients');
  return { title: t('title') };
}

export default async function ClientsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const t = await getTranslations('accounts.clients');
  const [page, cities, agencies, pending] = await Promise.all([
    getClientPage({
      ...listQueryFrom(params, { sort: 'name' }),
      isActive: activeParam(params),
      agencyId: firstParam(params.agencyId),
      validated: validatedParam(firstParam(params.validated)),
    }),
    getCities(),
    getAgencies(),
    // Best-effort : une API muette prive le bouton de son chiffre, elle
    // n'empêche pas la liste de s'afficher.
    getPendingCounts().catch(() => null),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <ClientsScreen
        page={page}
        cities={cities}
        agencies={agencies}
        pendingCount={pending?.clients}
      />
    </>
  );
}
