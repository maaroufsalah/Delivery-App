import { z } from 'zod';
import { DELIVERY_SLIP_STATUSES } from '../enums';
import { hasSelection, optionalIds, SELECTION_MAX, selectionByCriteria } from './selection';

/**
 * Écritures du BL — chantier « Retours, BL Clients Pro », point 5.
 *
 * Trois gestes seulement, et chacun se lit comme une phrase du terrain :
 * « je l'ai récupéré », « voici la liasse », « le vendeur a signé ».
 */

const optionalText = (max: number) => z.string().trim().max(max).optional().or(z.literal(''));

/**
 * « BL RÉCUPÉRÉ ? » — la question posée au livreur à la livraison.
 *
 * `collected: false` n'écrit rien et ne crée aucun BL : le destinataire n'avait
 * pas le papier, ou l'a gardé. Le colis reste `blRequested`, et la question se
 * reposera. Enregistrer un « non » comme un objet aurait fait un BL qui n'a
 * jamais existé.
 *
 * La référence est FACULTATIVE : un livreur sur un trottoir ne recopie pas
 * toujours un numéro, et lui refuser l'enregistrement du BL pour ça serait
 * perdre le papier pour sauver la saisie.
 */
export const blCollectSchema = z.object({
  collected: z.boolean(),
  reference: optionalText(60),
});

export type BlCollectPayload = z.output<typeof blCollectSchema>;

/**
 * OUVERTURE D'UN BON BL — « le livreur regroupe un ou plusieurs BL récupérés ».
 *
 * Le client est dans le corps et non déduit : un livreur rentre avec les BL de
 * trois vendeurs, et il en fait trois liasses. La sélection, elle, est bornée
 * par le serveur aux BL de CE client et de CE livreur.
 */
export const blNoteSchema = z.object({
  clientId: z.string().min(1, 'required'),
  agencyId: z.string().min(1, 'required'),
  // Bornes relevées (chantier « Plafond de 200 ») : la liste des BL n'est plus tronquée à 300.
  slipIds: z.array(z.string().min(1)).max(SELECTION_MAX, 'too-many').optional(),
  note: optionalText(500),
});

export type BlNoteInput = z.input<typeof blNoteSchema>;
export type BlNotePayload = z.output<typeof blNoteSchema>;

/** Ajout de BL EN LOT à un bon en brouillon. */
export const blNoteItemsSchema = z.object({
  slipIds: z.array(z.string().min(1)).min(1, 'required').max(SELECTION_MAX, 'too-many'),
});

export type BlNoteItemsPayload = z.output<typeof blNoteItemsSchema>;

/**
 * « CONFIRMER LA REMISE AU VENDEUR » — point 5, seconde action de l'admin.
 *
 * Le NOM du receveur est demandé, comme pour le bon de retour rendu au vendeur
 * (`ReturnSlip`) et pour la même raison : `DOMAIN.md §4` dette n°4 — le legacy
 * avait un code de retour et un mot de passe en clair dont la vérification
 * était désactivée. On ne remplace pas un secret inutile par un autre secret :
 * on demande qui a signé, et on le trace.
 */
export const blNoteReturnSchema = z.object({
  returnedToName: z.string().trim().min(2, 'too-short').max(120),
});

export type BlNoteReturnPayload = z.output<typeof blNoteReturnSchema>;

/* --- Lot E, E1 (D-201) : la liste des BL et ses gestes --------------------- */

/**
 * LES FILTRES DE LA LISTE DES BL — « état, client pro, livreur, période,
 * recherche ». La période porte sur la date de RÉCUPÉRATION du BL. Les mêmes
 * champs servent la liste, l'export et la sélection « toute la liste ».
 */
export const deliverySlipFiltersSchema = z.object({
  search: z.string().trim().max(120).optional(),
  status: z.enum(DELIVERY_SLIP_STATUSES).optional(),
  clientId: z.string().trim().optional(),
  courierId: z.string().trim().optional(),
  /** Bornes incluses, `AAAA-MM-JJ`, en jours de la société. */
  from: z.string().trim().optional(),
  to: z.string().trim().optional(),
});

export type DeliverySlipFilters = z.output<typeof deliverySlipFiltersSchema>;

/** Les deux gestes de l'agence sur un BL, unitaires ou en masse. */
export const DELIVERY_SLIP_TARGETS = ['received', 'returned'] as const;
export type DeliverySlipTarget = (typeof DELIVERY_SLIP_TARGETS)[number];

/**
 * « RÉCEPTIONNER (X) » / « REMETTRE AU VENDEUR (X) ». La sélection part en
 * identifiants cochés, ou en critère (`all` + les filtres de l’écran, moins les
 * décochés) que le serveur rejoue dans la portée du compte.
 */
export const deliverySlipAdvanceSchema = z
  .object({
    to: z.enum(DELIVERY_SLIP_TARGETS),
    ids: optionalIds,
    ...selectionByCriteria,
    filters: deliverySlipFiltersSchema.optional(),
    /**
     * « REMETTRE AU VENDEUR » EXIGE LE NOM DU RECEVEUR — lot F, F4. Le même
     * champ et la même règle que le bon BRB- (`blNoteReturnSchema`) : on trace
     * qui a signé, sur chaque BL et au journal.
     */
    returnedToName: blNoteReturnSchema.shape.returnedToName.optional(),
  })
  .refine(hasSelection, { message: 'required', path: ['ids'] })
  .refine((value) => value.to !== 'returned' || Boolean(value.returnedToName), {
    message: 'required',
    path: ['returnedToName'],
  });

export type DeliverySlipAdvancePayload = z.output<typeof deliverySlipAdvanceSchema>;

/** Ce que le geste en masse a fait : combien ont avancé, combien étaient déjà ailleurs. */
export interface DeliverySlipAdvanceResult {
  count: number;
  skipped: number;
}

/**
 * LE RETOUR EN ARRIÈRE d’un BL — une étape, par un administrateur, avec motif.
 * Le même principe que la contestation d’un statut terminal de colis.
 */
export const deliverySlipRevertSchema = z.object({
  // Obligatoire, sans longueur minimale (remarques Badr 12/11) : seul le vide est refusé.
  reason: z.string().trim().min(1, 'required').max(255),
});

export type DeliverySlipRevertPayload = z.output<typeof deliverySlipRevertSchema>;
