/**
 * LE QUARTIER EN SAISIE LIBRE — chantier « Quartier en saisie libre ».
 *
 * Le quartier d'un colis est un TEXTE, facultatif : le référentiel ne compte
 * des quartiers que pour une poignée des 325 villes de la grille, et un
 * destinataire écrit « Hay Salam » que le référentiel le connaisse ou non.
 * Le référentiel sert à SUGGÉRER et à rapprocher, jamais à refuser.
 *
 * Deux écritures du même quartier — « Maârif », « MAARIF », « maarif » —
 * désignent la même ligne du référentiel : c'est cette clé qui le dit, et elle
 * est la même partout (formulaires, import, changement de destination,
 * commande), sans quoi une saisie rapprochée ici ne le serait pas là.
 *
 * Les lettres ARABES sont conservées : `cityKey` réduit tout à `[a-z0-9]`, ce
 * qui ferait de chaque quartier écrit en arabe la même clé vide.
 */
export function districtKey(name: string): string {
  return name
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .toLowerCase()
    .replace(/[^\p{L}\p{N}]/gu, '');
}

/** Longueur maximale d'un quartier saisi — celle de la cellule d'import. */
export const DISTRICT_NAME_MAX = 120;

/**
 * Le quartier À AFFICHER : le texte saisi, à défaut le nom du quartier du
 * référentiel. Jamais vide si l'un des deux existe — les colis d'avant ce
 * chantier n'ont que le second.
 */
export function districtLabel(
  typed: string | null | undefined,
  referential: string | null | undefined,
): string | null {
  const text = typed?.trim();
  if (text) return text;
  return referential?.trim() || null;
}
