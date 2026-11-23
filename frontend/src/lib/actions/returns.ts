'use server';

import {
  type ReturnHandoverCancelPayload,
  returnHandoverCancelSchema,
  type ReturnHandoverDetail,
  type ReturnHandoverParcelsPayload,
  returnHandoverParcelsSchema,
  type ReturnHandoverPayload,
  returnHandoverSchema,
  type ReturnHandoverPickupPayload,
  returnHandoverPickupSchema,
  type ReturnScanPayload,
  returnScanSchema,
  type ReturnSlipClosePayload,
  returnSlipCloseSchema,
  type ReturnSlipBulkSummary,
  type ReturnSlipDetail,
  type ReturnSlipItemPayload,
  returnSlipItemSchema,
  type ReturnSlipItemsPayload,
  returnSlipItemsSchema,
  type ReturnSlipPayload,
  returnSlipSchema,
} from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import type { CandidatePage, ReturnParcelLine } from '@delivery/shared';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import {
  addHandoverParcel,
  addHandoverParcels,
  addSlipParcel,
  addSlipParcels,
  cancelHandover,
  closeHandover,
  closeSlip,
  confirmHandover,
  createHandover,
  createSlip,
  createHandoversInBulk,
  createSlipsInBulk,
  deleteHandover,
  deleteSlip,
  downloadHandoverPdf,
  downloadSlipPdf,
  getHandoverCandidates,
  getHandoverCandidatePage,
  getSlipCandidatePage,
  getHandoverBulkPreview,
  getSlip,
  getSlipBulkPreview,
  handOverHandover,
  pickupHandover,
  removeHandoverParcel,
  requestHandover,
  removeSlipParcel,
} from '@/lib/api/returns';

/**
 * Écritures du menu RETOURS.
 *
 * Même forme que le référentiel et les comptes : la Server Action revalide la
 * saisie, appelle l'endpoint, et laisse remonter le motif d'un 409. Ce motif
 * compte plus ici qu'ailleurs — devant une douchette, « refusé » sans raison
 * conduit à rescanner le même colis.
 */

function revalidateReturns(route: string): void {
  revalidatePath(`/[locale]/(app)/${route}`, 'page');
}

/**
 * Les QUATRE écrans que bouge une transition de bon de retour.
 *
 * Le détail et la liste, évidemment ; la pile d'entrée, parce qu'un colis
 * réservé en sort et qu'un colis relâché y revient ; l'espace du livreur,
 * parce que c'est là qu'il voit ce qu'on lui demande.
 */
function revalidateHandover(): void {
  revalidateReturns('returns/handovers/[code]');
  revalidateReturns('returns/handovers');
  revalidateReturns('returns/pending');
  revalidatePath('/[locale]/(courier)/me/returns', 'page');
}

/* --- Bons de retour colis ------------------------------------------------- */

export async function createHandoverAction(
  input: unknown,
): Promise<ReferentialResult<ReturnHandoverDetail>> {
  const parsed = returnHandoverSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const handover = await createHandover(parsed.data as ReturnHandoverPayload);
    revalidateReturns('returns/handovers');
    return { ok: true, data: handover };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Ajout par scan.
 *
 * La remise ENTIÈRE revient en cas de succès : l'écran se réaffiche sans
 * relecture, ce qui compte quand on scanne trente colis à la suite.
 */
export async function scanHandoverParcelAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<ReturnHandoverDetail>> {
  const parsed = returnScanSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const handover = await addHandoverParcel(code, parsed.data as ReturnScanPayload);
    revalidateReturns('returns/handovers/[code]');
    return { ok: true, data: handover };
  } catch (error) {
    return toFailure(error);
  }
}

export async function removeHandoverParcelAction(
  code: string,
  parcelId: string,
): Promise<ReferentialResult<ReturnHandoverDetail>> {
  try {
    const handover = await removeHandoverParcel(code, parcelId);
    revalidateReturns('returns/handovers/[code]');
    return { ok: true, data: handover };
  } catch (error) {
    return toFailure(error);
  }
}

export async function confirmHandoverAction(
  code: string,
): Promise<ReferentialResult<ReturnHandoverDetail>> {
  try {
    const handover = await confirmHandover(code);
    // La confirmation vide la pile d'entrée et remplit celle des bons : les
    // trois écrans changent d'un coup.
    revalidateReturns('returns/handovers/[code]');
    revalidateReturns('returns/handovers');
    revalidateReturns('returns/pending');
    return { ok: true, data: handover };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * AJOUT EN LOT — chantier « Retours, BL Clients Pro », point 2.
 *
 * La sélection part telle quelle : c'est le SERVEUR qui retient les colis
 * éligibles et qui écarte ceux d'un autre livreur. Filtrer ici ferait croire
 * à une garantie que l'écran ne peut pas donner.
 */
export async function addHandoverParcelsAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<ReturnHandoverDetail>> {
  const parsed = returnHandoverParcelsSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const handover = await addHandoverParcels(code, parsed.data as ReturnHandoverParcelsPayload);
    revalidateHandover();
    return { ok: true, data: handover };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * « DEMANDER LE RETOUR » — la sortie du brouillon, point 2.
 *
 * Elle relève de la COMPOSITION, pas de l'attestation : l'agent finit de
 * scanner et dit « c'est prêt ». Le livreur en est averti dans la foulée.
 */
export async function requestHandoverAction(
  code: string,
): Promise<ReferentialResult<ReturnHandoverDetail>> {
  try {
    const handover = await requestHandover(code);
    revalidateHandover();
    return { ok: true, data: handover };
  } catch (error) {
    return toFailure(error);
  }
}

/** Le livreur atteste avoir la marchandise — scan vérifié ou saisie manuelle. */
export async function pickupHandoverAction(
  code: string,
  input: unknown = {},
): Promise<ReferentialResult<ReturnHandoverDetail>> {
  const parsed = returnHandoverPickupSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const handover = await pickupHandover(code, parsed.data as ReturnHandoverPickupPayload);
    revalidateHandover();
    return { ok: true, data: handover };
  } catch (error) {
    return toFailure(error);
  }
}

/** Le livreur l'a déposée à l'entrepôt. */
export async function handOverHandoverAction(
  code: string,
): Promise<ReferentialResult<ReturnHandoverDetail>> {
  try {
    const handover = await handOverHandover(code);
    revalidateHandover();
    return { ok: true, data: handover };
  } catch (error) {
    return toFailure(error);
  }
}

/** « Clôturer » — après la réception, et seulement après. */
export async function closeHandoverAction(
  code: string,
): Promise<ReferentialResult<ReturnHandoverDetail>> {
  try {
    const handover = await closeHandover(code);
    revalidateHandover();
    return { ok: true, data: handover };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * ANNULATION — elle relâche les colis, donc la pile d'entrée change aussi.
 */
export async function cancelHandoverAction(
  code: string,
  input: unknown = {},
): Promise<ReferentialResult<ReturnHandoverDetail>> {
  const parsed = returnHandoverCancelSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const handover = await cancelHandover(code, parsed.data as ReturnHandoverCancelPayload);
    revalidateHandover();
    return { ok: true, data: handover };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteHandoverAction(code: string): Promise<ReferentialResult> {
  try {
    await deleteHandover(code);
    revalidateReturns('returns/handovers');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * COLIS RAPPORTABLES PAR UN LIVREUR — point 2, étape 2.
 *
 * Une LECTURE dans une Server Action, et c'est délibéré : `src/lib/http.ts`
 * n'est jamais importé par un composant client (D-018), et l'assistant change
 * de livreur sans changer de page. Sans cette action, il faudrait une route
 * `/api` de plus, c'est-à-dire un second endroit où se tromper de portée.
 */
export async function handoverCandidatesAction(
  courierId?: string,
  search?: string,
): Promise<ReferentialResult<ReturnParcelLine[]>> {
  /*
   * `courierId` est FACULTATIF depuis le chantier « Bon de retour livreur » :
   * l'assistant de l'admin le passe, l'écran du livreur non — sa portée le
   * donne, et le lui faire passer laisserait croire qu'il peut en passer un
   * autre (D-082).
   */
  try {
    return { ok: true, data: await getHandoverCandidates(courierId, search) };
  } catch (error) {
    return toFailure(error);
  }
}

/** Le bon de retour en PDF — même chemin base64 que les autres documents. */
export async function downloadHandoverPdfAction(code: string): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await downloadHandoverPdf(code) };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Bons de retour ------------------------------------------------------- */

export async function createSlipAction(
  input: unknown,
): Promise<ReferentialResult<ReturnSlipDetail>> {
  const parsed = returnSlipSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const slip = await createSlip(parsed.data as ReturnSlipPayload);
    revalidateReturns('returns/slips');
    return { ok: true, data: slip };
  } catch (error) {
    return toFailure(error);
  }
}

/** Lot E, E2 (D-202) : la phrase de la confirmation — « N bons pour N clients, M colis ». */
export async function slipBulkPreviewAction(): Promise<ReferentialResult<ReturnSlipBulkSummary>> {
  try {
    return { ok: true, data: await getSlipBulkPreview() };
  } catch (error) {
    return toFailure(error);
  }
}

/** Lot E, E2 (D-202) : la création en masse, en une transaction côté API. */
export async function createSlipsInBulkAction(): Promise<ReferentialResult<ReturnSlipBulkSummary>> {
  try {
    const summary = await createSlipsInBulk();
    revalidateReturns('returns/slips');
    return { ok: true, data: summary };
  } catch (error) {
    return toFailure(error);
  }
}

export async function addSlipParcelAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<ReturnSlipDetail>> {
  const parsed = returnSlipItemSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const slip = await addSlipParcel(code, parsed.data as ReturnSlipItemPayload);
    revalidateReturns('returns/slips/[code]');
    return { ok: true, data: slip };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Ajout EN LOT — la multi-sélection du point 7.
 *
 * Le schéma est rejoué ici alors que l'écran l'a déjà validé : une Server
 * Action est une frontière réseau, et ce qui lui arrive n'a pas forcément
 * traversé le formulaire.
 */
export async function addSlipParcelsAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<ReturnSlipDetail>> {
  const parsed = returnSlipItemsSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const slip = await addSlipParcels(code, parsed.data as ReturnSlipItemsPayload);
    revalidateReturns('returns/slips/[code]');
    return { ok: true, data: slip };
  } catch (error) {
    return toFailure(error);
  }
}

export async function removeSlipParcelAction(
  code: string,
  parcelId: string,
): Promise<ReferentialResult<ReturnSlipDetail>> {
  try {
    const slip = await removeSlipParcel(code, parcelId);
    revalidateReturns('returns/slips/[code]');
    return { ok: true, data: slip };
  } catch (error) {
    return toFailure(error);
  }
}

export async function closeSlipAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<ReturnSlipDetail>> {
  const parsed = returnSlipCloseSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const slip = await closeSlip(code, parsed.data as ReturnSlipClosePayload);
    revalidateReturns('returns/slips/[code]');
    revalidateReturns('returns/slips');
    return { ok: true, data: slip };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteSlipAction(code: string): Promise<ReferentialResult> {
  try {
    await deleteSlip(code);
    revalidateReturns('returns/slips');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/** Le PDF revient en base64 ; le fichier est fabriqué par le navigateur. */
export async function downloadSlipPdfAction(code: string): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await downloadSlipPdf(code) };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Les colis d'UN bon, lus à l'ouverture de son accordéon — chantier « Bons de
 * retour vendeur ». Une lecture : la portée du serveur (`slipWhere`) épingle
 * le vendeur sur SES bons ; un code d'un autre vendeur répond 404.
 */
export async function slipDetailAction(code: string): Promise<ReferentialResult<ReturnSlipDetail>> {
  try {
    return { ok: true, data: await getSlip(code) };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Les candidats, par pages (chantier « Plafond de 200 ») ------------- */

/** La pile d'un livreur, page par page, au vrai total. */
export async function handoverCandidatePageAction(
  courierId: string | undefined,
  query: { search: string; page: number },
): Promise<ReferentialResult<CandidatePage<ReturnParcelLine>>> {
  try {
    return { ok: true, data: await getHandoverCandidatePage({ courierId, ...query }) };
  } catch (error) {
    return toFailure(error);
  }
}

/** Les colis rendables d'un bon de retour client, page par page. */
export async function slipCandidatePageAction(
  code: string,
  query: { search: string; page: number },
): Promise<ReferentialResult<CandidatePage<ReturnParcelLine>>> {
  try {
    return { ok: true, data: await getSlipCandidatePage(code, query) };
  } catch (error) {
    return toFailure(error);
  }
}

/** Lot F, F3 (D-207) : la phrase de la confirmation des RL- en masse. */
export async function handoverBulkPreviewAction(): Promise<
  ReferentialResult<ReturnSlipBulkSummary>
> {
  try {
    return { ok: true, data: await getHandoverBulkPreview() };
  } catch (error) {
    return toFailure(error);
  }
}

/** Lot F, F3 (D-207) : la création en masse des RL-, en une transaction côté API. */
export async function createHandoversInBulkAction(): Promise<
  ReferentialResult<ReturnSlipBulkSummary>
> {
  try {
    const summary = await createHandoversInBulk();
    revalidateHandover();
    return { ok: true, data: summary };
  } catch (error) {
    return toFailure(error);
  }
}
