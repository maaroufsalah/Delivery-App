'use server';

import { courierStockPdfJobSchema, type LabelJob } from '@delivery/shared';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import {
  createCourierStockPdfJob,
  downloadCourierStockCourierPdf,
  downloadCourierStockPdf,
  downloadCourierStockPdfJob,
  getCourierStockPdfJob,
} from '@/lib/api/courier-stock';

/**
 * Les PDF « Situation du Stock Livreur » — lot M, M3 (D-222).
 *
 * En base64 par une Server Action, comme tous les documents (D-018) : le
 * cookie de session ne quitte pas le web. Le seuil synchrone est tenu par
 * l'API (422 `stock-pdf-async`) ; l'écran l'anticipe et crée le travail.
 */

export async function courierStockPdfAction(
  courierId: string | null,
  query: Record<string, string>,
): Promise<ReferentialResult<string>> {
  try {
    return {
      ok: true,
      data: courierId
        ? await downloadCourierStockCourierPdf(courierId, query)
        : await downloadCourierStockPdf(query),
    };
  } catch (error) {
    return toFailure(error, 'courier-stock');
  }
}

/** `input` : `kind`, `courierId` éventuel et les filtres de l'URL, tels quels — le schéma tranche. */
export async function createCourierStockPdfJobAction(
  input: Record<string, string>,
): Promise<ReferentialResult<LabelJob>> {
  const parsed = courierStockPdfJobSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    return { ok: true, data: await createCourierStockPdfJob(parsed.data) };
  } catch (error) {
    return toFailure(error, 'courier-stock');
  }
}

export async function courierStockPdfJobAction(id: string): Promise<ReferentialResult<LabelJob>> {
  try {
    return { ok: true, data: await getCourierStockPdfJob(id) };
  } catch (error) {
    return toFailure(error, 'courier-stock');
  }
}

export async function courierStockPdfJobFileAction(id: string): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await downloadCourierStockPdfJob(id) };
  } catch (error) {
    return toFailure(error, 'courier-stock');
  }
}
