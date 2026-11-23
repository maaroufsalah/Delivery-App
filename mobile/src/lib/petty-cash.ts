import type {
  PettyCashCourierExpenseInput,
  PettyCashCourierPage,
  PettyCashMovement,
} from '@delivery/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, UPLOAD_TIMEOUT_MS } from './api';

/**
 * LA PETITE CAISSE DU LIVREUR — D-168. `GET / POST / PATCH / DELETE
 * /courier/me/petty-cash` : ses dépenses seules, la portée est côté serveur.
 *
 * Pas de file hors-ligne : une dépense se déclare en réseau, et le POST porte
 * une Idempotency-Key (D-152) tirée UNE fois par ouverture du formulaire —
 * un second appui ou un rejeu après délai rend la même dépense, jamais deux.
 */

export const pettyCashKey = ['courier', 'petty-cash'] as const;

export function usePettyCash() {
  return useQuery({
    queryKey: pettyCashKey,
    queryFn: () => api<PettyCashCourierPage>('/courier/me/petty-cash'),
    // Sans la clé `pettycash.courier`, 403 : la tuile disparaît, sans rejeu.
    retry: false,
  });
}

export function useSavePettyCash() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: ({
      id,
      body,
      idempotencyKey,
    }: {
      id: string | null;
      body: PettyCashCourierExpenseInput;
      idempotencyKey: string;
    }) =>
      id
        ? api<PettyCashMovement>(`/courier/me/petty-cash/${encodeURIComponent(id)}`, {
            method: 'PATCH',
            body,
            timeoutMs: UPLOAD_TIMEOUT_MS,
          })
        : api<PettyCashMovement>('/courier/me/petty-cash', {
            method: 'POST',
            body,
            timeoutMs: UPLOAD_TIMEOUT_MS,
            idempotencyKey,
          }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: pettyCashKey });
    },
  });
}

export function useDeletePettyCash() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (id: string) =>
      api<void>(`/courier/me/petty-cash/${encodeURIComponent(id)}`, { method: 'DELETE' }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: pettyCashKey });
    },
  });
}
