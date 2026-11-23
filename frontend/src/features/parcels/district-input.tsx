'use client';

import type { District } from '@delivery/shared';
import { useId, useMemo } from 'react';
import type * as React from 'react';
import { Input } from '@/components/ui/input';

/**
 * LE QUARTIER, EN SAISIE LIBRE — chantier « Quartier en saisie libre ».
 *
 * Un champ texte, facultatif, qui SUGGÈRE les quartiers du référentiel de la
 * ville pendant la frappe (`<datalist>`) sans jamais les imposer : toute
 * valeur est acceptée. C'est le serveur qui rapproche le texte d'un quartier
 * connu (`resolveDistrict`) — l'écran n'a rien à valider.
 *
 * Un seul composant pour tous les formulaires (colis, commande, destination,
 * échange) : la règle est la même partout, l'écran aussi.
 */
export function DistrictInput({
  districts,
  cityId,
  ...props
}: Omit<React.ComponentProps<'input'>, 'list'> & {
  /** Tous les quartiers connus ; seuls ceux de `cityId` sont proposés. */
  districts: readonly District[];
  cityId: string | null | undefined;
}) {
  const listId = useId();
  const suggestions = useMemo(
    () => (cityId ? districts.filter((district) => district.cityId === cityId) : []),
    [cityId, districts],
  );

  return (
    <>
      <Input
        {...props}
        // Le navigateur mêlerait son historique aux quartiers de la ville.
        autoComplete="off"
        maxLength={120}
        list={suggestions.length > 0 ? listId : undefined}
      />
      {suggestions.length > 0 ? (
        <datalist id={listId}>
          {suggestions.map((district) => (
            <option key={district.id} value={district.name} />
          ))}
        </datalist>
      ) : null}
    </>
  );
}
