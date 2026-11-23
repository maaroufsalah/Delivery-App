import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { RouteDetailView } from '@/features/routes/route-detail-view';
import { getLatestBulkJob } from '@/lib/api/bulk-jobs';
import { getCurrentUser } from '@/lib/api/auth';
import { ApiError } from '@/lib/api/client';
import { getCities, getCouriers } from '@/lib/api/directory';
import { getRoute } from '@/lib/api/routes';

type Params = Promise<{ code: string }>;

export async function generateMetadata({ params }: { params: Params }): Promise<Metadata> {
  const { code } = await params;
  return { title: decodeURIComponent(code) };
}

export default async function RouteDetailPage({ params }: { params: Params }) {
  const { code } = await params;

  const route = await getRoute(decodeURIComponent(code)).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  // D-173 : un BROUILLON se modifie — le formulaire a besoin des villes et des
  // livreurs. Un circuit parti n'en a pas l'usage : on ne les charge pas.
  const [cities, couriers] =
    route.status === 'draft' ? await Promise.all([getCities(), getCouriers()]) : [[], []];

  /*
   * « TRANSFÉRER (X) » — lot I, I1 (D-211). Sur un circuit de livraison PARTI
   * (la barre de sélection n'existe qu'ici), pour qui compose les circuits :
   * les livreurs se chargent seulement pour ce bouton. Le serveur décide du
   * reste.
   */
  const user =
    route.kind === 'delivery' && route.status !== 'draft' && route.status !== 'closed'
      ? await getCurrentUser().catch(() => null)
      : null;
  const transferCouriers =
    user && user.clientId == null && user.permissions.includes('routes.write')
      ? await getCouriers().catch(() => null)
      : null;

  // Lot W2 (D-239) : la tâche de masse en cours (ou la dernière) se retrouve après un rechargement.
  const bulkJob = await getLatestBulkJob(route.code).catch(() => null);

  // Les candidats (colis, bons) se lisent dans les modales, par pages
  // (chantier « Plafond de 200 ») : plus de liste préchargée et tronquée ici.
  return (
    <RouteDetailView
      route={route}
      cities={cities}
      couriers={couriers}
      transferCouriers={transferCouriers}
      bulkJob={bulkJob}
    />
  );
}
