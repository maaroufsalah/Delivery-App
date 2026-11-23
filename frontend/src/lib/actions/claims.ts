'use server';

import type { ClaimDetail, ClaimType } from '@delivery/shared';
import {
  assignClaimSchema,
  claimTypeSchema,
  createClaimSchema,
  replyClaimSchema,
} from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import {
  assignClaim,
  createClaim,
  createClaimType,
  deleteClaimType,
  replyToClaim,
  updateClaimType,
} from '@/lib/api/claims';
import { apiFetchBase64 } from '@/lib/http';

/**
 * Écritures du menu DEMANDES.
 *
 * Même forme que partout : la Server Action revalide la saisie, appelle
 * l'endpoint, invalide la route. Les pièces jointes voyagent en base64 dans le
 * corps JSON (D-022) — une Server Action ne poste pas de `multipart`, et
 * embarquer `multer` pour trois photos coûterait plus que ça ne rapporte.
 */

function revalidateClaims(): void {
  revalidatePath('/[locale]/(app)/claims', 'page');
  revalidatePath('/[locale]/(app)/claims/[code]', 'page');
  // Le compteur du menu vit dans le layout : sans cette ligne, le badge
  // resterait sur son ancienne valeur jusqu'au prochain rechargement complet.
  revalidatePath('/[locale]/(app)', 'layout');
}

export async function createClaimAction(input: unknown): Promise<ReferentialResult<ClaimDetail>> {
  const parsed = createClaimSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const claim = await createClaim(parsed.data);
    revalidateClaims();
    return { ok: true, data: claim };
  } catch (error) {
    return toFailure(error);
  }
}

export async function replyClaimAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<ClaimDetail>> {
  const parsed = replyClaimSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const claim = await replyToClaim(code, parsed.data);
    revalidateClaims();
    return { ok: true, data: claim };
  } catch (error) {
    return toFailure(error);
  }
}

export async function assignClaimAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<ClaimDetail>> {
  const parsed = assignClaimSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const claim = await assignClaim(code, parsed.data);
    revalidateClaims();
    return { ok: true, data: claim };
  } catch (error) {
    return toFailure(error);
  }
}

/** Pièce jointe — base64, comme les étiquettes et les PDF (D-018). */
export async function claimAttachmentAction(
  code: string,
  attachmentId: string,
): Promise<ReferentialResult<string>> {
  try {
    return {
      ok: true,
      data: await apiFetchBase64(
        `/claims/${encodeURIComponent(code)}/attachments/${encodeURIComponent(attachmentId)}`,
      ),
    };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Référentiel des types ------------------------------------------------ */

export async function saveClaimTypeAction(
  id: string | null,
  input: unknown,
): Promise<ReferentialResult<ClaimType>> {
  const parsed = claimTypeSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const type = id ? await updateClaimType(id, parsed.data) : await createClaimType(parsed.data);
    revalidatePath('/[locale]/(app)/settings/claims', 'page');
    revalidateClaims();
    return { ok: true, data: type };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteClaimTypeAction(id: string): Promise<ReferentialResult> {
  try {
    await deleteClaimType(id);
    revalidatePath('/[locale]/(app)/settings/claims', 'page');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}
