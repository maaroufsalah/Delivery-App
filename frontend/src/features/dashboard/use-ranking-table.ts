'use client';

import { useSearchParams } from 'next/navigation';
import { useCallback, useMemo, useTransition } from 'react';
import type { DataTableServerState } from '@/components/data-table/data-table';
import { usePathname, useRouter } from '@/i18n/navigation';

/**
 * L'ÉTAT D'UN CLASSEMENT DU TABLEAU DE BORD, dans l'URL — chantier « Dashboard
 * de pilotage ».
 *
 * `useServerTable` écrit `page` / `sort` / `order` : une seule table par page.
 * Le tableau de bord en porte TROIS (livreurs, villes, clients), qui doivent se
 * paginer et se trier indépendamment — d'où un PRÉFIXE par table (`c`, `v`,
 * `k`) : `cpage`, `csort`… Le serveur relit ces clés et appelle l'API avec.
 *
 * `scroll: false` : changer de page au milieu de l'écran ne doit pas renvoyer
 * en haut du tableau de bord.
 */
export function useRankingTable(
  prefix: string,
  options: { total: number; sort: string; pageSize?: number },
): { server: DataTableServerState; set: (key: string, value: string | undefined) => void } {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [, startTransition] = useTransition();

  const write = useCallback(
    (changes: Record<string, string | number | undefined>) => {
      const next = new URLSearchParams(params.toString());
      for (const [key, value] of Object.entries(changes)) {
        if (value === undefined || value === '') next.delete(`${prefix}${key}`);
        else next.set(`${prefix}${key}`, String(value));
      }
      const query = next.toString();
      startTransition(() =>
        router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false }),
      );
    },
    [params, pathname, prefix, router],
  );

  const page = Number(params.get(`${prefix}page`) ?? 1);
  const pageSize = options.pageSize ?? 10;

  const server = useMemo<DataTableServerState>(
    () => ({
      page: Number.isFinite(page) && page >= 1 ? page : 1,
      pageSize,
      total: options.total,
      sort: options.sort,
      order: 'desc',
      onChange: (next) => {
        // Le tri d'un classement est un choix de CLASSEMENT (`sort=`), posé
        // par les onglets de la carte, pas par l'en-tête de colonne.
        write({ page: next.page });
      },
    }),
    [options.sort, options.total, page, pageSize, write],
  );

  const set = useCallback(
    (key: string, value: string | undefined) => write({ [key]: value, page: undefined }),
    [write],
  );

  return { server, set };
}
