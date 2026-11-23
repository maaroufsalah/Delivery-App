import type {
  Paginated,
  ReturnHandoverCancelPayload,
  ReturnHandoverDetail,
  ReturnHandoverListItem,
  ReturnHandoverParcelsPayload,
  ReturnHandoverPayload,
  ReturnHandoverPickupPayload,
  ReturnParcelLine,
  ReturnPendingGroup,
  ReturnScanPayload,
  ReturnSlipClosePayload,
  ReturnSlipBulkSummary,
  ReturnSlipDetail,
  ReturnSlipItemPayload,
  ReturnSlipItemsPayload,
  ReturnSlipListItem,
  ReturnSlipPayload,
} from '@delivery/shared';
import { CANDIDATE_PAGE_SIZE, type CandidatePage } from '@delivery/shared';
import { apiFetch, apiFetchBase64 } from '@/lib/http';

/**
 * Retours — remises du livreur (`RL-`) et bons rendus au client (`BRT-`).
 *
 * Une fonction = un endpoint, comme partout ailleurs. Les listes sont paginées
 * par le serveur ; la pile des colis à retourner ne l'est pas — c'est une pile
 * de travail, pas une archive, et elle se lit d'un coup d'œil.
 */

type Query = Record<string, string | number | undefined>;

function toSearch(query: object): Record<string, string | number> {
  const search: Record<string, string | number> = {};

  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue;
    search[key] = value as string | number;
  }

  return search;
}

/* --- Colis à retourner ---------------------------------------------------- */

export async function getReturnsPending(
  query: { agencyId?: string; courierId?: string; search?: string } = {},
): Promise<ReturnPendingGroup[]> {
  return apiFetch<ReturnPendingGroup[]>('/returns/pending', { query: toSearch(query) });
}

/* --- Bons de retour colis ------------------------------------------------- */

export async function getHandoverPage(
  query: Query & { status?: string; origin?: string; courierId?: string; agencyId?: string },
): Promise<Paginated<ReturnHandoverListItem>> {
  return apiFetch<Paginated<ReturnHandoverListItem>>('/return-handovers', {
    query: toSearch(query),
  });
}

export async function getHandover(code: string): Promise<ReturnHandoverDetail> {
  return apiFetch<ReturnHandoverDetail>(`/return-handovers/${encodeURIComponent(code)}`);
}

export async function createHandover(body: ReturnHandoverPayload): Promise<ReturnHandoverDetail> {
  return apiFetch<ReturnHandoverDetail>('/return-handovers', { method: 'POST', body });
}

export async function addHandoverParcel(
  code: string,
  body: ReturnScanPayload,
): Promise<ReturnHandoverDetail> {
  return apiFetch<ReturnHandoverDetail>(`/return-handovers/${encodeURIComponent(code)}/parcels`, {
    method: 'POST',
    body,
  });
}

export async function removeHandoverParcel(
  code: string,
  parcelId: string,
): Promise<ReturnHandoverDetail> {
  return apiFetch<ReturnHandoverDetail>(
    `/return-handovers/${encodeURIComponent(code)}/parcels/${parcelId}`,
    { method: 'DELETE' },
  );
}

/**
 * AJOUT EN LOT — chantier « Retours, BL Clients Pro », point 2.
 *
 * L'assistant de création coche des lignes ; la douchette lit des codes. Deux
 * entrées, deux endpoints, les mêmes contrôles au bout.
 */
export async function addHandoverParcels(
  code: string,
  body: ReturnHandoverParcelsPayload,
): Promise<ReturnHandoverDetail> {
  return apiFetch<ReturnHandoverDetail>(
    `/return-handovers/${encodeURIComponent(code)}/parcels/batch`,
    { method: 'POST', body },
  );
}

/**
 * COLIS RAPPORTABLES PAR UN LIVREUR — point 2, étape 2.
 *
 * Le livreur est en paramètre de requête et non dans l'URL : l'assistant
 * demande cette liste AVANT d'avoir un bon à nommer.
 */
export async function getHandoverCandidates(
  courierId?: string,
  search?: string,
): Promise<ReturnParcelLine[]> {
  return apiFetch<ReturnParcelLine[]>('/return-handovers/candidates', {
    query: toSearch({ courierId, search }),
  });
}

/**
 * « DEMANDER LE RETOUR » — chantier « Bon de retour livreur », point 2.
 *
 * Le brouillon du comptoir devient une demande que le livreur voit sur son
 * téléphone. C'est le « tant qu'il n'a pas validé » rendu explicite : jusqu'ici
 * un brouillon ne pouvait que se confirmer d'un bloc, et le livreur n'apprenait
 * jamais qu'on attendait quelque chose de lui.
 */
export async function requestHandover(code: string): Promise<ReturnHandoverDetail> {
  return apiFetch<ReturnHandoverDetail>(`/return-handovers/${encodeURIComponent(code)}/request`, {
    method: 'POST',
  });
}

/** Le livreur atteste avoir la marchandise en main — scan vérifié ou manuel. */
export async function pickupHandover(
  code: string,
  body: ReturnHandoverPickupPayload,
): Promise<ReturnHandoverDetail> {
  return apiFetch<ReturnHandoverDetail>(`/return-handovers/${encodeURIComponent(code)}/pickup`, {
    method: 'POST',
    body,
  });
}

/** Le livreur l'a déposée à l'entrepôt — en attente de réception. */
export async function handOverHandover(code: string): Promise<ReturnHandoverDetail> {
  return apiFetch<ReturnHandoverDetail>(`/return-handovers/${encodeURIComponent(code)}/handover`, {
    method: 'POST',
  });
}

/** « Confirmer la réception » — le geste qui fait bouger les colis. */
export async function confirmHandover(code: string): Promise<ReturnHandoverDetail> {
  return apiFetch<ReturnHandoverDetail>(`/return-handovers/${encodeURIComponent(code)}/confirm`, {
    method: 'POST',
  });
}

/** « Clôturer » — l'affaire est classée. */
export async function closeHandover(code: string): Promise<ReturnHandoverDetail> {
  return apiFetch<ReturnHandoverDetail>(`/return-handovers/${encodeURIComponent(code)}/close`, {
    method: 'POST',
  });
}

/** Annulation — elle relâche les colis, et seulement avant la réception. */
export async function cancelHandover(
  code: string,
  body: ReturnHandoverCancelPayload,
): Promise<ReturnHandoverDetail> {
  return apiFetch<ReturnHandoverDetail>(`/return-handovers/${encodeURIComponent(code)}/cancel`, {
    method: 'POST',
    body,
  });
}

/** Le bon de retour en PDF, encodé en base64 — même chemin que les autres bons. */
export async function downloadHandoverPdf(code: string): Promise<string> {
  return apiFetchBase64(`/return-handovers/${encodeURIComponent(code)}/pdf`);
}

export async function deleteHandover(code: string): Promise<{ code: string }> {
  return apiFetch<{ code: string }>(`/return-handovers/${encodeURIComponent(code)}`, {
    method: 'DELETE',
  });
}

/* --- Bons de retour ------------------------------------------------------- */

export async function getSlipPage(
  query: Query & { status?: string; clientId?: string; agencyId?: string },
): Promise<Paginated<ReturnSlipListItem>> {
  return apiFetch<Paginated<ReturnSlipListItem>>('/return-slips', { query: toSearch(query) });
}

export async function getSlip(code: string): Promise<ReturnSlipDetail> {
  return apiFetch<ReturnSlipDetail>(`/return-slips/${encodeURIComponent(code)}`);
}

export async function createSlip(body: ReturnSlipPayload): Promise<ReturnSlipDetail> {
  return apiFetch<ReturnSlipDetail>('/return-slips', { method: 'POST', body });
}

/** Lot E, E2 (D-202) : ce que la création en masse ferait — rien n'est écrit. */
export async function getSlipBulkPreview(): Promise<ReturnSlipBulkSummary> {
  return apiFetch<ReturnSlipBulkSummary>('/return-slips/bulk');
}

/** Lot E, E2 (D-202) : un brouillon par client et par agence ; idempotent. */
export async function createSlipsInBulk(): Promise<ReturnSlipBulkSummary> {
  return apiFetch<ReturnSlipBulkSummary>('/return-slips/bulk', { method: 'POST' });
}

/**
 * COLIS RENDABLES — retouches Anass, point 7.
 *
 * L'éligibilité est décidée par le SERVEUR — tout sauf ramassé, ramassé par le
 * livreur, en cours de livraison et livré — et la recherche aussi : filtrer
 * côté navigateur une liste bornée à trois cents lignes reviendrait à chercher
 * dans ce qu'on n'a pas.
 */
export async function getSlipCandidates(
  code: string,
  search?: string,
): Promise<ReturnParcelLine[]> {
  return apiFetch<ReturnParcelLine[]>(`/return-slips/${encodeURIComponent(code)}/candidates`, {
    query: search ? { search } : {},
  });
}

/** Ajout EN LOT, sous un même motif — la multi-sélection du point 7. */
export async function addSlipParcels(
  code: string,
  body: ReturnSlipItemsPayload,
): Promise<ReturnSlipDetail> {
  return apiFetch<ReturnSlipDetail>(`/return-slips/${encodeURIComponent(code)}/parcels/batch`, {
    method: 'POST',
    body,
  });
}

export async function addSlipParcel(
  code: string,
  body: ReturnSlipItemPayload,
): Promise<ReturnSlipDetail> {
  return apiFetch<ReturnSlipDetail>(`/return-slips/${encodeURIComponent(code)}/parcels`, {
    method: 'POST',
    body,
  });
}

export async function removeSlipParcel(code: string, parcelId: string): Promise<ReturnSlipDetail> {
  return apiFetch<ReturnSlipDetail>(
    `/return-slips/${encodeURIComponent(code)}/parcels/${parcelId}`,
    { method: 'DELETE' },
  );
}

export async function closeSlip(
  code: string,
  body: ReturnSlipClosePayload,
): Promise<ReturnSlipDetail> {
  return apiFetch<ReturnSlipDetail>(`/return-slips/${encodeURIComponent(code)}/close`, {
    method: 'POST',
    body,
  });
}

export async function deleteSlip(code: string): Promise<{ code: string }> {
  return apiFetch<{ code: string }>(`/return-slips/${encodeURIComponent(code)}`, {
    method: 'DELETE',
  });
}

/**
 * Le bon en PDF, encodé en base64.
 *
 * L'endpoint rend du binaire : il ne passe donc pas par `apiFetch`, qui parse
 * du JSON. Le base64 traverse une Server Action jusqu'au navigateur, qui en
 * fait un Blob — même chemin que l'export tarifaire et les pièces jointes.
 */
export async function downloadSlipPdf(code: string): Promise<string> {
  return apiFetchBase64(`/return-slips/${encodeURIComponent(code)}/pdf`);
}

/**
 * `GET /return-handovers/candidates?page=…` — la pile d'un livreur, par pages,
 * au vrai total (chantier « Plafond de 200 » : elle s'arrêtait à 300).
 */
export async function getHandoverCandidatePage(query: {
  courierId?: string;
  search?: string;
  page: number;
}): Promise<CandidatePage<ReturnParcelLine>> {
  return apiFetch('/return-handovers/candidates', {
    query: {
      ...toSearch({ courierId: query.courierId, search: query.search }),
      page: query.page,
      pageSize: CANDIDATE_PAGE_SIZE,
    },
  });
}

/** `GET /return-slips/:code/candidates?page=…` — par pages, au vrai total. */
export async function getSlipCandidatePage(
  code: string,
  query: { search?: string; page: number },
): Promise<CandidatePage<ReturnParcelLine>> {
  return apiFetch(`/return-slips/${encodeURIComponent(code)}/candidates`, {
    query: {
      ...(query.search ? { search: query.search } : {}),
      page: query.page,
      pageSize: CANDIDATE_PAGE_SIZE,
    },
  });
}

/** Lot F, F3 (D-207) : ce que la création en masse des RL- ferait — rien n'est écrit. */
export async function getHandoverBulkPreview(): Promise<ReturnSlipBulkSummary> {
  return apiFetch<ReturnSlipBulkSummary>('/return-handovers/bulk');
}

/** Lot F, F3 (D-207) : un bon par livreur et par agence ; idempotent. */
export async function createHandoversInBulk(): Promise<ReturnSlipBulkSummary> {
  return apiFetch<ReturnSlipBulkSummary>('/return-handovers/bulk', { method: 'POST' });
}
