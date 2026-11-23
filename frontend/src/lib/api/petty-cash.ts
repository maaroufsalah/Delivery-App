import type {
  PettyCashCourierExpensePayload,
  PettyCashCourierPage,
  PettyCashDayDetail,
  PettyCashDaysPage,
  PettyCashDaysQueryInput,
  PettyCashListQueryInput,
  PettyCashMeta,
  PettyCashMovement,
  PettyCashMovementPayload,
  PettyCashPage,
} from '@delivery/shared';
import { apiFetch, apiFetchBase64 } from '@/lib/http';

/*
 * LA PETITE CAISSE — chantier « Caisse, OV, Petite caisse », point 3.
 * Une fonction par endpoint de `/petty-cash`.
 */

function toSearch(query: object): Record<string, string | number> {
  const search: Record<string, string | number> = {};

  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue;
    search[key] = value as string | number;
  }

  return search;
}

export async function getPettyCashPage(query: PettyCashListQueryInput): Promise<PettyCashPage> {
  return apiFetch<PettyCashPage>('/petty-cash', { query: toSearch(query) });
}

export async function getPettyCashMeta(): Promise<PettyCashMeta> {
  return apiFetch<PettyCashMeta>('/petty-cash/meta');
}

export async function createPettyCashMovement(
  body: PettyCashMovementPayload,
): Promise<PettyCashMovement> {
  return apiFetch<PettyCashMovement>('/petty-cash', { method: 'POST', body });
}

export async function updatePettyCashMovement(
  id: string,
  body: PettyCashMovementPayload,
): Promise<PettyCashMovement> {
  return apiFetch<PettyCashMovement>(`/petty-cash/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body,
  });
}

export async function deletePettyCashMovement(id: string): Promise<void> {
  return apiFetch<void>(`/petty-cash/${encodeURIComponent(id)}`, { method: 'DELETE' });
}

export async function setPettyCashCategories(categories: string[]): Promise<PettyCashMeta> {
  return apiFetch<PettyCashMeta>('/petty-cash/categories', {
    method: 'PUT',
    body: { categories },
  });
}

export async function downloadPettyCashPdf(query: Record<string, string>): Promise<string> {
  return apiFetchBase64('/petty-cash/pdf', { query });
}

export async function downloadPettyCashProof(id: string): Promise<string> {
  return apiFetchBase64(`/petty-cash/${encodeURIComponent(id)}/proof`);
}

/* --- D-249 : les journées de caisse ---------------------------------------- */

export async function getPettyCashDays(query: PettyCashDaysQueryInput): Promise<PettyCashDaysPage> {
  return apiFetch<PettyCashDaysPage>('/petty-cash/days', { query: toSearch(query) });
}

export async function getPettyCashDay(day: string, agencyId?: string): Promise<PettyCashDayDetail> {
  return apiFetch<PettyCashDayDetail>(`/petty-cash/days/${encodeURIComponent(day)}`, {
    query: toSearch({ agencyId }),
  });
}

export async function closePettyCashDay(
  day: string,
  agencyId?: string,
): Promise<PettyCashDayDetail> {
  return apiFetch<PettyCashDayDetail>(`/petty-cash/days/${encodeURIComponent(day)}/close`, {
    method: 'POST',
    body: agencyId ? { agencyId } : {},
  });
}

export async function reopenPettyCashDay(
  day: string,
  reason: string,
  agencyId?: string,
): Promise<PettyCashDayDetail> {
  return apiFetch<PettyCashDayDetail>(`/petty-cash/days/${encodeURIComponent(day)}/reopen`, {
    method: 'POST',
    body: agencyId ? { agencyId, reason } : { reason },
  });
}

export async function downloadPettyCashDayExcel(day: string, agencyId?: string): Promise<string> {
  return apiFetchBase64(`/petty-cash/days/${encodeURIComponent(day)}/export`, {
    query: toSearch({ agencyId }),
  });
}

export async function downloadPettyCashDayPdf(day: string, agencyId?: string): Promise<string> {
  return apiFetchBase64(`/petty-cash/days/${encodeURIComponent(day)}/pdf`, {
    query: toSearch({ agencyId }),
  });
}

/* --- D-168 : la décision de l'administration sur le brouillon d'un livreur --- */

export async function validatePettyCashMovement(id: string): Promise<PettyCashMovement> {
  return apiFetch<PettyCashMovement>(`/petty-cash/${encodeURIComponent(id)}/validate`, {
    method: 'PATCH',
  });
}

export async function refusePettyCashMovement(
  id: string,
  reason: string,
): Promise<PettyCashMovement> {
  return apiFetch<PettyCashMovement>(`/petty-cash/${encodeURIComponent(id)}/refuse`, {
    method: 'PATCH',
    body: { reason },
  });
}

/* --- D-168 : la petite caisse du LIVREUR, `/courier/me/petty-cash` --------- */

export async function getCourierPettyCash(): Promise<PettyCashCourierPage> {
  return apiFetch<PettyCashCourierPage>('/courier/me/petty-cash');
}

export async function createCourierPettyCashExpense(
  body: PettyCashCourierExpensePayload,
): Promise<PettyCashMovement> {
  return apiFetch<PettyCashMovement>('/courier/me/petty-cash', { method: 'POST', body });
}

export async function updateCourierPettyCashExpense(
  id: string,
  body: PettyCashCourierExpensePayload,
): Promise<PettyCashMovement> {
  return apiFetch<PettyCashMovement>(`/courier/me/petty-cash/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body,
  });
}

export async function deleteCourierPettyCashExpense(id: string): Promise<void> {
  return apiFetch<void>(`/courier/me/petty-cash/${encodeURIComponent(id)}`, {
    method: 'DELETE',
  });
}
