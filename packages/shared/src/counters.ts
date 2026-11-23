/**
 * LES COMPTEURS DE SÉLECTION — Recette 7, point 1.
 *
 * Badr a photographié, sur le bon BR-2610-0009 : « 32 colis sur 6 »,
 * « Tout cocher 32 / 6 cochés », « Valider (32) » — avec trois cases cochées.
 * Les 32 étaient les identifiants cochés sur le bon PRÉCÉDENT, restés dans
 * l'état de l'écran (D-149) ; le total, lui, était bien celui du bon affiché.
 *
 * La coque `useFreshKey` empêche l'état de survivre ; cette fonction empêche
 * qu'un identifiant étranger à la liste soit jamais COMPTÉ ou ENVOYÉ, quelle
 * que soit la façon dont il est arrivé là (un bon modifié par l'agence entre
 * deux relectures, un colis retiré du bon pendant que la case était cochée).
 *
 * Convention, partout où l'on coche : **« X cochés / Y »** — le nombre coché
 * d'abord, le total ensuite, et X ≤ Y par construction. Le bouton porte X.
 */

/** Les identifiants cochés qui sont ENCORE dans la liste, dans l'ordre de la liste. */
export function keepEligible<T extends string>(picked: Iterable<T>, eligible: Iterable<T>): T[] {
  const wanted = new Set(picked);
  const kept: T[] = [];
  for (const id of eligible) if (wanted.has(id)) kept.push(id);
  return kept;
}

export interface SelectionCounter {
  /** Cochés ET présents dans la liste. */
  readonly checked: number;
  /** La taille de la liste affichée. */
  readonly total: number;
}

/** « X cochés / Y » : X ne dépasse jamais Y, et un identifiant hors liste ne compte pas. */
export function selectionCounter<T extends string>(
  picked: Iterable<T>,
  eligible: Iterable<T>,
): SelectionCounter {
  const ids = [...eligible];
  return { checked: keepEligible(picked, ids).length, total: ids.length };
}
