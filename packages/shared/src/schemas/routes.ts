import { z } from 'zod';
import { ROUTE_KINDS } from '../enums';
import { madAmountSchema } from './parcel';
import { hasSelection, optionalIds, SELECTION_MAX, selectionByCriteria } from './selection';

/**
 * Schémas d'écriture des CIRCUITS et des BONS — C0.
 *
 * Ils complètent ce que C1 avait laissé en lecture seule : jusqu'ici un circuit
 * ne pouvait ni naître, ni recevoir de colis, ni partir. Le flux terrain
 * s'arrêtait donc au bon de ramassage, et les retours de C3 ne pouvaient pas
 * fonctionner — `returns.service.ts` exige que le colis porte le `courierId` du
 * livreur, ce que seul le départ en circuit renseigne.
 */

const optionalText = (max: number) => z.string().trim().max(max).optional().or(z.literal(''));

/* --- Circuits ------------------------------------------------------------- */

export const routeSchema = z.object({
  /** Ville DESSERVIE par le circuit — celle des colis qu'il emporte. */
  cityId: z.string().min(1, 'required'),
  courierId: z.string().min(1, 'required'),
  kind: z.enum(ROUTE_KINDS).default('delivery'),
  /** Date de sortie prévue. Vide = aujourd'hui, décidé par le serveur. */
  exitDate: z.string().trim().optional().or(z.literal('')),
});

export type RouteInput = z.input<typeof routeSchema>;
export type RoutePayload = z.output<typeof routeSchema>;

/**
 * MODIFIER UN CIRCUIT EN BROUILLON — D-173 (lot B, point B2).
 *
 * Un `PATCH` : un champ absent n'est pas touché. Le serveur refuse un circuit
 * parti (`route-not-draft`) et, tant que le brouillon porte des colis ou des
 * bons, un changement de ville ou de type (`route-not-empty`) : ils y ont été
 * mis pour cette ville et ce type.
 */
export const routeUpdateSchema = z
  .object({
    cityId: z.string().min(1, 'required').optional(),
    courierId: z.string().min(1, 'required').optional(),
    kind: z.enum(ROUTE_KINDS).optional(),
    exitDate: z.string().trim().optional().or(z.literal('')),
  })
  .refine((value) => Object.values(value).some((field) => field !== undefined), {
    message: 'required',
  });

export type RouteUpdatePayload = z.output<typeof routeUpdateSchema>;

/**
 * Ajout de colis, en LOT.
 *
 * Un circuit se compose en cochant vingt lignes, pas en vingt requêtes : le
 * legacy ajoutait colis par colis, et une tournée à moitié composée après une
 * coupure réseau n'est pas un état qu'on veut expliquer à un livreur.
 */
export const routeParcelsSchema = z
  .object({
    // Des ids cochés, OU « tous ceux de la recherche » (chantier « Plafond de
    // 200 », `selection.ts`) ; plus de borne cachée à 200.
    parcelIds: optionalIds,
    ...selectionByCriteria,
    /** Les filtres de quartier et de ville (lot F) de la modale, rejoués avec `all`. */
    district: z.string().trim().max(120).optional(),
    cityId: z.string().trim().max(64).optional(),
  })
  .refine((value) => hasSelection({ all: value.all, ids: value.parcelIds }), {
    message: 'required',
    path: ['parcelIds'],
  });

export type RouteParcelsPayload = z.output<typeof routeParcelsSchema>;

/**
 * Ajout de BONS à un circuit de ramassage, en lot — retouches Anass, point 5.
 *
 * Même forme et même borne que les colis : un bon est une poignée sur un
 * paquet de colis, et l'écran en coche plusieurs d'un coup. Les colis de
 * chaque bon entrent dans le circuit avec lui — c'est le serveur qui les y
 * met, pas l'écran.
 */
export const routeNotesSchema = z
  .object({
    noteIds: optionalIds,
    ...selectionByCriteria,
  })
  .refine((value) => hasSelection({ all: value.all, ids: value.noteIds }), {
    message: 'required',
    path: ['noteIds'],
  });

export type RouteNotesPayload = z.output<typeof routeNotesSchema>;

/**
 * LE LIVREUR ATTESTE AVOIR PRIS UN BON — retouches Anass, point 5.
 *
 * « Validation au choix : par scan (scan des colis du bon, contrôle qu'ils
 * appartiennent au bon, alerte sinon) ou manuellement (cocher les colis). »
 *
 * Les deux gestes arrivent ici sous la même forme — une liste de colis — parce
 * que c'est le même fait : voici ce que j'ai en main. Ce qui les distingue est
 * l'écran, pas le contrat. En faire deux routes aurait dédoublé le contrôle
 * d'appartenance, qui est justement ce qui compte.
 *
 * `parcelIds` VIDE est refusé : attester d'un passage sans rien ramener n'est
 * pas un ramassage, c'est une course blanche — et elle se rapporte colis par
 * colis, avec son motif.
 */
/**
 * Un bon ne se borne pas à deux cents colis (BR-2609-0009 en portait 199) :
 * la borne est une garde contre un corps absurde, pas une règle métier.
 */
const NOTE_PARCELS_MAX = SELECTION_MAX;

export const routeNotePickupSchema = z.object({
  parcelIds: z.array(z.string().min(1)).min(1, 'required').max(NOTE_PARCELS_MAX, 'too-many'),
  /**
   * VALIDATION PARTIELLE — Recette 5, points 5 et 7, validé par Anass. Vrai :
   * les colis du bon NON cochés sont retirés du bon et supprimés logiquement
   * (« Les 50 non cochés seront retirés du bon »). Absent : la règle d'avant,
   * les autres restent où ils sont — une appli encore sur l'OTA précédente ne
   * retire rien sans l'avoir demandé.
   */
  removeUnpicked: z.boolean().optional(),
});

/**
 * VALIDER UN BON, en partie si besoin — Recette 5, point 7. Sans corps : le bon
 * entier, comme avant. Avec `parcelIds` : seuls ceux-là sont validés, les
 * autres sont retirés du bon (et supprimés logiquement).
 */
export const deliveryNoteValidateSchema = z.object({
  parcelIds: z
    .array(z.string().min(1))
    .min(1, 'required')
    .max(NOTE_PARCELS_MAX, 'too-many')
    .optional(),
});

export type DeliveryNoteValidatePayload = z.output<typeof deliveryNoteValidateSchema>;

export type RouteNotePickupPayload = z.output<typeof routeNotePickupSchema>;

/**
 * CLÔTURER UN CIRCUIT — chantier « Clôture d'un circuit de ramassage avec des
 * colis non ramassés » (D-145). Sur un RAMASSAGE, `removeUnpicked` confirme le
 * retrait des colis non ramassés de leur bon (non remis par le vendeur), comme
 * la validation partielle (D-139) : ils restent « En attente de ramassage »,
 * jamais supprimés (D-240). Absent : la clôture refuse et les nomme.
 */
export const routeCloseSchema = z
  .object({ removeUnpicked: z.boolean().optional() })
  .optional()
  .default({});

export type RouteClosePayload = z.output<typeof routeCloseSchema>;

/**
 * Confirmation d'un colis par le livreur, avec son issue.
 *
 * `statusId` absent = simple attestation « je l'ai en main » (le pivot legacy
 * `boxe_colis.confirmed`). Renseigné, il clôt le sort du colis : livré,
 * refusé, injoignable, reporté… La liste des statuts acceptables n'est pas
 * écrite ici mais dérivée de `transitions.ts` — une seule source.
 */
export const routeConfirmSchema = z.object({
  confirmed: z.boolean().default(true),
  statusId: z.number().int().positive().optional(),
  /** Reporté (14) : la date convenue avec le destinataire. */
  scheduledAt: z.string().trim().optional().or(z.literal('')),
  comment: optionalText(255),

  /**
   * Ce que le livreur a RÉELLEMENT encaissé, en dirhams — M1, D-074.
   *
   * Absent : le serveur calcule le dû (§3.8), ce qu'il faisait déjà. Présent :
   * c'est ce chiffre qui est figé, parce qu'un encaissement partiel arrive et
   * que `RouteParcel.codCollected` dit depuis toujours « montant réellement
   * encaissé ».
   *
   * Ce n'est pas un trou : la remise de fonds compare l'attendu au déclaré et
   * exige un motif à tout écart (C11 bloc 2). Un livreur qui sous-déclare ici
   * se retrouve en écart au comptoir, devant quelqu'un.
   */
  collectedAmount: z.number().min(0, 'negative').max(1_000_000, 'amount-too-large').optional(),

  /**
   * Où le livreur se trouvait. FACULTATIF, et il le reste.
   *
   * L'application fonctionne si la permission est refusée : une position
   * manquante ne doit pas empêcher de rapporter une livraison.
   */
  latitude: z.number().min(-90).max(90).optional(),
  longitude: z.number().min(-180).max(180).optional(),

  /**
   * LE PAIEMENT MIXTE — chantier « Finance », point 8.
   *
   * **Le livreur ne saisit QUE le chèque.** Les espèces sont la différence, et
   * c'est l'écran qui l'affiche en temps réel : lui faire saisir les deux
   * aurait invité la faute d'addition au pas de porte, une main sur le
   * téléphone et l'autre sur un carton.
   *
   * Absent, le paiement est intégralement en espèces — c'est ce que le terrain
   * fait dans l'immense majorité des cas, et le dire par l'absence évite un
   * champ à zéro sur chaque livraison.
   */
  chequeAmount: z.number().min(0, 'negative').max(1_000_000, 'amount-too-large').optional(),
  chequeNumber: z.string().trim().max(40).optional(),
  chequeBank: z.string().trim().max(120).optional(),
  /** La date portée SUR le chèque — un post-daté est encaissable plus tard. */
  chequeDate: z.string().trim().optional(),

  /**
   * « BL RÉCUPÉRÉ » — lot E, E1.1 (D-201). Coché dans le panneau « Livrer »
   * (D-156) d'un colis de Client Pro qui demande son BL : le BL est enregistré
   * avec la livraison, par la même requête et la même file hors ligne. Ignoré
   * pour toute autre issue que « Livré ».
   */
  blCollected: z.boolean().optional(),
  blReference: z.string().trim().max(60).optional(),
});

export type RouteConfirmInput = z.input<typeof routeConfirmSchema>;
export type RouteConfirmPayload = z.output<typeof routeConfirmSchema>;

/**
 * LE MÊME RÉSULTAT POUR PLUSIEURS COLIS D'UN CIRCUIT — chantier « Sélection en
 * masse sur les circuits » (D-146). Ramassage : « ramassés par le livreur »
 * (18). Livraison : un résultat commun (Livré, Reporté, Refusé…). « Livré » en
 * masse encaisse le COD de chaque colis EN ESPÈCES : pas de chèque, pas de
 * montant saisi — un colis payé par chèque se traite seul.
 */
export const routeBulkConfirmSchema = z.object({
  parcelIds: z.array(z.string().min(1)).min(1, 'required').max(SELECTION_MAX, 'too-many'),
  statusId: z.number().int().positive(),
  scheduledAt: z.string().trim().optional().or(z.literal('')),
  comment: optionalText(255),
});

export type RouteBulkConfirmPayload = z.output<typeof routeBulkConfirmSchema>;

/** Ce que l'action en masse a fait : le nombre de colis, et l'espèce attendue. */
export interface RouteBulkConfirmResult {
  count: number;
  /** Livré : le total des espèces à encaisser, en centimes. */
  cashExpected: number;
}

/**
 * TRANSFÉRER DES COLIS À UN AUTRE LIVREUR — lot I, I1 (D-211). Depuis la fiche
 * colis (un colis) ou la page circuit (les cochés). Le motif est facultatif ;
 * le serveur relit chaque colis et refuse ce qui n'est pas transférable.
 *
 * Lot L, L1 (D-219) : un colis qui ne va dans aucune ville de B y change de
 * destination. `destinationCityId` est la ville de B choisie quand il en a
 * plusieurs ; une seule, elle est prise d'office ; aucune choisie parmi
 * plusieurs : 409 `transfer-city-required` avec la liste (`cities`).
 *
 * Lot N (D-223) : pour UN colis, une ville choisie autre que sa destination
 * la change, même si B couvrait l'ancienne ; en masse, la règle D-219 tient.
 */
export const parcelTransferSchema = z.object({
  parcelIds: z.array(z.string().min(1)).min(1, 'required').max(SELECTION_MAX, 'too-many'),
  courierId: z.string().min(1, 'required'),
  reason: optionalText(255),
  destinationCityId: z.string().min(1).optional(),
});

/** Une ville de B proposée pour la nouvelle destination (lot L, D-219). */
export interface TransferCityChoice {
  id: string;
  name: string;
}

/**
 * LE CHAMP « VILLE DE DESTINATION » DE LA BOÎTE — lot N (D-223, précise D-219).
 * Demandé dès que l'admin choisit B, avant tout transfert : la boîte montre
 * toujours le champ, le serveur reste seul juge des villes de B.
 */
export const parcelTransferOptionsSchema = parcelTransferSchema.pick({
  parcelIds: true,
  courierId: true,
});
export type ParcelTransferOptionsPayload = z.output<typeof parcelTransferOptionsSchema>;

export interface ParcelTransferOptions {
  /**
   * Les villes COCHÉES de B (sa grille, lot F), ordre alphabétique — plus la
   * destination actuelle quand B la couvre sans l'avoir cochée (ville de sa
   * fiche) : « garder » se choisit toujours dans la liste.
   */
  cities: TransferCityChoice[];
  /** À pré-remplir : la destination de TOUS les colis quand B la couvre ; sinon `null`. */
  keepCityId: string | null;
  /** Combien de colis vont hors des villes de B : ils changeront de destination. */
  toReroute: number;
  count: number;
  /** B n'a aucune ville cochée (sa grille est vide). */
  noCity: boolean;
}

export type ParcelTransferInput = z.input<typeof parcelTransferSchema>;
export type ParcelTransferPayload = z.output<typeof parcelTransferSchema>;

/** Ce que le transfert a fait : combien, vers quel circuit, combien déjà chez B. */
export interface ParcelTransferResult {
  count: number;
  /** Déjà sur un circuit ouvert de B : rien refait (relancer ne refait rien). */
  skipped: number;
  routeCode: string | null;
  /** Vrai quand le transfert a dû ouvrir un brouillon pour B. */
  routeCreated: boolean;
  /**
   * Lot L (D-219) : combien de colis ont changé de destination, et vers où —
   * présents seulement quand il y en a eu (le transfert d'avant rend la même
   * réponse qu'avant).
   */
  rerouted?: number;
  reroutedCityName?: string;
}

/* --- Bons de ramassage et de dépôt ---------------------------------------- */

export const deliveryNoteSchema = z.object({
  /** `pickup` = on va chercher chez le client ; `dropoff` = il apporte. */
  kind: z.enum(['pickup', 'dropoff']).default('pickup'),
  clientId: z.string().min(1, 'required'),
  agencyId: z.string().min(1, 'required'),
  /** Ville où le geste a lieu. Vide = celle du client. */
  locationCityId: z.string().min(1).optional().or(z.literal('')),
});

export type DeliveryNoteInput = z.input<typeof deliveryNoteSchema>;
export type DeliveryNotePayload = z.output<typeof deliveryNoteSchema>;

export const deliveryNoteParcelsSchema = z
  .object({
    // Ids cochés, ou « tous les candidats de la recherche » (`selection.ts`).
    parcelIds: optionalIds,
    ...selectionByCriteria,
  })
  .refine((value) => hasSelection({ all: value.all, ids: value.parcelIds }), {
    message: 'required',
    path: ['parcelIds'],
  });

export type DeliveryNoteParcelsPayload = z.output<typeof deliveryNoteParcelsSchema>;

/* --- Étiquettes ----------------------------------------------------------- */

/**
 * DOMAIN.md §3.5 — règle 10×10 : tout écran d'impression propose A4 ou 10×10,
 * **défaut 10×10**. La liste blanche est ici ; le legacy passait la valeur de
 * `$request` directement à mPDF.
 */
export const LABEL_FORMATS = ['10x10', 'a4'] as const;
export type LabelFormat = (typeof LABEL_FORMATS)[number];

export const labelQuerySchema = z.object({
  format: z.enum(LABEL_FORMATS).default('10x10'),
});

export type LabelQueryPayload = z.output<typeof labelQuerySchema>;

/**
 * LES ÉTIQUETTES D'UNE SÉLECTION — V2 vendeur bloc 3, spécification §4.
 *
 * Les codes arrivent en une chaîne séparée par des virgules plutôt qu'en corps
 * de requête : c'est un TÉLÉCHARGEMENT, donc un `GET`, donc une URL qu'un
 * navigateur suit sans JavaScript. Le corps aurait obligé à passer par
 * `fetch`, puis à fabriquer un `Blob` et un lien invisible — pour le même
 * résultat.
 *
 * **Deux cents au plus.** Au-delà, la planche met trop longtemps à se rendre
 * et l'URL approche la limite des serveurs. La spécification demande que les
 * téléchargements en masse « soient optimisés » (§9) ; la borne en fait partie,
 * et un refus explicite vaut mieux qu'un délai d'attente sans explication.
 */
export const labelBatchQuerySchema = z.object({
  codes: z
    .string()
    .trim()
    .min(1)
    .transform((value) =>
      value
        .split(',')
        .map((code) => code.trim())
        .filter((code) => code !== ''),
    )
    .refine((codes) => codes.length > 0, { error: 'no-codes' })
    .refine((codes) => codes.length <= 200, { error: 'too-many-codes' }),
  format: z.enum(LABEL_FORMATS).default('10x10'),
});

export type LabelBatchQueryPayload = z.output<typeof labelBatchQuerySchema>;

/** Rémunération de la course, quand elle est saisie à la clôture. */
export const courierFeeSchema = z.object({
  courierFee: madAmountSchema.nullable().default(null),
});
