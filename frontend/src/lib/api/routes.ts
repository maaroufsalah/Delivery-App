import type {
  DeliveryNoteDetail,
  DeliveryNoteListItem,
  DeliveryNotePayload,
  DeliveryNoteStats,
  Paginated,
  RouteCandidate,
  RouteBulkConfirmPayload,
  RouteBulkConfirmResult,
  RouteConfirmPayload,
  RouteDetail,
  RouteListItem,
  RouteNoteCandidate,
  RoutePayload,
  RouteUpdatePayload,
  RouteStats,
  ParcelTransferOptions,
  ParcelTransferOptionsPayload,
  ParcelTransferPayload,
  ParcelTransferResult,
} from '@delivery/shared';
import {
  BULK_JOBS_HEADER,
  type BulkJobAccepted,
  CANDIDATE_PAGE_SIZE,
  type CandidatePage,
  type SelectionPayload,
} from '@delivery/shared';
import { selectionBody } from '@/lib/api/selection';
import { apiFetch } from '@/lib/http';

/** Lot W2 (D-239) : le web sait suivre une tâche de masse — il le dit à l'API. */
const BULK_JOBS = { [BULK_JOBS_HEADER]: '1' };

/**
 * Circuits et bons — `GET /routes`, `GET /delivery-notes`, plus les écritures
 * `POST /routes/:code/parcels/:id/confirm`, `POST /routes/:code/close`,
 * `POST /delivery-notes/:code/validate`.
 */

export interface RouteListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
  cityId?: string;
  courierId?: string;
  kind?: string;
  /** `'true'` : les plus récents en premier — l'écran, jamais l'export (chantier « Tri par défaut »). */
  recent?: 'true';
}

export async function getRoutes(query: RouteListQuery = {}): Promise<Paginated<RouteListItem>> {
  return apiFetch<Paginated<RouteListItem>>('/routes', { query: { ...query } });
}

/**
 * KPI de tête de liste — calculés par l'API sur TOUTE la sélection.
 *
 * Depuis que la liste est paginée côté serveur (C7 bloc 4), les déduire des
 * lignes chargées afficherait les compteurs de la page courante.
 */
export async function getRouteStats(query: RouteListQuery = {}): Promise<RouteStats> {
  return apiFetch<RouteStats>('/routes/stats', { query: { ...query } });
}

export async function getRoute(code: string): Promise<RouteDetail> {
  return apiFetch<RouteDetail>(`/routes/${encodeURIComponent(code)}`);
}

export async function createRoute(body: RoutePayload): Promise<RouteDetail> {
  return apiFetch<RouteDetail>('/routes', { method: 'POST', body });
}

/** D-173 — modifie un circuit en BROUILLON ; l'API refuse un circuit parti. */
export async function updateRoute(code: string, body: RouteUpdatePayload): Promise<RouteDetail> {
  return apiFetch<RouteDetail>(`/routes/${encodeURIComponent(code)}`, { method: 'PATCH', body });
}

/** D-173 — supprime un circuit en BROUILLON et VIDE ; sinon 409 avec son motif. */
export async function deleteRoute(code: string): Promise<{ code: string }> {
  return apiFetch<{ code: string }>(`/routes/${encodeURIComponent(code)}`, { method: 'DELETE' });
}

/** Colis éligibles — l'éligibilité est décidée par le serveur, jamais ici. */
export async function getRouteCandidates(code: string, search?: string): Promise<RouteCandidate[]> {
  return apiFetch<RouteCandidate[]>(`/routes/${encodeURIComponent(code)}/candidates`, {
    query: search ? { search } : {},
  });
}

/**
 * `GET /routes/:code/candidates?page=…` — une PAGE au vrai total, avec les
 * quartiers de toute la liste (chantier « Plafond de 200 »).
 */
export async function getRouteCandidatePage(
  code: string,
  query: { search?: string; page: number; district?: string; cityId?: string },
): Promise<
  CandidatePage<RouteCandidate> & {
    districts: Array<{ key: string; name: string }>;
    cities: Array<{ id: string; name: string }>;
  }
> {
  return apiFetch(`/routes/${encodeURIComponent(code)}/candidates`, {
    query: {
      page: query.page,
      pageSize: CANDIDATE_PAGE_SIZE,
      ...(query.search ? { search: query.search } : {}),
      ...(query.district ? { district: query.district } : {}),
      ...(query.cityId ? { cityId: query.cityId } : {}),
    },
  });
}

export async function addRouteParcels(
  code: string,
  selection: SelectionPayload | string[],
  district?: string,
  /** Lot F : le filtre Ville de la modale, rejoué avec « Tout cocher ». */
  cityId?: string,
): Promise<RouteDetail> {
  return apiFetch<RouteDetail>(`/routes/${encodeURIComponent(code)}/parcels`, {
    method: 'POST',
    body: selectionBody(selection, 'parcelIds', {
      ...(district ? { district } : {}),
      ...(cityId ? { cityId } : {}),
    }),
  });
}

/**
 * LES BONS D'UN CIRCUIT DE RAMASSAGE — retouches Anass, point 5.
 *
 * Une fonction par endpoint, comme partout (règle n°1 de CLAUDE.md). La liste
 * rend un tableau VIDE sur un circuit de livraison ou de retour : l'écran
 * interroge la même route pour les trois types, et un refus l'obligerait à
 * savoir avant de demander.
 */
export async function getRouteNoteCandidates(
  code: string,
  search?: string,
): Promise<RouteNoteCandidate[]> {
  return apiFetch<RouteNoteCandidate[]>(`/routes/${encodeURIComponent(code)}/note-candidates`, {
    query: search ? { search } : {},
  });
}

/** `GET /routes/:code/note-candidates?page=…` — une page au vrai total. */
export async function getRouteNoteCandidatePage(
  code: string,
  query: { search?: string; page: number },
): Promise<CandidatePage<RouteNoteCandidate>> {
  return apiFetch(`/routes/${encodeURIComponent(code)}/note-candidates`, {
    query: {
      page: query.page,
      pageSize: CANDIDATE_PAGE_SIZE,
      ...(query.search ? { search: query.search } : {}),
    },
  });
}

/** Les colis du bon entrent dans le circuit avec lui — c'est le serveur qui les y met. */
export async function addRouteNotes(
  code: string,
  selection: SelectionPayload | string[],
): Promise<RouteDetail> {
  return apiFetch<RouteDetail>(`/routes/${encodeURIComponent(code)}/notes`, {
    method: 'POST',
    body: selectionBody(selection, 'noteIds'),
  });
}

export async function removeRouteNote(code: string, noteId: string): Promise<RouteDetail> {
  return apiFetch<RouteDetail>(
    `/routes/${encodeURIComponent(code)}/notes/${encodeURIComponent(noteId)}`,
    { method: 'DELETE' },
  );
}

/** Le livreur atteste avoir pris ce bon : ses colis passent en « ramassé par le livreur ». */
export async function pickupRouteNote(
  code: string,
  noteId: string,
  parcelIds: string[],
  removeUnpicked = false,
): Promise<RouteDetail> {
  return apiFetch<RouteDetail>(
    `/routes/${encodeURIComponent(code)}/notes/${encodeURIComponent(noteId)}/pickup`,
    { method: 'POST', body: removeUnpicked ? { parcelIds, removeUnpicked } : { parcelIds } },
  );
}

export async function removeRouteParcel(code: string, parcelId: string): Promise<RouteDetail> {
  return apiFetch<RouteDetail>(
    `/routes/${encodeURIComponent(code)}/parcels/${encodeURIComponent(parcelId)}`,
    { method: 'DELETE' },
  );
}

/**
 * Départ : colis en livraison ET affectés au livreur. Au-delà de 300 colis, une
 * tâche par paquets (202) — lot W2 (D-239).
 */
export async function dispatchRoute(code: string): Promise<RouteDetail | BulkJobAccepted> {
  return apiFetch<RouteDetail | BulkJobAccepted>(`/routes/${encodeURIComponent(code)}/dispatch`, {
    method: 'POST',
    headers: BULK_JOBS,
  });
}

/** Le livreur rend compte : colis en main, et son issue. */
export async function confirmRouteParcel(
  code: string,
  parcelId: string,
  body: RouteConfirmPayload,
): Promise<RouteDetail> {
  return apiFetch<RouteDetail>(
    `/routes/${encodeURIComponent(code)}/parcels/${encodeURIComponent(parcelId)}/confirm`,
    { method: 'POST', body },
  );
}

/**
 * Le même résultat pour les colis cochés (D-146). Au-delà de 300 colis, une
 * tâche par paquets (202) — lot W2 (D-239).
 */
export async function confirmRouteParcelsInBulk(
  code: string,
  body: RouteBulkConfirmPayload,
): Promise<RouteBulkConfirmResult | BulkJobAccepted> {
  return apiFetch<RouteBulkConfirmResult | BulkJobAccepted>(
    `/routes/${encodeURIComponent(code)}/outcomes`,
    { method: 'POST', body, headers: BULK_JOBS },
  );
}

/** Refusée côté serveur tant que tous les colis ne sont pas confirmés. */
/**
 * Clôture. Sur un RAMASSAGE, `removeUnpicked` confirme le retrait des colis non
 * ramassés de leur bon (D-145) ; sans lui, l'API refuse et les nomme.
 */
export async function closeRoute(code: string, removeUnpicked = false): Promise<RouteDetail> {
  return apiFetch<RouteDetail>(`/routes/${encodeURIComponent(code)}/close`, {
    method: 'POST',
    body: removeUnpicked ? { removeUnpicked: true } : {},
  });
}

export interface DeliveryNoteListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  kind?: string;
  status?: string;
  clientId?: string;
}

export async function getDeliveryNotes(
  query: DeliveryNoteListQuery = {},
): Promise<Paginated<DeliveryNoteListItem>> {
  return apiFetch<Paginated<DeliveryNoteListItem>>('/delivery-notes', { query: { ...query } });
}

/** Même raison que `getRouteStats` : les compteurs portent sur la sélection. */
export async function getDeliveryNoteStats(
  query: DeliveryNoteListQuery = {},
): Promise<DeliveryNoteStats> {
  return apiFetch<DeliveryNoteStats>('/delivery-notes/stats', { query: { ...query } });
}

export async function getDeliveryNote(code: string): Promise<DeliveryNoteDetail> {
  return apiFetch<DeliveryNoteDetail>(`/delivery-notes/${encodeURIComponent(code)}`);
}

/**
 * Validation d'un bon : les colis passent au statut 3 « Colis ramassé »
 * (DOMAIN.md §2.3), et le ramassage est exonéré au-delà de 5 colis (§3.4).
 * L'API refuse un bon déjà validé — le legacy rejouait la remise à chaque appel.
 */
export async function createDeliveryNote(body: DeliveryNotePayload): Promise<DeliveryNoteDetail> {
  return apiFetch<DeliveryNoteDetail>('/delivery-notes', { method: 'POST', body });
}

export async function addDeliveryNoteParcels(
  code: string,
  selection: SelectionPayload | string[],
): Promise<DeliveryNoteDetail> {
  return apiFetch<DeliveryNoteDetail>(`/delivery-notes/${encodeURIComponent(code)}/parcels`, {
    method: 'POST',
    body: selectionBody(selection, 'parcelIds'),
  });
}

export async function removeDeliveryNoteParcel(
  code: string,
  parcelId: string,
): Promise<DeliveryNoteDetail> {
  return apiFetch<DeliveryNoteDetail>(
    `/delivery-notes/${encodeURIComponent(code)}/parcels/${encodeURIComponent(parcelId)}`,
    { method: 'DELETE' },
  );
}

/**
 * `POST /delivery-notes/:code/validate` — avec `parcelIds`, validation
 * PARTIELLE (Recette 5, point 7) : les autres colis sont retirés du bon.
 */
export async function validateDeliveryNote(
  code: string,
  parcelIds?: string[],
): Promise<DeliveryNoteDetail | BulkJobAccepted> {
  return apiFetch<DeliveryNoteDetail | BulkJobAccepted>(
    `/delivery-notes/${encodeURIComponent(code)}/validate`,
    {
      method: 'POST',
      headers: BULK_JOBS,
      // Lot W2 (D-239) : au-delà de 300 colis, une tâche par paquets (202).
      ...(parcelIds ? { body: { parcelIds } } : {}),
    },
  );
}

/**
 * `POST /delivery-notes/:code/removed/:parcelId/restore` — un colis retiré par
 * erreur revient « En attente de ramassage », hors bon (Recette 5, point 7).
 */
export async function restoreRemovedNoteParcel(
  code: string,
  parcelId: string,
): Promise<DeliveryNoteDetail> {
  return apiFetch<DeliveryNoteDetail>(
    `/delivery-notes/${encodeURIComponent(code)}/removed/${encodeURIComponent(parcelId)}/restore`,
    { method: 'POST' },
  );
}

/**
 * Supprime un bon NON VALIDÉ — point 0 du chantier « Retours, BL Clients Pro ».
 *
 * Trois refus côté API, chacun avec son motif : bon validé, bon déjà pris par
 * le livreur, bon porté par un circuit. L'écran n'en décide aucun.
 */
export async function deleteDeliveryNote(code: string): Promise<{ code: string }> {
  return apiFetch<{ code: string }>(`/delivery-notes/${encodeURIComponent(code)}`, {
    method: 'DELETE',
  });
}

/**
 * Transférer des colis sur le circuit d'un autre livreur — lot I, I1 (D-211).
 * Depuis la fiche colis (un colis) ou la page circuit (les cochés).
 */
/** Transfert vers un autre livreur. Au-delà de 300 colis, une tâche par paquets (202) — lot W2 (D-239). */
export async function transferParcels(
  body: ParcelTransferPayload,
): Promise<ParcelTransferResult | BulkJobAccepted> {
  return apiFetch<ParcelTransferResult | BulkJobAccepted>('/routes/transfer', {
    method: 'POST',
    body,
    headers: BULK_JOBS,
  });
}

/**
 * Les villes de B pour le champ de la boîte « Transférer » — lot N (D-223).
 * POST : la sélection d'un circuit peut compter des centaines d'identifiants.
 */
export async function getTransferOptions(
  body: ParcelTransferOptionsPayload,
): Promise<ParcelTransferOptions> {
  return apiFetch<ParcelTransferOptions>('/routes/transfer/options', { method: 'POST', body });
}
