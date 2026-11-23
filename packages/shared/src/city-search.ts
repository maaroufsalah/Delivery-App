import { districtKey } from './district-name';

/**
 * LA RECHERCHE DANS UN MENU — chantier « Retours recette 26/09 », point 2,
 * généralisée au lot J (J2).
 *
 * 325 villes, 123 clients : une liste déroulante ne se parcourt plus, elle se
 * CHERCHE. Cette règle est la seule : le menu web (`SearchableSelect`, dont
 * `CitySelect` est la version « villes »), le sélecteur mobile et la barre de
 * filtres l'appliquent tous, sans quoi « ain diab » trouverait Aïn Diab ici et
 * pas là.
 *
 * Insensible à la casse et aux accents, le morceau tapé pouvant être au
 * milieu. `districtKey` fait déjà exactement cela — et garde les lettres
 * arabes et les chiffres (« 3ich » → 3ICH KHFIF) — donc on ne réécrit pas une
 * clé.
 */

/** Vrai si l'un des textes répond à la saisie. Une saisie vide répond toujours. */
export function textMatchesQuery(
  candidates: ReadonlyArray<string | null | undefined>,
  query: string,
): boolean {
  const key = districtKey(query);
  if (key === '') return true;

  return candidates.some(
    (candidate) => candidate != null && candidate !== '' && districtKey(candidate).includes(key),
  );
}

/** Une entrée de menu : son libellé, et d'autres mots qui la trouvent (nom arabe, code…). */
export interface SearchableText {
  label: string;
  keywords?: ReadonlyArray<string | null | undefined>;
}

/** Les entrées qui répondent à la saisie, dans l'ordre reçu — aucune n'est coupée. */
export function searchOptions<T extends SearchableText>(options: readonly T[], query: string): T[] {
  return options.filter((option) =>
    textMatchesQuery([option.label, ...(option.keywords ?? [])], query),
  );
}

export interface CitySearchable {
  name: string;
  nameAr?: string | null;
  code?: string | null;
}

/** Vrai si la ville répond à la saisie : nom français, nom arabe ou code. */
export function cityMatchesQuery(city: CitySearchable, query: string): boolean {
  return textMatchesQuery([city.name, city.nameAr, city.code], query);
}

/** Les villes qui répondent à la saisie, dans l'ordre reçu. */
export function searchCities<T extends CitySearchable>(cities: readonly T[], query: string): T[] {
  return cities.filter((city) => cityMatchesQuery(city, query));
}
