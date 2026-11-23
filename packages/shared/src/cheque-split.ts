import type { AmountInCents } from './types/index';

/**
 * LE CHÈQUE AU PAS DE PORTE — chantier « Finance », point 8, extrait pour le
 * chantier « Livrer en un appui » (D-156).
 *
 * Le livreur ne saisit QUE le chèque ; les espèces sont la différence. La
 * saisie est du texte libre (clavier décimal, virgule marocaine) : on la lit
 * ici, une fois, pour la fiche du colis, la carte du scan et la ligne du
 * circuit — trois écrans, une règle. Un chèque au-delà du dû est refusé ici
 * comme à l'API (422), un montant illisible aussi.
 */
export interface ChequeSplit {
  /** Ce que le chèque vaut, en centimes ; 0 si rien n'est saisi ou si c'est illisible. */
  chequeCents: AmountInCents;
  /** Ce qu'il reste à encaisser en espèces : dû − chèque (jamais calculé sur un chèque illisible). */
  cashCents: AmountInCents;
  /** Texte non numérique ou négatif. */
  invalid: boolean;
  /** Chèque > dû : le bouton « Livrer » se grise, comme à l'API. */
  tooLarge: boolean;
  /** Le bouton peut partir : rien d'illisible, rien au-delà du dû. */
  canDeliver: boolean;
}

export function splitCheque(codToCollect: AmountInCents, chequeInput: string): ChequeSplit {
  const text = chequeInput.trim().replace(',', '.');
  const value = text === '' ? 0 : Number(text);
  const invalid = !Number.isFinite(value) || value < 0;
  const chequeCents = invalid ? 0 : Math.round(value * 100);
  const tooLarge = chequeCents > codToCollect;

  return {
    chequeCents,
    cashCents: codToCollect - chequeCents,
    invalid,
    tooLarge,
    canDeliver: !invalid && !tooLarge,
  };
}
