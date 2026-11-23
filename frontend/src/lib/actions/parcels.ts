'use server';

import type {
  AdminFieldStatusResult,
  CancelDeliveryResult,
  ParcelDetail,
  ParcelImportPreview,
  ParcelImportResult,
  Quote,
  StatusCorrectionPreview,
  StatusCorrectionResult,
  ParcelClientChangePreview,
  ParcelClientChangeResult,
} from '@delivery/shared';
import {
  parcelAdminDeleteSchema,
  parcelClientChangePreviewSchema,
  parcelClientChangeSchema,
  statusCorrectionSchema,
  addParcelNoteSchema,
  adminFieldStatusChangeSchema,
  cancelDeliverySchema,
  changeStatusSchema,
  createParcelSchema,
  disputeStatusSchema,
  changeDestinationSchema,
  editParcelFeesSchema,
  expectedPaymentChangeSchema,
  parcelImportApplySchema,
  parcelImportSourceSchema,
  sellerParcelUpdateSchema,
} from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import { ApiError } from '@/lib/api/client';
import {
  addParcelNote,
  adminDeleteParcel,
  changeParcelClient,
  previewClientChange,
  applyStatusCorrection,
  previewStatusCorrection,
  cancelDelivery,
  changeFieldStatus,
  type LabelJobSelection,
  changeParcelExpectedPayment,
  changeParcelStatus,
  createParcel,
  deleteSellerParcel,
  editParcelFees,
  updateSellerParcel,
  previewParcelCode,
  parcelImportAnomalies,
  previewParcelImport,
  type QuoteParcelQuery,
  quoteParcel,
  runParcelImport,
  changeDestination,
} from '@/lib/api/parcels';
import { apiFetchBase64 } from '@/lib/http';

/**
 * Transport des écritures colis.
 *
 * `src/lib/api/*` reste le contrat d'endpoint (une fonction = un futur endpoint
 * NestJS) ; ces actions ne font que l'exposer au client et invalider le cache
 * de la route. Quand l'API arrivera, le corps de l'action deviendra un `fetch`
 * et rien d'autre ne bougera.
 *
 * Toute entrée est revalidée ici : une Server Action est un endpoint POST
 * public, la validation du formulaire côté client n'est pas une garantie.
 */

export type ActionResult<T = undefined> =
  | { ok: true; data: T }
  | {
      ok: false;
      error: string;
      /** Le champ fautif et le motif machine, quand l'API les nomme (point 7 du 26/09). */
      field?: string;
      reason?: string;
    };

function failure(error: unknown): Extract<ActionResult<never>, { ok: false }> {
  if (error instanceof ApiError) {
    const details = error.details as { field?: string; reason?: string } | undefined;
    return {
      ok: false,
      error: error.message,
      ...(details?.field ? { field: details.field } : {}),
      ...(details?.reason ? { reason: details.reason } : {}),
    };
  }
  return { ok: false, error: 'unexpected' };
}

export async function changeParcelStatusAction(
  code: string,
  input: unknown,
): Promise<ActionResult> {
  const parsed = changeStatusSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await changeParcelStatus(code, parsed.data);
    revalidatePath(`/[locale]/(app)/parcels/[code]`, 'page');
    revalidatePath(`/[locale]/(app)/parcels`, 'page');
    return { ok: true, data: undefined };
  } catch (error) {
    return failure(error);
  }
}

/** Contestation d'un statut terminal — staff, motif obligatoire (DOMAIN.md §2.2). */
export async function disputeParcelStatusAction(
  code: string,
  input: unknown,
): Promise<ActionResult> {
  const parsed = disputeStatusSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await changeParcelStatus(code, { ...parsed.data, dispute: true });
    revalidatePath(`/[locale]/(app)/parcels/[code]`, 'page');
    revalidatePath(`/[locale]/(app)/parcels`, 'page');
    return { ok: true, data: undefined };
  } catch (error) {
    return failure(error);
  }
}

/**
 * « Annuler la livraison » — lot I, I2 (D-210). Administrateur, motif
 * obligatoire. `ReferentialResult` : le refus utile est un 409 motivé (déjà
 * remis en caisse, déjà facturé), que l'écran traduit par son motif.
 */
export async function cancelDeliveryAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<CancelDeliveryResult>> {
  const parsed = cancelDeliverySchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid', field: 'reason' };

  try {
    const result = await cancelDelivery(code, parsed.data);
    revalidatePath(`/[locale]/(app)/parcels/[code]`, 'page');
    revalidatePath(`/[locale]/(app)/parcels`, 'page');
    revalidatePath('/[locale]/(app)/routes/[code]', 'page');
    return { ok: true, data: result };
  } catch (error) {
    return toFailure(error, 'parcels.cancelDelivery');
  }
}

/**
 * LA CORRECTION LIBRE DU STATUT — D-250. Le geste est revalidé ici (une Server
 * Action est un endpoint public) ; le plan, les verrous et le rôle sont jugés
 * par l'API. L'aperçu n'écrit rien ; l'écriture refuse si le plan a changé.
 */
export async function previewStatusCorrectionAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<StatusCorrectionPreview>> {
  const parsed = statusCorrectionSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid', field: 'reason' };
  try {
    return { ok: true, data: await previewStatusCorrection(code, parsed.data) };
  } catch (error) {
    return toFailure(error, 'parcels.correction.preview');
  }
}

/**
 * D-256 — l'administrateur supprime un colis pas encore pris en charge. Le
 * motif est revalidé ici (une Server Action est un endpoint public).
 */
export async function adminDeleteParcelAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<{ code: string }>> {
  const parsed = parcelAdminDeleteSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid', field: 'reason' };
  try {
    const result = await adminDeleteParcel(code, parsed.data);
    revalidatePath(`/[locale]/(app)/parcels`, 'page');
    revalidatePath('/[locale]/(app)/pickup-notes/[code]', 'page');
    revalidatePath('/[locale]/(app)/dropoff-notes/[code]', 'page');
    return { ok: true, data: result };
  } catch (error) {
    return toFailure(error, 'parcels.admin-delete');
  }
}

/** D-256 — l'aperçu du changement de client, sans rien écrire. */
export async function previewClientChangeAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<ParcelClientChangePreview>> {
  const parsed = parcelClientChangePreviewSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid', field: 'clientId' };
  try {
    return { ok: true, data: await previewClientChange(code, parsed.data) };
  } catch (error) {
    return toFailure(error, 'parcels.client.preview');
  }
}

/** D-256 — le changement de client, motif et empreinte de l'aperçu. */
export async function changeParcelClientAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<ParcelClientChangeResult>> {
  const parsed = parcelClientChangeSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid', field: 'reason' };
  try {
    const result = await changeParcelClient(code, parsed.data);
    revalidatePath(`/[locale]/(app)/parcels/[code]`, 'page');
    revalidatePath(`/[locale]/(app)/parcels`, 'page');
    revalidatePath('/[locale]/(app)/pickup-notes/[code]', 'page');
    revalidatePath('/[locale]/(app)/dropoff-notes/[code]', 'page');
    return { ok: true, data: result };
  } catch (error) {
    return toFailure(error, 'parcels.client.apply');
  }
}

export async function applyStatusCorrectionAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<StatusCorrectionResult>> {
  const parsed = statusCorrectionSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid', field: 'reason' };
  try {
    const result = await applyStatusCorrection(code, parsed.data);
    revalidatePath(`/[locale]/(app)/parcels/[code]`, 'page');
    revalidatePath(`/[locale]/(app)/parcels`, 'page');
    revalidatePath('/[locale]/(app)/routes/[code]', 'page');
    return { ok: true, data: result };
  } catch (error) {
    return toFailure(error, 'parcels.correction.apply');
  }
}

/**
 * « Statut libre pour l'admin » (D-245) : d'un statut de terrain à un autre,
 * sur un colis ou sur une sélection. Le commentaire est revalidé ici (une
 * Server Action est un endpoint public) ; la sélection part telle quelle, et
 * c'est le serveur qui la résout, la borne à 300 colis et juge chaque colis.
 *
 * Un 200 peut porter des refus : `data.refused` les nomme, colis par colis.
 * Les refus GLOBAUX (403, cible interdite, sélection trop grande) reviennent en
 * échec, avec le message de l'API.
 */
export async function adminFieldStatusAction(
  change: unknown,
  selection: LabelJobSelection,
): Promise<ReferentialResult<AdminFieldStatusResult>> {
  const parsed = adminFieldStatusChangeSchema.safeParse(change);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const { statusId, comment, scheduledDate } = parsed.data;
    const result = await changeFieldStatus(
      { statusId, comment, ...(scheduledDate ? { scheduledDate } : {}) },
      selection,
    );
    if (result.changed.length > 0) {
      revalidatePath(`/[locale]/(app)/parcels/[code]`, 'page');
      revalidatePath(`/[locale]/(app)/parcels`, 'page');
      // Sur un circuit de livraison parti, la ligne du circuit a suivi.
      revalidatePath('/[locale]/(app)/routes/[code]', 'page');
    }
    return { ok: true, data: result };
  } catch (error) {
    return toFailure(error, 'parcels.fieldStatus');
  }
}

/**
 * Modifier les FRAIS d'un colis — C11 bloc 4, D-063.
 *
 * Elle rend `ReferentialResult` et non `ActionResult` : le refus le plus utile
 * de ce geste est un 409 motivé — le colis est déjà facturé — et `ActionResult`
 * perd le motif machine en route.
 */
export async function editParcelFeesAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<ParcelDetail>> {
  const parsed = editParcelFeesSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const parcel = await editParcelFees(code, parsed.data);
    revalidatePath(`/[locale]/(app)/parcels/[code]`, 'page');
    return { ok: true, data: parcel };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Recette SpeedyTrans 3, point 3 : le vendeur avant ramassage, le comptoir après */

export async function updateSellerParcelAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<ParcelDetail>> {
  const parsed = sellerParcelUpdateSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const parcel = await updateSellerParcel(code, parsed.data);
    revalidatePath(`/[locale]/(app)/parcels/[code]`, 'page');
    return { ok: true, data: parcel };
  } catch (error) {
    return toFailure(error, 'parcels.sellerUpdate');
  }
}

export async function deleteSellerParcelAction(
  code: string,
): Promise<ReferentialResult<{ code: string }>> {
  try {
    const result = await deleteSellerParcel(code);
    revalidatePath(`/[locale]/(app)/parcels`, 'page');
    return { ok: true, data: result };
  } catch (error) {
    return toFailure(error, 'parcels.sellerDelete');
  }
}

export async function changeExpectedPaymentAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<ParcelDetail>> {
  const parsed = expectedPaymentChangeSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const parcel = await changeParcelExpectedPayment(code, parsed.data);
    revalidatePath(`/[locale]/(app)/parcels/[code]`, 'page');
    return { ok: true, data: parcel };
  } catch (error) {
    return toFailure(error, 'parcels.expectedPayment');
  }
}

export async function addParcelNoteAction(code: string, input: unknown): Promise<ActionResult> {
  const parsed = addParcelNoteSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await addParcelNote(code, parsed.data.body);
    revalidatePath(`/[locale]/(app)/parcels/[code]`, 'page');
    return { ok: true, data: undefined };
  } catch (error) {
    return failure(error);
  }
}

/** Renvoie le code attribué, pour que l'appelant navigue vers le détail. */
export async function createParcelAction(input: unknown): Promise<ActionResult<string>> {
  const parsed = createParcelSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const parcel = await createParcel(parsed.data);
    revalidatePath(`/[locale]/(app)/parcels`, 'page');
    return { ok: true, data: parcel.code };
  } catch (error) {
    return failure(error);
  }
}

export interface ParcelDraftPreview {
  quote: Quote;
  code: string;
}

/**
 * Tarif et code du prochain colis, en un aller-retour.
 *
 * Une seule action plutôt que deux : Next dispatche les Server Actions une par
 * une par client, donc `Promise.all` de deux actions les sérialiserait sans
 * rien gagner. Elle garde aussi la matrice tarifaire côté serveur, hors du
 * bundle client.
 */
export async function previewParcelDraftAction(
  query: QuoteParcelQuery,
): Promise<ActionResult<ParcelDraftPreview>> {
  try {
    const [quote, code] = await Promise.all([
      quoteParcel(query),
      previewParcelCode(query.originCityId, query.destinationCityId),
    ]);
    return { ok: true, data: { quote, code } };
  } catch (error) {
    return failure(error);
  }
}

/* -------------------------------------------------------------------------- */
/* Import Excel — C7 bloc 1                                                   */
/* -------------------------------------------------------------------------- */

/**
 * Aperçu. La source est un classeur OU des lignes déjà corrigées à l'écran :
 * c'est ce qui permet la correction inline sans redemander le fichier.
 *
 * Le résultat porte `ReferentialResult` et non `ActionResult` parce que le
 * refus doit voyager avec ses DÉTAILS — la liste d'erreurs et le motif — que
 * l'écran retraduit. Un `{ ok: false, error }` nu ne dirait rien à corriger.
 */
export async function previewParcelImportAction(
  input: unknown,
): Promise<ReferentialResult<ParcelImportPreview>> {
  const parsed = parcelImportSourceSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    return { ok: true, data: await previewParcelImport(parsed.data) };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * « Télécharger les anomalies » — lot J, J4. Les lignes de la relecture en
 * cours (corrections comprises) repartent au même validateur que l'aperçu.
 */
export async function parcelImportAnomaliesAction(
  input: unknown,
): Promise<ReferentialResult<string>> {
  const parsed = parcelImportSourceSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    return { ok: true, data: await parcelImportAnomalies(parsed.data) };
  } catch (error) {
    return toFailure(error);
  }
}

export async function runParcelImportAction(
  input: unknown,
): Promise<ReferentialResult<ParcelImportResult>> {
  const parsed = parcelImportApplySchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const result = await runParcelImport(parsed.data);
    revalidatePath('/[locale]/(app)/parcels', 'page');
    revalidatePath('/[locale]/(app)/parcels/import', 'page');
    return { ok: true, data: result };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Le modèle et les rapports passent par une Server Action, pas par un lien.
 *
 * La session vit dans un cookie `httpOnly` posé sur l'origine du web : un
 * `<a href>` vers l'API partirait sans elle (D-018). Le classeur revient donc
 * en base64, et le navigateur en fait un `Blob`.
 */
export async function parcelImportTemplateAction(): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await apiFetchBase64('/parcels/import/template.xlsx') };
  } catch (error) {
    return toFailure(error);
  }
}

export async function parcelImportReportAction(id: string): Promise<ReferentialResult<string>> {
  try {
    return {
      ok: true,
      data: await apiFetchBase64(`/parcels/imports/${encodeURIComponent(id)}/report.xlsx`),
    };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * CHANGER LA DESTINATION — chantier « Finance », point 7.
 *
 * La réponse porte les circuits quittés et le tarif recalculé : l'écran les
 * annonce, parce que ce sont des CONSÉQUENCES que l'agent doit traiter, pas
 * des détails du geste réussi.
 */
export async function changeDestinationAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<ParcelDetail>> {
  const parsed = changeDestinationSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const parcel = await changeDestination(code, parsed.data);
    revalidatePath('/[locale]/(app)/parcels/[code]', 'page');
    revalidatePath('/[locale]/(app)/parcels', 'page');
    return { ok: true, data: parcel };
  } catch (error) {
    return toFailure(error);
  }
}
