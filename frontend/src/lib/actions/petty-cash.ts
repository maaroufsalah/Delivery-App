'use server';

import {
  type PettyCashDayDetail,
  type PettyCashMeta,
  type PettyCashMovement,
  pettyCashCategoriesSchema,
  pettyCashCourierExpenseSchema,
  pettyCashDayReopenSchema,
  pettyCashMovementSchema,
  pettyCashRefuseSchema,
} from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import {
  closePettyCashDay,
  createCourierPettyCashExpense,
  createPettyCashMovement,
  downloadPettyCashDayExcel,
  downloadPettyCashDayPdf,
  reopenPettyCashDay,
  deleteCourierPettyCashExpense,
  deletePettyCashMovement,
  downloadPettyCashPdf,
  downloadPettyCashProof,
  refusePettyCashMovement,
  setPettyCashCategories,
  updateCourierPettyCashExpense,
  updatePettyCashMovement,
  validatePettyCashMovement,
} from '@/lib/api/petty-cash';

/*
 * LA PETITE CAISSE — chantier « Caisse, OV, Petite caisse », point 3.
 *
 * Seul `/petty-cash` est revalidé : la caisse ne nourrit aucun autre écran,
 * pas même le tableau de bord financier.
 */

function revalidatePettyCash(): void {
  revalidatePath('/[locale]/(app)/petty-cash', 'page');
}

export async function savePettyCashMovementAction(
  id: string | null,
  input: unknown,
): Promise<ReferentialResult<PettyCashMovement>> {
  const parsed = pettyCashMovementSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const data = id
      ? await updatePettyCashMovement(id, parsed.data)
      : await createPettyCashMovement(parsed.data);
    revalidatePettyCash();
    return { ok: true, data };
  } catch (error) {
    return toFailure(error, 'petty-cash');
  }
}

export async function deletePettyCashMovementAction(id: string): Promise<ReferentialResult> {
  try {
    await deletePettyCashMovement(id);
    revalidatePettyCash();
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error, 'petty-cash');
  }
}

export async function savePettyCashCategoriesAction(
  input: unknown,
): Promise<ReferentialResult<PettyCashMeta>> {
  const parsed = pettyCashCategoriesSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const data = await setPettyCashCategories(parsed.data.categories);
    revalidatePettyCash();
    return { ok: true, data };
  } catch (error) {
    return toFailure(error, 'petty-cash');
  }
}

/** Le PDF des mouvements filtrés — base64, le fichier est fait par le navigateur. */
export async function downloadPettyCashPdfAction(
  query: Record<string, string>,
): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await downloadPettyCashPdf(query) };
  } catch (error) {
    return toFailure(error, 'petty-cash');
  }
}

export async function downloadPettyCashProofAction(id: string): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await downloadPettyCashProof(id) };
  } catch (error) {
    return toFailure(error, 'petty-cash');
  }
}

/* --- D-249 : clôturer, corriger, reclôturer une journée -------------------- */

const DAY = /^\d{4}-\d{2}-\d{2}$/;

export async function closePettyCashDayAction(
  day: string,
  agencyId?: string,
): Promise<ReferentialResult<PettyCashDayDetail>> {
  if (!DAY.test(day)) return { ok: false, error: 'invalid' };

  try {
    const data = await closePettyCashDay(day, agencyId);
    revalidatePettyCash();
    return { ok: true, data };
  } catch (error) {
    return toFailure(error, 'petty-cash');
  }
}

export async function reopenPettyCashDayAction(
  day: string,
  input: unknown,
): Promise<ReferentialResult<PettyCashDayDetail>> {
  const parsed = pettyCashDayReopenSchema.safeParse(input);
  if (!DAY.test(day) || !parsed.success) return { ok: false, error: 'invalid' };

  try {
    const data = await reopenPettyCashDay(day, parsed.data.reason, parsed.data.agencyId);
    revalidatePettyCash();
    return { ok: true, data };
  } catch (error) {
    return toFailure(error, 'petty-cash');
  }
}

/** Excel ou PDF d'une journée — base64, le fichier est fait par le navigateur. */
export async function downloadPettyCashDayAction(
  day: string,
  format: 'xlsx' | 'pdf',
  agencyId?: string,
): Promise<ReferentialResult<string>> {
  if (!DAY.test(day)) return { ok: false, error: 'invalid' };

  try {
    const data =
      format === 'pdf'
        ? await downloadPettyCashDayPdf(day, agencyId)
        : await downloadPettyCashDayExcel(day, agencyId);
    return { ok: true, data };
  } catch (error) {
    return toFailure(error, 'petty-cash');
  }
}

/* --- D-168 : valider ou refuser le brouillon d'un livreur ------------------ */

export async function validatePettyCashMovementAction(
  id: string,
): Promise<ReferentialResult<PettyCashMovement>> {
  try {
    const data = await validatePettyCashMovement(id);
    revalidatePettyCash();
    return { ok: true, data };
  } catch (error) {
    return toFailure(error, 'petty-cash');
  }
}

export async function refusePettyCashMovementAction(
  id: string,
  input: unknown,
): Promise<ReferentialResult<PettyCashMovement>> {
  const parsed = pettyCashRefuseSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const data = await refusePettyCashMovement(id, parsed.data.reason ?? '');
    revalidatePettyCash();
    return { ok: true, data };
  } catch (error) {
    return toFailure(error, 'petty-cash');
  }
}

/* --- D-168 : la petite caisse du LIVREUR (`/me/petty-cash`) --------------- */

function revalidateCourierPettyCash(): void {
  revalidatePath('/[locale]/(courier)/me/petty-cash', 'page');
  revalidatePath('/[locale]/(courier)/me', 'page');
}

export async function saveCourierPettyCashExpenseAction(
  id: string | null,
  input: unknown,
): Promise<ReferentialResult<PettyCashMovement>> {
  const parsed = pettyCashCourierExpenseSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const data = id
      ? await updateCourierPettyCashExpense(id, parsed.data)
      : await createCourierPettyCashExpense(parsed.data);
    revalidateCourierPettyCash();
    return { ok: true, data };
  } catch (error) {
    return toFailure(error, 'petty-cash');
  }
}

export async function deleteCourierPettyCashExpenseAction(id: string): Promise<ReferentialResult> {
  try {
    await deleteCourierPettyCashExpense(id);
    revalidateCourierPettyCash();
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error, 'petty-cash');
  }
}
