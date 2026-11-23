import type { SelectionPayload } from '@delivery/shared';

/**
 * Le corps d'un ajout EN LOT — chantier « Plafond de 200 ».
 *
 * Un tableau d'ids (les anciens appels) ou une sélection du `CandidatePicker`
 * (ids cochés, ou « tous les candidats de la recherche »). L'API accepte les
 * deux sous la même clé (`parcelIds`, `noteIds`, `slipIds`…).
 */
export function selectionBody(
  selection: SelectionPayload | readonly string[],
  idsKey: string,
  extra: Record<string, unknown> = {},
): Record<string, unknown> {
  if (Array.isArray(selection)) return { [idsKey]: [...selection], ...extra };

  const { ids, all, search, exclude } = selection as SelectionPayload;
  return all
    ? { all: true, ...(search ? { search } : {}), exclude: exclude ?? [], ...extra }
    : { [idsKey]: ids ?? [], ...extra };
}
