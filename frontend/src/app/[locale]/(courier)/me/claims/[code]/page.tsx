import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { getTranslations } from 'next-intl/server';
import { BackButton } from '@/components/back-button';
import { ClaimDetailView } from '@/features/claims/claim-detail';
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

/**
 * LA FICHE D'UNE RÉCLAMATION, VUE DU LIVREUR — le complément de L2.
 *
 * **Le MÊME composant que le back-office et que l'e-commerçant**, avec
 * `canHandle={false}` : le fil, les pièces jointes, et de quoi répondre. En
 * écrire une version « livreur » aurait fait un troisième fil de discussion à
 * tenir d'accord avec les deux autres, sur l'écran où se règlent les litiges.
 *
 * **Ce que `canHandle={false}` retire** est exactement ce qui ne le regarde
 * pas : changer le statut, assigner un agent, écrire une note d'agence. Le
 * serveur refuse les trois de son côté — l'écran ne fait que ne pas les
 * proposer.
 *
 * **Les notes internes ne lui parviennent pas** : le service les filtre du fil
 * (`handler || !event.isInternal`) avant de répondre. Ce n'est donc pas un
 * masquage d'affichage, c'est une absence.
 *
 * **`team` est vide, et l'appel n'est pas fait.** Une liste de comptes d'agence
 * n'a rien à faire dans la réponse envoyée à un livreur (D-082) — c'est le même
 * raisonnement que la fiche du back-office, qui ne charge l'équipe que pour qui
 * peut assigner.
 *
 * **Un code qui n'est pas le sien rend `404`**, et c'est la PORTÉE qui le
 * décide : `claimWhere()` joint le colis et exige `parcel.courierId`. La page
 * ne teste rien — elle traduit le refus de l'API en page introuvable.
 */
export default async function CourierClaimPage({ params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;

  let claim;
  try {
    claim = await getClaim(decodeURIComponent(code));
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  }

  const t = await getTranslations('claims.detail');

  return (
    <div className="flex flex-col gap-4">
      {/* Un retour — point 4. Cette fiche n'en avait aucun. */}
      <BackButton fallbackHref="/me/claims" />

      <header className="flex flex-col gap-1">
        <h1 className="font-mono text-xl font-semibold tracking-tight">{claim.code}</h1>
        <p className="text-muted-foreground text-sm">{t('subtitle')}</p>
      </header>

      <ClaimDetailView claim={claim} team={[]} canHandle={false} />
    </div>
  );
}
