/**
 * LE GRAND LIVRE D'UN LIVREUR — D-107.
 *
 * Ce fichier portait la caisse du jour ; elle a fusionné dans le retour de
 * fonds (D-115), et il ne reste ici que ce qui n'a jamais été une caisse : le
 * compte courant du livreur, alimenté par les écarts de clôture et soldé par
 * ses règlements.
 */

import type { AmountInCents, Id, IsoDateTime } from './common';

/** Nature d'une écriture : un écart constaté, ou un règlement. */
export const COURIER_BALANCE_KINDS = ['variance', 'settlement'] as const;
export type CourierBalanceKind = (typeof COURIER_BALANCE_KINDS)[number];

/** Une écriture au grand livre d'un livreur — point 4, D-107. */
export interface CourierBalanceEntry {
  id: Id;
  kind: CourierBalanceKind;
  /** SIGNÉ : négatif quand le livreur doit, positif quand il règle. */
  amount: AmountInCents;
  /**
   * Le BON d'où vient l'écart, par son NUMÉRO.
   *
   * C'était un jour de caisse ; un numéro vaut mieux — il se retrouve,
   * s'imprime, et se montre au livreur qui conteste. Une date ne disait pas de
   * quel comptage il s'agissait.
   */
  remittanceCode: string | null;
  method: 'cash' | 'transfer' | 'check' | null;
  note: string | null;
  authorName: string;
  at: IsoDateTime;
}

/**
 * Le SOLDE d'un livreur, et son histoire — point 4, D-107.
 *
 * `balance` est la SOMME du grand livre, jamais une colonne : une colonne se
 * serait désynchronisée le jour où une écriture est rejouée. Négatif veut dire
 * « le livreur doit », et c'est le sens que la fiche affiche.
 */
export interface CourierBalance {
  courierId: Id;
  courierName: string;
  balance: AmountInCents;
  entries: CourierBalanceEntry[];
}
