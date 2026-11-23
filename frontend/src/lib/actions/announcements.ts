'use server';

import type { AnnouncementDetail } from '@delivery/shared';
import { announcementSchema } from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import {
  createAnnouncement,
  deleteAnnouncement,
  updateAnnouncement,
} from '@/lib/api/announcements';
import { apiFetchBase64 } from '@/lib/http';

/**
 * Écritures des actualités.
 *
 * L'image voyage en base64 dans le corps JSON (D-022) : une Server Action ne
 * poste pas de `multipart`, et embarquer `multer` côté API pour un bandeau
 * coûterait plus que ça ne rapporte.
 */

function revalidateNews(): void {
  revalidatePath('/[locale]/(app)/news', 'page');
  revalidatePath('/[locale]/(app)/news/[id]', 'page');
  // Le bandeau « Nouveautés » vit sur le tableau de bord : publier sans
  // l'invalider laisserait l'annonce invisible jusqu'au prochain rechargement.
  revalidatePath('/[locale]/(app)/dashboard', 'page');
}

export async function saveAnnouncementAction(
  id: string | null,
  input: unknown,
): Promise<ReferentialResult<AnnouncementDetail>> {
  const parsed = announcementSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const announcement = id
      ? await updateAnnouncement(id, parsed.data)
      : await createAnnouncement(parsed.data);

    revalidateNews();
    return { ok: true, data: announcement };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteAnnouncementAction(id: string): Promise<ReferentialResult> {
  try {
    await deleteAnnouncement(id);
    revalidateNews();
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/** L'image passe par une Server Action, pas par un `<img src>` vers l'API (D-018). */
export async function announcementImageAction(id: string): Promise<ReferentialResult<string>> {
  try {
    return {
      ok: true,
      data: await apiFetchBase64(`/announcements/${encodeURIComponent(id)}/image`),
    };
  } catch (error) {
    return toFailure(error);
  }
}
