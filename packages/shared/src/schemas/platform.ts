import { z } from 'zod';
import { COMPANY_STATUSES } from '../enums';

/** Console plateforme — C8 bloc 4. */

/**
 * Le slug EST le sous-domaine : il obéit aux règles d'un label DNS.
 *
 * Minuscules, chiffres et tirets, ni au début ni à la fin, 63 caractères au
 * maximum — la borne d'un label DNS. Un slug invalide ne produirait pas une
 * erreur d'application mais un certificat impossible à émettre.
 */
export const tenantSlugSchema = z
  .string()
  .trim()
  .toLowerCase()
  .min(2, 'too-short')
  .max(40, 'too-large')
  .regex(/^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$/, 'invalid-slug');

export const createTenantSchema = z.object({
  name: z.string().trim().min(2, 'too-short').max(120),
  slug: tenantSlugSchema,
  adminEmail: z.string().trim().toLowerCase().email('invalid-email'),
  adminFirstName: z.string().trim().min(2, 'too-short').max(80),
  adminLastName: z.string().trim().min(2, 'too-short').max(80),
  primaryColor: z.string().optional(),
  secondaryColor: z.string().optional(),
  /** Défaut 30 jours, posé par le service — pas ici, pour n'avoir qu'une source. */
  trialDays: z.coerce.number().int().min(1).max(365).optional(),
  plan: z.string().trim().max(60).optional(),
});

export type CreateTenantInput = z.input<typeof createTenantSchema>;
export type CreateTenantPayload = z.output<typeof createTenantSchema>;

export const tenantStatusSchema = z.object({
  status: z.enum(COMPANY_STATUSES),
});

export const extendTrialSchema = z.object({
  days: z.coerce.number().int().min(1, 'too-short').max(365, 'too-large'),
});

export const tenantFiltersSchema = z.object({
  search: z.string().trim().optional(),
  status: z.enum(COMPANY_STATUSES).optional(),
  page: z.coerce.number().int().min(1).default(1),
  pageSize: z.coerce.number().int().min(5).max(100).default(25),
});

export type TenantFiltersPayload = z.output<typeof tenantFiltersSchema>;

/**
 * Demande d'essai déposée depuis la landing de la plateforme — C8 bloc 5.
 *
 * Il n'y a PAS d'inscription automatique en v1 : la demande arrive au journal
 * de la console, un humain rappelle, et c'est lui qui crée l'espace. Ouvrir la
 * création au tout-venant reviendrait à laisser n'importe qui prendre un
 * sous-domaine et un jeu de données dans la production d'autres clients.
 */
export const trialRequestSchema = z.object({
  company: z.string().trim().min(2, 'too-short').max(120),
  contactName: z.string().trim().min(2, 'too-short').max(120),
  email: z.string().trim().toLowerCase().email('invalid-email'),
  phone: z.string().trim().max(40).optional(),
  message: z.string().trim().max(1000).optional(),
});

export type TrialRequestInput = z.input<typeof trialRequestSchema>;
export type TrialRequestPayload = z.output<typeof trialRequestSchema>;

/**
 * Échange du lien « se connecter en tant que » — C8 bloc 4.
 *
 * Le jeton voyage dans l'URL, faute d'autre moyen de franchir une frontière de
 * domaine : la console vit sur celui de la plateforme, la session doit naître
 * sur celui du tenant, et les cookies sont liés à l'hôte. Il ne vaut que deux
 * minutes, et que pour l'espace qu'il nomme.
 */
export const impersonationExchangeSchema = z.object({
  token: z.string().trim().min(20, 'too-short').max(4096),
});
