import type { Id, IsoDateTime } from './common';

/**
 * LES TÂCHES DE MASSE — lot W2 (D-239), audit E2E-001.
 *
 * Au-delà de `BULK_ASYNC_THRESHOLD` colis, un geste de masse (livraison en
 * masse, validation d'un bon, départ d'un circuit) ne tient plus dans une
 * transaction de 60 s. Il devient une TÂCHE persistée, traitée par paquets de
 * `BULK_CHUNK_SIZE` — chaque paquet applique les mêmes règles et le même
 * journal qu'un geste unitaire — et l'objet (bon, circuit) n'est marqué
 * terminé qu'après le dernier paquet. En dessous du seuil, rien ne change :
 * le geste reste synchrone.
 */

/** Au-delà, la tâche ; en dessous ou égal, le geste synchrone d'avant. */
export const BULK_ASYNC_THRESHOLD = 300;

/**
 * Colis par transaction. Mesuré : la livraison en masse coûte ≈ 84 ms par colis
 * (audit E2E du 02/11) — 250 tiennent en ≈ 21 s, loin des 60 s de la
 * transaction ; la validation (≈ 20 ms) et le départ (≈ 22 ms) bien davantage.
 */
export const BULK_CHUNK_SIZE = 250;

/**
 * L'en-tête par lequel un client dit qu'il SAIT suivre une tâche (202 + tâche).
 * Sans lui, la réponse synchrone d'avant : une application déjà installée,
 * qui lirait `count` dans un 202, n'est jamais surprise.
 */
export const BULK_JOBS_HEADER = 'x-bulk-jobs';

export const BULK_JOB_KINDS = [
  'route-outcomes',
  'note-validate',
  'route-dispatch',
  // Le transfert vers un autre livreur — mesuré à 5 000 : 504 (lot W2).
  'parcel-transfer',
] as const;
export type BulkJobKind = (typeof BULK_JOB_KINDS)[number];

export type BulkJobStatus = 'pending' | 'running' | 'done' | 'failed';

/** Un colis que la tâche n'a pas pu traiter, et pourquoi (code d'erreur de l'API). */
export interface BulkJobFailure {
  parcelId: Id;
  code: string | null;
  reason: string;
}

export interface BulkJob {
  id: Id;
  kind: BulkJobKind;
  /** Le code de l'objet : `CIR-2611-0001`, `BR-2611-0002`. */
  target: string;
  status: BulkJobStatus;
  total: number;
  /** Colis traités (réussis ou en échec) — la progression « 1 200 / 5 000 ». */
  processed: number;
  failures: BulkJobFailure[];
  /** Le résultat final du geste (même forme que la réponse synchrone), une fois `done`. */
  result: Record<string, unknown> | null;
  /** Pourquoi la tâche s'est arrêtée (`failed`) : code d'erreur, rejouable par `retry`. */
  error: string | null;
  createdAt: IsoDateTime;
  finishedAt: IsoDateTime | null;
}

/** Ce que rend un geste de masse passé en tâche : HTTP 202. */
export interface BulkJobAccepted {
  job: BulkJob;
}

/** Une réponse de geste de masse : le résultat synchrone, ou la tâche (202). */
export function isBulkJobAccepted(value: unknown): value is BulkJobAccepted {
  return (
    typeof value === 'object' &&
    value !== null &&
    'job' in value &&
    typeof (value as { job?: { id?: unknown } }).job?.id === 'string'
  );
}
