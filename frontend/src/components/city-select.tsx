'use client';

import type { CitySearchable } from '@delivery/shared';
import { useTranslations } from 'next-intl';
import { type ComponentProps, useMemo } from 'react';
import { type SearchableOption, SearchableSelect } from '@/components/searchable-select';

/** Ce qu'une ville doit porter pour être choisie et cherchée. */
export type CityChoice = CitySearchable & { id: string };

/**
 * LE SÉLECTEUR DE VILLE — chantier « Retours recette 26/09 », point 2.
 *
 * UN seul composant pour tous les endroits où l'on choisit une ville :
 * formulaires (colis, bons, tournées, agences, clients, livreurs, quartiers,
 * fiche société, inscription), changement de destination, commande, filtres
 * de liste et grille tarifaire. La recherche porte sur le nom français, le nom
 * arabe et le code, sans casse ni accents.
 *
 * **Lot J (J2)** : c'est désormais la version « villes » du menu commun
 * `SearchableSelect` — même recherche, même clavier, même curseur que les
 * autres menus. Le contrat ne bouge pas : la valeur émise est l'`id` de la
 * ville, une option vide garde sa sentinelle (`emptyOption`), et les attributs
 * de `<FormControl>` vont sur le bouton.
 */
export function CitySelect({
  cities,
  ...props
}: {
  cities: readonly CityChoice[];
  /** L'`id` de la ville, ou la valeur de `emptyOption`, ou `''`. */
  value: string;
  onValueChange: (value: string) => void;
  placeholder?: string;
  /** Une première ligne hors référentiel : « Toutes », « Même ville que le client »… */
  emptyOption?: { value: string; label: string };
  disabled?: boolean;
  className?: string;
} & Omit<ComponentProps<'button'>, 'value' | 'onChange' | 'children'>) {
  const t = useTranslations('common');

  const options = useMemo<SearchableOption[]>(
    () =>
      cities.map((city) => ({
        value: city.id,
        label: city.name,
        hint: city.nameAr,
        keywords: [city.nameAr, city.code],
      })),
    [cities],
  );

  return (
    <SearchableSelect
      options={options}
      searchPlaceholder={t('citySearch')}
      emptyText={t('cityEmpty')}
      {...props}
    />
  );
}
