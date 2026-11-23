import type {
  CourierStockDetail,
  CourierStockPage,
  CourierStockPdfJobInput,
  LabelJob,
} from '@delivery/shared';
import { apiFetch, apiFetchBase64 } from '@/lib/http';

/**
 * Le stock des livreurs — lot M (D-222). Une fonction = un endpoint.
 *
 * Les filtres voyagent tels que l'URL les porte : la liste, l'export et les
 * PDF reçoivent la même requête, donc disent la même chose (D4).
 */

type Query = Record<string, string | number | undefined>;

function toSearch(query: Query): Record<string, string | number> {
  const search: Record<string, string | number> = {};
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === '') continue;
    search[key] = value;
  }
  return search;
}

export async function getCourierStockPage(query: Query): Promise<CourierStockPage> {
  return apiFetch<CourierStockPage>('/courier-stock', { query: toSearch(query) });
}

export async function getCourierStockDetail(
  courierId: string,
  query: Query,
): Promise<CourierStockDetail> {
  return apiFetch<CourierStockDetail>(`/courier-stock/${encodeURIComponent(courierId)}`, {
    query: toSearch(query),
  });
}

/** Le PDF global, filtres de la liste compris. */
export async function downloadCourierStockPdf(query: Query): Promise<string> {
  return apiFetchBase64('/courier-stock/pdf', { query: toSearch(query) });
}

/** Le PDF d'un livreur, filtres du détail compris. */
export async function downloadCourierStockCourierPdf(
  courierId: string,
  query: Query,
): Promise<string> {
  return apiFetchBase64(`/courier-stock/${encodeURIComponent(courierId)}/pdf`, {
    query: toSearch(query),
  });
}

/** Au-delà de 1 500 colis : le PDF se prépare en arrière-plan (file D3). */
export async function createCourierStockPdfJob(input: CourierStockPdfJobInput): Promise<LabelJob> {
  return apiFetch<LabelJob>('/courier-stock/pdf-jobs', { method: 'POST', body: input });
}

export async function getCourierStockPdfJob(id: string): Promise<LabelJob> {
  return apiFetch<LabelJob>(`/courier-stock/pdf-jobs/${encodeURIComponent(id)}`);
}

export async function downloadCourierStockPdfJob(id: string): Promise<string> {
  return apiFetchBase64(`/courier-stock/pdf-jobs/${encodeURIComponent(id)}/file`);
}
