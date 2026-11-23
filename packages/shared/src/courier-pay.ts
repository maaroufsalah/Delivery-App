import { isFailureFeeStatus } from './pricing';
import type { AmountInCents, Id } from './types/common';

/**
 * Rémunération du livreur — C11 bloc 1, docs/DECISIONS.md D-058 et D-059.
 *
 * Deux axes INDÉPENDANTS, et c'est tout l'objet de ce fichier :
 *
 * ```
 *                  ce qu'on lui doit          quand on le lui règle
 *   payModel  →  SALARY · COMMISSION · HYBRID
 *   settlementMode  →                       AT_REMITTANCE · MONTHLY
 * ```
 *
 * Le legacy n'avait ni l'un ni l'autre : `frais_livreur` était renseigné 58
 * fois sur toute la base et `facture_livreur_colis` comptait UNE ligne
 * (DOMAIN.md §5 question n°3). D-020 avait posé le premier axe au schéma sans
 * l'implémenter ; C11 ajoute le second, parce que « combien » et « quand » ne
 * sont pas la même question — un livreur payé à la commission peut très bien
 * être réglé au mois, et un salarié toucher une prime déduite à la remise.
 *
 * Rien ici ne touche la base : la fonction reçoit la grille et rend un montant.
 * C'est la même discipline que `pricing.ts`, et elle a la même raison — un
 * calcul d'argent se teste sans PostgreSQL.
 */

/** D'où vient le montant figé. Imprimé sur le relevé : un livreur conteste. */
export const COURIER_COMMISSION_SOURCES = ['grid', 'negotiated', 'failure-setting'] as const;
export type CourierCommissionSource = (typeof COURIER_COMMISSION_SOURCES)[number];

/**
 * Ce que la course a produit, du point de vue de la PAIE.
 *
 * Volontairement plus pauvre que les 34 statuts : la grille `CourierCity` ne
 * connaît que deux tarifs, et l'échec n'en a aucun — il a un réglage.
 */
export type CourierCourseOutcome = 'delivery' | 'pickup' | 'failure';

/** Une ligne de la grille `livreur_ville` réduite à ce que le calcul utilise. */
export interface CourierGridRate {
  cityId: Id;
  pickupFee: AmountInCents;
  deliveryFee: AmountInCents;
}

export interface CourierCommissionInput {
  outcome: CourierCourseOutcome;
  /** Ville qui porte le tarif : origine pour un ramassage, destination sinon. */
  cityId: Id;
  /** Rémunération négociée à la course (`route_parcels.courierFee`). Elle PRIME. */
  negotiatedFee: AmountInCents | null;
  grid: readonly CourierGridRate[];
  /** `finance.courierFailedCommission` — ce que vaut une course sans issue payante. */
  failedCommission: AmountInCents;
  /** Ville du CIRCUIT, repli quand celle du colis n'est pas dans la grille. */
  fallbackCityId?: Id;
}

export interface CourierCommission {
  amount: AmountInCents;
  source: CourierCommissionSource;
}

/**
 * La commission d'UNE course, au moment où elle se fige.
 *
 * Trois sources, dans cet ordre :
 *  1. le montant **négocié** sur la ligne de circuit, s'il est renseigné — une
 *    course payée à part est une décision prise en connaissance de cause ;
 *  2. la **grille** du livreur pour la ville, ramassage ou livraison ;
 *  3. le **réglage** d'échec, pour une course qui n'aboutit pas.
 *
 * Une ville hors grille rend ZÉRO et le dit (`source: 'grid'`), plutôt que de
 * chercher ailleurs : un total court se voit sur un relevé, une commission
 * inventée non.
 */
export function courierCommissionFor(input: CourierCommissionInput): CourierCommission {
  if (input.negotiatedFee !== null) {
    return { amount: input.negotiatedFee, source: 'negotiated' };
  }

  if (input.outcome === 'failure') {
    return { amount: input.failedCommission, source: 'failure-setting' };
  }

  const field = input.outcome === 'pickup' ? 'pickupFee' : 'deliveryFee';
  const row =
    input.grid.find((rate) => rate.cityId === input.cityId) ??
    (input.fallbackCityId === undefined
      ? undefined
      : input.grid.find((rate) => rate.cityId === input.fallbackCityId));

  return { amount: row?.[field] ?? 0, source: 'grid' };
}

/**
 * L'issue d'une course, traduite depuis le statut rapporté par le livreur.
 *
 * `null` = rien à figer. Une tentative sans issue — reporté, injoignable qui
 * repassera — n'est ni une livraison ni un échec définitif : le livreur s'est
 * déplacé, mais la course n'est pas finie. Le legacy ne payait rien non plus,
 * et on ne lui invente pas une générosité rétroactive (D-038).
 */
export function courseOutcomeOf(
  routeKind: 'PICKUP' | 'DELIVERY' | string,
  finalStatusId: number | null | undefined,
  deliveredStatusId = 4,
): CourierCourseOutcome | null {
  if (routeKind === 'PICKUP') return 'pickup';
  if (finalStatusId === null || finalStatusId === undefined) return null;
  if (finalStatusId === deliveredStatusId) return 'delivery';
  return isFailureFeeStatus(finalStatusId) ? 'failure' : null;
}

/**
 * Quand la commission est réglée — le second axe.
 *
 * `AT_REMITTANCE` : elle est déduite du cash que le livreur rapporte. Il garde
 * sa part en main, la société encaisse le net. C'est le fonctionnement décrit
 * par le PO comme « normal ».
 *
 * `MONTHLY` : il rend TOUT le cash, et ses commissions s'accumulent sur son
 * relevé de période. C'est le fonctionnement d'un salarié.
 */
export const SETTLEMENT_MODES = ['at-remittance', 'monthly'] as const;
export type SettlementMode = (typeof SETTLEMENT_MODES)[number];

/** Le fixe entre-t-il dans le dû ? `COMMISSION` seul ne connaît pas de fixe. */
export function fixedPartOf(payModel: string, fixedSalary: AmountInCents | null): AmountInCents {
  if (payModel === 'salary' || payModel === 'hybrid') return fixedSalary ?? 0;
  return 0;
}

/**
 * Le variable entre-t-il dans le dû ?
 *
 * En `SALARY`, les commissions sont CALCULÉES et AFFICHÉES — elles disent le
 * travail fourni — mais ne s'ajoutent pas : le fixe EST la paie. C'est la seule
 * lecture qui rende le mode utile plutôt que vide.
 */
export function variablePartOf(payModel: string, commissions: AmountInCents): AmountInCents {
  return payModel === 'salary' ? 0 : commissions;
}

/* --- Facturation : ce qui est facturable, et quand (C11 bloc 3) ------------ */

/**
 * La règle générale du PO, en une fonction — docs/DECISIONS.md D-061.
 *
 * > **On ne facture que ce qui est livré ET remboursé par le livreur.**
 *
 * DOMAIN.md §3.9 relevait le défaut comme un CONSTAT : « un colis peut être
 * facturé au client sans que le cash ait été remis par le livreur ». D-036 a
 * posé l'unicité en base mais n'a rien interdit de tel — « on saura le dire, on
 * ne l'empêche pas ». Le PO a tranché : on l'empêche.
 *
 * **L'échec est l'exception, et il ne peut pas en être autrement.** Un colis
 * refusé n'encaisse rien : il n'y a aucun cash à rembourser, donc exiger un
 * remboursement le rendrait éternellement non facturable alors qu'il porte des
 * frais d'échec bien réels. Il devient facturable une fois son MOIS CLOS — le
 * temps qu'une reprise, une contestation ou une seconde tentative se manifeste.
 *
 * Elle vit ici plutôt que dans une requête Prisma pour que les deux
 * consommateurs — le sélecteur de colis facturables et la facture mensuelle —
 * lisent la MÊME définition. Deux `where` recopiés auraient divergé au premier
 * ajustement, et c'est une divergence dont personne ne s'aperçoit avant qu'un
 * client réclame.
 */
export function isBillableNow(
  parcel: {
    statusId: number;
    reimbursedByCourier?: boolean;
    /** Date du passage au statut courant. */
    statusAt: string | Date;
  },
  now: Date,
  deliveredStatusId = 4,
): boolean {
  if (parcel.statusId === deliveredStatusId) return parcel.reimbursedByCourier === true;
  return isMonthClosed(parcel.statusAt, now);
}

/**
 * LES TROIS CAS DE LA FACTURATION — chantier « Retours, BL Clients Pro »,
 * point 6.
 *
 * > « Un colis Livré avec frais de livraison = 0 est facturable et apparaît
 * > dans le détail de la facture avec “Frais de livraison : 0,00 MAD”, compté
 * > dans les colis livrés. Distinguer non facturable / facturable > 0 /
 * > facturable = 0 dans la logique et les tests. »
 *
 * **Le zéro n'est pas une absence, et c'est tout le sujet.** En JavaScript,
 * `0` est falsy : `if (fee)`, `fee ? … : …`, `fee || défaut` traitent un colis
 * facturé zéro exactement comme un colis qu'on ne facture pas. La confusion ne
 * se voit jamais à l'écriture — elle se voit six mois plus tard, quand un
 * e-commerçant compte ses colis livrés et n'en trouve pas autant sur sa
 * facture.
 *
 * Un colis à zéro franc N'EST PAS un colis gratuit par accident : c'est un
 * geste commercial, une remise, un rattrapage de litige. Il doit apparaître, et
 * il doit apparaître AVEC son zéro — c'est précisément la ligne qui prouve que
 * le geste a été fait.
 *
 * `billabilityOf()` nomme les trois cas pour qu'aucun `if` ne les confonde. Le
 * code consommateur teste une CHAÎNE, pas un montant ; `'billable-free'` se lit
 * de travers, `0` se lit à l'envers.
 */
export type Billability = 'not-billable' | 'billable' | 'billable-free';

export function billabilityOf(
  parcel: {
    statusId: number;
    reimbursedByCourier?: boolean;
    statusAt: string | Date;
    /** Frais de livraison en centimes, tels qu'ils sont sur le colis. */
    deliveryFee: AmountInCents;
    /** Frais d'échec figés, `null` sur un colis livré. */
    failureFee?: AmountInCents | null;
  },
  now: Date,
  deliveredStatusId = 4,
): Billability {
  if (!isBillableNow(parcel, now, deliveredStatusId)) return 'not-billable';

  /*
   * Ce que la facture RETIENT sur ce colis : les frais de livraison s'il est
   * livré, les frais d'échec sinon. La même règle que `feeCharged()`, qu'on ne
   * peut pas appeler ici sans faire dépendre la paie du livreur des utilitaires
   * monétaires — et l'inverse serait pire.
   */
  const charged =
    parcel.statusId === deliveredStatusId ? parcel.deliveryFee : (parcel.failureFee ?? 0);

  return charged === 0 ? 'billable-free' : 'billable';
}

/**
 * Facturable, zéro compris — LA fonction que le code métier doit appeler.
 *
 * Elle existe pour qu'on n'écrive jamais `fee > 0` en croyant filtrer les colis
 * non facturables. Les deux cas facturables se répondent oui ; seul
 * `'not-billable'` se répond non.
 */
export function isBillable(billability: Billability): boolean {
  return billability !== 'not-billable';
}

/**
 * Le mois de cette date est-il révolu ?
 *
 * En UTC, comme toutes les bornes du produit : DOMAIN.md §4 dette n°21 — le
 * legacy écrivait l'heure serveur sans fuseau, et Dakar et Casablanca
 * cohabitaient déjà dans la même base.
 */
export function isMonthClosed(at: string | Date, now: Date): boolean {
  const date = typeof at === 'string' ? new Date(at) : at;
  return date.getTime() < startOfMonthUtc(now).getTime();
}

/** Premier instant du mois courant, en UTC — la borne de `isMonthClosed`. */
export function startOfMonthUtc(now: Date): Date {
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));
}
