'use client';

import { Search } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { usePathname, useRouter } from '@/i18n/navigation';
import { cn } from '@/lib/utils';

/**
 * LA RECHERCHE ET LES FILTRES DE « MES COLIS » — L1 bloc 3.
 *
 * **Quatre puces, pas trente-huit** — celles d'Anass (maquette 3) et celles du
 * mobile depuis L2 bloc 2 : Tous, À livrer, En retour (« Livrés » retirée le
 * 11/11/2026). Le reste du
 * référentiel appartient à l'agence, et le back-office le montre déjà.
 *
 * **« En retour » est une FAMILLE, pas un statut** : le référentiel en compte
 * plusieurs dans le groupe `return`, et un livreur les vit tous comme « ça
 * repart ». Filtrer sur un seul en cacherait les autres. `GET /parcels` sait
 * déjà filtrer par groupe — rien de neuf côté API.
 *
 * **Le filtre « reporté » de L1 disparaît** : il n'est pas dans la maquette, et
 * un report reste un colis à livrer — la puce « À livrer » le montre déjà.
 *
 * **Tout passe par l'URL.** La recherche et le filtre sont des paramètres de
 * requête, donc la page reste rendue au serveur, le lien est partageable, et
 * le bouton retour du navigateur refait la recherche précédente. Un état local
 * aurait exigé de charger la liste côté client — donc un écran vide le temps de
 * l'appel, sur la 3G d'une agence.
 */

const FILTERS = [
  // 11/11/2026 : plus de puce « Livrés », comme sur le mobile. Le serveur
  // retire les « Livré » de la liste d'un livreur : ce menu ne montre que les
  // colis à traiter.
  { key: 'toDeliver', statusId: 13 },
  { key: 'returning', statusGroup: 'return' },
] as const;

export function ParcelSearch({
  search,
  statusId,
  statusGroup,
}: {
  search: string;
  statusId?: number;
  statusGroup?: string;
}) {
  const t = useTranslations('courier.parcels');
  const router = useRouter();
  const pathname = usePathname();

  const [value, setValue] = useState(search);

  const go = (next: { search?: string; filter?: (typeof FILTERS)[number] | null }) => {
    const params = new URLSearchParams();

    const term = next.search ?? value;
    if (term.trim()) params.set('search', term.trim());

    /*
     * `undefined` veut dire « ne touche pas au filtre », `null` « retire-le ».
     * Sans cette distinction, taper une recherche effacerait la puce active,
     * ce qui est exactement ce qu'on ne veut pas quand on cherche DANS ses
     * colis en retour.
     */
    const filter = next.filter === undefined ? current() : next.filter;
    if (filter && 'statusId' in filter) params.set('statusId', String(filter.statusId));
    if (filter && 'statusGroup' in filter) params.set('statusGroup', filter.statusGroup);

    const query = params.toString();
    // Un filtre ne fait pas remonter la page (chantier « Affichage », point 1).
    router.push(query ? `${pathname}?${query}` : pathname, { scroll: false });
  };

  /** La puce active, déduite de l'URL : l'URL est la seule source d'état. */
  const current = () =>
    FILTERS.find((filter) =>
      'statusId' in filter ? filter.statusId === statusId : filter.statusGroup === statusGroup,
    ) ?? null;

  return (
    <div className="flex flex-col gap-3">
      <form
        className="flex gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          go({});
        }}
      >
        <Input
          value={value}
          onChange={(event) => setValue(event.target.value)}
          placeholder={t('searchPlaceholder')}
          aria-label={t('searchPlaceholder')}
          autoComplete="off"
          className="max-w-md"
        />
        <Button type="submit" size="icon" aria-label={t('search')}>
          <Search className="size-4" aria-hidden />
        </Button>
      </form>

      <div className="flex flex-wrap gap-2">
        <FilterChip label={t('all')} active={!current()} onClick={() => go({ filter: null })} />

        {FILTERS.map((filter) => (
          <FilterChip
            key={filter.key}
            label={t(filter.key)}
            active={current() === filter}
            // Ré-appuyer sur la puce active la retire : c'est le geste attendu,
            // et il évite d'ajouter un « Tous » à chaque groupe.
            onClick={() => go({ filter: current() === filter ? null : filter })}
          />
        ))}
      </div>
    </div>
  );
}

function FilterChip({
  label,
  active,
  onClick,
}: {
  label: string;
  active: boolean;
  onClick: () => void;
}) {
  return (
    <button type="button" onClick={onClick} aria-pressed={active} className="outline-none">
      <Badge
        variant={active ? 'default' : 'outline'}
        className={cn('min-h-9 cursor-pointer px-3 text-sm', !active && 'hover:bg-muted')}
      >
        {label}
      </Badge>
    </button>
  );
}
