/**
 * CE QUE LE LIVREUR LIT QUAND ÇA REFUSE — Recette 7, point 3.
 *
 * Badr a scanné, sur le bon BR-2610-0009, un colis d'un autre bon : écran
 * rouge, « Le colis … n'appartient pas au bon … Référence : 90c78bef-… ». Rien
 * n'était cassé — c'est le cas le plus courant du trottoir, deux piles de deux
 * vendeurs voisins — mais il était habillé comme une panne.
 *
 * Deux règles pures, sans React, tenues par
 * `apps/api/src/common/mobile/courier-feedback.spec.ts` :
 *
 *  1. **la gravité se lit sur le statut HTTP.** Un refus 4xx que le serveur a
 *     formulé (400, 403, 404, 409, 422) est une erreur MÉTIER : phrase courte,
 *     orange, référence rangée derrière « Détails ». Tout le reste — réseau,
 *     délai, 5xx, session — est TECHNIQUE : rouge, référence visible, pour le
 *     support ;
 *  2. **un code lu sur un bon est situé** : sur ce bon, sur un AUTRE bon du
 *     même circuit (→ « Ouvrir BR-xxxx »), ou nulle part dans la tournée.
 */

export type FailureSeverity = 'business' | 'technical';

/** Les refus que le serveur formule pour le métier : lisibles tels quels. */
const BUSINESS_STATUSES = new Set([400, 403, 404, 409, 422]);

/**
 * `status` 0 = réseau absent ; 408 = délai ; 401 = session ; 429 = frein par
 * IP ; 5xx = serveur. Tous techniques : le livreur ne peut rien y corriger.
 */
export function failureSeverity(status: number | undefined): FailureSeverity {
  return status !== undefined && BUSINESS_STATUSES.has(status) ? 'business' : 'technical';
}

interface LocatableParcel {
  id: string;
  code: string;
  externalCode?: string | null;
}

interface LocatableNote {
  id: string;
  code: string;
  parcelIds: string[];
}

export type ScannedCodeLocation<P extends LocatableParcel, N extends LocatableNote> =
  | { kind: 'on-note'; parcel: P }
  | { kind: 'other-note'; parcel: P; note: N }
  | { kind: 'not-on-route' };

/**
 * Où est le code lu ? Le code colis OU la référence commande du vendeur
 * (point 7 du 26/09), comparés en capitales sans espaces, comme le serveur.
 */
export function locateScannedCode<P extends LocatableParcel, N extends LocatableNote>(
  route: { parcels: readonly P[]; notes: readonly N[] },
  noteId: string,
  normalizedCode: string,
): ScannedCodeLocation<P, N> {
  const parcel = route.parcels.find(
    (row) =>
      row.code.toUpperCase() === normalizedCode ||
      (row.externalCode ?? '').trim().toUpperCase() === normalizedCode,
  );
  if (!parcel) return { kind: 'not-on-route' };

  const current = route.notes.find((note) => note.id === noteId);
  if (current?.parcelIds.includes(parcel.id)) return { kind: 'on-note', parcel };

  const other = route.notes.find(
    (note) => note.id !== noteId && note.parcelIds.includes(parcel.id),
  );
  if (other) return { kind: 'other-note', parcel, note: other };

  // Sur la tournée mais sur aucun bon : un ramassage sans bon, pas « hors tournée ».
  return { kind: 'not-on-route' };
}
