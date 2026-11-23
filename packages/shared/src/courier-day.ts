import { DELIVERED, findParcelStatus } from './statuses';
import { routeTargetsSender } from './transitions';
import type { CourierPerformance, CourierRoute, CourierRouteParcel } from './types/courier-app';

/**
 * LA JOURNÉE DU LIVREUR, en quatre chiffres — M4 bloc 2, **partagée** par L1.
 *
 * Elle vivait dans `apps/mobile` ; le web en a eu besoin le jour où son espace
 * livreur a reçu le même accueil. La recopier aurait produit deux comptes du
 * même jour — et un livreur qui lit « 3 à livrer » sur son téléphone et « 4 »
 * sur le poste de l'agence ne fait plus confiance à aucun des deux.
 *
 * **Aucun endpoint neuf.** Les quatre chiffres se déduisent de la tournée
 * active, que l'application charge déjà : elle EST la journée. Demander à
 * l'API un récapitulatif aurait produit une seconde vérité, qui aurait dérivé
 * de celle-ci au premier changement de statut hors ligne — le geste part en
 * file, la tournée locale avance, un compteur serveur non.
 *
 * **Ce que chaque chiffre dit, et pourquoi celui-là :**
 *
 *  - **à livrer** — les colis dont l'issue n'est pas encore rapportée. C'est
 *    le seul chiffre qu'un livreur regarde avant de partir, et c'est déjà celui
 *    que l'API nomme `remainingCount` ;
 *  - **livrés** — statut 4, et lui seul. Un colis « déjà livré » (28) est un
 *    échec de course, pas une livraison ;
 *  - **retours** — les groupes `return` et `failure` réunis. Le livreur les
 *    rapporte à l'agence dans le même geste, et les compter séparément
 *    demanderait une tuile de plus pour une distinction qui ne change rien à
 *    sa fin de journée ;
 *  - **encaissé** — la somme des `codToCollect` des colis LIVRÉS. Le montant
 *    par colis est calculé au serveur (DOMAIN.md §3.8) et ne s'efface pas à la
 *    livraison : c'est bien ce qu'il a dans la poche.
 */
export interface DayStats {
  toDeliver: number;
  delivered: number;
  returns: number;
  collectedInCents: number;
}

const RETURN_GROUPS = new Set(['return', 'failure']);

export function dayStats(routes: CourierRoute[] | undefined): DayStats {
  /*
   * Les circuits de LIVRAISON seulement — Recette 5, point 1. Un colis d'un
   * circuit de ramassage n'est pas « à livrer » : le compter ici gonflait le
   * badge « Livraison » de tout ce qu'on allait chercher chez le vendeur.
   */
  const parcels: CourierRouteParcel[] = (routes ?? [])
    .filter((route) => !routeTargetsSender(route.kind))
    .flatMap((route) => route.parcels);

  return {
    toDeliver: parcels.filter((parcel) => !parcel.confirmed).length,
    delivered: parcels.filter((parcel) => parcel.statusId === DELIVERED).length,
    returns: parcels.filter((parcel) => {
      const status = findParcelStatus(parcel.statusId);
      return status ? RETURN_GROUPS.has(status.group) : false;
    }).length,
    collectedInCents: parcels
      .filter((parcel) => parcel.statusId === DELIVERED)
      .reduce((sum, parcel) => sum + parcel.codToCollect, 0),
  };
}

/**
 * L'ACTIVITÉ RÉCENTE — les derniers colis dont l'issue a été rapportée.
 *
 * Pas d'appel réseau non plus : ce sont les colis confirmés de la tournée,
 * les plus avancés en tête. Un vrai journal d'événements demanderait
 * `GET /parcels/:code` colis par colis — vingt-cinq appels pour une liste de
 * cinq lignes.
 */
export function recentActivity(
  routes: CourierRoute[] | undefined,
  limit = 5,
): CourierRouteParcel[] {
  return (routes ?? [])
    .flatMap((route) => route.parcels)
    .filter((parcel) => parcel.confirmed)
    .sort((a, b) => b.sequence - a.sequence)
    .slice(0, limit);
}

/**
 * LES HUIT CHIFFRES DE L'ACCUEIL, ET CE QU'ON MONTRE SANS EUX — L2 bloc 1.
 *
 * Anass en demande huit là où M4 en rendait quatre. Deux ne se déduisent pas
 * de la tournée — « à remettre » est une dette sans date, le taux demande une
 * période — et c'est ce qui a justifié `GET /courier/me/performance`.
 *
 * **Cette fonction existe pour que l'écran n'ait pas à savoir tout cela.** Elle
 * prend ce que l'API a bien voulu rendre et ce que la tournée sait déjà, et
 * rend les huit valeurs, `null` pour celles qu'on ne peut pas honnêtement
 * afficher. Une tuile dont la valeur est `null` **disparaît** : c'est la règle
 * de la mission — tant que la production n'a pas la route, le livreur voit
 * moins, jamais un zéro qui ment ni un écran cassé.
 *
 * Elle est **partagée** parce que le web montre le même accueil (bloc 9) : la
 * recopier ferait deux règles de repli, dont une se tairait le jour où l'autre
 * change.
 */
export interface CourierFigures {
  toPickup: number | null;
  toDeliver: number;
  delivered: number;
  returning: number;
  failed: number | null;
  collectedInCents: number;
  toRemitInCents: number | null;
  deliveryRate: { delivered: number; total: number; percent: number } | null;
}

export function courierFigures(
  performance: CourierPerformance | undefined,
  routes: CourierRoute[] | undefined,
): CourierFigures {
  const day = dayStats(routes);

  /*
   * L'API D'ABORD, quand elle répond. Ses cinq compteurs portent sur TOUT ce
   * que le livreur détient, pas seulement sur la tournée du jour : un colis
   * en retour depuis avant-hier compte encore, et c'est le chiffre juste.
   *
   * L'encaissé, lui, reste celui de la tournée quand elle en sait plus — un
   * geste parti en file a déjà fait monter le compteur local, et le serveur
   * ne le saura qu'à la reconnexion. Prendre le plus grand des deux évite le
   * recul visible qui ferait croire à une livraison perdue.
   */
  if (performance) {
    return {
      toPickup: performance.toPickup,
      toDeliver: performance.toDeliver,
      delivered: performance.delivered,
      returning: performance.returning,
      failed: performance.failed,
      collectedInCents: Math.max(performance.collectedInCents, day.collectedInCents),
      toRemitInCents: performance.toRemitInCents,
      deliveryRate: performance.deliveryRate,
    };
  }

  return {
    toPickup: null,
    toDeliver: day.toDeliver,
    delivered: day.delivered,
    // `dayStats` réunit retours et échecs : sans l'API, on ne sait pas les
    // séparer, et on montre donc la seule valeur qu'on peut défendre.
    returning: day.returns,
    failed: null,
    collectedInCents: day.collectedInCents,
    toRemitInCents: null,
    deliveryRate: null,
  };
}

/**
 * LES TROIS ÉTATS D'UN BON DE RAMASSAGE — L2 bloc 4, maquette 7.
 *
 * Anass montre trois onglets : **En attente**, **En cours**, **Terminés**. Le
 * bon n'a que deux états en base — à valider, validé (DOMAIN.md §1.12) — et
 * ajouter le troisième aurait demandé une migration pour une nuance
 * d'affichage.
 *
 * Le troisième n'est d'ailleurs pas un état du BON, c'est un état du TRAVAIL :
 * « en cours » veut dire que le livreur a commencé à prendre les colis. Il se
 * déduit donc des colis, et il se déduit bien — c'est exactement ce que le
 * livreur veut distinguer d'un bon auquel personne n'a touché.
 *
 * **Partagée** : le web montre les mêmes trois onglets (bloc 9). Deux copies
 * auraient fini par classer le même bon dans deux onglets différents.
 */
export type PickupNoteState = 'pending' | 'in-progress' | 'done';

export function pickupNoteState(note: {
  status: 'awaiting-validation' | 'validated';
  pickedCount: number;
}): PickupNoteState {
  /*
   * LA VALIDATION D'ABORD, quoi qu'en disent les colis. Un bon validé est
   * clos : l'agence a compté et signé. Un colis qui traînerait encore en
   * attente après ça est une anomalie à traiter au bureau, pas une raison de
   * renvoyer le livreur sur place.
   */
  if (note.status === 'validated') return 'done';

  return note.pickedCount > 0 ? 'in-progress' : 'pending';
}
