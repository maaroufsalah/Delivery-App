import { z } from 'zod';
import { DASHBOARD_PERIODS } from '../dashboard-period';

/**
 * LA PETITE CAISSE — chantier « Caisse, OV, Petite caisse », point 3.
 *
 * Une caisse indépendante pour les dépenses courantes de la société :
 * fournitures, petits achats du personnel. Rien ici ne touche un colis, un bon
 * de caisse, une facture ni un règlement — et rien de ces modules ne la lit.
 *
 * Deux mouvements : l'APPROVISIONNEMENT remplit la caisse, la DÉPENSE la vide.
 * Au bureau, pas de workflow : une saisie est un fait, sa modification et sa
 * suppression sont journalisées (qui, quand, avant/après) par l'extension
 * d'audit, sans code dédié. Seule la dépense d'un LIVREUR passe par une
 * validation (D-168, `PETTY_CASH_STATUSES`).
 */

export const PETTY_CASH_KINDS = ['supply', 'expense'] as const;
export type PettyCashKind = (typeof PETTY_CASH_KINDS)[number];

/**
 * Le statut d'un mouvement — petite caisse livreur, D-168.
 *
 * Une dépense saisie par un LIVREUR naît `draft` : elle n'entre ni dans le
 * solde, ni dans « Dépensé », ni dans les exports tant que l'administration ne
 * l'a pas `validated`. `refused` la garde lisible sans jamais la compter. Tout
 * ce qui est saisi au bureau est `validated` d'office : rien ne change pour
 * les mouvements d'avant.
 */
export const PETTY_CASH_STATUSES = ['draft', 'validated', 'refused'] as const;
export type PettyCashStatus = (typeof PETTY_CASH_STATUSES)[number];

/**
 * Les catégories par défaut, tant que la société n'a pas posé les siennes
 * (Setting `pettyCash.categories`, D-019). La liste est SIMPLE : des libellés,
 * sans code ni hiérarchie. Une catégorie retirée reste lisible sur les
 * mouvements qui la portent — le libellé est recopié sur la ligne.
 */
export const DEFAULT_PETTY_CASH_CATEGORIES = ['Fournitures', 'Transport', 'Entretien', 'Autre'];

/** La clé du Setting qui porte la liste. */
export const PETTY_CASH_CATEGORIES_SETTING = 'pettyCash.categories';

const optionalText = (max: number) => z.string().trim().max(max).optional().or(z.literal(''));

/** Saisi en dirhams comme tout montant de formulaire ; converti en centimes au bord. */
const madAmount = z
  .number({ error: 'required' })
  .positive('must-be-positive')
  .max(1_000_000, 'amount-too-large');

/** Le justificatif : même contrat que les pièces des documents d'argent. */
const pettyCashProofSchema = z
  .object({
    name: z.string().trim().min(1, 'required').max(120),
    mimeType: z.enum(['application/pdf', 'image/jpeg', 'image/png', 'image/webp'], {
      error: 'unsupported-file-type',
    }),
    content: z.string().min(1, 'required'),
  })
  .nullable()
  .default(null);

/**
 * Un mouvement, approvisionnement ou dépense.
 *
 * Les champs propres à la dépense — libellé, catégorie, acheteur — sont exigés
 * pour elle et ignorés pour l'approvisionnement : le service les remet à
 * `null`, un approvisionnement n'a pas d'acheteur.
 */
export const pettyCashMovementSchema = z
  .object({
    kind: z.enum(PETTY_CASH_KINDS),
    /** Jour du mouvement, `AAAA-MM-JJ`. */
    date: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'invalid-date'),
    amount: madAmount,
    agencyId: z.string().min(1, 'required'),
    label: optionalText(160),
    category: optionalText(60),
    /** Le membre de l'équipe qui a acheté. */
    buyerId: optionalText(40),
    note: optionalText(500),
    proof: pettyCashProofSchema,
    /** À la modification : retirer le justificatif existant. */
    removeProof: z.boolean().optional(),
  })
  .refine((value) => value.kind !== 'expense' || Boolean(value.label), {
    error: 'required',
    path: ['label'],
  })
  .refine((value) => value.kind !== 'expense' || Boolean(value.category), {
    error: 'required',
    path: ['category'],
  })
  .refine((value) => value.kind !== 'expense' || Boolean(value.buyerId), {
    error: 'required',
    path: ['buyerId'],
  });

export type PettyCashMovementInput = z.input<typeof pettyCashMovementSchema>;
export type PettyCashMovementPayload = z.output<typeof pettyCashMovementSchema>;

/** La liste de catégories, réglée depuis l'écran lui-même. */
export const pettyCashCategoriesSchema = z.object({
  categories: z
    .array(z.string().trim().min(1, 'required').max(60))
    .min(1, 'required')
    .max(30)
    .refine((list) => new Set(list.map((item) => item.toLowerCase())).size === list.length, {
      error: 'invalid',
    }),
});

export type PettyCashCategoriesPayload = z.output<typeof pettyCashCategoriesSchema>;

/**
 * Les filtres de l'écran. La PÉRIODE est celle des tableaux de bord et borne
 * la liste comme les totaux « approvisionné / dépensé » ; le SOLDE, lui, est
 * toujours celui de la caisse à ce jour, toutes dates confondues.
 */
export const pettyCashListQuerySchema = z.object({
  page: z.coerce.number().int().min(1).default(1),
  pageSize: z.coerce.number().int().min(1).max(200).default(25),
  search: z.string().trim().max(120).optional(),
  sort: z.string().trim().max(40).optional(),
  order: z.enum(['asc', 'desc']).default('desc'),
  period: z.enum(DASHBOARD_PERIODS).default('month'),
  from: z.string().trim().optional(),
  to: z.string().trim().optional(),
  agencyId: z.string().trim().optional(),
  kind: z.enum(PETTY_CASH_KINDS).optional(),
  category: z.string().trim().max(60).optional(),
  buyerId: z.string().trim().optional(),
  /** D-168 — sans filtre, la liste montre tous les statuts ; les totaux, les validés seuls. */
  status: z.enum(PETTY_CASH_STATUSES).optional(),
});

export type PettyCashListQueryInput = z.input<typeof pettyCashListQuerySchema>;
export type PettyCashListQuery = z.output<typeof pettyCashListQuerySchema>;

/**
 * Le refus d'un brouillon par l'administration — D-168. Le motif est
 * facultatif ; il est rendu au livreur sur sa liste.
 */
export const pettyCashRefuseSchema = z.object({
  reason: optionalText(300),
});

export type PettyCashRefusePayload = z.output<typeof pettyCashRefuseSchema>;

/**
 * La dépense d'un LIVREUR — D-168. Ni date, ni agence, ni acheteur : le
 * serveur pose le jour de la société, l'agence résolue pour le livreur
 * (D-154) et le livreur connecté comme acheteur. Le corps n'en porte donc
 * aucun, et rien ne peut les détourner.
 */
export const pettyCashCourierExpenseSchema = z.object({
  label: z.string().trim().min(1, 'required').max(160),
  category: z.string().trim().min(1, 'required').max(60),
  amount: madAmount,
  note: optionalText(500),
  proof: pettyCashProofSchema,
  removeProof: z.boolean().optional(),
});

export type PettyCashCourierExpenseInput = z.input<typeof pettyCashCourierExpenseSchema>;
export type PettyCashCourierExpensePayload = z.output<typeof pettyCashCourierExpenseSchema>;

/* --- D-249 : la journée de caisse ------------------------------------------ */

/** Ouverte : on y écrit. Clôturée : figée tant qu'elle n'est pas rouverte. */
export const PETTY_CASH_DAY_STATUSES = ['open', 'closed'] as const;
export type PettyCashDayStatus = (typeof PETTY_CASH_DAY_STATUSES)[number];

const dayText = z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'invalid-date');

/** Le bloc « Journées » : plus récentes d'abord, filtre d'état et « avec brouillons ». */
export const pettyCashDaysQuerySchema = z.object({
  agencyId: z.string().trim().optional(),
  status: z.enum(PETTY_CASH_DAY_STATUSES).optional(),
  /** `true` : seules les journées qui portent au moins un brouillon à valider. */
  withDrafts: z
    .enum(['true', 'false'])
    .optional()
    .transform((value) => value === 'true'),
  limit: z.coerce.number().int().min(1).max(1000).default(400),
});

export type PettyCashDaysQueryInput = z.input<typeof pettyCashDaysQuerySchema>;
export type PettyCashDaysQuery = z.output<typeof pettyCashDaysQuerySchema>;

/** Le jour visé par une route `/petty-cash/days/:day`. */
export const pettyCashDayParamSchema = dayText;

/** Clôturer ou reclôturer : une agence, ou toutes celles de la portée. */
export const pettyCashDayCloseSchema = z.object({
  agencyId: z.string().trim().optional(),
});

export type PettyCashDayClosePayload = z.output<typeof pettyCashDayCloseSchema>;

/**
 * Rouvrir pour corriger : le motif est OBLIGATOIRE, il est journalisé — sans
 * longueur minimale, comme tout motif depuis les remarques de Badr du 12/11.
 */
export const pettyCashDayReopenSchema = z.object({
  agencyId: z.string().trim().optional(),
  reason: z.string().trim().min(1, 'required').max(300),
});

export type PettyCashDayReopenPayload = z.output<typeof pettyCashDayReopenSchema>;

/** La liste du livreur : ses dépenses seules, toutes agences, les plus récentes d'abord. */
export const pettyCashCourierListQuerySchema = z.object({
  page: z.coerce.number().int().min(1).default(1),
  pageSize: z.coerce.number().int().min(1).max(100).default(50),
});

export type PettyCashCourierListQuery = z.output<typeof pettyCashCourierListQuerySchema>;
