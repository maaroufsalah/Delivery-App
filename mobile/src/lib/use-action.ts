import { ACTION_TIMEOUT_MS, createActionGuard, type ActionOutcome } from '@delivery/shared';
import * as Haptics from 'expo-haptics';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useI18n } from '@/i18n';
import { describeFailure, failureText, type ActionFeedback } from './failure';

/**
 * UN BOUTON D'ACTION — Recette 5, point 4 a.
 *
 * « État en cours visible, appui unique, délai maximal, retour à l'état normal
 * dans tous les cas, message d'erreur lisible avec la référence de requête,
 * aucune action perdue en silence. » La règle vit dans `createActionGuard()`
 * (`packages/shared/src/mobile-actions.ts`, tenue par
 * `apps/api/src/common/mobile/mobile-actions.spec.ts`) ; ce hook la branche sur
 * l'écran :
 *
 * - `pending` alimente le `busy` du bouton ;
 * - le second appui d'un double appui est IGNORÉ, de façon synchrone ;
 * - l'issue est TOUJOURS rendue — succès, refus, délai, réseau — et une erreur
 *   devient `error`, un texte prêt à afficher, avec « Référence : … » quand
 *   l'API en a donné une.
 *
 * L'écran affiche `error` PRÈS DU BOUTON, dans la vue où il se trouve : le
 * blocage du 03/10 venait d'un message écrit sous une fenêtre modale, donc
 * invisible (cause racine au rapport de la Recette 5).
 */
export function useAction(options: { timeoutMs?: number } = {}) {
  const { t } = useI18n();
  /*
   * Le filet de l'ÉCRAN, au-dessus du délai de chaque envoi (client HTTP, 15 s) :
   * une action peut enchaîner deux envois (renouvellement du jeton, puis
   * reprise). Cinq secondes de marge, et le bouton revient quoi qu'il arrive.
   */
  const guard = useRef(
    createActionGuard({ timeoutMs: options.timeoutMs ?? ACTION_TIMEOUT_MS + 5_000 }),
  ).current;
  const mounted = useRef(true);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  /*
   * `failure` est l'issue STRUCTURÉE (Recette 7, point 3) : gravité, phrase,
   * référence à part, rendue par `<ActionFeedbackLine>`. `error` reste la
   * version d'une ligne pour les écrans qui n'affichent qu'un texte.
   */
  const [failure, setFailure] = useState<ActionFeedback | null>(null);

  const run = useCallback(
    async <T>(
      task: () => Promise<T>,
      handlers: {
        onSuccess?: (value: T) => void;
        onError?: (message: string, failure: ActionFeedback) => void;
      } = {},
    ): Promise<ActionOutcome<T>> => {
      if (guard.running) return { kind: 'ignored' };

      setError(null);
      setFailure(null);
      setPending(true);
      const outcome = await guard.run(() => task());
      if (mounted.current) setPending(false);

      if (outcome.kind === 'done') {
        handlers.onSuccess?.(outcome.value);
        return outcome;
      }
      if (outcome.kind === 'ignored') return outcome;

      const described = describeFailure(outcome.error, t);
      const message = failureText(described, t);
      void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error).catch(() => undefined);
      if (mounted.current) {
        setError(message);
        setFailure(described);
      }
      handlers.onError?.(message, described);
      return outcome;
    },
    [guard, t],
  );

  const clearError = useCallback(() => {
    setError(null);
    setFailure(null);
  }, []);

  return { run, pending, error, failure, clearError };
}

type Translate = ReturnType<typeof useI18n>['t'];

/**
 * Une erreur, lisible par un livreur, en UNE ligne. La référence n'y figure
 * que sur une panne technique (Recette 7, point 3) ; un refus métier se lit
 * sans identifiant, et `useAction().failure` la garde pour « Détails ».
 */
export function describeActionError(error: unknown, t: Translate): string {
  return failureText(describeFailure(error, t), t);
}
