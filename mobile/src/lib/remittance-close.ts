import type { RemittanceStage } from '@delivery/shared';

/**
 * LA CLÔTURE DE CAISSE PAR LE LIVREUR — D-243, la règle côté téléphone.
 *
 * **Ce fichier n'importe RIEN à l'exécution**, et c'est voulu : ni React
 * Native, ni la file, ni le client HTTP. La règle est pure, donc
 * `apps/api/src/common/mobile/remittance-close-offline-queue.spec.ts` l'importe
 * et l'exécute réellement, alors que `apps/mobile` n'a pas de socle de test.
 *
 * **Pourquoi la file bloque la clôture.** Une livraison faite dans une cage
 * d'escalier attend dans la file hors ligne (`queue.ts`, D-077). Tant qu'elle
 * n'est pas partie, le serveur ne la connaît pas : clôturer maintenant fermerait
 * une caisse à laquelle il manque un encaissement, et cette livraison ouvrirait
 * ensuite une SECONDE caisse en arrivant. Le livreur remettrait à l'agence un
 * montant qui ne correspond à aucun des deux bons.
 *
 * La clôture est donc un geste EN LIGNE : jamais mise en file (le type
 * `QueuedKind` ne la connaît pas), et refusée tant que la file n'est pas vide.
 */

/** Le motif machine du refus local, le pendant des `reason` de l'API. */
export const REMITTANCE_CLOSE_QUEUE_REASON = 'offline-queue-pending';

export type RemittanceCloseGate =
  | { allowed: true }
  | { allowed: false; reason: typeof REMITTANCE_CLOSE_QUEUE_REASON; pendingCount: number };

/**
 * Peut-on clôturer ? Seulement quand la file hors ligne est VIDE.
 *
 * Un compte illisible (`NaN`, négatif, non entier) refuse aussi : dans le
 * doute, on ne ferme pas une caisse.
 */
export function canCloseRemittance(pendingCount: number): RemittanceCloseGate {
  if (pendingCount === 0) return { allowed: true };

  return {
    allowed: false,
    reason: REMITTANCE_CLOSE_QUEUE_REASON,
    pendingCount: Number.isInteger(pendingCount) && pendingCount > 0 ? pendingCount : 0,
  };
}

/**
 * Le refus LOCAL, levé avant tout appel réseau. Une classe à part pour que
 * l'écran le distingue d'un refus du serveur et affiche SA phrase.
 */
export class RemittanceCloseBlockedError extends Error {
  readonly reason = REMITTANCE_CLOSE_QUEUE_REASON;

  constructor(readonly pendingCount: number) {
    super('Offline queue is not empty: the remittance cannot be closed yet.');
    this.name = 'RemittanceCloseBlockedError';
  }
}

/** Lève si la file n'est pas vide. Appelée par le hook AVANT `POST …/close`. */
export function assertCanCloseRemittance(pendingCount: number): void {
  const gate = canCloseRemittance(pendingCount);
  if (!gate.allowed) throw new RemittanceCloseBlockedError(gate.pendingCount);
}

/**
 * Le statut AFFICHÉ d'une caisse. L'API le sert (`stage`) et l'écran n'en
 * dérive aucun ; le repli ne sert qu'à une API d'avant D-243, qui ne rend que
 * `status` : une mise à jour OTA peut arriver avant le déploiement du serveur.
 */
export function remittanceStageOf(row: {
  stage?: RemittanceStage | null;
  status: string;
}): RemittanceStage {
  if (row.stage) return row.stage;
  if (row.status === 'confirmed') return 'received';
  if (row.status === 'cancelled') return 'cancelled';
  return 'open';
}

/**
 * Le JETON de couleur de chaque statut, le même pour le livreur et l'agence.
 *
 * Ambre tant que l'argent attend quelqu'un (ouverte, fermée), la couleur de la
 * marque quand l'agence l'a reçu, vert quand les vendeurs sont payés. Un nom
 * de jeton, pas une couleur : l'écran le lit dans le thème du client.
 */
export function remittanceStageTone(
  stage: RemittanceStage,
): 'warning' | 'primary' | 'success' | 'destructive' {
  if (stage === 'validated') return 'success';
  if (stage === 'received') return 'primary';
  if (stage === 'cancelled') return 'destructive';
  return 'warning';
}

/** La clé de traduction de chaque statut, la même pour le livreur et l'agence. */
export const REMITTANCE_STAGE_LABEL = {
  open: 'remittances.stage.open',
  closed: 'remittances.stage.closed',
  received: 'remittances.stage.received',
  validated: 'remittances.stage.validated',
  cancelled: 'remittances.stage.cancelled',
} as const satisfies Record<RemittanceStage, string>;
