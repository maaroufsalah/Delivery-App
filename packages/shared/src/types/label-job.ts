import type { COURIER_STOCK_JOB_FORMAT } from '../schemas/courier-stock';
import type { LabelFormat } from '../schemas/routes';
import type { Id, IsoDateTime } from './common';

/**
 * UN TRAVAIL D'ÉTIQUETTES — lot D, D3 (D-190).
 *
 * Au-delà de `LABELS_ASYNC_THRESHOLD`, la planche se prépare en arrière-plan :
 * `pending` (en file), `running` (en rendu), `ready` (téléchargeable jusqu'à
 * `expiresAt`), `failed` (`error` dit pourquoi : la relancer). Seul son
 * demandeur le voit.
 */
export type LabelJobStatus = 'pending' | 'running' | 'ready' | 'failed';

export interface LabelJob {
  id: Id;
  status: LabelJobStatus;
  /** Lot M : `stock` pour un PDF de stock livreur (même file, D-222). */
  format: LabelFormat | typeof COURIER_STOCK_JOB_FORMAT;
  /** Le nombre d'étiquettes demandées, résolues dans la portée du demandeur. */
  count: number;
  createdAt: IsoDateTime;
  finishedAt: IsoDateTime | null;
  /** Au-delà, le fichier est effacé et la ligne disparaît. */
  expiresAt: IsoDateTime;
  sizeBytes: number | null;
  /**
   * Lot W2 (D-239), E2E-002 : `zip` au-delà de 1 000 étiquettes (des PDF de
   * 500, rendus par morceaux), `pdf` sinon. Absent avant le lot : un PDF.
   */
  fileType?: 'pdf' | 'zip';
  /** `interrupted` (redémarrage du serveur) ou `failed`. */
  error: string | null;
}
