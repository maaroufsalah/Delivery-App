import type { BulkJob } from '@delivery/shared';
import { apiFetch } from '@/lib/http';

/**
 * Les tâches de masse — lot W2 (D-239). Un geste de plus de 300 colis
 * (livraison en masse, validation d'un bon, départ d'un circuit) répond 202 et
 * une tâche ; on la suit ici, et la page la relit après un rechargement.
 */

/** `GET /bulk-jobs/:id` — progression, échecs, résultat. */
export async function getBulkJob(id: string): Promise<BulkJob> {
  return apiFetch<BulkJob>(`/bulk-jobs/${encodeURIComponent(id)}`);
}

/** `GET /bulk-jobs?target=` — la dernière tâche d'un circuit ou d'un bon, ou `null`. */
export async function getLatestBulkJob(target: string): Promise<BulkJob | null> {
  return apiFetch<BulkJob | null>('/bulk-jobs', { query: { target } });
}

/** `POST /bulk-jobs/:id/retry` — relance une tâche arrêtée, là où elle en était. */
export async function retryBulkJob(id: string): Promise<BulkJob> {
  return apiFetch<BulkJob>(`/bulk-jobs/${encodeURIComponent(id)}/retry`, { method: 'POST' });
}
