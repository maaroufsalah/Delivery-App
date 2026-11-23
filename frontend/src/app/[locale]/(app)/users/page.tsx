import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { UsersScreen } from '@/features/accounts/users-screen';
import { getAgencyPage, getUserPage } from '@/lib/api/accounts';
import { getCurrentUser } from '@/lib/api/auth';
import { activeParam, firstParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('accounts.users');
  return { title: t('title') };
}

/**
 * Comptes de connexion — DOMAIN.md §1.4.
 *
 * Les agences descendent en entier (200 au maximum) : elles peuplent un filtre
 * et deux listes de cases à cocher, pas une liste paginée.
 */
export default async function UsersPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const t = await getTranslations('accounts.users');

  const [page, agencies, current] = await Promise.all([
    getUserPage({
      ...listQueryFrom(params, { sort: 'lastName' }),
      isActive: activeParam(params),
      role: firstParam(params.role),
      agencyId: firstParam(params.agencyId),
    }),
    getAgencyPage({ page: 1, pageSize: 200, sort: 'name', order: 'asc' }),
    getCurrentUser(),
  ]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <UsersScreen page={page} agencies={agencies.items} currentUserId={current?.id ?? ''} />
    </>
  );
}
