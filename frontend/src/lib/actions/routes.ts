'use server';

import { type BulkJobAccepted, isBulkJobAccepted } from '@delivery/shared';
import {
  type DeliveryNoteDetail,
  deliveryNoteSchema,
  type RouteConfirmPayload,
  routeConfirmSchema,
  routeBulkConfirmSchema,
  type RouteBulkConfirmResult,
  type RouteDetail,
  routeSchema,
  routeUpdateSchema,
  type CandidatePage,
  type ParcelListItem,
  type RouteCandidate,
  type RouteNoteCandidate,
  type SelectionPayload,
  CANDIDATE_PAGE_SIZE,
  parcelTransferOptionsSchema,
  type ParcelTransferOptions,
  parcelTransferSchema,
  type ParcelTransferResult,
} from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import type { ReferentialResult } from '@/lib/actions/action-result';
import { toFailure } from '@/lib/actions/action-result';
import {
  addDeliveryNoteParcels,
  addRouteNotes,
  addRouteParcels,
  closeRoute,
  confirmRouteParcel,
  confirmRouteParcelsInBulk,
  createDeliveryNote,
  createRoute,
  deleteRoute,
  updateRoute,
  deleteDeliveryNote,
  dispatchRoute,
  pickupRouteNote,
  removeDeliveryNoteParcel,
  removeRouteNote,
  removeRouteParcel,
  validateDeliveryNote,
  restoreRemovedNoteParcel,
  getRouteCandidatePage,
  getRouteNoteCandidatePage,
  transferParcels,
  getTransferOptions,
} from '@/lib/api/routes';
import { getParcels } from '@/lib/api/parcels';
import { cancelMerchantNote, requestDropoffNote, requestPickupNote } from '@/lib/api/merchant';

/**
 * Écritures sur les circuits et les bons.
 *
 * Même principe que `actions/parcels.ts` : `src/lib/api/*` porte le contrat
 * d'endpoint, ces actions ne font que l'exposer et invalider la route.
 *
 * Elles rendent un `ReferentialResult` et non le `ActionResult` d'origine :
 * la composition d'un circuit se heurte à des refus qui ont un MOTIF — colis
 * déjà sur un autre circuit, circuit parti, transition interdite. Perdre ce
 * motif en route obligerait l'écran à afficher le message français de l'API à
 * un agent qui travaille en arabe.
 */

function revalidateRoutes(): void {
  revalidatePath('/[locale]/(app)/routes/[code]', 'page');
  revalidatePath('/[locale]/(app)/routes', 'page');
}

export async function createRouteAction(input: unknown): Promise<ReferentialResult<RouteDetail>> {
  const parsed = routeSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const route = await createRoute(parsed.data);
    revalidateRoutes();
    return { ok: true, data: route };
  } catch (error) {
    return toFailure(error);
  }
}

/** D-173 — modifier un brouillon. L'API refuse un circuit parti, quoi qu'on envoie. */
export async function updateRouteAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<RouteDetail>> {
  const parsed = routeUpdateSchema.safeParse(input);
  if (!code || !parsed.success) return { ok: false, error: 'invalid' };

  try {
    const route = await updateRoute(code, parsed.data);
    revalidateRoutes();
    return { ok: true, data: route };
  } catch (error) {
    return toFailure(error);
  }
}

/** D-173 — supprimer un brouillon VIDE (sinon 409 `route-not-empty`). */
export async function deleteRouteAction(code: string): Promise<ReferentialResult> {
  if (!code) return { ok: false, error: 'invalid' };

  try {
    await deleteRoute(code);
    revalidateRoutes();
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/** Ajout EN LOT : un écran de composition, une requête. */
export async function addRouteParcelsAction(
  code: string,
  selection: SelectionPayload | string[],
  district?: string,
  cityId?: string,
): Promise<ReferentialResult<RouteDetail>> {
  if (!code || isEmptySelection(selection)) return { ok: false, error: 'invalid' };

  try {
    const route = await addRouteParcels(code, selection, district, cityId);
    revalidateRoutes();
    revalidatePath('/[locale]/(app)/parcels', 'page');
    return { ok: true, data: route };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * LES BONS D'UN CIRCUIT DE RAMASSAGE — retouches Anass, point 5.
 *
 * Les colis du bon suivent : c'est le serveur qui les pose, pas l'écran. Les
 * faire poser par le navigateur aurait demandé deux requêtes dont la seconde
 * peut échouer — et un circuit qui porte un bon sans ses colis n'est pas un
 * état qu'on veut expliquer à un livreur.
 */
export async function addRouteNotesAction(
  code: string,
  selection: SelectionPayload | string[],
): Promise<ReferentialResult<RouteDetail>> {
  if (!code || isEmptySelection(selection)) return { ok: false, error: 'invalid' };

  try {
    const route = await addRouteNotes(code, selection);
    revalidateRoutes();
    return { ok: true, data: route };
  } catch (error) {
    return toFailure(error);
  }
}

export async function removeRouteNoteAction(
  code: string,
  noteId: string,
): Promise<ReferentialResult<RouteDetail>> {
  if (!code || !noteId) return { ok: false, error: 'invalid' };

  try {
    const route = await removeRouteNote(code, noteId);
    revalidateRoutes();
    return { ok: true, data: route };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Le livreur atteste avoir pris ce bon — depuis l'espace livreur du web, qui
 * double l'application mobile (L2).
 *
 * La liste de colis part telle quelle : le contrôle d'appartenance est refait
 * par le serveur, et c'est là qu'il compte. Un code scanné vient d'un carton,
 * pas d'une liste.
 */
export async function pickupRouteNoteAction(
  code: string,
  noteId: string,
  parcelIds: string[],
  /** Recette 5, point 7 : les non cochés sont retirés du bon. */
  removeUnpicked = false,
): Promise<ReferentialResult<RouteDetail>> {
  if (!code || !noteId || parcelIds.length === 0) return { ok: false, error: 'invalid' };

  try {
    const route = await pickupRouteNote(code, noteId, parcelIds, removeUnpicked);
    revalidateRoutes();
    revalidatePath('/[locale]/(app)/parcels', 'page');
    return { ok: true, data: route };
  } catch (error) {
    return toFailure(error);
  }
}

export async function removeRouteParcelAction(
  code: string,
  parcelId: string,
): Promise<ReferentialResult<RouteDetail>> {
  if (!code || !parcelId) return { ok: false, error: 'invalid' };

  try {
    const route = await removeRouteParcel(code, parcelId);
    revalidateRoutes();
    return { ok: true, data: route };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Départ du circuit. Les colis changent de statut ET de livreur : la liste des
 * colis change donc elle aussi, d'où sa revalidation.
 */
export async function dispatchRouteAction(
  code: string,
): Promise<ReferentialResult<RouteDetail | BulkJobAccepted>> {
  if (!code) return { ok: false, error: 'invalid' };

  try {
    const route = await dispatchRoute(code);
    revalidateRoutes();
    revalidatePath('/[locale]/(app)/parcels', 'page');
    return { ok: true, data: route };
  } catch (error) {
    return toFailure(error);
  }
}

export async function confirmRouteParcelAction(
  code: string,
  parcelId: string,
  input: unknown,
): Promise<ReferentialResult<RouteDetail>> {
  if (!code || !parcelId) return { ok: false, error: 'invalid' };

  const parsed = routeConfirmSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const route = await confirmRouteParcel(code, parcelId, parsed.data as RouteConfirmPayload);
    revalidateRoutes();
    revalidatePath('/[locale]/(app)/parcels', 'page');
    return { ok: true, data: route };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Lot I, I1 (D-211) — transférer des colis sur le circuit d'un autre livreur,
 * depuis la fiche colis ou la page circuit. La page circuit et la fiche des
 * colis transférés se rechargent.
 */
export async function transferParcelsAction(
  input: unknown,
): Promise<ReferentialResult<ParcelTransferResult | BulkJobAccepted>> {
  const parsed = parcelTransferSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const result = await transferParcels(parsed.data);
    revalidateRoutes();
    revalidatePath('/[locale]/(app)/parcels/[code]', 'page');
    revalidatePath('/[locale]/(app)/parcels', 'page');
    return { ok: true, data: result };
  } catch (error) {
    return toFailure(error, 'routes.transfer');
  }
}

/**
 * Lot N (D-223) — les villes de B pour le champ « Ville de destination », lues
 * dès que l'admin choisit le livreur. Lecture seule : rien n'est revalidé.
 */
export async function transferOptionsAction(
  input: unknown,
): Promise<ReferentialResult<ParcelTransferOptions>> {
  const parsed = parcelTransferOptionsSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    return { ok: true, data: await getTransferOptions(parsed.data) };
  } catch (error) {
    return toFailure(error, 'routes.transfer');
  }
}

/** D-146 — le même résultat pour les colis cochés d'un circuit. */
export async function bulkConfirmRouteAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<RouteBulkConfirmResult | BulkJobAccepted>> {
  if (!code) return { ok: false, error: 'invalid' };

  const parsed = routeBulkConfirmSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const result = await confirmRouteParcelsInBulk(code, parsed.data);
    revalidateRoutes();
    revalidatePath('/[locale]/(app)/parcels', 'page');
    return { ok: true, data: result };
  } catch (error) {
    return toFailure(error);
  }
}

export async function closeRouteAction(
  code: string,
  /** Ramassage : retirer les colis non ramassés de leur bon (D-145). */
  removeUnpicked = false,
): Promise<ReferentialResult> {
  if (!code) return { ok: false, error: 'invalid' };

  try {
    await closeRoute(code, removeUnpicked === true);
    revalidatePath('/[locale]/(app)/routes/[code]', 'page');
    revalidatePath('/[locale]/(app)/routes', 'page');
    // Les bons du circuit ont changé : validés, colis retirés (« 5/7 ramassés »).
    revalidatePath('/[locale]/(app)/pickup-notes', 'page');
    revalidatePath('/[locale]/(app)/pickup-notes/[code]', 'page');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

function revalidateNotes(): void {
  revalidatePath('/[locale]/(app)/pickup-notes/[code]', 'page');
  revalidatePath('/[locale]/(app)/pickup-notes', 'page');
  revalidatePath('/[locale]/(app)/dropoff-notes/[code]', 'page');
  revalidatePath('/[locale]/(app)/dropoff-notes', 'page');
}

export async function createDeliveryNoteAction(
  input: unknown,
): Promise<ReferentialResult<DeliveryNoteDetail>> {
  const parsed = deliveryNoteSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const note = await createDeliveryNote(parsed.data);
    revalidateNotes();
    return { ok: true, data: note };
  } catch (error) {
    return toFailure(error);
  }
}

export async function addDeliveryNoteParcelsAction(
  code: string,
  selection: SelectionPayload | string[],
): Promise<ReferentialResult<DeliveryNoteDetail>> {
  if (!code || isEmptySelection(selection)) return { ok: false, error: 'invalid' };

  try {
    const note = await addDeliveryNoteParcels(code, selection);
    revalidateNotes();
    return { ok: true, data: note };
  } catch (error) {
    return toFailure(error);
  }
}

export async function removeDeliveryNoteParcelAction(
  code: string,
  parcelId: string,
): Promise<ReferentialResult<DeliveryNoteDetail>> {
  if (!code || !parcelId) return { ok: false, error: 'invalid' };

  try {
    const note = await removeDeliveryNoteParcel(code, parcelId);
    revalidateNotes();
    return { ok: true, data: note };
  } catch (error) {
    return toFailure(error);
  }
}

/** Validation d'un bon : passe ses colis au statut 3 (DOMAIN.md §2.3). */
export async function validateDeliveryNoteAction(
  code: string,
  /** Recette 5, point 7 : seuls ceux-là ; les autres sont retirés du bon. */
  parcelIds?: string[],
): Promise<ReferentialResult<BulkJobAccepted | undefined>> {
  if (!code) return { ok: false, error: 'invalid' };

  try {
    const result = await validateDeliveryNote(code, parcelIds);
    revalidateNotePages();
    // Lot W2 (D-239) : au-delà de 300 colis, la tâche — l'écran suit sa progression.
    return { ok: true, data: isBulkJobAccepted(result) ? result : undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/** Restaure un colis retiré du bon par erreur — Recette 5, point 7. */
export async function restoreRemovedNoteParcelAction(
  code: string,
  parcelId: string,
): Promise<ReferentialResult> {
  if (!code || !parcelId) return { ok: false, error: 'invalid' };

  try {
    await restoreRemovedNoteParcel(code, parcelId);
    revalidateNotePages();
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/** Ce qu'une validation ou une restauration change : les bons, les circuits, les colis. */
function revalidateNotePages() {
  revalidatePath('/[locale]/(app)/pickup-notes/[code]', 'page');
  revalidatePath('/[locale]/(app)/dropoff-notes/[code]', 'page');
  revalidatePath('/[locale]/(app)/pickup-notes', 'page');
  revalidatePath('/[locale]/(app)/dropoff-notes', 'page');
  revalidatePath('/[locale]/(app)/routes/[code]', 'page');
  revalidatePath('/[locale]/(app)/routes', 'page');
  revalidatePath('/[locale]/(app)/parcels', 'page');
}

/**
 * LE VENDEUR DEMANDE UN BON — V2 vendeur bloc 4.
 *
 * La sélection part telle quelle : c'est le SERVEUR qui retient les colis dans
 * l'état attendu du genre de bon et qui écarte ceux d'un autre client. Filtrer
 * ici ferait croire à une garantie que l'écran ne peut pas donner.
 */
export async function requestPickupNoteAction(
  selection: SelectionPayload | readonly string[],
): Promise<ReferentialResult<DeliveryNoteDetail>> {
  try {
    const note = await requestPickupNote(selection);
    revalidateNotes();
    return { ok: true, data: note };
  } catch (error) {
    return toFailure(error);
  }
}

export async function requestDropoffNoteAction(
  selection: SelectionPayload | readonly string[],
): Promise<ReferentialResult<DeliveryNoteDetail>> {
  try {
    const note = await requestDropoffNote(selection);
    revalidateNotes();
    return { ok: true, data: note };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * SUPPRESSION D'UN BON — point 0 du chantier « Retours, BL Clients Pro ».
 *
 * **Deux endpoints pour un même geste, et c'est voulu.** L'agent passe par
 * `DELETE /delivery-notes/:code` (`notes.write`), le vendeur par
 * `DELETE /merchant/me/notes/:code` (`notes.request`) : le second ne prend
 * aucun identifiant de client et lit la portée. Un seul chemin aurait forcé
 * à donner `notes.write` au vendeur, donc la validation des bons avec.
 */
export async function deleteDeliveryNoteAction(
  code: string,
  as: 'staff' | 'merchant' = 'staff',
): Promise<ReferentialResult> {
  if (!code) return { ok: false, error: 'invalid' };

  try {
    await (as === 'merchant' ? cancelMerchantNote(code) : deleteDeliveryNote(code));
    revalidateNotes();
    revalidatePath('/[locale]/(app)/parcels', 'page');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Les listes de candidats, par pages (chantier « Plafond de 200 ») ----- */

/** Vide : ni ids cochés, ni « tout ». */
function isEmptySelection(selection: SelectionPayload | readonly string[]): boolean {
  if (Array.isArray(selection)) return selection.length === 0;
  const value = selection as SelectionPayload;
  return !value.all && (value.ids?.length ?? 0) === 0;
}

/**
 * Les candidats d'un bon BR-/BD-, page par page — la MÊME liste que
 * `GET /parcels?noteCandidate=true` (D-132), au vrai total. `clientId` nul :
 * le vendeur connecté (sa portée le borne).
 */
export async function noteCandidatePageAction(
  clientId: string | null,
  query: { search: string; page: number },
): Promise<ReferentialResult<CandidatePage<ParcelListItem>>> {
  try {
    const page = await getParcels({
      ...(clientId ? { clientId } : {}),
      noteCandidate: true,
      // « Ajouter des colis » : les plus récents en premier (chantier « Tri par défaut »).
      recent: true,
      search: query.search || undefined,
      page: query.page,
      pageSize: CANDIDATE_PAGE_SIZE,
    });
    return { ok: true, data: { ...page, selectableTotal: page.total } };
  } catch (error) {
    return toFailure(error);
  }
}

export async function routeCandidatePageAction(
  code: string,
  query: { search: string; page: number; district?: string; cityId?: string },
): Promise<
  ReferentialResult<
    CandidatePage<RouteCandidate> & {
      districts: Array<{ key: string; name: string }>;
      cities: Array<{ id: string; name: string }>;
    }
  >
> {
  try {
    return { ok: true, data: await getRouteCandidatePage(code, query) };
  } catch (error) {
    return toFailure(error);
  }
}

export async function routeNoteCandidatePageAction(
  code: string,
  query: { search: string; page: number },
): Promise<ReferentialResult<CandidatePage<RouteNoteCandidate>>> {
  try {
    return { ok: true, data: await getRouteNoteCandidatePage(code, query) };
  } catch (error) {
    return toFailure(error);
  }
}
