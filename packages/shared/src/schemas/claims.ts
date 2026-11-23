import { z } from 'zod';
import { CLAIM_STATUSES, CLAIM_SUBJECT_TYPES } from '../enums';

/**
 * Réclamations — C7 bloc 2. Validés côté client ET rejoués côté serveur, comme
 * partout : une Server Action est un endpoint POST public.
 */

/**
 * Pièce jointe transportée en base64, comme le classeur d'import (D-022).
 *
 * Les écritures du front partent d'une Server Action, qui poste du JSON ;
 * accepter du `multipart` imposerait `multer` et ses types pour trois photos.
 * Le plafond est posé ici ET côté service : une borne qui n'existe qu'à un
 * endroit finit par être contournée.
 */
export const claimAttachmentSchema = z.object({
  filename: z.string().trim().min(1, 'required').max(200),
  mimeType: z.enum(
    ['image/jpeg', 'image/png', 'image/webp', 'application/pdf'],
    'unsupported-type',
  ),
  /** ~4 Mo décodés. Une photo de téléphone y tient largement. */
  content: z.string().min(1, 'required').max(6_000_000, 'file-too-large'),
});

export type ClaimAttachmentPayload = z.infer<typeof claimAttachmentSchema>;

/** Au plus cinq pièces par message : au-delà, c'est un dossier, pas une réclamation. */
const attachments = z.array(claimAttachmentSchema).max(5, 'too-many-attachments').default([]);

export const createClaimSchema = z.object({
  typeId: z.string().min(1, 'required'),
  // Recette 6, point 6 : obligatoires, sans longueur minimale (« Retard » suffit).
  subject: z.string().trim().min(1, 'required').max(160),
  message: z.string().trim().min(1, 'required').max(4000),
  /**
   * Code du colis, et non son identifiant : c'est ce que l'e-commerçant a sous
   * les yeux, sur l'étiquette comme dans sa liste. Le service le résout.
   * Obligatoire quand le type porte `subjectType: 'parcel'` — la règle vit au
   * service, qui seul connaît le type.
   */
  parcelCode: z.string().trim().max(40).optional().or(z.literal('')),
  /** Ignoré quand le compte est un e-commerçant : il réclame pour lui-même. */
  clientId: z.string().optional(),
  attachments,
});

export type CreateClaimInput = z.input<typeof createClaimSchema>;
export type CreateClaimPayload = z.output<typeof createClaimSchema>;

/**
 * Une entrée du fil. Un message, un changement de statut, ou les deux à la
 * fois — répondre en clôturant est le geste le plus courant, et l'imposer en
 * deux appels ferait deux lignes d'historique pour un seul acte.
 */
export const replyClaimSchema = z
  .object({
    body: z.string().trim().max(4000).optional().or(z.literal('')),
    status: z.enum(CLAIM_STATUSES).optional(),
    /** Note d'agence : jamais rendue à l'e-commerçant. Staff seulement. */
    isInternal: z.boolean().default(false),
    attachments,
  })
  .refine(
    (value) =>
      Boolean(value.body?.trim()) || value.status !== undefined || value.attachments.length > 0,
    { path: ['body'], error: 'required' },
  );

export type ReplyClaimInput = z.input<typeof replyClaimSchema>;
export type ReplyClaimPayload = z.output<typeof replyClaimSchema>;

/** Assignation — `null` rend la réclamation à la pile commune. */
export const assignClaimSchema = z.object({
  assigneeId: z.string().min(1).nullable(),
});

export type AssignClaimPayload = z.infer<typeof assignClaimSchema>;

/** Type de réclamation, éditable en Paramètres (D-019). */
export const claimTypeSchema = z.object({
  code: z
    .string()
    .trim()
    .min(2, 'too-short')
    .max(40)
    .regex(/^[a-z0-9-]+$/, 'invalid-code'),
  label: z.string().trim().min(2, 'too-short').max(120),
  subjectType: z.enum(CLAIM_SUBJECT_TYPES).default('parcel'),
  sortOrder: z.coerce.number().int().min(0).max(999).default(0),
  isActive: z.boolean().default(true),
});

export type ClaimTypeInput = z.input<typeof claimTypeSchema>;
export type ClaimTypePayload = z.output<typeof claimTypeSchema>;

export const claimFiltersSchema = z.object({
  search: z.string().trim().optional(),
  status: z.enum(CLAIM_STATUSES).optional(),
  typeId: z.string().optional(),
  clientId: z.string().optional(),
  assigneeId: z.string().optional(),
  late: z.coerce.boolean().optional(),
  page: z.coerce.number().int().min(1).default(1),
  pageSize: z.coerce.number().int().min(5).max(200).default(20),
});

export type ClaimFiltersInput = z.input<typeof claimFiltersSchema>;
/** Après coercition : c'est ce que le contrôleur reçoit, et ce qu'il passe au service. */
export type ClaimFiltersPayload = z.output<typeof claimFiltersSchema>;
