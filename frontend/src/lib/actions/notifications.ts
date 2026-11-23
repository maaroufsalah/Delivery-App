'use server';

import type { NotificationSettings } from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import { setNotificationPreference } from '@/lib/api/notifications';

/**
 * Basculer un sujet de notification — L2 bloc 9.
 *
 * **Elle rend l'état COMPLET que le serveur a retenu**, et l'écran le pose :
 * un interrupteur optimiste qui ne se réconcilie jamais reste menteur le jour
 * où le serveur refuse — par exemple parce que le sujet est devenu critique.
 */
export async function setNotificationPreferenceAction(
  topic: string,
  enabled: boolean,
): Promise<ReferentialResult<NotificationSettings>> {
  try {
    const settings = await setNotificationPreference(topic, enabled);
    revalidatePath('/[locale]/(courier)/me/settings', 'page');

    return { ok: true, data: settings };
  } catch (error) {
    return toFailure(error);
  }
}
