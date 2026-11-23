export type Id = string;

/** Montant en centimes entiers — DOMAIN.md §4 dette n°2 : `colis.prix` était un varchar. */
export type AmountInCents = number;

/** Date ISO 8601 avec fuseau — DOMAIN.md §4 dette n°21 : le legacy écrivait l'heure serveur. */
export type IsoDateTime = string;

export interface Paginated<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  sort?: string;
  order?: 'asc' | 'desc';
}
