import { failureSeverity, isActionTimeout, type FailureSeverity } from '@delivery/shared';
import type { MessageKey, useI18n } from '@/i18n';
import { ApiError } from './api';

type Translate = ReturnType<typeof useI18n>['t'];

/**
 * UNE ERREUR, PRÊTE À AFFICHER — Recette 7, point 3.
 *
 * Tout ce qu'un écran du livreur montre après un refus passe par ici, qu'il
 * vienne d'un bouton (`useAction`), du scanner ou d'un bon :
 *
 *  - `tone` : `warn` (orange) pour un refus MÉTIER prévisible — colis inconnu,
 *    déjà scanné, hors bon, hors circuit, statut non permis — ; `error`
 *    (rouge) pour une panne TECHNIQUE — réseau, délai, 5xx, session ;
 *  - `text` : la phrase métier seule, sans identifiant technique ;
 *  - `reference` : le `x-request-id`, que l'écran range derrière « Détails »
 *    sur un refus métier et affiche d'emblée sur une panne (le support en a
 *    besoin, le livreur devant la personne n'en a pas).
 *
 * La règle de gravité est pure (`failureSeverity`, `packages/shared`), et
 * testée ; ce fichier ne fait que la brancher sur `ApiError` et sur `t`.
 */
export interface ActionFeedback {
  tone: 'ok' | 'warn' | 'error';
  text: string;
  reference?: string;
  severity?: FailureSeverity;
  /** Une sortie proposée au livreur : « Ouvrir BR-2610-0012 ». */
  action?: { label: string; onPress: () => void };
}

export function describeFailure(error: unknown, t: Translate): ActionFeedback {
  if (isActionTimeout(error) || (error instanceof ApiError && error.reason === 'timeout')) {
    return { tone: 'error', severity: 'technical', text: t('action.timeout') };
  }
  if (error instanceof ApiError && error.status === 0) {
    return { tone: 'error', severity: 'technical', text: t('action.offline') };
  }

  const reference = error instanceof ApiError ? error.requestId : undefined;
  const severity = failureSeverity(error instanceof ApiError ? error.status : undefined);

  /*
   * Un motif MACHINE connu du catalogue (`scan.refusal.parcel-unknown`…) prime
   * sur la phrase du serveur : il est traduit dans la langue du téléphone.
   */
  const reason = error instanceof ApiError ? error.reason : undefined;
  const key = `scan.refusal.${reason}` as MessageKey;
  const known = reason ? t(key) !== key : false;

  const text = known
    ? t(key)
    : error instanceof Error && error.message.trim()
      ? error.message.trim()
      : t('action.failed');

  return {
    tone: severity === 'business' ? 'warn' : 'error',
    severity,
    text,
    reference,
  };
}

/** Le texte tel qu'il se lit quand il n'y a qu'une ligne : la référence n'y est que sur une panne. */
export function failureText(feedback: ActionFeedback, t: Translate): string {
  if (feedback.severity === 'technical' && feedback.reference) {
    return `${feedback.text}\n${t('action.reference', { reference: feedback.reference })}`;
  }
  return feedback.text;
}
