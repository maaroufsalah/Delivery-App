import type { NotificationFeed } from '@delivery/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';

/**
 * LE CENTRE DE NOTIFICATIONS — V2 vendeur bloc 8, spécification §10.
 *
 * **Trois routes, aucune permission.** Ses propres notifications ne sont pas
 * une ressource du domaine : tout compte connecté lit les siennes, et la
 * portée — société et compte — est dans la requête côté serveur, pas dans une
 * clé que la matrice pourrait retirer.
 *
 * **Le compteur de non-lues revient de CHAQUE écriture.** Marquer une ligne
 * lue change la pastille du menu : la recalculer côté téléphone demanderait de
 * connaître le total, que la page de cinquante ne porte pas.
 */

const keys = {
  feed: ['notifications', 'feed'] as const,
};

export function useNotifications() {
  return useQuery({
    queryKey: keys.feed,
    queryFn: () => api<NotificationFeed>('/me/notification-feed'),
    // Trente secondes : l'écran se rouvre souvent, et une notification de plus
    // n'a pas besoin d'arriver à la seconde. Le tirer-pour-rafraîchir reste là.
    staleTime: 30_000,
  });
}

export function useMarkNotificationRead() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (id: string) =>
      api<{ unread: number }>(`/me/notification-feed/${encodeURIComponent(id)}/read`, {
        method: 'POST',
      }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.feed });
    },
  });
}

export function useMarkAllNotificationsRead() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: () => api<{ unread: number }>('/me/notification-feed/read-all', { method: 'POST' }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.feed });
    },
  });
}
