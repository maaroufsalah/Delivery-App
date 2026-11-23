import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { TeamScreen } from '@/features/accounts/team-screen';
import { getTeamPage } from '@/lib/api/accounts';
import { activeParam, listQueryFrom, type SearchParams } from '@/lib/list-query';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('accounts.team');
  return { title: t('title') };
}

/**
 * « Mon équipe » — l'API borne la liste à l'agence du compte connecté. Aucun
 * filtre d'agence ici : ce serait l'écran `/users`, qui n'est pas le même geste.
 */
export default async function TeamPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const t = await getTranslations('accounts.team');

  const page = await getTeamPage({
    ...listQueryFrom(params, { sort: 'lastName' }),
    isActive: activeParam(params),
  });

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <TeamScreen page={page} />
    </>
  );
}
