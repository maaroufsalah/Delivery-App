'use server';

import { revalidatePath } from 'next/cache';
import type { ReferentialResult } from '@/lib/actions/action-result';
import { toFailure } from '@/lib/actions/action-result';
import { apiFetch } from '@/lib/http';

/**
 * Marquer lu — V2 vendeur bloc 8.
 *
 * **Aucun identifiant de compte ne transite.** Le serveur filtre par
 * `updateMany` sur (société, compte, id) : marquer lue la notification d'un
 * autre est impossible, et la réponse ne révèle même pas qu'elle existe.
 */
export async function markNotificationReadAction(id: string): Promise<ReferentialResult> {
  try {
    await apiFetch(`/me/notification-feed/${encodeURIComponent(id)}/read`, { method: 'POST' });
    revalidatePath('/[locale]/notifications', 'page');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function markAllNotificationsReadAction(): Promise<ReferentialResult> {
  try {
    await apiFetch('/me/notification-feed/read-all', { method: 'POST' });
    revalidatePath('/[locale]/notifications', 'page');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}
