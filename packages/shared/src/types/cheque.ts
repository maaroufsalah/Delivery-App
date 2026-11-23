import type { AmountInCents, Id, IsoDateTime, Paginated } from './common';

/**
 * LE CHÈQUE REÇU DU DESTINATAIRE — chantier « Finance », points 8 et 9, D-108.
 *
 * **C'est un TROISIÈME papier.** Deux chèques existaient déjà, et aucun n'est
 * celui-ci :
 *
 * | Où | Qui paie qui |
 * |---|---|
 * | `Payment.cheque*` | l'e-commerçant règle sa facture à la société |
 * | `CourierStatement.cheque*` | la société paie son livreur |
 * | `Cheque` (ici) | le DESTINATAIRE paie son colis au livreur |
 *
 * Trois flux opposés, trois comptes, trois cycles : les mettre dans la même
 * table aurait fait dire n'importe quoi au premier rapprochement bancaire.
 */

/**
 * **SPEEDYTRANS N'ENCAISSE JAMAIS** — chantier « Chèques », point 2.
 *
 * Le chèque appartient au VENDEUR. La société le transporte, de la main du
 * destinataire à celle de son propriétaire, et ne le dépose sur aucun compte.
 * Cinq marches et deux issues, qui suivent le PAPIER.
 *
 * Les paires se séparent aux deux bouts, pour la même raison qu'entre « pris
 * en charge » et « remis à l'entrepôt » sur les bons de retour (D-094) : celui
 * qui donne et celui qui reçoit ne parlent pas au même moment, et un chèque
 * perdu entre les deux doit rester repérable. Le livreur dit « je l'ai
 * donné », l'agence « je l'ai reçu » ; l'agence dit « je l'ai transmis », le
 * vendeur « je l'ai bien reçu ».
 */
export const CHEQUE_STATUSES = [
  'recu-livreur',
  'remis-admin',
  'recu-admin',
  'transmis-vendeur',
  'recu-vendeur',
  'rejete',
  'annule',
] as const;

export type ChequeStatus = (typeof CHEQUE_STATUSES)[number];

export const CHEQUE_STATUS_COLORS: Record<ChequeStatus, `#${string}`> = {
  'recu-livreur': '#D35400',
  'remis-admin': '#2980B9',
  'recu-admin': '#8C7AE6',
  'transmis-vendeur': '#F39C12',
  'recu-vendeur': '#6AB04C',
  rejete: '#D63031',
  annule: '#78716C',
};

/** L'ordre des marches nominales. Les deux issues n'y figurent pas. */
export const CHEQUE_FLOW: readonly ChequeStatus[] = [
  'recu-livreur',
  'remis-admin',
  'recu-admin',
  'transmis-vendeur',
  'recu-vendeur',
];

/**
 * La marche suivante, ou `null` au bout du chemin.
 *
 * Une seule définition de l'ordre : l'écran ne la recopie pas, l'API la lit
 * d'ici, et un test vérifie qu'elles disent la même chose.
 */
export function nextChequeStatus(status: ChequeStatus): ChequeStatus | null {
  const at = CHEQUE_FLOW.indexOf(status);
  if (at === -1) return null;

  return CHEQUE_FLOW[at + 1] ?? null;
}

/**
 * Ce chèque peut-il être REJETÉ ?
 *
 * Le rejet vient du VENDEUR qui rend le papier — provision insuffisante,
 * signature non conforme, montant contesté. Il n'est donc possible qu'une fois
 * l'agence en possession du chèque, et il exige un MOTIF : un chèque rendu
 * sans raison est une ligne que personne ne saura expliquer six semaines plus
 * tard, ni au vendeur, ni au destinataire qui l'a signé.
 */
export function canRejectCheque(status: ChequeStatus): boolean {
  return status === 'recu-admin' || status === 'transmis-vendeur' || status === 'recu-vendeur';
}

/**
 * Annulable tant que le VENDEUR ne l'a pas confirmé.
 *
 * Après sa confirmation, le papier est chez lui et l'affaire est close : une
 * annulation remettrait au dû (point 3) un montant que le vendeur a déjà
 * encaissé de son côté.
 */
export function canCancelCheque(status: ChequeStatus): boolean {
  return status !== 'recu-vendeur' && status !== 'annule' && status !== 'rejete';
}

/**
 * Ce chèque se retranche-t-il de ce que Speedy verse au vendeur ?
 *
 * **D-237 (lot W4, décision de Salah)** : SpeedyTrans n'encaisse jamais un
 * chèque et ne paie jamais au vendeur un argent qu'il n'a pas reçu. La part
 * versée en espèces (BV, net de la facture normale, OV) vaut les espèces
 * RÉELLEMENT encaissées : COD − tout chèque reçu pour le colis, **rejeté
 * compris**. Un chèque rejeté est un litige entre le vendeur et son
 * destinataire : le papier lui est rendu, Speedy ne le rembourse pas.
 *
 * Seul un chèque ANNULÉ sort de la règle — et pas toujours : voir
 * `cancelledChequeDeduction()`. Avant D-237, un rejet remettait le montant au
 * dû, et le BV versait en espèces 500 jamais encaissés (SC-003).
 */
export function chequeCountsAsPaid(status: ChequeStatus): boolean {
  return status !== 'annule';
}

/**
 * Ce qu'un chèque ANNULÉ retranche encore — D-237.
 *
 * L'annulation sert au chèque saisi par erreur : le destinataire a en fait
 * payé en espèces. Ces espèces ne sont prouvées que par la caisse RF- du
 * livreur, dont la ligne gèle la part chèque (`cash_remittance_parcels.cheque`) :
 * le livreur qui reconfirme sa course en espèces la remet à zéro.
 *
 * - ligne RF- connue : la part que la caisse a comptée en chèque (0 si les
 *   espèces y sont entrées, le montant versé est alors le COD entier) ;
 * - aucune ligne RF- (`null`) : aucune espèce prouvée, le chèque se retranche
 *   encore — lecture la plus prudente.
 */
export function cancelledChequeDeduction(
  amount: AmountInCents,
  remittedCheque: AmountInCents | null,
): AmountInCents {
  if (remittedCheque === null) return amount;
  return Math.min(amount, Math.max(0, remittedCheque));
}

/**
 * Les chèques encore EN ROUTE vers leur vendeur — lot S (D-231).
 *
 * Ce que compte la tuile « Mes chèques » : tout ce qui n'est pas arrivé chez
 * lui (réception confirmée) ni sorti du circuit (rejeté, annulé). Dérivé du
 * chemin, pas recopié : une marche ajoutée demain y tombera d'elle-même.
 */
export const PENDING_CHEQUE_STATUSES: readonly ChequeStatus[] = CHEQUE_FLOW.filter(
  (status) => status !== 'recu-vendeur',
);

/**
 * La valeur du filtre `status` qui sélectionne `PENDING_CHEQUE_STATUSES`.
 * Elle ne peut pas être un état : ce n'en est pas un, c'en est quatre.
 */
export const CHEQUE_PENDING_FILTER = 'en-cours';

/**
 * Une page de chèques et ce qu'elle totalise — lot S.
 *
 * `totalAmount` couvre TOUT le filtre, pas la seule page : un total qui change
 * en passant de la page 1 à la page 2 ne répond à aucune question.
 */
export interface ChequePage extends Paginated<ChequeListItem> {
  totalAmount: AmountInCents;
}

export interface ChequeListItem {
  id: Id;
  status: ChequeStatus;
  number: string;
  amount: AmountInCents;
  bank: string | null;
  chequeDate: string | null;
  receivedAt: IsoDateTime;
  payerName: string;

  parcelId: Id;
  parcelCode: string;
  clientName: string;
  courierId: Id;
  courierName: string;
  invoiceCode: string | null;
}

export interface ChequeEvent {
  id: Id;
  status: ChequeStatus;
  /**
   * `transition` : une marche du cycle. `slip` : un bon de remise téléchargé
   * (D-265) — une trace, `status` y est celui du moment, inchangé.
   */
  kind: 'transition' | 'slip';
  comment: string | null;
  authorName: string | null;
  createdAt: IsoDateTime;
}

export interface ChequeDetail extends ChequeListItem {
  note: string | null;
  rejectionReason: string | null;
  events: ChequeEvent[];

  /**
   * Les gestes que le SERVEUR autorise — calculés là et nulle part ailleurs
   * (D-102). `nextStatus` est `null` quand le chèque est au bout ou sorti.
   */
  nextStatus: ChequeStatus | null;
  rejectable: boolean;
  cancellable: boolean;
}

/**
 * Le détail du PAIEMENT d'un colis — chantier « Finance », point 8.
 *
 * `cash + cheque = collected`, et les trois voyagent. La soustraction faite à
 * l'écran se serait faite quatre fois différemment.
 */
export interface ParcelPayment {
  collected: AmountInCents;
  cash: AmountInCents;
  cheque: AmountInCents;
  /** Le chèque reçu, s'il y en a un. */
  chequeNumber: string | null;
  chequeStatus: ChequeStatus | null;
}

/**
 * CE QU'UN GESTE EN MASSE A FAIT — chantier « Chèques », point 2.
 *
 * Les refus sont NOMMÉS, un par un. Un « 17 sur 20 » sans dire lesquels oblige
 * l'opérateur à rouvrir la liste et à comparer à l'œil ; il ne le fera pas, et
 * les trois chèques restés en arrière ne partiront jamais.
 */
export interface ChequeBulkResult {
  done: Id[];
  skipped: Array<{
    id: Id;
    /** `null` quand le chèque n'a pas pu être lu du tout. */
    number: string | null;
    reason: 'cheque-wrong-status' | 'cheque-unreachable';
    status: ChequeStatus | null;
  }>;
}
