'use server';

import {
  type BlCollectPayload,
  blCollectSchema,
  type BlNoteDetail,
  type BlNoteItemsPayload,
  blNoteItemsSchema,
  type BlNotePayload,
  type BlNoteReturnPayload,
  blNoteReturnSchema,
  blNoteSchema,
  type DeliverySlipAdvanceResult,
  deliverySlipAdvanceSchema,
  type DeliverySlipListItem,
  deliverySlipRevertSchema,
  type ParcelBlTracking,
  parcelBlRequestSchema,
} from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import {
  addBlNoteSlips,
  advanceDeliverySlips,
  closeBlNote,
  collectParcelBl,
  createBlNote,
  deleteBlNote,
  downloadBlNotePdf,
  getCollectableSlips,
  handOverBlNote,
  receiveBlNote,
  removeBlNoteSlip,
  returnBlNote,
  revertDeliverySlip,
} from '@/lib/api/bl';
import { setParcelBlRequested } from '@/lib/api/parcels';

/**
 * Écritures du BL — chantier « Retours, BL Clients Pro », point 5.
 *
 * Même forme que les retours : l'action revalide la saisie, appelle
 * l'endpoint, et laisse remonter le motif d'un 409. Ce motif compte ici autant
 * qu'ailleurs — « ce client n'est pas Pro » se corrige sur sa fiche, « échec »
 * envoie chercher au mauvais endroit.
 */

/** Les trois écrans qu'un geste sur un bon BL fait bouger. */
function revalidateBl(): void {
  revalidatePath('/[locale]/(app)/returns/bl', 'page');
  revalidatePath('/[locale]/(app)/returns/bl/notes', 'page');
  revalidatePath('/[locale]/(app)/returns/bl/[code]', 'page');
  revalidatePath('/[locale]/(courier)/me/returns', 'page');
}

/**
 * « BL À RÉCUPÉRER ? » sur un colis existant — point 5.
 *
 * Refusée par l'API si l'e-commerçant n'est pas Pro, et si le BL est déjà
 * revenu : le papier est là, l'effacer de la demande ne le ferait pas
 * disparaître, seulement devenir introuvable.
 */
export async function setParcelBlRequestedAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult> {
  const parsed = parcelBlRequestSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await setParcelBlRequested(code, parsed.data);
    revalidatePath('/[locale]/(app)/parcels/[code]', 'page');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/** « BL récupéré ? » — le geste du livreur à la livraison. */
export async function collectParcelBlAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<ParcelBlTracking>> {
  const parsed = blCollectSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const tracking = await collectParcelBl(code, parsed.data as BlCollectPayload);
    revalidatePath('/[locale]/(courier)/me/parcels/[code]', 'page');
    revalidateBl();
    return { ok: true, data: tracking };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * LES BL GROUPABLES — une LECTURE dans une Server Action.
 *
 * Même raison que les colis rapportables (point 2) : `src/lib/http.ts` n'est
 * jamais importé par un composant client (D-018), et l'écran change de vendeur
 * sans changer de page.
 */
export async function collectableSlipsAction(
  clientId: string,
): Promise<ReferentialResult<DeliverySlipListItem[]>> {
  if (!clientId) return { ok: false, error: 'invalid' };

  try {
    return { ok: true, data: await getCollectableSlips({ clientId }) };
  } catch (error) {
    return toFailure(error);
  }
}

export async function createBlNoteAction(input: unknown): Promise<ReferentialResult<BlNoteDetail>> {
  const parsed = blNoteSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const note = await createBlNote(parsed.data as BlNotePayload);
    revalidateBl();
    return { ok: true, data: note };
  } catch (error) {
    return toFailure(error);
  }
}

export async function addBlNoteSlipsAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<BlNoteDetail>> {
  const parsed = blNoteItemsSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const note = await addBlNoteSlips(code, parsed.data as BlNoteItemsPayload);
    revalidateBl();
    return { ok: true, data: note };
  } catch (error) {
    return toFailure(error);
  }
}

export async function removeBlNoteSlipAction(
  code: string,
  slipId: string,
): Promise<ReferentialResult<BlNoteDetail>> {
  try {
    const note = await removeBlNoteSlip(code, slipId);
    revalidateBl();
    return { ok: true, data: note };
  } catch (error) {
    return toFailure(error);
  }
}

export async function handOverBlNoteAction(code: string): Promise<ReferentialResult<BlNoteDetail>> {
  try {
    const note = await handOverBlNote(code);
    revalidateBl();
    return { ok: true, data: note };
  } catch (error) {
    return toFailure(error);
  }
}

/** « Confirmer la réception » — première des deux actions de l'admin. */
export async function receiveBlNoteAction(code: string): Promise<ReferentialResult<BlNoteDetail>> {
  try {
    const note = await receiveBlNote(code);
    revalidateBl();
    return { ok: true, data: note };
  } catch (error) {
    return toFailure(error);
  }
}

/** « Confirmer la remise au vendeur » — seconde action, et elle est séparée. */
export async function returnBlNoteAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<BlNoteDetail>> {
  const parsed = blNoteReturnSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const note = await returnBlNote(code, parsed.data as BlNoteReturnPayload);
    revalidateBl();
    return { ok: true, data: note };
  } catch (error) {
    return toFailure(error);
  }
}

export async function closeBlNoteAction(code: string): Promise<ReferentialResult<BlNoteDetail>> {
  try {
    const note = await closeBlNote(code);
    revalidateBl();
    return { ok: true, data: note };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteBlNoteAction(code: string): Promise<ReferentialResult> {
  try {
    await deleteBlNote(code);
    revalidateBl();
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function downloadBlNotePdfAction(code: string): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await downloadBlNotePdf(code) };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Lot E, E1 (D-201) : les gestes de la liste des BL ------------------ */

/**
 * « RÉCEPTIONNER (X) » / « REMETTRE AU VENDEUR (X) ». La sélection part telle
 * quelle — cochés, ou toute la liste avec les filtres de l'écran : c'est le
 * serveur qui la résout dans la portée du compte et qui ignore les BL déjà
 * ailleurs.
 */
export async function advanceDeliverySlipsAction(
  input: unknown,
): Promise<ReferentialResult<DeliverySlipAdvanceResult>> {
  const parsed = deliverySlipAdvanceSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const result = await advanceDeliverySlips(parsed.data);
    revalidateBl();
    return { ok: true, data: result };
  } catch (error) {
    return toFailure(error);
  }
}

/** Le retour arrière d'un BL — l'API refuse à qui n'est pas administrateur. */
export async function revertDeliverySlipAction(
  id: string,
  input: unknown,
): Promise<ReferentialResult<DeliverySlipListItem>> {
  const parsed = deliverySlipRevertSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const row = await revertDeliverySlip(id, parsed.data);
    revalidateBl();
    return { ok: true, data: row };
  } catch (error) {
    return toFailure(error);
  }
}
