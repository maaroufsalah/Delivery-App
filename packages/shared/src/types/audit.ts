import type { Id, IsoDateTime } from './common';

/**
 * Le journal des modifications — C11 bloc 5, docs/DECISIONS.md D-064.
 *
 * À distinguer du journal de PLATEFORME (C8, D-050), qui dit ce que la console
 * fait à un tenant. Celui-ci dit ce qui se passe DANS un tenant, et il est
 * écrit sous les services par une extension Prisma : un service ne peut pas
 * oublier de journaliser puisqu'il ne journalise pas.
 */

/**
 * Les trois formes unitaires et les trois formes de LOT.
 *
 * Un `update-many` n'a ni avant ni après : il a un filtre et un nombre de
 * lignes. Les confondre avec une modification unitaire ferait croire à un avant
 * qu'on n'a jamais relu.
 */
export const AUDIT_ACTIONS = [
  'create',
  'update',
  'delete',
  'create-many',
  'update-many',
  'delete-many',
] as const;

export type AuditAction = (typeof AUDIT_ACTIONS)[number];

export interface AuditLogListItem {
  id: Id;
  /** Nom du modèle : `Parcel`, `Invoice`, `Courier`… */
  entity: string;
  /** `id` de la ligne. Chaîne vide sur un lot : il n'y a pas UNE ligne. */
  entityId: string;
  action: AuditAction;
  /**
   * Les champs qui ont réellement changé.
   *
   * C'est la colonne qu'on lit en premier : comparer deux JSON à l'œil pour
   * trouver la valeur modifiée est le travail que cette liste évite.
   */
  diff: string[];
  userId: Id | null;
  userEmail: string | null;
  role: string | null;
  ip: string | null;
  at: IsoDateTime;
}

export interface AuditLogDetail extends AuditLogListItem {
  /**
   * L'état avant et après, en JSON libre.
   *
   * `before` est `null` à la création, `after` à la suppression. Sur un lot,
   * `before` porte le FILTRE et `after` le nombre de lignes touchées.
   */
  before: Record<string, unknown> | null;
  after: Record<string, unknown> | null;
  userAgent: string | null;
  /** Corrèle les entrées d'une MÊME requête HTTP. */
  requestId: string | null;
}

export interface AuditLogQuery {
  page?: number;
  pageSize?: number;
  /** Recherche par `entityId` ou par courriel d'auteur. */
  search?: string;
  entity?: string;
  entityId?: string;
  action?: AuditAction;
  userId?: Id;
  from?: string;
  to?: string;
}
