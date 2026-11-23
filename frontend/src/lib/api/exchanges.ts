import type {
  ExchangeCreatePayload,
  ExchangeDetail,
  ExchangeListItem,
  Paginated,
  ParcelExchangePayload,
} from '@delivery/shared';
import { apiFetch } from '@/lib/http';

/**
 * Les ÉCHANGES — chantier « Finance », point 6.
 *
 * Une fonction par endpoint, comme partout (CLAUDE.md §1). Aucun identifiant de
 * portée ne circule : le serveur borne la lecture au périmètre de l'appelant.
 */

export interface ExchangeListQuery {
  page?: number;
  pageSize?: number;
  status?: string;
  search?: string;
}

export async function getExchanges(
  query: ExchangeListQuery = {},
): Promise<Paginated<ExchangeListItem>> {
  return apiFetch<Paginated<ExchangeListItem>>('/exchanges', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      status: query.status,
      search: query.search,
    },
  });
}

export async function getExchange(code: string): Promise<ExchangeDetail> {
  return apiFetch<ExchangeDetail>(`/exchanges/${encodeURIComponent(code)}`);
}

export async function createExchange(body: ExchangeCreatePayload): Promise<ExchangeDetail> {
  return apiFetch<ExchangeDetail>('/exchanges', { method: 'POST', body });
}

/**
 * ÉCHANGER DEPUIS LE COLIS LIVRÉ — chantier « Étiquette orange », point 2.
 *
 * Sur le COLIS et non sur `/exchanges` : c'est là que le geste se fait, et le
 * serveur en tire deux colis — `RET-` et `ECH-` — plutôt qu'un seul. L'autre
 * route reste pour le dossier saisi au bureau.
 */
export async function requestParcelExchange(
  code: string,
  body: ParcelExchangePayload,
): Promise<ExchangeDetail> {
  return apiFetch<ExchangeDetail>(`/parcels/${encodeURIComponent(code)}/exchange`, {
    method: 'POST',
    body,
  });
}

/** Les quatre transitions partagent leur forme : un code, un commentaire. */
export async function advanceExchange(
  code: string,
  step: 'pickup' | 'deliver' | 'close' | 'cancel',
  body: { comment?: string },
): Promise<ExchangeDetail> {
  return apiFetch<ExchangeDetail>(`/exchanges/${encodeURIComponent(code)}/${step}`, {
    method: 'POST',
    body,
  });
}
