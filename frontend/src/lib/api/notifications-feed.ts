import type { NotificationFeed } from '@delivery/shared';
import { apiFetch } from '@/lib/http';

/**
 * LE CENTRE DE NOTIFICATIONS — V2 vendeur bloc 8, spécification §10.
 *
 * Une fonction, un endpoint (règle n°1 de CLAUDE.md). Le chemin s'appelle
 * `notification-feed` et non `notifications` : ce dernier sert déjà les
 * PRÉFÉRENCES de sujets depuis M3, et le réutiliser aurait cassé les
 * applications installées.
 */
export async function getNotificationFeed(): Promise<NotificationFeed> {
  return apiFetch<NotificationFeed>('/me/notification-feed');
}
