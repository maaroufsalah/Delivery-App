import { COURIER_RETURN_STATUS_IDS, DELIVERED } from './statuses';
import { canTransition } from './transitions';
import type { CourierStockBlock, CourierStockCounters } from './types/courier-stock';

/**
 * CE QU'UN LIVREUR DÉTIENT — lot M, D-222.
 *
 * Une fonction PURE : elle reçoit un colis tel que la base le décrit (statut,
 * livreur, circuits, crans passés) et rend le bloc où il tombe, ou `null`
 * s'il n'est pas chez ce livreur. La liste, le détail, les compteurs, l'export
 * et les deux PDF la lisent tous : ils ne peuvent pas diverger.
 *
 * L'ordre des tests rend les blocs DISJOINTS — un colis ne compte qu'une fois.
 * Aucun statut n'est inventé : chaque règle cite la machine à états ou l'écran
 * qui la porte déjà (`docs/CHECKPOINT-LOT-M.md`, « Définition retenue »).
 */

/** Un circuit du colis, tel que le classement en a besoin. */
export interface CourierStockRouteLink {
  courierId: string;
  /** `DELIVERY`, `PICKUP`, `RETURN` (base) ou minuscules (fil) : indifférent. */
  kind: string;
  /** `DRAFT` … `CLOSED` (base) ou minuscules : indifférent. */
  status: string;
}

export interface CourierStockInput {
  statusId: number;
  courierId: string | null;
  reimbursedByCourier: boolean;
  routes: readonly CourierStockRouteLink[];
  /** Les statuts que le colis a portés (événements) — seuls 3 et 18 comptent. */
  pastStatusIds: readonly number[];
}

const PICKED_UP = 3;
const PICKED_UP_BY_COURIER = 18;
const OUT_FOR_DELIVERY = 13;
const SHIPPED = 7;
const AWAITING_DELIVERY = 16;
const RECOVERY_IN_PROGRESS = 21;
const RETURNED_TO_AGENCY = 22;
const RETURN_IN_PROGRESS = 24;

const isOpen = (link: CourierStockRouteLink) => link.status.toUpperCase() !== 'CLOSED';
const isKind = (link: CourierStockRouteLink, kind: string) => link.kind.toUpperCase() === kind;

/**
 * Le bloc d'un colis dans le stock du livreur `courierId`, `null` hors stock.
 *
 * `courierId` est celui que le départ du circuit pose et que le transfert
 * réécrit (D-211) : un colis transféré sort du stock de A sans autre règle.
 */
export function classifyCourierStock(
  parcel: CourierStockInput,
  courierId: string,
): CourierStockBlock | null {
  if (parcel.courierId !== courierId) return null;

  const mine = parcel.routes.filter((link) => link.courierId === courierId);
  const status = parcel.statusId;

  // 18 : en main, l'agence n'a pas encore validé la réception (→ 3).
  if (status === PICKED_UP_BY_COURIER) return 'picked-up';

  // 13 : la course est en cours (`COURIER_SOURCE_STATUS_IDS`).
  if (status === OUT_FOR_DELIVERY) return 'out-for-delivery';

  // 7 « Expédié » ne naît d'aucun geste v2, seulement d'une saisie d'agence
  // depuis 3 ou 16 : son `courierId` peut être celui du RAMASSAGE. Il ne
  // compte que sur un de ses circuits de livraison — l'inter-ville qu'il porte.
  if (status === SHIPPED) {
    return mine.some((link) => isKind(link, 'DELIVERY')) ? 'out-for-delivery' : null;
  }

  // 16 sur un de SES circuits non clos ; sur un circuit clos, il est rentré
  // à l'agence (D-210 §3, D-221).
  if (status === AWAITING_DELIVERY) {
    return mine.some(isOpen) ? 'awaiting' : null;
  }

  if (status === DELIVERED) {
    // « MAD en main » : l'argent n'est pas encore rentré par un RF- confirmé.
    return parcel.reimbursedByCourier ? null : 'delivered-unremitted';
  }

  // 21 et 24 : il roule vers le vendeur avec la marchandise.
  if (status === RECOVERY_IN_PROGRESS || status === RETURN_IN_PROGRESS) {
    return 'failed-or-returning';
  }

  // 22 « non remis » sur un circuit de retour ouvert : il reste en main
  // (`transitions.ts`, issue 24 → 22). Partout ailleurs, 22 = reçu à l'agence.
  if (status === RETURNED_TO_AGENCY) {
    return mine.some((link) => isKind(link, 'RETURN') && isOpen(link))
      ? 'failed-or-returning'
      : null;
  }

  /*
   * Les échecs et relances que SON bon de retour propose (D-153) — à condition
   * qu'il ait eu le colis en main : sur un de ses circuits de livraison, ou
   * passé par 18 sans avoir été reçu (3). Un ramassage qui tombe (17 → 19, 27,
   * 8) laisse sinon dans son stock un carton qu'il n'a jamais touché.
   */
  if (COURIER_RETURN_STATUS_IDS.includes(status)) {
    const delivered = mine.some((link) => isKind(link, 'DELIVERY'));
    const heldAfterPickup =
      parcel.pastStatusIds.includes(PICKED_UP_BY_COURIER) &&
      !parcel.pastStatusIds.includes(PICKED_UP);
    return delivered || heldAfterPickup ? 'failed-or-returning' : null;
  }

  return null;
}

/** Les quatre blocs physiques : ce que « colis en main » additionne. */
export function isInHand(block: CourierStockBlock): boolean {
  return block !== 'delivered-unremitted';
}

/**
 * Le COD de ce colis est-il encore ATTENDU ? Oui s'il est en main et que la
 * machine à états autorise encore la livraison (13, 7, 16, relances, 9).
 */
export function expectsCollection(statusId: number, block: CourierStockBlock): boolean {
  return isInHand(block) && canTransition(statusId, DELIVERED);
}

/** Compteurs vides — le point de départ de toute somme. */
export function emptyCourierStockCounters(): CourierStockCounters {
  return {
    inHand: 0,
    pickedUp: 0,
    outForDelivery: 0,
    awaiting: 0,
    failedOrReturning: 0,
    deliveredUnremitted: 0,
    collectedUnremittedInCents: 0,
    toCollectInCents: 0,
    lastMovementAt: null,
  };
}

/** Ajoute un colis aux compteurs. UNE somme, lue par la liste, le détail et les PDF. */
export function addToCourierStock(
  counters: CourierStockCounters,
  parcel: {
    block: CourierStockBlock;
    statusId: number;
    codInCents: number;
    lastMovementAt: string | null;
  },
): CourierStockCounters {
  const next = { ...counters };

  switch (parcel.block) {
    case 'picked-up':
      next.pickedUp += 1;
      break;
    case 'out-for-delivery':
      next.outForDelivery += 1;
      break;
    case 'awaiting':
      next.awaiting += 1;
      break;
    case 'failed-or-returning':
      next.failedOrReturning += 1;
      break;
    case 'delivered-unremitted':
      next.deliveredUnremitted += 1;
      next.collectedUnremittedInCents += parcel.codInCents;
      break;
  }

  if (isInHand(parcel.block)) next.inHand += 1;
  if (expectsCollection(parcel.statusId, parcel.block)) next.toCollectInCents += parcel.codInCents;
  next.lastMovementAt = latest(next.lastMovementAt, parcel.lastMovementAt);

  return next;
}

/** Additionne deux jeux de compteurs — les totaux d'une liste. */
export function sumCourierStock(
  left: CourierStockCounters,
  right: CourierStockCounters,
): CourierStockCounters {
  return {
    inHand: left.inHand + right.inHand,
    pickedUp: left.pickedUp + right.pickedUp,
    outForDelivery: left.outForDelivery + right.outForDelivery,
    awaiting: left.awaiting + right.awaiting,
    failedOrReturning: left.failedOrReturning + right.failedOrReturning,
    deliveredUnremitted: left.deliveredUnremitted + right.deliveredUnremitted,
    collectedUnremittedInCents: left.collectedUnremittedInCents + right.collectedUnremittedInCents,
    toCollectInCents: left.toCollectInCents + right.toCollectInCents,
    lastMovementAt: latest(left.lastMovementAt, right.lastMovementAt),
  };
}

/** L'ordre d'affichage des blocs : celui du trajet d'un colis. */
export const COURIER_STOCK_BLOCKS: readonly CourierStockBlock[] = [
  'picked-up',
  'awaiting',
  'out-for-delivery',
  'failed-or-returning',
  'delivered-unremitted',
];

function latest(left: string | null, right: string | null): string | null {
  if (left === null) return right;
  if (right === null) return left;
  return left >= right ? left : right;
}
