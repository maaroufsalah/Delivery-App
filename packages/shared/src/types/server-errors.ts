import type { Id, IsoDateTime } from './common';

/**
 * LES ERREURS SERVEUR — recette SpeedyTrans 4, point 5 c (D-134).
 *
 * Ce que la page « Erreurs » de la console plateforme lit : les 500 de l'API et
 * les échecs des tâches de fond, groupés par route et message.
 */
export type ServerErrorSource = 'api' | 'task';

export interface ServerErrorGroup {
  source: ServerErrorSource;
  /** Motif de route (`/api/parcels/:code`) ou nom de la tâche. */
  route: string;
  message: string;
  status: number;
  count: number;
  firstAt: IsoDateTime;
  lastAt: IsoDateTime;
  /** L'identifiant de requête de la dernière occurrence : la « Référence » du client. */
  lastRequestId: string | null;
}

export interface ServerErrorOccurrence {
  id: Id;
  at: IsoDateTime;
  source: ServerErrorSource;
  requestId: string | null;
  method: string | null;
  route: string;
  status: number;
  code: string | null;
  message: string;
  stack: string | null;
  durationMs: number | null;
  userId: Id | null;
  role: string | null;
  tenantId: Id | null;
  ip: string | null;
}
