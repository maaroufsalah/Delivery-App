import type { NotificationSettings } from '@delivery/shared';
import { apiFetch } from '@/lib/http';

/**
 * LES RÉGLAGES DE NOTIFICATION — L2 bloc 9, maquette 13.
 *
 * **Les mêmes endpoints que le téléphone.** La préférence vit sur le COMPTE et
 * non sur l'appareil : couper « colis reporté » depuis le navigateur de
 * l'agence le coupe aussi sur le téléphone, ce qui est le comportement attendu
 * — c'est la même personne qui reçoit.
 *
 * Le web n'enregistre pas de jeton push (`POST /me/push-token`) : un navigateur
 * d'agence n'a pas d'application installée, et l'abonnement web demanderait un
 * service worker et une clé VAPID, c'est-à-dire un chantier à part (B-18). Il
 * lit et écrit les préférences, ce qui est tout ce que la maquette demande.
 */
export async function getNotificationSettings(): Promise<NotificationSettings> {
  return apiFetch<NotificationSettings>('/me/notifications');
}

export async function setNotificationPreference(
  topic: string,
  enabled: boolean,
): Promise<NotificationSettings> {
  return apiFetch<NotificationSettings>('/me/notifications', {
    method: 'PATCH',
    body: { topic, enabled },
  });
}
