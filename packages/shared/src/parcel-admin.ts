import { z } from 'zod';
import type { AmountInCents } from './types/common';

/**
 * LES GESTES DE L'ADMINISTRATEUR SUR LA PROPRIÉTÉ D'UN COLIS — D-256
 * (chantier « Suppression d'un colis par l'admin et changement de client »).
 *
 *  - supprimer un colis pas encore pris en charge, comme le vendeur peut le
 *    faire, avec un motif ;
 *  - changer le client d'un colis saisi sur le mauvais, tant que son argent
 *    n'a pas bougé : aperçu, puis écriture.
 */

/** La suppression par l'admin : le motif, toujours. */
export const parcelAdminDeleteSchema = z.object({
  reason: z.string().trim().min(1, 'required').max(500, 'too-long'),
});

export type ParcelAdminDeleteInput = z.input<typeof parcelAdminDeleteSchema>;
export type ParcelAdminDeletePayload = z.output<typeof parcelAdminDeleteSchema>;

/** L'aperçu du changement de client : le nouveau client seul. */
export const parcelClientChangePreviewSchema = z.object({
  clientId: z.string().trim().min(1, 'required'),
});

export type ParcelClientChangePreviewPayload = z.output<typeof parcelClientChangePreviewSchema>;

/**
 * L'écriture : le nouveau client, le motif, et l'empreinte de l'aperçu — si le
 * colis a bougé entre les deux, l'écriture refuse plutôt que d'appliquer un
 * plan que personne n'a vu.
 */
export const parcelClientChangeSchema = parcelClientChangePreviewSchema.extend({
  reason: z.string().trim().min(1, 'required').max(500, 'too-long'),
  fingerprint: z.string().trim().optional(),
});

export type ParcelClientChangeInput = z.input<typeof parcelClientChangeSchema>;
export type ParcelClientChangePayload = z.output<typeof parcelClientChangeSchema>;

/** Une valeur avant / après, telle que l'aperçu la montre. */
export interface ParcelClientChangeField {
  field:
    | 'clientId'
    | 'deliveryFee'
    | 'pickupAddress'
    | 'originCityId'
    | 'agencyId'
    | 'expectedPayment'
    | 'blRequested';
  before: string;
  after: string;
}

/** Un bon dont le colis sort (bon actif) ou qui garde sa ligne (bon validé). */
export interface ParcelClientChangeNote {
  code: string;
  /** `detached` : bon non validé, la ligne sort. `kept` : bon validé, inchangé. */
  effect: 'detached' | 'kept';
}

export interface ParcelClientChangePreview {
  code: string;
  from: { id: string; name: string };
  to: { id: string; name: string };
  feeBefore: AmountInCents;
  feeAfter: AmountInCents;
  /** Ne change JAMAIS — rappelé à l'écran. */
  codAmount: AmountInCents;
  changes: ParcelClientChangeField[];
  notes: ParcelClientChangeNote[];
  /** Vrai : l'adresse, la ville de ramassage et l'agence suivent le nouveau client. */
  beforePickup: boolean;
  fingerprint: string;
}

export interface ParcelClientChangeResult {
  code: string;
  clientId: string;
  feeBefore: AmountInCents;
  feeAfter: AmountInCents;
  notes: ParcelClientChangeNote[];
}
