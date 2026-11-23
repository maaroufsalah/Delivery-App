import { DELIVERED } from '@delivery/shared';
import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { ParcelDetailView } from '@/features/parcels/parcel-detail-view';
import { ParcelBlCard } from '@/features/returns/parcel-bl-card';
import { getParcelBl } from '@/lib/api/bl';
import { ApiError } from '@/lib/api/client';
import { getCurrentUser } from '@/lib/api/auth';
import {
  getAgencies,
  getAllCities,
  getClients,
  getCouriers,
  getDistricts,
} from '@/lib/api/directory';
import { getParcel } from '@/lib/api/parcels';

type Params = Promise<{ code: string }>;

export async function generateMetadata({ params }: { params: Params }): Promise<Metadata> {
  const { code } = await params;
  return { title: decodeURIComponent(code) };
}

export default async function ParcelDetailPage({ params }: { params: Params }) {
  const { code } = await params;

  // `.catch` plutôt qu'un try/catch : construire le JSX dans un `try` ne
  // capturerait rien — le rendu du composant a lieu après, hors du bloc.
  const user = await getCurrentUser();

  /*
   * D-257 : un colis introuvable peut être un colis SUPPRIMÉ. L'administrateur
   * en relit la fiche (lecture seule, avec son historique) ; pour tout autre
   * compte l'API répond encore 404.
   */
  const isAdmin = !user?.clientId && (user?.roles.includes('SUPER_ADMIN') ?? false);
  const parcel = await getParcel(decodeURIComponent(code))
    .catch((error: unknown) => {
      if (error instanceof ApiError && error.status === 404 && isAdmin) {
        return getParcel(decodeURIComponent(code), { includeDeleted: true });
      }
      throw error;
    })
    .catch((error: unknown) => {
      if (error instanceof ApiError && error.status === 404) notFound();
      throw error;
    });
  const deleted = Boolean(parcel.deletedAt);

  /*
   * LE SUIVI DU BL — chantier « Retours, BL Clients Pro », point 5.
   *
   * `.catch(() => null)` : un compte sans `returns.read` — il en existe — ne
   * doit pas voir sa fiche colis tomber pour une carte facultative. Et la
   * carte ne s'affiche pas du tout quand aucun BL n'est demandé, ce qui est la
   * lettre de la mission pour un client qui n'est pas Pro.
   */
  const bl = await getParcelBl(decodeURIComponent(code)).catch(() => null);

  /*
   * Les villes servies, pour le sélecteur de destination — point 7. Chargées
   * ICI parce que l'éditeur est un composant client : il ne peut pas aller les
   * chercher lui-même (D-018).
   */
  const cities = await getAllCities().catch(() => []);

  /*
   * ÉCHANGER — chantier « Étiquette orange », point 2.
   *
   * Les agences de RETRAIT, chargées ici pour la même raison que les villes :
   * le dialogue est un composant client (D-018). Seulement sur un colis livré —
   * le bouton n'existe pas ailleurs, et une requête par fiche colis pour un
   * bouton absent ne se justifie pas.
   *
   * **Les frais de retour, eux, viennent du COLIS** (`parcel.returnFee`) : ils
   * dépendent de son agence et de ses deux villes, et le vendeur — qui doit
   * les lire — n'a pas `settings.read`.
   */
  const agencies = parcel.statusId === DELIVERED ? await getAgencies().catch(() => []) : [];

  /*
   * LES DEUX GESTES DE TERRAIN — chantier « Correctifs avant démo », point 1.
   *
   * La permission ne suffit pas, et c'était le défaut : l'espace vendeur
   * enveloppe CET écran (D-090), et le MERCHANT porte `parcels.write` pour
   * saisir ses colis. Il voyait donc « Changer la destination », et « Changer le
   * statut » n'était gardé par rien du tout.
   *
   * Le RATTACHEMENT tranche, comme `NotMerchantGuard` côté API : un compte qui
   * porte un `clientId` est un vendeur, quel que soit le rôle qu'on lui a
   * collé. Il garde Étiquette et Réclamer, qui ne sont gardés par personne.
   */
  const isMerchant = user?.clientId !== null && user?.clientId !== undefined;
  const canChangeDestination =
    !isMerchant && (user?.permissions.includes('parcels.write') ?? false);

  /*
   * Les quartiers connus, SUGGÉRÉS dans le dialogue de destination — chantier
   * « Quartier en saisie libre ». Seulement quand le dialogue existe.
   */
  const districts = canChangeDestination ? await getDistricts().catch(() => []) : [];

  /*
   * L'E-COMMERÇANT EST-IL CLIENT PRO ? — recette 3, point 3. L'annuaire léger
   * porte `isPro` (badge de /clients) ; borné par la portée, il ne rend au
   * vendeur que sa propre fiche.
   */
  const directory = await getClients().catch(() => []);
  const sellerIsPro =
    directory.find((client) => client.id === parcel.clientId)?.isPro ?? false;

  /*
   * LOT I (D-210, D-211). « Annuler la livraison » à l'administrateur seul, sur
   * un colis livré (D-204 : le rôle, pas une clé de la matrice éditable).
   * « Transférer » à qui compose les circuits, quand le colis est sur un circuit
   * de livraison ouvert et n'est ni livré ni rentré — le serveur refuse le
   * reste. Les livreurs ne se chargent que pour ce bouton.
   */
  const canCancelDelivery =
    !isMerchant && parcel.statusId === DELIVERED && (user?.roles.includes('SUPER_ADMIN') ?? false);
  /*
   * « STATUT LIBRE POUR L'ADMIN » (D-245) : le rôle, comme « Annuler la
   * livraison ». L'écran ne fait que masquer un bouton que l'API refuserait
   * en 403 ; les verrous sont tous au serveur.
   */
  const canAdminFieldStatus = !isMerchant && (user?.roles.includes('SUPER_ADMIN') ?? false);
  /*
   * « CORRIGER LE STATUT » (D-250) : la permission dédiée ET le rôle, comme le
   * service les exige. « Changer le statut » ne bouge pas.
   */
  const canCorrectStatus =
    canAdminFieldStatus && (user?.permissions.includes('parcels.correctStatus') ?? false);
  /*
   * « SUPPRIMER » ET « CHANGER DE CLIENT » (D-256) : le rôle, comme la
   * correction du statut, et la clé d'écriture des colis. Le serveur refuse
   * tout autre compte et décide du reste (prise en charge, argent).
   */
  const adminClients =
    canAdminFieldStatus && (user?.permissions.includes('parcels.write') ?? false)
      ? directory
      : null;
  const onOpenDelivery = parcel.routes.some(
    (route) => route.kind === 'delivery' && route.status !== 'closed',
  );
  /*
   * D-246 : un colis « En cours de livraison » qui n'est sur AUCUN circuit
   * ouvert se transfère aussi (« Transférer à un livreur ») : c'est la porte de
   * sortie d'un colis que personne ne porte. Le serveur garde ses verrous.
   */
  const offRouteInDelivery =
    parcel.statusId === 13 && !parcel.routes.some((route) => route.status !== 'closed');
  const canTransfer =
    !isMerchant &&
    (onOpenDelivery || offRouteInDelivery) &&
    parcel.statusId !== DELIVERED &&
    ![5, 21, 22, 23, 24].includes(parcel.statusId) &&
    (user?.permissions.includes('routes.write') ?? false);
  /*
   * D-246 : « En cours de livraison » posé au bureau demande un livreur. La
   * liste sert aussi au transfert : une seule lecture pour les deux boutons.
   */
  const canChangeStatus = !isMerchant && (user?.permissions.includes('parcels.status') ?? false);
  const couriers = canTransfer || canChangeStatus ? await getCouriers().catch(() => null) : null;
  const transferCouriers = canTransfer ? couriers : null;

  return (
    <>
      <ParcelDetailView
        parcel={parcel}
        canEditFees={user?.permissions.includes('parcels.editFees') ?? false}
        canChangeStatus={canChangeStatus}
        statusCouriers={canChangeStatus ? couriers : null}
        canChangeDestination={canChangeDestination}
        cities={cities}
        districts={districts}
        agencies={agencies}
        returnFee={parcel.returnFee}
        internalView={!isMerchant}
        sellerIsPro={sellerIsPro}
        canCancelDelivery={canCancelDelivery}
        canAdminFieldStatus={canAdminFieldStatus}
        canCorrectStatus={canCorrectStatus}
        transferCouriers={transferCouriers}
        adminClients={adminClients}
        // D-257 : un colis supprimé se lit, aucun geste ne s'y applique.
        readOnly={deleted}
      />
      {bl ? <ParcelBlCard tracking={bl} /> : null}
    </>
  );
}
