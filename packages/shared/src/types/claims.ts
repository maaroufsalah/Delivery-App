import type { ClaimStatus, ClaimSubjectType } from '../enums';
import type { Id, IsoDateTime } from './common';

/**
 * Réclamations — DOMAIN.md §1.19, C7 bloc 2.
 *
 * La table legacy comptait 0 ligne en production malgré six types configurés et
 * trois statuts : la fonction était livrée, jamais adoptée. Ce qui lui manquait
 * n'était pas un champ de plus, c'était un fil de discussion, un responsable et
 * une échéance — sans quoi une réclamation est un formulaire qui tombe dans le
 * vide.
 */

export interface ClaimType {
  id: Id;
  /** Clé stable, indépendante du libellé affiché. */
  code: string;
  label: string;
  subjectType: ClaimSubjectType;
  sortOrder: number;
  isActive: boolean;
}

export interface ClaimListItem {
  id: Id;
  code: string;
  status: ClaimStatus;
  subject: string;
  typeId: Id;
  typeLabel: string;
  typeCode: string;
  clientId: Id;
  clientName: string;
  parcelCode: string | null;
  assigneeId: Id | null;
  assigneeName: string | null;
  authorName: string;
  /** Échéance de traitement, gelée à la création. `null` = pas de délai réglé. */
  dueAt: IsoDateTime | null;
  /** Vrai quand l'échéance est passée ET que la réclamation est toujours ouverte. */
  isLate: boolean;
  resolvedAt: IsoDateTime | null;
  createdAt: IsoDateTime;
  updatedAt: IsoDateTime;
}

export interface ClaimAttachment {
  id: Id;
  filename: string;
  mimeType: string;
  sizeBytes: number;
  createdAt: IsoDateTime;
}

/** Une entrée du fil : un changement d'état, une réponse, ou les deux. */
export interface ClaimEvent {
  id: Id;
  status: ClaimStatus | null;
  body: string | null;
  /** Note d'agence : jamais rendue à l'e-commerçant. */
  isInternal: boolean;
  authorName: string;
  attachments: ClaimAttachment[];
  createdAt: IsoDateTime;
}

export interface ClaimDetail extends ClaimListItem {
  /** L'exposé initial, immuable. */
  message: string;
  /** Pièces jointes de l'exposé initial — celles des réponses sont sur l'événement. */
  attachments: ClaimAttachment[];
  events: ClaimEvent[];
  /** Statuts atteignables depuis l'état courant, du point de vue du compte. */
  allowedTransitions: ClaimStatus[];
}

export interface ClaimFilters {
  search?: string;
  status?: ClaimStatus;
  typeId?: Id;
  clientId?: Id;
  assigneeId?: Id;
  /** `true` : uniquement celles dont l'échéance est dépassée. */
  late?: boolean;
}

/** Compteur de tête de menu : le nombre de réclamations non traitées. */
export interface ClaimCounters {
  open: number;
  inProgress: number;
  late: number;
}
