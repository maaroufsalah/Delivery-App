import { z } from 'zod';

/**
 * LES FILTRES DU STOCK LIVREUR — lot M (D-222).
 *
 * Partagés par l'écran, l'export et le PDF : les trois rejouent la même
 * requête avec les mêmes paramètres (D4), ils ne peuvent pas dire autre chose.
 * La taille de page borne l'AFFICHAGE, jamais ce qui est compté : les totaux
 * portent sur toute la liste filtrée.
 */

/** Les colonnes triables de la liste des livreurs. */
export const COURIER_STOCK_SORTS = [
  'courierName',
  'agencyName',
  'inHand',
  'pickedUp',
  'outForDelivery',
  'awaiting',
  'failedOrReturning',
  'deliveredUnremitted',
  'collectedUnremittedInCents',
  'toCollectInCents',
  'lastMovementAt',
] as const;

export type CourierStockSort = (typeof COURIER_STOCK_SORTS)[number];

export const courierStockListQuerySchema = z.object({
  page: z.coerce.number().int().min(1).default(1),
  pageSize: z.coerce.number().int().min(1).max(200).default(25),
  search: z.string().trim().max(120).optional(),
  agencyId: z.string().trim().optional(),
  /** `true` : les livreurs sans stock aussi. */
  all: z.enum(['true', 'false']).optional(),
  sort: z.enum(COURIER_STOCK_SORTS).default('inHand'),
  order: z.enum(['asc', 'desc']).default('desc'),
});

export type CourierStockListQuery = z.infer<typeof courierStockListQuerySchema>;

/** Les colonnes triables du détail d'un livreur. */
export const COURIER_STOCK_PARCEL_SORTS = [
  'code',
  'recipientName',
  'destinationCityName',
  'clientName',
  'codInCents',
  'statusId',
  'routeCode',
  'assignedAt',
  'lastMovementAt',
] as const;

export const courierStockParcelQuerySchema = z.object({
  page: z.coerce.number().int().min(1).default(1),
  pageSize: z.coerce.number().int().min(1).max(200).default(25),
  /** Code, destinataire, téléphone. */
  search: z.string().trim().max(120).optional(),
  statusId: z.coerce.number().int().optional(),
  clientId: z.string().trim().optional(),
  cityId: z.string().trim().optional(),
  routeId: z.string().trim().optional(),
  sort: z.enum(COURIER_STOCK_PARCEL_SORTS).default('lastMovementAt'),
  order: z.enum(['asc', 'desc']).default('desc'),
});

export type CourierStockParcelQuery = z.infer<typeof courierStockParcelQuerySchema>;

/** L'export du détail porte le livreur dans la requête : la liste blanche est statique. */
export const courierStockParcelExportQuerySchema = courierStockParcelQuerySchema.extend({
  courierId: z.string().trim().min(1),
});

export type CourierStockParcelExportQuery = z.infer<typeof courierStockParcelExportQuerySchema>;

/**
 * AU-DELÀ, LE PDF SE PRÉPARE EN ARRIÈRE-PLAN — le chemin des étiquettes (D3,
 * D-190), comme la mission le demande. Mesuré en blocs de 100 lignes : 1 000
 * lignes = 10 s, 1 500 = 13 s sur le poste ; au-delà, la requête tiendrait
 * l'API trop longtemps et approcherait le délai du proxy (60 s).
 */
export const COURIER_STOCK_PDF_ASYNC_THRESHOLD = 1_500;

/** Le `format` d'un travail de la file D3 qui porte un stock livreur, pas des étiquettes. */
export const COURIER_STOCK_JOB_FORMAT = 'stock';

/** La demande d'un PDF en arrière-plan : le global (filtres de la liste) ou un livreur. */
export const courierStockPdfJobSchema = z.discriminatedUnion('kind', [
  courierStockListQuerySchema.omit({ page: true, pageSize: true }).extend({
    kind: z.literal('global'),
  }),
  courierStockParcelQuerySchema.omit({ page: true, pageSize: true }).extend({
    kind: z.literal('courier'),
    courierId: z.string().trim().min(1),
  }),
]);

export type CourierStockPdfJobInput = z.infer<typeof courierStockPdfJobSchema>;
