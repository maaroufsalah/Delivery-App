import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { ClaimDetailView } from '@/features/claims/claim-detail';
import { getCurrentUser } from '@/lib/api/auth';
import { getTeamPage } from '@/lib/api/accounts';
import { getClaim } from '@/lib/api/claims';
import { ApiError } from '@/lib/http';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ code: string }>;
}): Promise<Metadata> {
  const { code } = await params;
  return { title: decodeURIComponent(code) };
}

export default async function ClaimPage({ params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;
  const user = await getCurrentUser();
  const canHandle = user?.permissions.includes('claims.handle') ?? false;

  let claim;
  try {
    claim = await getClaim(decodeURIComponent(code));
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  }

  // Les agents assignables ne sont chargés QUE pour qui peut assigner : une
  // liste de comptes n'a rien à faire dans la réponse envoyée à un marchand.
  const team = canHandle ? await getTeamPage({ page: 1, pageSize: 200, sort: 'lastName' }) : null;
  const t = await getTranslations('claims.detail');

  return (
    <>
      <PageHeader backHref="/claims" title={claim.code} subtitle={t('subtitle')} />
      <ClaimDetailView claim={claim} team={team?.items ?? []} canHandle={canHandle} />
    </>
  );
}
