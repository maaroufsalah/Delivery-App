import type {
  BlCollectPayload,
  BlNoteDetail,
  BlNoteItemsPayload,
  BlNoteListItem,
  BlNotePayload,
  BlNoteReturnPayload,
  DeliverySlipAdvancePayload,
  DeliverySlipAdvanceResult,
  DeliverySlipListItem,
  DeliverySlipRevertPayload,
  Paginated,
  ParcelBlTracking,
} from '@delivery/shared';
import { apiFetch, apiFetchBase64 } from '@/lib/http';

/**
 * LE BL — chantier « Retours, BL Clients Pro », point 5.
 *
 * Une fonction, un endpoint, comme partout dans `src/lib/api`. Rien ici ne
 * vérifie qu'un client est Pro : c'est l'API qui le refuse, et c'est le seul
 * endroit où cette garantie tienne (D-099). L'écran, lui, ne pose pas la
 * question — ce qui est une commodité, pas une sécurité.
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

/** Le BL d'un colis et l'historique de ses étapes — le suivi du vendeur Pro. */
export async function getParcelBl(code: string): Promise<ParcelBlTracking> {
  return apiFetch<ParcelBlTracking>(`/bl/parcels/${encodeURIComponent(code)}`);
}

/** « BL récupéré ? » — `collected: false` n'écrit rien, et c'est voulu. */
export async function collectParcelBl(
  code: string,
  body: BlCollectPayload,
): Promise<ParcelBlTracking> {
  return apiFetch<ParcelBlTracking>(`/bl/parcels/${encodeURIComponent(code)}/collect`, {
    method: 'POST',
    body,
  });
}

/** Les BL en sacoche, groupables dans un bon. */
export async function getCollectableSlips(query: {
  clientId?: string;
  courierId?: string;
}): Promise<DeliverySlipListItem[]> {
  return apiFetch<DeliverySlipListItem[]>('/bl/slips', { query: toSearch(query) });
}

export async function getBlNotePage(
  query: Query & { status?: string; clientId?: string; courierId?: string; agencyId?: string },
): Promise<Paginated<BlNoteListItem>> {
  return apiFetch<Paginated<BlNoteListItem>>('/bl/notes', { query: toSearch(query) });
}

export async function getBlNote(code: string): Promise<BlNoteDetail> {
  return apiFetch<BlNoteDetail>(`/bl/notes/${encodeURIComponent(code)}`);
}

export async function createBlNote(body: BlNotePayload): Promise<BlNoteDetail> {
  return apiFetch<BlNoteDetail>('/bl/notes', { method: 'POST', body });
}

export async function addBlNoteSlips(
  code: string,
  body: BlNoteItemsPayload,
): Promise<BlNoteDetail> {
  return apiFetch<BlNoteDetail>(`/bl/notes/${encodeURIComponent(code)}/slips`, {
    method: 'POST',
    body,
  });
}

export async function removeBlNoteSlip(code: string, slipId: string): Promise<BlNoteDetail> {
  return apiFetch<BlNoteDetail>(
    `/bl/notes/${encodeURIComponent(code)}/slips/${encodeURIComponent(slipId)}`,
    { method: 'DELETE' },
  );
}

/** Le livreur remet la liasse à l'entrepôt. */
export async function handOverBlNote(code: string): Promise<BlNoteDetail> {
  return apiFetch<BlNoteDetail>(`/bl/notes/${encodeURIComponent(code)}/handover`, {
    method: 'POST',
  });
}

/**
 * LES DEUX ACTIONS DE L'ADMIN, et deux fonctions — point 5.
 *
 * Recevoir une liasse et la rendre sont deux gestes, faits deux jours
 * différents, souvent par deux personnes. Une fonction unique à drapeau aurait
 * fait d'un clic la moitié de l'autre.
 */
export async function receiveBlNote(code: string): Promise<BlNoteDetail> {
  return apiFetch<BlNoteDetail>(`/bl/notes/${encodeURIComponent(code)}/receive`, {
    method: 'POST',
  });
}

export async function returnBlNote(code: string, body: BlNoteReturnPayload): Promise<BlNoteDetail> {
  return apiFetch<BlNoteDetail>(`/bl/notes/${encodeURIComponent(code)}/return`, {
    method: 'POST',
    body,
  });
}

export async function closeBlNote(code: string): Promise<BlNoteDetail> {
  return apiFetch<BlNoteDetail>(`/bl/notes/${encodeURIComponent(code)}/close`, { method: 'POST' });
}

export async function deleteBlNote(code: string): Promise<{ code: string }> {
  return apiFetch<{ code: string }>(`/bl/notes/${encodeURIComponent(code)}`, { method: 'DELETE' });
}

/** Le bon BL en PDF, encodé en base64 — même chemin que les autres documents. */
export async function downloadBlNotePdf(code: string): Promise<string> {
  return apiFetchBase64(`/bl/notes/${encodeURIComponent(code)}/pdf`);
}

/* --- Lot E, E1 (D-201) : la liste des BL et ses gestes ------------------ */

/** « Bons de livraison » — la liste des BL ; le vendeur n'y reçoit que les siens. */
export async function getDeliverySlipPage(
  query: Query & {
    status?: string;
    clientId?: string;
    courierId?: string;
    from?: string;
    to?: string;
  },
): Promise<Paginated<DeliverySlipListItem>> {
  return apiFetch<Paginated<DeliverySlipListItem>>('/bl/delivery-slips', {
    query: toSearch(query),
  });
}

/** « Réceptionner (X) » / « Remettre au vendeur (X) » — cochés ou toute la liste. */
export async function advanceDeliverySlips(
  body: DeliverySlipAdvancePayload,
): Promise<DeliverySlipAdvanceResult> {
  return apiFetch<DeliverySlipAdvanceResult>('/bl/delivery-slips/advance', {
    method: 'POST',
    body,
  });
}

/** Le retour arrière d'une étape — administrateur, motif obligatoire. */
export async function revertDeliverySlip(
  id: string,
  body: DeliverySlipRevertPayload,
): Promise<DeliverySlipListItem> {
  return apiFetch<DeliverySlipListItem>(
    `/bl/delivery-slips/${encodeURIComponent(id)}/revert`,
    { method: 'POST', body },
  );
}
