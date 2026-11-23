import type { DeliveryTarget } from './enums';
import type { AmountInCents, Id } from './types/common';

/**
 * Calcul du tarif de livraison — DOMAIN.md §3.1
 * (`ColisController::calculeFraisLivraison`, app/Http/Controllers/ColisController.php:1079-1122).
 *
 * Séquence legacy reproduite :
 *   1. chercher une remise client active dont la zone contient la ville de livraison
 *   2. base = tarifs[depart][destination].prix_{agence|quartier|point_relais}
 *   3. si remise : même ville → remise.sameCity, sinon → remise.flatRate
 *      (la remise ÉCRASE le tarif, elle ne s'y soustrait pas)
 *   4. frais = frais × multiplicateur du type de livraison
 *
 * Dettes corrigées ici :
 *  - `$tarifs[$livraison]` n'était pas protégé (`:1096`) : une paire de villes
 *    absente de la matrice levait une exception non gérée. Ici, `matched: false`
 *    et repli sur le tarif de base de la zone.
 *  - les montants sont en centimes entiers, jamais en flottant (§4 dette n°2).
 */

/** Une ligne de la matrice ville × ville, avec un prix par cible de livraison. */
export interface PricingRule {
  originCityId: Id;
  destinationCityId: Id;
  district: AmountInCents;
  agency: AmountInCents;
  relayPoint: AmountInCents;
}

/**
 * Remise client — legacy `ClientRemise`. Elle écrase le tarif de la matrice.
 * `zoneIds` ET `cityIds` vides = remise applicable à toutes les zones.
 */
export interface ClientDiscount {
  clientId: Id;
  zoneIds: Id[];
  /**
   * Lot K (D-218) — les villes de DESTINATION visées. Facultatif : absent = la
   * remise ne vise aucune ville, comme avant le lot K.
   */
  cityIds?: Id[];
  /** Tarif forfaitaire inter-ville. `null` = pas de forfait. */
  flatRate: AmountInCents | null;
  /** Tarif forfaitaire même ville. `null` = pas de forfait. */
  sameCityRate: AmountInCents | null;
  isActive: boolean;
}

/** Legacy `livraison_types` — une seule ligne en prod, multiplicateur 1. */
export interface DeliveryType {
  id: Id;
  multiplier: number;
}

export interface QuoteInput {
  originCityId: Id;
  destinationCityId: Id;
  deliveryTarget: DeliveryTarget;
  clientId?: Id;
  /** Multiplicateur du type de livraison. 1 si absent. */
  multiplier?: number;
}

export interface PricingContext {
  rules: readonly PricingRule[];
  discounts: readonly ClientDiscount[];
  /** Zone de chaque ville, pour savoir si une remise s'applique. */
  zoneOfCity: (cityId: Id) => Id | undefined;
  /** Tarifs de repli de la zone de destination, si la paire est absente de la matrice. */
  zoneRates: (zoneId: Id | undefined) => { baseRate: AmountInCents; sameCityRate: AmountInCents };
}

export interface Quote {
  /** Frais de livraison retenus, en centimes. */
  fee: AmountInCents;
  /** Tarif de la matrice avant remise, en centimes. */
  baseFee: AmountInCents;
  /** La paire de villes existe dans la matrice. Sinon, repli sur la zone. */
  matched: boolean;
  /** Une remise client a écrasé le tarif. */
  discounted: boolean;
  sameCity: boolean;
}

const TARGET_FIELD: Record<
  DeliveryTarget,
  keyof Pick<PricingRule, 'district' | 'agency' | 'relayPoint'>
> = {
  district: 'district',
  agency: 'agency',
  'relay-point': 'relayPoint',
};

/**
 * Le niveau de précision d'une remise — lot K (D-218) : `city` si elle cite au
 * moins une ville, `zone` si elle ne cite que des zones, `all` sinon. C'est
 * l'ordre dans lequel la fiche client les affiche ; le devis, lui, regarde la
 * destination (une remise « ville » qui cite aussi des zones vaut « zone » hors
 * de ses villes).
 */
export type DiscountTarget = 'city' | 'zone' | 'all';

export function discountTarget(discount: {
  zoneIds: readonly Id[];
  cityIds?: readonly Id[];
}): DiscountTarget {
  if ((discount.cityIds ?? []).length > 0) return 'city';
  return discount.zoneIds.length > 0 ? 'zone' : 'all';
}

export function quoteDeliveryFee(input: QuoteInput, context: PricingContext): Quote {
  const sameCity = input.originCityId === input.destinationCityId;
  const destinationZoneId = context.zoneOfCity(input.destinationCityId);

  const rule = context.rules.find(
    (item) =>
      item.originCityId === input.originCityId &&
      item.destinationCityId === input.destinationCityId,
  );

  const fallback = context.zoneRates(destinationZoneId);
  const baseFee = rule
    ? rule[TARGET_FIELD[input.deliveryTarget]]
    : sameCity
      ? fallback.sameCityRate
      : fallback.baseRate;

  /*
   * LA REMISE LA PLUS PRÉCISE GAGNE — lot H (D-209), lot K (D-218).
   *
   * Remise qui NOMME la ville de destination > remise qui nomme sa zone >
   * remise « toutes les zones » > tarif société. Une remise qui cite des villes
   * et des zones compte au niveau « ville » pour ses villes, au niveau « zone »
   * ailleurs dans ses zones ; une remise qui ne cite que des villes ne vaut pas
   * « toutes les zones ». À précision égale, l'ordre fourni (le service trie
   * par date de création) départage : la plus ancienne.
   */
  const active = input.clientId
    ? context.discounts.filter((item) => item.isActive && item.clientId === input.clientId)
    : [];
  const discount =
    active.find((item) => (item.cityIds ?? []).includes(input.destinationCityId)) ??
    active.find(
      (item) => destinationZoneId !== undefined && item.zoneIds.includes(destinationZoneId),
    ) ??
    active.find((item) => discountTarget(item) === 'all');

  // §3.1 étape 3 : la remise écrase le tarif, elle ne s'y soustrait pas.
  const override = discount
    ? sameCity
      ? (discount.sameCityRate ?? discount.flatRate)
      : discount.flatRate
    : null;

  const multiplier = input.multiplier ?? 1;
  const fee = Math.round((override ?? baseFee) * multiplier);

  return {
    fee,
    baseFee,
    matched: rule !== undefined,
    discounted: override !== null,
    sameCity,
  };
}

/**
 * Repli des politiques tarifaires — valeurs observées en production.
 *
 * Ce ne sont PAS les valeurs de référence : docs/DECISIONS.md D-019 impose que
 * frais d'échec, frais de ramassage et seuil de gratuité soient des `Setting`,
 * réglables depuis Paramètres > Société. L'API les lit et les passe aux
 * fonctions ci-dessous ; ces constantes ne servent que lorsque la configuration
 * est muette — base pas encore seedée, ou clé supprimée à la main.
 *
 * DOMAIN.md §3.3 : `config(1) = 0` MAD en même ville, `config(2) = 10` MAD
 * sinon. §3.4 : ramassage à 20 MAD, offert à partir de 5 colis.
 */
export const DEFAULT_PRICING_POLICY = {
  failureFeeSameCity: 0 as AmountInCents,
  failureFeeCrossCity: 1000 as AmountInCents,
  pickupFee: 2000 as AmountInCents,
  freePickupThreshold: 5,
} as const;

export interface PricingPolicy {
  failureFeeSameCity: AmountInCents;
  failureFeeCrossCity: AmountInCents;
  pickupFee: AmountInCents;
  freePickupThreshold: number;
}

/**
 * Liste legacy des statuts « colis échoué » (app/Colis.php:115), moins les
 * statuts morts. Ceci EST une règle du domaine — quels statuts constituent un
 * échec — et non une politique commerciale : elle reste une constante.
 */
const FAILURE_FEE_STATUS_IDS: readonly number[] = [
  5, 6, 8, 9, 10, 11, 23, 24, 31, 32, 33, 34, 35, 37,
];

export function isFailureFeeStatus(statusId: number): boolean {
  return FAILURE_FEE_STATUS_IDS.includes(statusId);
}

/** Les statuts du flux retour (20 En attente de retour, 22 Retourné à l'agence, 23 Rendu au client, 21 / 24 historiques). */
const RETURN_FLOW_IDS: readonly number[] = [20, 21, 22, 23, 24];

/**
 * CE QUE DEVIENNENT `isFailed` ET `failureFee` À UN CHANGEMENT DE STATUT — CALC-02, D-167.
 *
 * Avant : les deux étaient RECALCULÉS à chaque statut. Un colis refusé
 * inter-villes (frais d'échec 10 DH) qui entrait dans le flux retour (20) les
 * perdait — 20 n'est pas un statut d'échec — et rien ne les retrouvait en 22 ni
 * en 23 : le frais n'était plus jamais facturable (confirmé par la recette
 * argent du 05/10/2026 : `failureFee` NULL, `isFailed` false après 20).
 *
 * La règle : un ÉCHEC est un fait acquis. Quand le colis entre ou avance dans
 * le flux retour après un échec, l'échec et son frais sont PORTÉS tels quels
 * (`failureFee: undefined` = ne pas toucher la colonne). Partout ailleurs, le
 * calcul d'avant s'applique. Les montants ne sont pas recalculés avec la grille
 * du jour : c'est le frais constaté au moment de l'échec qui est dû.
 */
export function failureAfterTransition(
  parcel: { isFailed: boolean; failureFee: AmountInCents | null },
  target: { statusId: number; isFailure: boolean },
  nextFailureFee: AmountInCents | null,
): { isFailed: boolean; failureFee: AmountInCents | null | undefined } {
  if (parcel.isFailed && RETURN_FLOW_IDS.includes(target.statusId)) {
    /*
     * UN ÉCHEC PORTÉ SANS FRAIS EN PREND UN — lot Argent 2, A2-4 (D-183).
     * CAS100092610CAS (27 annulé par le client), CAS110072610CAS (25
     * messagerie) : leur échec ne figure pas dans `FAILURE_FEE_STATUS_IDS`, il
     * était porté à NULL jusqu'au retour au vendeur (23), qui en est un. Un
     * frais déjà posé — même 0, par « Modifier les frais » — n'est jamais
     * écrasé : seul le NULL (jamais posé) prend celui du statut cible.
     */
    return {
      isFailed: true,
      failureFee: parcel.failureFee === null && nextFailureFee !== null ? nextFailureFee : undefined,
    };
  }
  return { isFailed: target.isFailure, failureFee: nextFailureFee };
}

/**
 * Frais d'échec — DOMAIN.md §3.3.
 * Le montant vient de la POLITIQUE, la liste des statuts de la RÈGLE.
 */
export function failureFee(
  statusId: number,
  sameCity: boolean,
  policy: PricingPolicy = DEFAULT_PRICING_POLICY,
): AmountInCents | null {
  if (!isFailureFeeStatus(statusId)) return null;
  return sameCity ? policy.failureFeeSameCity : policy.failureFeeCrossCity;
}

/** DOMAIN.md §3.4 — le ramassage est-il offert sur ce bon ? */
export function isPickupWaived(
  parcelCount: number,
  policy: PricingPolicy = DEFAULT_PRICING_POLICY,
): boolean {
  return parcelCount >= policy.freePickupThreshold;
}

/** Les conditions de ramassage PROPRES à un client — `null` = la valeur globale. */
export interface ClientPickupTerms {
  pickupFee: AmountInCents | null;
  freePickupThreshold: number | null;
}

/**
 * La politique de ramassage EFFECTIVE d'un client — lot T (D-232).
 *
 * Chaque champ renseigné sur la fiche remplace le global (Paramètres >
 * Finance, surchargé par l'agence) ; un champ vide le laisse. `0` est une
 * valeur, pas une absence : frais 0 = jamais facturé, seuil 0 = toujours
 * offert. Les frais d'échec ne sont pas concernés.
 */
export function effectivePickupPolicy(
  policy: PricingPolicy,
  client: Partial<ClientPickupTerms> | null | undefined,
): PricingPolicy {
  return {
    ...policy,
    pickupFee: client?.pickupFee ?? policy.pickupFee,
    freePickupThreshold: client?.freePickupThreshold ?? policy.freePickupThreshold,
  };
}
