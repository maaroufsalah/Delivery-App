import type { ListQuery } from '@delivery/shared';

/**
 * Lecture des paramètres de liste depuis l'URL, côté SERVEUR.
 *
 * L'URL est la source de vérité de l'état d'une liste : page, taille, tri,
 * recherche et filtres. C'est ce qui rend un écran partageable par lien,
 * rechargeable sans perdre sa place, et navigable au bouton « précédent ».
 * C'est aussi ce qui permet au composant serveur de demander à l'API la BONNE
 * page — la pagination ne vit plus dans le navigateur.
 */

export type SearchParams = Record<string, string | string[] | undefined>;

/** Un paramètre répété (`?a=1&a=2`) n'a pas de sens ici : on garde le premier. */
export function firstParam(value: string | string[] | undefined): string | undefined {
  const found = Array.isArray(value) ? value[0] : value;
  return found === '' ? undefined : found;
}

export interface ListQueryDefaults {
  sort: string;
  order?: 'asc' | 'desc';
  pageSize?: number;
}

export function listQueryFrom(params: SearchParams, defaults: ListQueryDefaults): ListQuery {
  const page = Number(firstParam(params.page) ?? 1);
  const pageSize = Number(firstParam(params.pageSize) ?? defaults.pageSize ?? 25);
  const order = firstParam(params.order);

  return {
    // Une page ou une taille absurde dans l'URL ne doit pas casser l'écran :
    // elle retombe sur la valeur par défaut.
    page: Number.isFinite(page) && page >= 1 ? Math.floor(page) : 1,
    pageSize: Number.isFinite(pageSize) && pageSize >= 1 ? Math.min(Math.floor(pageSize), 200) : 25,
    search: firstParam(params.search),
    sort: firstParam(params.sort) ?? defaults.sort,
    order: order === 'desc' ? 'desc' : (defaults.order ?? 'asc'),
  };
}

/** Filtre d'activation, commun à tous les écrans du référentiel. */
export function activeParam(params: SearchParams): 'true' | 'false' | undefined {
  const value = firstParam(params.isActive);
  return value === 'true' || value === 'false' ? value : undefined;
}
