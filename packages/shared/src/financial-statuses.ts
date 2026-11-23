/**
 * LE STATUT FINANCIER DU COLIS — chantier « Finance », point 1, D-105.
 *
 * **Deux axes, jamais mélangés.** Le statut logistique dit où est le carton
 * (35 valeurs, `statuses.ts`) ; celui-ci dit où est son ARGENT. Le legacy
 * faisait porter les deux par `colis.statut_id`, qui a fini par ne plus rien
 * vouloir dire — DOMAIN.md §4 dette n°1. Le schéma v2 s'en défendait déjà à
 * moitié : `Parcel.reimbursedByCourier` porte le commentaire « un ATTRIBUT, pas
 * un statut ». Ce fichier termine le travail.
 *
 * **Il ne remplace rien, il RÉUNIT.** Trois colonnes disaient déjà des morceaux
 * de cette histoire :
 *
 * | Colonne | Ce qu'elle dit | Marche correspondante |
 * |---|---|---|
 * | `RouteParcel.codCollected` | le livreur a encaissé | `encaisse` |
 * | `Parcel.reimbursedByCourier` | il a rendu le cash | `remis-livreur` |
 * | `Parcel.billingState` | la facture est émise / réglée | `facture` / `rembourse` |
 *
 * Aucune n'est retirée — les tableaux de bord, l'espace vendeur et les exports
 * les lisent. Elles continuent d'être écrites exactement comme avant ; la
 * nouvelle colonne est écrite EN MÊME TEMPS, dans la même transaction, et
 * `financialStatusFrom()` ci-dessous est la seule définition du lien entre les
 * deux mondes. La migration de reprise s'en sert, les services s'en servent, et
 * un test le vérifie — c'est ce qui empêche les deux vérités de diverger.
 */

/**
 * Les cinq marches, DANS L'ORDRE de l'argent.
 *
 * L'ordre n'est pas décoratif : `financialRank()` s'en sert pour refuser un
 * retour en arrière silencieux.
 */
export const FINANCIAL_STATUSES = [
  /** Livré ou non, l'argent n'est pas encore entré. */
  'non-encaisse',
  /** Le livreur a encaissé — espèces, chèque, ou zéro sur un colis déjà payé. */
  'encaisse',
  /** Le livreur a rendu ce qu'il portait : bon de retour de fonds clôturé. */
  'remis-livreur',
  /** Porté par une facture client ÉMISE. */
  'facture',
  /** La facture est réglée : l'e-commerçant a été remboursé. */
  'rembourse',
] as const;

export type FinancialStatus = (typeof FINANCIAL_STATUSES)[number];

/**
 * Couleurs, comme partout : le vocabulaire visuel vient d'ici, jamais d'une
 * palette Tailwind posée dans un composant (CLAUDE.md §3).
 *
 * La progression va du gris — rien n'est entré — au vert : l'affaire est close.
 */
export const FINANCIAL_STATUS_COLORS: Record<FinancialStatus, `#${string}`> = {
  'non-encaisse': '#78716C',
  encaisse: '#2980B9',
  'remis-livreur': '#8C7AE6',
  facture: '#D35400',
  rembourse: '#6AB04C',
};

/**
 * LE STATUT FINANCIER VU PAR LE VENDEUR — lot O, D-227.
 *
 * « Facturé si le colis est facturé, Payé si le colis est payé » (Anass,
 * 25/10/2026). Ce n'est pas un second calcul : c'est la MÊME colonne, lue par
 * `merchantFinancialStatusOf()`, avec deux réserves :
 *
 *  - `rembourse` (la facture est soldée) se dit **Payé** chez le vendeur ;
 *  - `remis-livreur` se dit **Encaissé**, comme `encaisse` : que le livreur ait
 *    déjà vidé sa sacoche est de la trésorerie interne (D-060), et les deux
 *    marches confondues ne la trahissent pas.
 *
 * Un colis sur un BROUILLON reste à sa marche d'encaissement : la facture
 * n'existe pour le vendeur qu'une fois émise (REGLES-FACTURATION.md § 1).
 */
export const MERCHANT_FINANCIAL_STATUSES = ['non-encaisse', 'encaisse', 'facture', 'paye'] as const;

export type MerchantFinancialStatus = (typeof MERCHANT_FINANCIAL_STATUSES)[number];

/** La marche interne → ce que lit le vendeur. Priorité : Payé > Facturé > Encaissé > Non encaissé. */
export function merchantFinancialStatusOf(status: FinancialStatus): MerchantFinancialStatus {
  if (status === 'rembourse') return 'paye';
  if (status === 'facture') return 'facture';
  if (status === 'encaisse' || status === 'remis-livreur') return 'encaisse';
  return 'non-encaisse';
}

/** Le chemin inverse, pour le filtre : les marches internes d'une valeur vendeur. */
export function financialStatusesOf(status: MerchantFinancialStatus): FinancialStatus[] {
  return FINANCIAL_STATUSES.filter((internal) => merchantFinancialStatusOf(internal) === status);
}

/** Les couleurs de l'admin, reprises : « Payé » est le vert de « Remboursé ». */
export const MERCHANT_FINANCIAL_STATUS_COLORS: Record<MerchantFinancialStatus, `#${string}`> = {
  'non-encaisse': FINANCIAL_STATUS_COLORS['non-encaisse'],
  encaisse: FINANCIAL_STATUS_COLORS.encaisse,
  facture: FINANCIAL_STATUS_COLORS.facture,
  paye: FINANCIAL_STATUS_COLORS.rembourse,
};

/** Le rang d'une marche, 0 pour la première. */
export function financialRank(status: FinancialStatus): number {
  return FINANCIAL_STATUSES.indexOf(status);
}

/**
 * Cette transition avance-t-elle ?
 *
 * L'argent ne remonte pas le courant tout seul : une facture émise ne se
 * « désencaisse » pas parce qu'un colis a été retouché. Les retours en arrière
 * existent — une facture dévalidée, un règlement annulé — mais ce sont des
 * gestes EXPLICITES, pris devant un écran, et ils passent par leur propre
 * route. Ce prédicat garde les chemins automatiques.
 */
export function advancesFinancially(from: FinancialStatus, to: FinancialStatus): boolean {
  return financialRank(to) > financialRank(from);
}

/**
 * La marche qu'un colis MÉRITE, lue sur les colonnes historiques.
 *
 * C'est la définition unique du lien entre l'ancien monde et le nouveau : la
 * migration de reprise la rejoue en SQL, les services la rejouent en TypeScript,
 * et `financial-status.spec.ts` vérifie que les deux disent la même chose.
 *
 * **L'ordre des tests va du plus avancé au plus reculé**, et c'est ce qui rend
 * la fonction juste sur des données réelles : un colis facturé PUIS réglé porte
 * `billingState = 'reimbursed'` ET `reimbursedByCourier = true`. Tester dans
 * l'autre sens le ferait redescendre à `remis-livreur`.
 */
export function financialStatusFrom(input: {
  /** `Parcel.billingState`, en valeur de contrat. */
  billingState: 'to-invoice' | 'invoiced' | 'reimbursed';
  /** `Parcel.reimbursedByCourier` — le livreur a rendu le cash. */
  reimbursedByCourier: boolean;
  /**
   * Le livreur a-t-il encaissé ?
   *
   * `true` dès qu'une course a été confirmée en « livré », **y compris pour
   * zéro franc** : un colis déjà payé en ligne est encaissé d'un montant nul,
   * et le laisser en `non-encaisse` le ferait disparaître de toutes les
   * factures. C'est la leçon de D-101 — zéro n'est pas « rien ».
   */
  collected: boolean;
}): FinancialStatus {
  if (input.billingState === 'reimbursed') return 'rembourse';
  if (input.billingState === 'invoiced') return 'facture';
  if (input.reimbursedByCourier) return 'remis-livreur';
  if (input.collected) return 'encaisse';

  return 'non-encaisse';
}
