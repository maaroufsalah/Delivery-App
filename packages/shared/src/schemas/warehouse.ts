import { z } from 'zod';
import { ORDER_SOURCES, STOCK_MOVEMENT_KINDS } from '../enums';
import { DISTRICT_NAME_MAX } from '../district-name';
import {
  DELIVERY_ADDRESS_MIN,
  madAmountSchema,
  phoneSchema,
  PARCEL_TEXT_MIN,
} from './parcel';

/**
 * Schémas d'écriture du menu WAREHOUSE, partagés par les formulaires web et
 * l'API.
 *
 * Deux conventions y sont tenues, comme ailleurs :
 *  - un montant se SAISIT en dirhams et se LIT en centimes ;
 *  - une dimension se saisit en **millimètres** et un poids en **grammes**,
 *    tous deux entiers. Le legacy stockait des `Decimal` en centimètres et en
 *    kilos, ce qui donnait des `0.001` kg sur les petits articles et des
 *    arrondis différents selon l'écran. Un entier ne s'arrondit pas.
 */

const optionalText = (max: number) => z.string().trim().max(max).optional().or(z.literal(''));

/* --- Produits ------------------------------------------------------------- */

/** Dimension en millimètres : 0 à 10 m. Vide = non renseignée, pas zéro. */
const millimetreSchema = z
  .number()
  .int('not-integer')
  .min(0, 'negative')
  .max(10_000, 'too-large')
  .nullable()
  .default(null);

/** Poids en grammes : 0 à 200 kg. */
const gramSchema = z
  .number()
  .int('not-integer')
  .min(0, 'negative')
  .max(200_000, 'too-large')
  .nullable()
  .default(null);

export const productSchema = z.object({
  clientId: z.string().min(1, 'required'),
  reference: optionalText(64),
  name: z.string().trim().min(2, 'too-short').max(160),
  lengthMm: millimetreSchema,
  widthMm: millimetreSchema,
  heightMm: millimetreSchema,
  weightG: gramSchema,
  price: madAmountSchema.nullable().default(null),
  /** Valeur déclarée : elle sert à l'assurance, pas à la facturation. */
  declaredValue: madAmountSchema.nullable().default(null),
  internalStock: z.number().int().min(0, 'negative').nullable().default(null),
  /** Valeurs d'attribut retenues (`ProductAttributeValue.id`). */
  attributeValueIds: z.array(z.string().min(1)).default([]),
  isActive: z.boolean().default(true),
});

export type ProductInput = z.input<typeof productSchema>;
export type ProductPayload = z.output<typeof productSchema>;

/** Image du produit — même transport que les pièces jointes client : base64. */
export const productImageSchema = z.object({
  name: z.string().trim().min(1, 'required').max(120),
  mimeType: z.enum(['image/jpeg', 'image/png', 'image/webp'], {
    error: 'unsupported-file-type',
  }),
  content: z.string().min(1, 'required'),
});

export type ProductImagePayload = z.output<typeof productImageSchema>;

/** Attribut et ses valeurs — `Taille`, `Couleur` (DOMAIN.md §1.18). */
export const productAttributeSchema = z.object({
  name: z.string().trim().min(2, 'too-short').max(60),
});

export type ProductAttributePayload = z.output<typeof productAttributeSchema>;

export const productAttributeValueSchema = z.object({
  attributeId: z.string().min(1, 'required'),
  value: z.string().trim().min(1, 'required').max(60),
});

export type ProductAttributeValuePayload = z.output<typeof productAttributeValueSchema>;

/* --- Stock ---------------------------------------------------------------- */

/**
 * Ajustement manuel d'une quantité.
 *
 * `reason` est OBLIGATOIRE, et c'est tout l'intérêt : une quantité qui change
 * sans raison écrite est un écart d'inventaire qu'on ne saura pas expliquer
 * trois mois plus tard. Le legacy écrasait `produit_stock.quantity` sans rien
 * demander.
 */
export const stockAdjustSchema = z.object({
  productId: z.string().min(1, 'required'),
  agencyId: z.string().min(1, 'required'),
  /** Variation signée : `-3` retire trois pièces. `0` est refusé. */
  quantity: z
    .number()
    .int('not-integer')
    .refine((value) => value !== 0, { error: 'zero-movement' }),
  // Obligatoire, sans longueur minimale (remarques Badr 12/11) : seul le vide est refusé.
  reason: z.string().trim().min(1, 'required').max(255),
});

export type StockAdjustPayload = z.output<typeof stockAdjustSchema>;

export const stockMovementQuerySchema = z.object({
  kind: z.enum(STOCK_MOVEMENT_KINDS).optional(),
});

/* --- Entreposages --------------------------------------------------------- */

export const storageRequestSchema = z.object({
  clientId: z.string().min(1, 'required'),
  agencyId: z.string().min(1, 'required'),
  note: optionalText(500),
  /** Une demande sans ligne n'a rien à entreposer. */
  items: z
    .array(
      z.object({
        productId: z.string().min(1, 'required'),
        quantity: z.number().int('not-integer').min(1, 'too-small').max(100_000, 'too-large'),
      }),
    )
    .min(1, 'required'),
});

export type StorageRequestInput = z.input<typeof storageRequestSchema>;
export type StorageRequestPayload = z.output<typeof storageRequestSchema>;

/**
 * Décision du staff.
 *
 * Le motif n'est exigé QUE pour un refus : accepter n'appelle pas de
 * justification, refuser si. Le legacy n'avait ni l'un ni l'autre — le statut
 * 3 « Refusée » comptait 0 ligne, et pour cause : rien ne permettait de dire
 * pourquoi.
 */
export const storageDecisionSchema = z
  .object({
    approve: z.boolean(),
    refusalReason: optionalText(255),
  })
  // Un refus exige un motif, sans longueur minimale (remarques Badr 12/11).
  .refine((value) => value.approve || (value.refusalReason ?? '').trim().length > 0, {
    path: ['refusalReason'],
    error: 'required',
  });

export type StorageDecisionPayload = z.output<typeof storageDecisionSchema>;

/* --- Commandes ------------------------------------------------------------ */

export const orderLineSchema = z.object({
  productId: z.string().min(1, 'required'),
  quantity: z.number().int('not-integer').min(1, 'too-small').max(10_000, 'too-large'),
  /** Vide = le prix du produit au moment de la création. */
  unitPrice: madAmountSchema.nullable().default(null),
});

export const orderSchema = z.object({
  clientId: z.string().min(1, 'required'),
  agencyId: z.string().min(1).optional().or(z.literal('')),
  source: z.enum(ORDER_SOURCES).default('manual'),
  externalRef: optionalText(64),
  recipientName: z.string().trim().min(PARCEL_TEXT_MIN, 'required').max(120),
  recipientPhone: phoneSchema,
  destinationCityId: z.string().min(1, 'required'),
  destinationDistrictId: z.string().min(1).optional().or(z.literal('')),
  /** Quartier en texte libre, facultatif — chantier « Quartier en saisie libre ». */
  destinationDistrictName: optionalText(DISTRICT_NAME_MAX),
  // Recette 6, point 6 : seule une adresse vide est refusée.
  deliveryAddress: z.string().trim().min(DELIVERY_ADDRESS_MIN, 'required').max(255),
  note: optionalText(500),
  lines: z.array(orderLineSchema).min(1, 'required'),
});

export type OrderInput = z.input<typeof orderSchema>;
export type OrderPayload = z.output<typeof orderSchema>;

/**
 * Transformation d'une commande en colis.
 *
 * Le montant du contre-remboursement n'est pas redemandé : c'est le total de
 * la commande. Ce qui reste à décider, c'est la logistique — qui ramasse, et
 * si les frais sont inclus.
 */
export const orderToParcelSchema = z.object({
  pickupAtSender: z.boolean().default(true),
  feeIncluded: z.boolean().default(true),
  fragile: z.boolean().default(false),
  openOnDelivery: z.boolean().default(false),
});

export type OrderToParcelPayload = z.output<typeof orderToParcelSchema>;

/** Import de commandes — même forme que l'import tarifaire (D-022). */
export const warehouseImportSchema = z.object({
  content: z.string().min(1, 'required'),
  filename: optionalText(255),
  /** `false` = aperçu seul, rien n'est écrit. C'est le défaut. */
  apply: z.boolean().default(false),
  /** Import de commandes : le client propriétaire des lignes. */
  clientId: z.string().min(1).optional().or(z.literal('')),
});

export type WarehouseImportPayload = z.output<typeof warehouseImportSchema>;
