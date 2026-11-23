import type {
  AssignClaimPayload,
  ClaimCounters,
  ClaimDetail,
  ClaimFilters,
  ClaimListItem,
  ClaimType,
  ClaimTypePayload,
  CreateClaimPayload,
  Paginated,
  ReplyClaimPayload,
} from '@delivery/shared';
import { apiFetch } from '@/lib/http';

/**
 * Réclamations — `GET /claims`, `GET /claims/:code`, `POST /claims`,
 * `POST /claims/:code/events`, `PATCH /claims/:code/assignee`.
 *
 * Le filtrage par e-commerçant n'est PAS un paramètre de confiance : la portée
 * serveur le pose déjà depuis la session. `clientId` n'est ici qu'un filtre de
 * lecture pour le staff — un e-commerçant qui l'enverrait serait ignoré, pas
 * obéi.
 */

export interface ClaimListQuery extends ClaimFilters {
  page?: number;
  pageSize?: number;
}

export async function getClaims(query: ClaimListQuery = {}): Promise<Paginated<ClaimListItem>> {
  return apiFetch<Paginated<ClaimListItem>>('/claims', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      search: query.search,
      status: query.status,
      typeId: query.typeId,
      clientId: query.clientId,
      assigneeId: query.assigneeId,
      late: query.late ? 'true' : undefined,
    },
  });
}

export async function getClaim(code: string): Promise<ClaimDetail> {
  return apiFetch<ClaimDetail>(`/claims/${encodeURIComponent(code)}`);
}

/** Compteur de tête de menu : ce qui attend d'être traité. */
export async function getClaimCounters(): Promise<ClaimCounters> {
  return apiFetch<ClaimCounters>('/claims/counters');
}

export async function getClaimTypes(): Promise<ClaimType[]> {
  return apiFetch<ClaimType[]>('/claims/types');
}

export async function createClaim(input: CreateClaimPayload): Promise<ClaimDetail> {
  return apiFetch<ClaimDetail>('/claims', { method: 'POST', body: input });
}

export async function replyToClaim(code: string, input: ReplyClaimPayload): Promise<ClaimDetail> {
  return apiFetch<ClaimDetail>(`/claims/${encodeURIComponent(code)}/events`, {
    method: 'POST',
    body: input,
  });
}

export async function assignClaim(code: string, input: AssignClaimPayload): Promise<ClaimDetail> {
  return apiFetch<ClaimDetail>(`/claims/${encodeURIComponent(code)}/assignee`, {
    method: 'PATCH',
    body: input,
  });
}

/* --- Référentiel des types, éditable en Paramètres (D-019) ---------------- */

export async function createClaimType(input: ClaimTypePayload): Promise<ClaimType> {
  return apiFetch<ClaimType>('/claims/types', { method: 'POST', body: input });
}

export async function updateClaimType(id: string, input: ClaimTypePayload): Promise<ClaimType> {
  return apiFetch<ClaimType>(`/claims/types/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: input,
  });
}

export async function deleteClaimType(id: string): Promise<{ id: string }> {
  return apiFetch<{ id: string }>(`/claims/types/${encodeURIComponent(id)}`, { method: 'DELETE' });
}
