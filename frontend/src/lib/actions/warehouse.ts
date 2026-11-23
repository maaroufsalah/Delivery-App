'use server';

import {
  type OrderDetail,
  type OrderImportPreview,
  type OrderPayload,
  orderSchema,
  type OrderToParcelPayload,
  orderToParcelSchema,
  type ProductAttributeItem,
  productAttributeSchema,
  productAttributeValueSchema,
  type ProductDetail,
  type ProductImagePayload,
  productImageSchema,
  type ProductImportPreview,
  type ProductPayload,
  productSchema,
  type StockAdjustPayload,
  stockAdjustSchema,
  type StockDetail,
  storageDecisionSchema,
  type StorageDecisionPayload,
  type StorageRequestDetail,
  type StorageRequestPayload,
  storageRequestSchema,
} from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import {
  adjustStock,
  cancelOrder,
  confirmOrder,
  createOrder,
  createOrderParcel,
  createProduct,
  createProductAttribute,
  createProductAttributeValue,
  createStorageRequest,
  decideStorageRequest,
  deleteOrder,
  deleteProduct,
  deleteStorageRequest,
  downloadStorageReceipt,
  importOrders,
  importProducts,
  setProductImage,
  updateProduct,
} from '@/lib/api/warehouse';

/**
 * Écritures du menu WAREHOUSE.
 *
 * Même forme que partout : la Server Action revalide la saisie, appelle
 * l'endpoint, et laisse remonter le motif d'un 409 — `insufficient-stock` et
 * `order-has-parcel` sont ceux que l'écran doit savoir dire.
 */

function revalidateWarehouse(...routes: string[]): void {
  for (const route of routes) {
    revalidatePath(`/[locale]/(app)/${route}`, 'page');
  }
}

/* --- Produits ------------------------------------------------------------- */

export async function saveProductAction(
  id: string | null,
  input: unknown,
): Promise<ReferentialResult<ProductDetail>> {
  const parsed = productSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const body = parsed.data as ProductPayload;
    const product = id ? await updateProduct(id, body) : await createProduct(body);
    revalidateWarehouse('products', 'products/[id]');
    return { ok: true, data: product };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteProductAction(id: string): Promise<ReferentialResult> {
  try {
    await deleteProduct(id);
    revalidateWarehouse('products');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function setProductImageAction(
  id: string,
  input: unknown,
): Promise<ReferentialResult<ProductDetail>> {
  const parsed = productImageSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const product = await setProductImage(id, parsed.data as ProductImagePayload);
    revalidateWarehouse('products', 'products/[id]');
    return { ok: true, data: product };
  } catch (error) {
    return toFailure(error);
  }
}

export async function createAttributeAction(
  input: unknown,
): Promise<ReferentialResult<ProductAttributeItem[]>> {
  const parsed = productAttributeSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    return { ok: true, data: await createProductAttribute(parsed.data) };
  } catch (error) {
    return toFailure(error);
  }
}

export async function createAttributeValueAction(
  input: unknown,
): Promise<ReferentialResult<ProductAttributeItem[]>> {
  const parsed = productAttributeValueSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    return { ok: true, data: await createProductAttributeValue(parsed.data) };
  } catch (error) {
    return toFailure(error);
  }
}

export async function importProductsAction(
  content: string,
  apply: boolean,
  clientId?: string,
): Promise<ReferentialResult<ProductImportPreview>> {
  try {
    const preview = await importProducts({ content, apply, clientId });
    if (apply) revalidateWarehouse('products');
    return { ok: true, data: preview };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Stock ---------------------------------------------------------------- */

export async function adjustStockAction(input: unknown): Promise<ReferentialResult<StockDetail>> {
  const parsed = stockAdjustSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const stock = await adjustStock(parsed.data as StockAdjustPayload);
    revalidateWarehouse('stock', 'stock/[id]', 'products');
    return { ok: true, data: stock };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Entreposages --------------------------------------------------------- */

export async function createStorageRequestAction(
  input: unknown,
): Promise<ReferentialResult<StorageRequestDetail>> {
  const parsed = storageRequestSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const request = await createStorageRequest(parsed.data as StorageRequestPayload);
    revalidateWarehouse('storage', 'storage-requests');
    return { ok: true, data: request };
  } catch (error) {
    return toFailure(error);
  }
}

export async function decideStorageRequestAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<StorageRequestDetail>> {
  const parsed = storageDecisionSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const request = await decideStorageRequest(code, parsed.data as StorageDecisionPayload);
    // Une validation fait entrer du stock : les deux écrans changent.
    revalidateWarehouse('storage', 'storage/[code]', 'storage-requests', 'stock');
    return { ok: true, data: request };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteStorageRequestAction(code: string): Promise<ReferentialResult> {
  try {
    await deleteStorageRequest(code);
    revalidateWarehouse('storage', 'storage-requests');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/** Le bon de réception revient en base64 ; le fichier naît dans le navigateur. */
export async function downloadStorageReceiptAction(
  code: string,
): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await downloadStorageReceipt(code) };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Commandes ------------------------------------------------------------ */

export async function createOrderAction(input: unknown): Promise<ReferentialResult<OrderDetail>> {
  const parsed = orderSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const order = await createOrder(parsed.data as OrderPayload);
    revalidateWarehouse('orders');
    return { ok: true, data: order };
  } catch (error) {
    return toFailure(error);
  }
}

export async function confirmOrderAction(code: string): Promise<ReferentialResult<OrderDetail>> {
  try {
    const order = await confirmOrder(code);
    revalidateWarehouse('orders', 'orders/[code]');
    return { ok: true, data: order };
  } catch (error) {
    return toFailure(error);
  }
}

export async function cancelOrderAction(code: string): Promise<ReferentialResult<OrderDetail>> {
  try {
    const order = await cancelOrder(code);
    revalidateWarehouse('orders', 'orders/[code]');
    return { ok: true, data: order };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * « Créer le colis ». Le stock sort, un colis naît : quatre écrans bougent.
 */
export async function createOrderParcelAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<OrderDetail>> {
  const parsed = orderToParcelSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const order = await createOrderParcel(code, parsed.data as OrderToParcelPayload);
    revalidateWarehouse('orders', 'orders/[code]', 'stock', 'parcels');
    return { ok: true, data: order };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteOrderAction(code: string): Promise<ReferentialResult> {
  try {
    await deleteOrder(code);
    revalidateWarehouse('orders');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function importOrdersAction(
  content: string,
  apply: boolean,
  clientId?: string,
): Promise<ReferentialResult<OrderImportPreview>> {
  try {
    const preview = await importOrders({ content, apply, clientId });
    if (apply) revalidateWarehouse('orders');
    return { ok: true, data: preview };
  } catch (error) {
    return toFailure(error);
  }
}
