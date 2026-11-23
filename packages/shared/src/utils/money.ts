import type { AmountInCents } from '../types/common';

/**
 * Arithmétique en centimes entiers — DOMAIN.md §4 dette n°2 : le legacy stockait
 * `colis.prix` en varchar(255) et `frais` en double. Aucun flottant ici.
 */

export const CURRENCY = 'MAD';

export function toCents(amount: number): AmountInCents {
  return Math.round(amount * 100);
}

export function fromCents(amount: AmountInCents): number {
  return amount / 100;
}

export function sumCents(amounts: readonly AmountInCents[]): AmountInCents {
  return amounts.reduce((total, amount) => total + amount, 0);
}

/**
 * Montant à encaisser à la livraison — DOMAIN.md §3.8.
 * En prod, `feeIncluded` est vrai sur 99,8 % des colis : le COD est le prix, frais compris.
 */
export function codToCollect(parcel: {
  codAmount: AmountInCents;
  deliveryFee: AmountInCents;
  feeIncluded: boolean;
  alreadyPaid: boolean;
}): AmountInCents {
  if (parcel.feeIncluded) {
    return parcel.alreadyPaid ? 0 : parcel.codAmount;
  }
  if (parcel.alreadyPaid) return parcel.deliveryFee;
  return parcel.codAmount + parcel.deliveryFee;
}

/**
 * Ce qu'un bon de versement (BV-) verse pour UN colis — lot Q, décision du
 * 07/10/2026.
 *
 * Le COD encaissé MOINS le chèque compté (`chequeCountsAsPaid()`) : SpeedyTrans
 * n'encaisse jamais un chèque, il va du destinataire au vendeur. Le verser en
 * plus en espèces, c'est virer de l'argent jamais reçu (CALC-15, PAY-B). C'est
 * la règle de la caisse RF- (espèces = COD − chèque), appliquée au versement.
 * Un colis payé tout en chèque ne verse rien.
 */
export function codPayoutAmount(
  collected: AmountInCents,
  chequePaid: AmountInCents,
): AmountInCents {
  return Math.max(0, collected - chequePaid);
}

/**
 * Le colis vu par la FACTURE — DOMAIN.md §3.8 et §3.9.
 *
 * Le legacy ne calculait jamais ce montant : `app/Facture.php:39-106`
 * additionnait sept relations filtrantes à chaque affichage, sur des colis dont
 * le statut pouvait changer après émission (§4 dette n°17). Ici la ligne est
 * calculée une fois, puis GELÉE.
 */
export interface BillableParcel {
  codAmount: AmountInCents;
  deliveryFee: AmountInCents;
  /** Frais d'échec figés au moment où le colis a basculé (§3.3). */
  failureFee: AmountInCents | null;
  /**
   * « Autres frais » refacturés par l'agence (emballage, stockage…) — audit du
   * 03/10, E4. Saisis sur la fiche, ils n'étaient facturés nulle part. Ils
   * s'ajoutent aux frais du colis, livré ou non. Absent = aucun.
   */
  otherFees?: AmountInCents;
  feeIncluded: boolean;
  alreadyPaid: boolean;
  /** `4` livré, `5` retourné, `6` refusé — les trois statuts facturables. */
  statusId: number;
}

/** Ce que la société a réellement encaissé : rien si le colis n'est pas livré. */
export function codCollected(parcel: BillableParcel, deliveredStatusId = 4): AmountInCents {
  return parcel.statusId === deliveredStatusId ? codToCollect(parcel) : 0;
}

/**
 * Ce que la société retient à l'e-commerçant.
 *
 * Livré : les frais de livraison. Échec : les frais d'échec, tels qu'ils ont
 * été figés sur le colis — on ne les recalcule pas, sinon une facture déjà
 * émise bougerait le jour où le réglage change.
 *
 * `alreadyPaid` ne dispense de rien ici : il dit que la MARCHANDISE était déjà
 * payée, pas la course.
 */
export function feeCharged(parcel: BillableParcel, deliveredStatusId = 4): AmountInCents {
  const other = parcel.otherFees ?? 0;
  if (parcel.statusId === deliveredStatusId) return parcel.deliveryFee + other;
  return (parcel.failureFee ?? 0) + other;
}

/**
 * TVA sur un montant HORS TAXES — B-11, tranchée par le PO.
 *
 * Le taux est en CENTIÈMES de pourcent : `2000` vaut 20,00 %. Aucun flottant
 * n'entre dans le calcul, et l'arrondi est fait une fois, au centime.
 */
export function vatOn(amountHt: AmountInCents, vatRate: number): AmountInCents {
  if (vatRate <= 0) return 0;
  return Math.round((amountHt * vatRate) / 10_000);
}

/**
 * Les taux de TVA qu'un Client Pro peut porter — lot R (D-230), en centièmes :
 * 20 %, 14 %, 10 %, 7 % et 0 % (exonéré). Le défaut société vaut 20 %.
 */
export const PRO_VAT_RATES = [2000, 1400, 1000, 700, 0] as const;
export type ProVatRate = (typeof PRO_VAT_RATES)[number];
export const DEFAULT_PRO_VAT_RATE: ProVatRate = 2000;

/**
 * Le taux d'une facture de Client Pro — lot R (D-230) : celui du client, sinon
 * le défaut société. `null` pour un client qui n'est pas Pro : sa facture garde
 * la TVA ligne par ligne de `finance.vatRate` (B-11), inchangée.
 */
export function proVatRateFor(
  client: { isPro?: boolean | null; vatRate?: number | null } | null | undefined,
  companyDefault: number,
): number | null {
  if (!client?.isPro) return null;
  return client.vatRate ?? companyDefault;
}

/**
 * La TVA d'un TOTAL hors taxes, signé — lot R (D-230). Calculée UNE fois sur
 * le total, jamais ligne par ligne : arrondie au centime, demi vers le haut en
 * valeur absolue (comme `round()` de PostgreSQL, que lit `check-invariants`).
 * Un total négatif (régularisation en faveur du vendeur) rend une TVA négative.
 */
export function vatOnTotal(amountHt: AmountInCents, vatRate: number): AmountInCents {
  const vat = vatOn(Math.abs(amountHt), vatRate);
  return amountHt < 0 && vat > 0 ? -vat : vat;
}

/**
 * Le net dû à l'e-commerçant pour UN colis : encaissé moins frais TTC.
 *
 * Les frais sont hors taxes et la TVA s'y ajoute (B-11) : c'est la réponse du
 * PO, et elle change le net — un colis livré 250 MAD avec 25 MAD de frais et
 * 20 % de TVA reverse 220 MAD, pas 225.
 *
 * Négatif quand l'e-commerçant doit de l'argent : un colis refusé n'encaisse
 * rien et laisse les frais d'échec, TVA comprise, à sa charge.
 */
export function merchantNetDue(
  parcel: BillableParcel,
  deliveredStatusId = 4,
  vatRate = 0,
): AmountInCents {
  const fee = feeCharged(parcel, deliveredStatusId);
  return codCollected(parcel, deliveredStatusId) - fee - vatOn(fee, vatRate);
}

/**
 * Le net dû pour UN colis, SELON LE RÉGIME du client — C11 bloc 3, D-061.
 *
 * `normal` : inchangé. La facture est le point de rencontre des deux flux —
 * elle encaisse le COD et retient les frais — et `merchantNetDue()` dit le
 * reste.
 *
 * `professional` : le COD **n'est pas sur cette facture**. Sa part espèces a
 * été versée à part par un `CodPayout`, sa part chèque transmise au vendeur
 * (lot Q). La facture ne réclame donc QUE les frais, TVA comprise, et son net
 * est toujours négatif ou nul : c'est l'e-commerçant qui doit.
 *
 * L'y compter reviendrait à le payer deux fois, et personne ne s'en apercevrait
 * avant le rapprochement bancaire du mois suivant.
 *
 * Le COD reste CALCULÉ et IMPRIMÉ sur la facture professionnelle : la ligne dit
 * ce que le colis a encaissé, parce que c'est ce que l'e-commerçant rapproche
 * de ses commandes. Il est informatif, jamais sommé dans le total général.
 */
export function netDueFor(
  billingType: string,
  parcel: BillableParcel,
  deliveredStatusId = 4,
  vatRate = 0,
  /**
   * Ce que le vendeur a DÉJÀ reçu en chèque sur ce colis — chantier
   * « Chèques », point 3.
   *
   * Le chèque est son argent : il change de mains du destinataire à lui, sans
   * passer par la trésorerie de la société. La facture ne peut donc pas le lui
   * redevoir — elle le lui devrait une seconde fois.
   *
   * Un chèque REJETÉ ou ANNULÉ ne compte pas : l'argent n'est jamais arrivé, et
   * le montant retourne au dû. La règle vit dans `chequeCountsAsPaid()`, une
   * seule fois, et l'appelant la lui demande.
   *
   * En `professional`, il est INFORMATIF (lot Q) : le bon de versement l'exclut
   * déjà (`codPayoutAmount`). Le retrancher ici réclamait au vendeur un chèque
   * que personne ne lui avait versé.
   */
  chequePaid: AmountInCents = 0,
): AmountInCents {
  const fee = feeCharged(parcel, deliveredStatusId);
  if (billingType === 'professional') return 0 - fee - vatOn(fee, vatRate);

  return codCollected(parcel, deliveredStatusId) - chequePaid - fee - vatOn(fee, vatRate);
}
