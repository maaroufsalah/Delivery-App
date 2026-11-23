import type {
  OrderDetail,
  OrderImportPreview,
  OrderListItem,
  OrderPayload,
  OrderToParcelPayload,
  Paginated,
  ProductAttributeItem,
  ProductAttributePayload,
  ProductAttributeValuePayload,
  ProductDetail,
  ProductImagePayload,
  ProductImportPreview,
  ProductListItem,
  ProductPayload,
  StockAdjustPayload,
  StockDetail,
  StockListItem,
  StorageDecisionPayload,
  StorageRequestDetail,
  StorageRequestListItem,
  StorageRequestPayload,
} from '@delivery/shared';
import { apiFetch, apiFetchBase64 } from '@/lib/http';

/**
 * Entrepôt — produits, stock, entreposages, commandes.
 *
 * Une fonction = un endpoint. Le `clientId` n'est envoyé que par un compte
 * staff : pour un e-commerçant, la portée le fixe côté serveur, et l'envoyer
 * n'y changerait rien.
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

/* --- Produits ------------------------------------------------------------- */

export async function getProductPage(query: Query): Promise<Paginated<ProductListItem>> {
  return apiFetch<Paginated<ProductListItem>>('/products', { query: toSearch(query) });
}

export async function getProduct(id: string): Promise<ProductDetail> {
  return apiFetch<ProductDetail>(`/products/${id}`);
}

export async function createProduct(body: ProductPayload): Promise<ProductDetail> {
  return apiFetch<ProductDetail>('/products', { method: 'POST', body });
}

export async function updateProduct(id: string, body: ProductPayload): Promise<ProductDetail> {
  return apiFetch<ProductDetail>(`/products/${id}`, { method: 'PATCH', body });
}

export async function deleteProduct(id: string): Promise<{ id: string }> {
  return apiFetch<{ id: string }>(`/products/${id}`, { method: 'DELETE' });
}

export async function setProductImage(
  id: string,
  body: ProductImagePayload,
): Promise<ProductDetail> {
  return apiFetch<ProductDetail>(`/products/${id}/image`, { method: 'POST', body });
}

export async function getProductAttributes(): Promise<ProductAttributeItem[]> {
  return apiFetch<ProductAttributeItem[]>('/products/attributes');
}

export async function createProductAttribute(
  body: ProductAttributePayload,
): Promise<ProductAttributeItem[]> {
  return apiFetch<ProductAttributeItem[]>('/products/attributes', { method: 'POST', body });
}

export async function createProductAttributeValue(
  body: ProductAttributeValuePayload,
): Promise<ProductAttributeItem[]> {
  return apiFetch<ProductAttributeItem[]>('/products/attributes/values', {
    method: 'POST',
    body,
  });
}

export async function importProducts(body: {
  content: string;
  clientId?: string;
  apply: boolean;
}): Promise<ProductImportPreview> {
  return apiFetch<ProductImportPreview>('/products/import', { method: 'POST', body });
}

/* --- Stock ---------------------------------------------------------------- */

export async function getStockPage(query: Query): Promise<Paginated<StockListItem>> {
  return apiFetch<Paginated<StockListItem>>('/stock', { query: toSearch(query) });
}

export async function getStockItem(id: string): Promise<StockDetail> {
  return apiFetch<StockDetail>(`/stock/${id}`);
}

export async function adjustStock(body: StockAdjustPayload): Promise<StockDetail> {
  return apiFetch<StockDetail>('/stock/adjust', { method: 'POST', body });
}

/* --- Entreposages --------------------------------------------------------- */

export async function getStorageRequestPage(
  query: Query,
): Promise<Paginated<StorageRequestListItem>> {
  return apiFetch<Paginated<StorageRequestListItem>>('/storage-requests', {
    query: toSearch(query),
  });
}

export async function getStorageRequest(code: string): Promise<StorageRequestDetail> {
  return apiFetch<StorageRequestDetail>(`/storage-requests/${encodeURIComponent(code)}`);
}

export async function createStorageRequest(
  body: StorageRequestPayload,
): Promise<StorageRequestDetail> {
  return apiFetch<StorageRequestDetail>('/storage-requests', { method: 'POST', body });
}

export async function decideStorageRequest(
  code: string,
  body: StorageDecisionPayload,
): Promise<StorageRequestDetail> {
  return apiFetch<StorageRequestDetail>(`/storage-requests/${encodeURIComponent(code)}/decision`, {
    method: 'POST',
    body,
  });
}

export async function deleteStorageRequest(code: string): Promise<{ code: string }> {
  return apiFetch<{ code: string }>(`/storage-requests/${encodeURIComponent(code)}`, {
    method: 'DELETE',
  });
}

/** Le bon de réception, en base64 — même chemin que les autres PDF. */
export async function downloadStorageReceipt(code: string): Promise<string> {
  return apiFetchBase64(`/storage-requests/${encodeURIComponent(code)}/receipt`);
}

/* --- Commandes ------------------------------------------------------------ */

export async function getOrderPage(query: Query): Promise<Paginated<OrderListItem>> {
  return apiFetch<Paginated<OrderListItem>>('/orders', { query: toSearch(query) });
}

export async function getOrder(code: string): Promise<OrderDetail> {
  return apiFetch<OrderDetail>(`/orders/${encodeURIComponent(code)}`);
}

export async function createOrder(body: OrderPayload): Promise<OrderDetail> {
  return apiFetch<OrderDetail>('/orders', { method: 'POST', body });
}

export async function confirmOrder(code: string): Promise<OrderDetail> {
  return apiFetch<OrderDetail>(`/orders/${encodeURIComponent(code)}/confirm`, { method: 'POST' });
}

export async function cancelOrder(code: string): Promise<OrderDetail> {
  return apiFetch<OrderDetail>(`/orders/${encodeURIComponent(code)}/cancel`, { method: 'POST' });
}

export async function createOrderParcel(
  code: string,
  body: OrderToParcelPayload,
): Promise<OrderDetail> {
  return apiFetch<OrderDetail>(`/orders/${encodeURIComponent(code)}/parcel`, {
    method: 'POST',
    body,
  });
}

export async function deleteOrder(code: string): Promise<{ code: string }> {
  return apiFetch<{ code: string }>(`/orders/${encodeURIComponent(code)}`, { method: 'DELETE' });
}

export async function importOrders(body: {
  content: string;
  clientId?: string;
  apply: boolean;
}): Promise<OrderImportPreview> {
  return apiFetch<OrderImportPreview>('/orders/import', { method: 'POST', body });
}
