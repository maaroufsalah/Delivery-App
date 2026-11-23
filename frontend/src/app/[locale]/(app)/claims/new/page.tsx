import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { ClaimForm } from '@/features/claims/claim-form';
import { getCurrentUser } from '@/lib/api/auth';
import { getClaimTypes } from '@/lib/api/claims';
import { getClients } from '@/lib/api/directory';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('claims.form');
  return { title: t('title') };
}

/**
 * `parcel` en paramètre : la fiche d'un colis ouvre ce formulaire avec son code
 * déjà posé. Recopier un code de quinze caractères à la main est le genre de
 * friction qui fait qu'on ne réclame pas.
 */
export default async function NewClaimPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const preset = Array.isArray(params.parcel) ? params.parcel[0] : params.parcel;

  const [types, clients, user, t] = await Promise.all([
    getClaimTypes(),
    getClients(),
    getCurrentUser(),
    getTranslations('claims.form'),
  ]);

  const merchant = user?.roles.includes('MERCHANT') ?? false;

  return (
    <>
      <PageHeader backHref="/claims" title={t('title')} subtitle={t('subtitle')} />
      <ClaimForm
        types={types}
        clients={clients}
        lockedClientId={merchant ? (clients[0]?.id ?? undefined) : undefined}
        presetParcelCode={preset}
      />
    </>
  );
}
