import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { PageHeader } from '@/components/layout/page-header';
import { SlipDetailScreen } from '@/features/returns/slip-detail';
import { getSlip, getSlipCandidates } from '@/lib/api/returns';
import { ApiError } from '@/lib/http';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ code: string }>;
}): Promise<Metadata> {
  const { code } = await params;
  return { title: decodeURIComponent(code) };
}

/**
 * Fiche d'un bon de retour.
 *
 * Les colis RENDABLES descendent avec la page — retouches Anass, point 7 : ceux
 * de cet e-commerçant dont le statut n'est ni « ramassé », ni « ramassé par le
 * livreur », ni « en cours de livraison », ni « livré ».
 *
 * La liste valait auparavant `statusId = 22`, c'est-à-dire les seuls colis
 * passés par une remise confirmée. L'agence rend pourtant aussi ceux qui n'ont
 * jamais quitté ses étagères — un « annulé par le vendeur », un « hors zone »
 * revenu le jour même — et ils n'apparaissaient nulle part.
 *
 * L'éligibilité est décidée par le SERVEUR, qui la relit à l'ajout : cette
 * liste n'est pas le contrôle, elle est le confort de saisie.
 */
export default async function SlipDetailPage({ params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;
  const t = await getTranslations('returns.slips');

  const slip = await getSlip(decodeURIComponent(code)).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  const available = slip.status === 'draft' ? await getSlipCandidates(slip.code) : [];

  return (
    <>
      <PageHeader backHref="/returns/slips" title={slip.code} subtitle={t('subtitle')} />
      <SlipDetailScreen slip={slip} available={available} />
    </>
  );
}
