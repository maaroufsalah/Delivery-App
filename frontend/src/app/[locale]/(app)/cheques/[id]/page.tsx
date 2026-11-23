import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { ChequeDetailView } from '@/features/finance/cheque-detail';
import { getCurrentUser } from '@/lib/api/auth';
import { ApiError } from '@/lib/api/client';
import { getCheque } from '@/lib/api/finance';

type Params = Promise<{ id: string }>;

export async function generateMetadata({ params }: { params: Params }): Promise<Metadata> {
  const { id } = await params;
  const cheque = await getCheque(decodeURIComponent(id)).catch(() => null);
  return { title: cheque?.number ?? decodeURIComponent(id) };
}

/**
 * LA FICHE D'UN CHÈQUE — chantier « Correctifs avant démo », point 6.
 *
 * Une page et non un panneau : l'historique daté se partage par lien, et un
 * vendeur qui écrit à son transporteur colle une URL.
 *
 * **La portée fait le 403.** `ChequesService.scoped()` borne la lecture au
 * vendeur du colis, au livreur qui a reçu le chèque ou à l'agence ; un chèque
 * hors portée n'est pas trouvé, et la page rend 404. C'est le refus voulu — un
 * 403 sur un identifiant qui existe et un 404 sur un qui n'existe pas
 * transformerait l'URL en oracle (D-082).
 */
export default async function ChequePage({ params }: { params: Params }) {
  const { id } = await params;

  const [cheque, user] = await Promise.all([
    getCheque(decodeURIComponent(id)).catch((error: unknown) => {
      if (error instanceof ApiError && (error.status === 404 || error.status === 403)) notFound();
      throw error;
    }),
    getCurrentUser(),
  ]);
  // D-265 : le bon de remise est un papier de l'agence et du livreur, pas du vendeur.
  const merchant = user?.clientId !== null && user?.clientId !== undefined;

  return <ChequeDetailView cheque={cheque} canPrintSlip={!merchant} />;
}
