'use client';

import { useSearchParams } from 'next/navigation';
import { useCallback, useEffect, useMemo, useRef, useState, useTransition } from 'react';
import type { DataTableServerState } from '@/components/data-table/data-table';
import { usePathname, useRouter } from '@/i18n/navigation';

/**
 * État de liste piloté par l'URL.
 *
 * Un seul endroit écrit les paramètres — page, taille, tri, recherche, filtres
 * — et le composant serveur les relit pour appeler l'API. Deux conséquences
 * qu'on veut : l'écran est partageable par lien et survit au rechargement, et
 * la pagination n'a plus besoin de charger toutes les lignes.
 *
 * `router.replace` plutôt que `push` : parcourir cinq pages d'une liste ne
 * doit pas obliger à appuyer cinq fois sur « précédent » pour en sortir.
 */
/**
 * UNE LISTE QUI SE FILTRE NE SAUTE PAS EN HAUT — chantier « Affichage web et
 * étiquette », point 1.
 *
 * Par défaut, Next remonte en haut de la page à chaque navigation, y compris
 * un `replace` qui ne change que la requête. Sur /remittances, taper dans la
 * recherche (placée sous les cartes de chiffres) faisait donc sauter l'écran
 * au premier caractère validé, le champ sorti de la vue. Un filtre n'est pas
 * un changement de page : la position et le curseur restent où ils sont.
 *
 * Exporté pour TOUS les écrivains de filtres d'URL (période, agence, retours…),
 * pas seulement ce hook.
 */
export const LIST_NAVIGATION = { scroll: false } as const;

export interface ServerTableController {
  /** À passer tel quel à `<DataTable server={...} />`. */
  server: DataTableServerState;
  /** Valeur courante d'un filtre, telle qu'elle est dans l'URL. */
  param: (key: string) => string | undefined;
  /** Écrit un filtre et revient en page 1 — le filtre change le jeu de lignes. */
  setFilter: (key: string, value: string | undefined) => void;
  /** Efface tout : filtres, recherche, tri, page. */
  reset: () => void;
  /** Vrai pendant la navigation : l'écran peut se griser sans sauter. */
  isPending: boolean;
}

export interface ServerTableOptions {
  total: number;
  defaultSort: string;
  defaultOrder?: 'asc' | 'desc';
  defaultPageSize?: number;
}

export function useServerTable({
  total,
  defaultSort,
  defaultOrder = 'asc',
  defaultPageSize = 25,
}: ServerTableOptions): ServerTableController {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [isPending, startTransition] = useTransition();

  const write = useCallback(
    (changes: Record<string, string | number | undefined>) => {
      const next = new URLSearchParams(searchParams.toString());

      for (const [key, value] of Object.entries(changes)) {
        if (value === undefined || value === '') next.delete(key);
        else next.set(key, String(value));
      }

      const query = next.toString();
      startTransition(() => {
        router.replace(query ? `${pathname}?${query}` : pathname, LIST_NAVIGATION);
      });
    },
    [pathname, router, searchParams],
  );

  const page = Number(searchParams.get('page') ?? 1);
  const pageSize = Number(searchParams.get('pageSize') ?? defaultPageSize);
  const order = searchParams.get('order');

  const server = useMemo<DataTableServerState>(
    () => ({
      page: Number.isFinite(page) && page >= 1 ? page : 1,
      pageSize: Number.isFinite(pageSize) && pageSize >= 1 ? pageSize : defaultPageSize,
      total,
      sort: searchParams.get('sort') ?? defaultSort,
      order: order === 'desc' ? 'desc' : defaultOrder,
      onChange: write,
    }),
    [defaultOrder, defaultPageSize, defaultSort, order, page, pageSize, searchParams, total, write],
  );

  const param = useCallback((key: string) => searchParams.get(key) ?? undefined, [searchParams]);

  const setFilter = useCallback(
    (key: string, value: string | undefined) => write({ [key]: value, page: 1 }),
    [write],
  );

  const reset = useCallback(() => {
    startTransition(() => router.replace(pathname, LIST_NAVIGATION));
  }, [pathname, router]);

  return { server, param, setFilter, reset, isPending };
}

/**
 * Champ de recherche relié à l'URL, avec un délai de grâce.
 *
 * La recherche part au serveur : l'envoyer à chaque frappe ferait une requête
 * par lettre. La valeur affichée reste donc locale et immédiate, et n'est
 * écrite dans l'URL qu'après une pause de saisie.
 */
export function useUrlSearch(
  setFilter: (key: string, value: string | undefined) => void,
  initial: string | undefined,
  delay = 350,
): [string, (value: string) => void] {
  const [value, setValue] = useState(initial ?? '');
  const committed = useRef(initial ?? '');

  useEffect(() => {
    if (value === committed.current) return;

    const timer = setTimeout(() => {
      committed.current = value;
      setFilter('search', value || undefined);
    }, delay);

    return () => clearTimeout(timer);
  }, [delay, setFilter, value]);

  return [value, setValue];
}
