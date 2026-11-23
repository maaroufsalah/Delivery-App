import { z } from 'zod';
import { DELIVERY_TARGETS, EXPECTED_PAYMENTS } from '../enums';
import { DISTRICT_NAME_MAX } from '../district-name';
import {
  normalizePhone,
  RECIPIENT_PHONES_JOIN,
  RECIPIENT_PHONES_MAX,
  splitPhones,
} from '../phone';

/**
 * Schémas de validation partagés — ils seront réutilisés tels quels par l'API
 * NestJS. DOMAIN.md §4 dette n°12 : le legacy n'avait aucune Form Request,
 * `traking_form` ayant même littéralement `$rules = []`.
 */

/**
 * LE TÉLÉPHONE EN SAISIE LIBRE — D-172 (Anass, lot B, B1), amendé par D-220.
 *
 * Le vendeur saisit le numéro qu'il veut : un numéro étranger, un fixe, un
 * indicatif à rallonge. Seul contrôle, des CHIFFRES et des « + », sans longueur
 * imposée ni format marocain. Le « + » reste composable par le bouton Appeler
 * (`tel:`).
 *
 * Lot L (D-220) : les séparateurs (espaces, tirets, points, parenthèses…) sont
 * retirés AVANT ce contrôle par `normalizePhone`, et c'est la valeur nettoyée
 * qui est gardée. Une lettre est toujours refusée.
 */
export const PHONE_PATTERN = /^[0-9+]+$/;

export const phoneSchema = z
  .string()
  .transform(normalizePhone)
  .pipe(z.string().regex(PHONE_PATTERN, 'invalid-phone'));

/**
 * LE TÉLÉPHONE DU DESTINATAIRE : UN À TROIS NUMÉROS — D-244. Même champ, même
 * règle par numéro (`PHONE_PATTERN`) ; la valeur gardée est normalisée,
 * `num1 / num2 / num3`. Les téléphones de COMPTE restent à `phoneSchema`.
 */
export const recipientPhonesSchema = z
  .string()
  .transform((value) => splitPhones(value))
  .pipe(
    z
      .array(z.string().regex(PHONE_PATTERN, 'invalid-phone'))
      .min(1, 'invalid-phone')
      .max(RECIPIENT_PHONES_MAX, 'too-many-phones'),
  )
  .transform((phones) => phones.join(RECIPIENT_PHONES_JOIN));

/**
 * L'ancien nom, gardé pour l'application mobile qui l'importe encore : c'est la
 * MÊME règle (D-172), il n'y a plus de contrôle marocain.
 */
export const moroccanPhoneSchema = phoneSchema;

/*
 * LES COMPTES AUSSI — lot D, D1 (D-188). Le numéro marocain normalisé de C13
 * (`normalizedMoroccanPhoneSchema`, qui réécrivait `+212…` en `0…` et retirait
 * espaces, points et tirets) a disparu : comptes, profil, inscription et fiche
 * du vendeur passent par `phoneSchema`, et rien ne reformate plus un numéro en
 * silence.
 */

/** Montant saisi en dirhams, converti en centimes entiers à la sortie. */
export const madAmountSchema = z
  .number({ error: 'required' })
  .min(0, 'negative-amount')
  .max(1_000_000, 'amount-too-large');

/**
 * L'ADRESSE DE RAMASSAGE : non vide, rien de plus — recette SpeedyTrans 4,
 * point 1 (D-131).
 *
 * Elle exigeait cinq caractères, quand la fiche du vendeur, d'où l'écran la
 * RECOPIE, n'en exige aucun (`clientSchema`, `merchantCompanySchema`). Une
 * fiche « Casa » rendait donc la création impossible, et sans un mot : le
 * champ refusé était masqué. La fiche fait foi ; le colis n'a pas à être plus
 * exigeant qu'elle. Le web, l'appli et l'import lisent ce même seuil.
 */
export const PICKUP_ADDRESS_MIN = 1;

/**
 * L'ADRESSE DE LIVRAISON : OBLIGATOIRE, SANS LONGUEUR MINIMALE — recette 6,
 * point 6 (décision du client).
 *
 * Cinq caractères étaient exigés : « ae », « casa » étaient refusés à
 * l'import comme à la saisie (« Adresse absente ou trop courte »). Seule une
 * adresse VIDE l'est désormais — web, appli, import, API lisent ce seuil.
 */
export const DELIVERY_ADDRESS_MIN = 1;

/**
 * Les autres textes saisis par le vendeur (destinataire, contenu, articles
 * d'un échange) : obligatoires, sans longueur minimale non plus. Le téléphone
 * garde son contrôle de FORMAT, qui n'est pas une longueur arbitraire.
 */
export const PARCEL_TEXT_MIN = 1;

/** Le nombre de cartons d'un colis — lot G (D-208) : un entier de 1 à 999. */
export const PARCEL_QUANTITY_MAX = 999;

export const parcelQuantitySchema = z.coerce
  .number({ error: 'required' })
  .int('not-an-integer')
  .min(1, 'quantity-min')
  .max(PARCEL_QUANTITY_MAX, 'quantity-max');

export const createParcelSchema = z.object({
  clientId: z.string().min(1, 'required'),
  /**
   * L'AGENCE DE PRISE EN CHARGE — retouches Anass, point 4.
   *
   * Facultative, et c'est la moitié de la règle. Vide, le serveur la déduit :
   * la fiche du client d'abord — c'est son agence de rattachement, déduite
   * elle-même de sa ville — puis l'agence qui dessert la ville de départ,
   * puis le siège. Le formulaire de l'administration la PRÉREMPLIT depuis le
   * client choisi et laisse en changer : un colis part parfois d'ailleurs.
   *
   * Le vendeur, lui, ne la voit pas : elle n'a qu'une réponse possible, la
   * sienne, et le serveur borne déjà la création à sa portée (D-082).
   */
  agencyId: z.string().min(1).optional().or(z.literal('')),
  externalCode: z.string().trim().max(64).optional().or(z.literal('')),

  description: z.string().trim().min(PARCEL_TEXT_MIN, 'required').max(255),
  recipientName: z.string().trim().min(PARCEL_TEXT_MIN, 'required').max(120),
  // Un à trois numéros dans le même champ (D-244).
  recipientPhone: recipientPhonesSchema,

  originCityId: z.string().min(1, 'required'),
  pickupAddress: z.string().trim().min(PICKUP_ADDRESS_MIN, 'required').max(255),
  /**
   * DOMAIN.md §2.3 — détermine le statut initial : le transporteur passe
   * chercher le colis (1) ou le client le dépose en agence (2).
   */
  pickupAtSender: z.boolean().default(true),

  destinationCityId: z.string().min(1, 'required'),
  /**
   * Le quartier du référentiel. Facultatif, et le serveur le DÉDUIT du texte
   * ci-dessous quand celui-ci correspond à un quartier de la ville.
   * Conservé pour les appelants d'avant la saisie libre (app mobile).
   */
  destinationDistrictId: z.string().optional(),
  /**
   * Le quartier en TEXTE LIBRE, facultatif, jamais bloquant — chantier
   * « Quartier en saisie libre ». Voir `district-name.ts`.
   */
  destinationDistrictName: z.string().trim().max(DISTRICT_NAME_MAX).optional(),
  deliveryTarget: z.enum(DELIVERY_TARGETS).default('district'),
  deliveryAddress: z.string().trim().min(DELIVERY_ADDRESS_MIN, 'required').max(255),

  codAmount: madAmountSchema,
  /**
   * DOMAIN.md §0.3 — frais inclus dans le COD. N'est plus saisi (dernières
   * corrections, point 3) : accepté pour ne casser aucun appelant, mais
   * l'API écrit TOUJOURS `true` à la création.
   */
  feeIncluded: z.boolean().default(true),
  alreadyPaid: z.boolean().default(false),

  openOnDelivery: z.boolean().default(false),
  fragile: z.boolean().default(false),
  /**
   * « BL à récupérer lors de la livraison ? » — chantier « Retours, BL
   * Clients Pro », point 5.
   *
   * `false` par défaut, et **l'API le REFUSE si l'e-commerçant n'est pas
   * Pro** (D-099). Le schéma ne peut pas porter cette règle : il ne connaît
   * pas le client, seulement son identifiant. Elle vit donc dans le service,
   * qui lit la fiche — et l'écran, lui, ne pose même pas la question.
   */
  blRequested: z.boolean().default(false),
  /**
   * LE NOMBRE DE CARTONS — lot G (D-208). 1 par défaut ; plus de 1 pour tout
   * client, normal ou Pro, depuis D-264 (16/11/2026).
   */
  quantity: parcelQuantitySchema.default(1),
  /**
   * « Paiement attendu : Espèces / Chèque » — recette SpeedyTrans 3, point 3.
   *
   * Même règle que le BL : posée pour un client Pro seulement, Espèces si rien
   * n'est dit. L'API refuse « Chèque » pour un client qui n'est pas Pro, et
   * n'enregistre rien pour lui.
   */
  expectedPayment: z.enum(EXPECTED_PAYMENTS).optional(),
  scheduledAt: z.string().optional(),
});

/**
 * LE VENDEUR CORRIGE SON COLIS — recette SpeedyTrans 3, point 3.
 *
 * Exactement ce qu'il a saisi à la création, et rien d'autre : destinataire,
 * téléphone, ville, quartier, adresse, montant, désignation, et pour un client
 * Pro la question BL et le paiement attendu. L'API ne l'accepte que tant que
 * le colis n'est pas ramassé (`isEditableBySeller`), et recalcule les frais
 * si la ville change.
 */
export const sellerParcelUpdateSchema = createParcelSchema
  .pick({
    description: true,
    recipientName: true,
    recipientPhone: true,
    destinationCityId: true,
    destinationDistrictName: true,
    deliveryAddress: true,
    codAmount: true,
  })
  .extend({
    blRequested: z.boolean().optional(),
    // Lot G (D-208) : modifiable tant que le colis n’est pas ramassé, comme le reste.
    quantity: parcelQuantitySchema.optional(),
    expectedPayment: z.enum(EXPECTED_PAYMENTS).optional(),
  });

export type SellerParcelUpdateInput = z.input<typeof sellerParcelUpdateSchema>;
export type SellerParcelUpdatePayload = z.output<typeof sellerParcelUpdateSchema>;

/**
 * L'ADMIN BASCULE ESPÈCES ↔ CHÈQUE après ramassage — point 3. Le client
 * final paie autrement que prévu : le motif est exigé, il part au journal.
 */
export const expectedPaymentChangeSchema = z.object({
  expectedPayment: z.enum(EXPECTED_PAYMENTS),
  // Obligatoire, sans longueur minimale (remarques Badr 12/11) : seul le vide est refusé.
  reason: z.string().trim().min(1, 'required').max(255),
});

export type ExpectedPaymentChangePayload = z.output<typeof expectedPaymentChangeSchema>;
/*
 * LE QUARTIER n'est JAMAIS obligatoire — chantier « Quartier en saisie libre »,
 * qui lève la règle de « Purge de speedy » (obligatoire dans une ville qui a
 * des quartiers). Le rapprochement avec le référentiel vit dans
 * `resolveDistrict` (API) ; il renseigne, il ne refuse pas.
 */

export type CreateParcelInput = z.input<typeof createParcelSchema>;
export type CreateParcelPayload = z.output<typeof createParcelSchema>;

export const parcelFiltersSchema = z.object({
  search: z.string().trim().optional(),
  statusId: z.coerce.number().int().positive().optional(),
  statusGroup: z.string().optional(),
  clientId: z.string().optional(),
  courierId: z.string().optional(),
  destinationCityId: z.string().optional(),
  page: z.coerce.number().int().min(1).default(1),
  pageSize: z.coerce.number().int().min(5).max(100).default(10),
});

export type ParcelFiltersInput = z.input<typeof parcelFiltersSchema>;

/**
 * Changement de statut — DOMAIN.md §1.11, journal append-only.
 * La validité de la transition est vérifiée à part (`transitions.ts`) : ce
 * schéma ne connaît pas le statut courant.
 */
export const changeStatusSchema = z.object({
  statusId: z.coerce.number().int().positive({ error: 'required' }),
  comment: z.string().trim().max(500).optional(),
  scheduledDate: z.string().optional(),
  // D-246 : le livreur qui reçoit le colis, exigé PAR LE SERVEUR pour « En
  // cours de livraison » (le schéma ne connaît pas la règle).
  courierId: z.string().trim().optional(),
  // La nouvelle destination quand ce livreur ne couvre pas la ville du colis.
  destinationCityId: z.string().trim().optional(),
});

export type ChangeStatusInput = z.input<typeof changeStatusSchema>;
export type ChangeStatusPayload = z.output<typeof changeStatusSchema>;

/**
 * Contestation d'un statut terminal. Le motif est OBLIGATOIRE : c'est la
 * contrepartie du droit de sortir d'un état terminal (DOMAIN.md §2.2, les 39
 * transitions `4 → 6` observées en prod, jusqu'ici sans justification).
 */
export const disputeStatusSchema = z.object({
  statusId: z.coerce.number().int().positive({ error: 'required' }),
  comment: z.string().trim().min(1, 'required').max(500),
  // D-246 : même règle que le changement de statut.
  courierId: z.string().trim().optional(),
  destinationCityId: z.string().trim().optional(),
});

export type DisputeStatusInput = z.input<typeof disputeStatusSchema>;
export type DisputeStatusPayload = z.output<typeof disputeStatusSchema>;

/**
 * L'ADMIN REVOIT UN STATUT DE TERRAIN — D-245. Le geste lui-même : la cible et
 * le commentaire, OBLIGATOIRE (seul le vide est refusé). La sélection des colis
 * (cochés ou toute la liste filtrée) s'y ajoute côté API, comme pour les
 * étiquettes.
 */
export const ADMIN_FIELD_STATUS_MAX = 300;

export const adminFieldStatusChangeSchema = z.object({
  statusId: z.coerce.number().int().positive({ error: 'required' }),
  comment: z.string().trim().min(1, 'required').max(500),
  /** Reporté (14) : la date convenue, facultative. */
  scheduledDate: z.string().optional(),
});

/** Ce que le geste rend : colis par colis, ce qui a changé et ce qui a été refusé. */
export interface AdminFieldStatusResult {
  changed: Array<{ code: string; from: number; to: number }>;
  refused: Array<{ code: string; reason: string; message: string }>;
}

/**
 * « ANNULER LA LIVRAISON » — lot I, I2 (D-210). Un « Livré » posé par erreur,
 * défait par un administrateur : motif obligatoire, sur le modèle du retour
 * arrière d'un BL (D-204). Sans longueur minimale depuis le 12/11 (remarques
 * Badr) : seul le vide est refusé.
 */
export const cancelDeliverySchema = z.object({
  reason: z.string().trim().min(1, 'required').max(255, 'too-long'),
});

export type CancelDeliveryInput = z.input<typeof cancelDeliverySchema>;
export type CancelDeliveryPayload = z.output<typeof cancelDeliverySchema>;

/** Ce que l'annulation a défait — la fiche le dit au toast. */
export interface CancelDeliveryResult {
  code: string;
  /** Le statut rendu au colis (13, ou 16 si son circuit est clos pour de bon). */
  statusId: number;
  routeCode: string | null;
  /** Vrai quand le circuit, clos automatiquement, a été rouvert (D-155). */
  routeReopened: boolean;
  /** Le RF- brouillon d'où la ligne est sortie, s'il y en avait un. */
  remittanceCode: string | null;
  /** Vrai quand un BL encore chez le livreur a été retiré. */
  slipRemoved: boolean;
}

/**
 * Modification des FRAIS d'un colis — C11 bloc 4, D-063.
 *
 * Le motif est OBLIGATOIRE et fait dix caractères au moins, comme celui d'une
 * contestation de statut : même exigence, même raison. Un montant qui change
 * sans justification est exactement ce que le legacy laissait faire, et une
 * contestation d'e-commerçant se tranchait alors à la parole.
 *
 * Les deux montants sont FACULTATIFS mais il en faut au moins un : on vient
 * corriger les frais de livraison, ceux d'échec, ou les deux. Un appel qui ne
 * change rien mais écrit un motif salirait l'historique qu'il prétend tenir.
 *
 * Ce que ce schéma ne porte PAS, et ne portera jamais :
 * `parcel.courierCommission`. Elle est figée à la course (D-059) et ne se
 * renegocie pas parce qu'on a corrigé ce que paie le client — le livreur a
 * fait le trajet qu'il a fait.
 */
export const editParcelFeesSchema = z
  .object({
    /**
     * LE PRIX DU COLIS — chantier « Finance », point 2.
     *
     * Le montant à encaisser au pas de porte se corrige comme les frais, avec
     * le même motif obligatoire. C'est la demande de la mission, et elle
     * s'entend : une erreur de saisie sur un COD se découvre le plus souvent
     * quand le livreur est déjà devant la porte.
     */
    codAmount: madAmountSchema.optional(),
    deliveryFee: madAmountSchema.optional(),
    failureFee: madAmountSchema.optional(),
    /** Emballage, attente, seconde présentation — point 2. */
    otherFees: madAmountSchema.optional(),
    reason: z.string().trim().min(1, 'required').max(500),
  })
  .refine(
    (value) =>
      value.codAmount !== undefined ||
      value.deliveryFee !== undefined ||
      value.failureFee !== undefined ||
      value.otherFees !== undefined,
    {
      error: 'no-fee-changed',
      path: ['deliveryFee'],
    },
  );

export type EditParcelFeesInput = z.input<typeof editParcelFeesSchema>;
export type EditParcelFeesPayload = z.output<typeof editParcelFeesSchema>;

/** Note interne libre — la moitié « commentaire » de `colis_trakings`. */
export const addParcelNoteSchema = z.object({
  body: z.string().trim().min(1, 'required').max(1000),
});

export type AddParcelNoteInput = z.infer<typeof addParcelNoteSchema>;

/* -------------------------------------------------------------------------- */
/* Import Excel — C7 bloc 1                                                   */
/* -------------------------------------------------------------------------- */

/**
 * Plafond de lignes par classeur.
 *
 * C'est une règle du DOMAINE, pas une politique commerciale : au-delà, chaque
 * ligne consomme un rang de séquence, un devis et un événement dans la MÊME
 * transaction, et la transaction finit par dépasser le timeout de Postgres.
 * Le chiffre est donc une borne technique assumée, pas un `Setting` (D-019).
 */
export const PARCEL_IMPORT_MAX_ROWS = 2000;

/** Les douze cellules d'une ligne, brutes. Vides admises : c'est la validation qui tranche. */
export const parcelImportRowSchema = z.object({
  externalCode: z.string().trim().max(64).default(''),
  recipientName: z.string().trim().max(160).default(''),
  // 120 : trois numéros internationaux et leurs séparateurs tiennent (D-244).
  recipientPhone: z.string().trim().max(120).default(''),
  city: z.string().trim().max(120).default(''),
  district: z.string().trim().max(120).default(''),
  address: z.string().trim().max(255).default(''),
  cod: z.string().trim().max(32).default(''),
  description: z.string().trim().max(255).default(''),
  openOnDelivery: z.string().trim().max(16).default(''),
  fragile: z.string().trim().max(16).default(''),
  notes: z.string().trim().max(500).default(''),
  // Lot G (D-208) : absente ou vide = 1 carton ; la validation tranche (entier, Pro).
  // Facultative : une relecture d'avant le lot G reste un corps valide.
  quantity: z.string().trim().max(16).optional(),
  /*
   * Lot J (J4) : le numéro de la ligne DANS LE FICHIER, renvoyé par l'écran
   * avec la ligne corrigée — une ligne vide au milieu ne décale plus rien.
   * Absent (ancien corps) : le rang dans la liste, comme avant.
   */
  row: z.number().int().min(2).max(1_000_000).optional(),
});

export type ParcelImportRowInput = z.infer<typeof parcelImportRowSchema>;

const parcelImportBase = z.object({
  /** Classeur en base64 — D-022, le fichier voyage dans un corps JSON. */
  content: z.string().max(4_000_000, 'file-too-large').optional(),
  rows: z.array(parcelImportRowSchema).max(PARCEL_IMPORT_MAX_ROWS, 'too-many-rows').optional(),
  filename: z.string().trim().max(200).optional(),
  /** Ignoré quand le compte est un e-commerçant : il importe pour lui-même. */
  clientId: z.string().optional(),
});

/** Exactement une source : le classeur, ou les lignes. Jamais les deux, jamais aucune. */
const exactlyOneSource = (value: { content?: string; rows?: unknown[] }): boolean =>
  Boolean(value.content) !== Boolean(value.rows);

/**
 * Source d'un aperçu : un classeur, OU des lignes déjà corrigées à l'écran.
 *
 * Les deux entrées mènent au même validateur. C'est ce qui permet la correction
 * inline sans redemander le fichier : l'opérateur édite une cellule, l'écran
 * renvoie les lignes, et la réponse est calculée par le même code que la
 * première lecture. Un second chemin de validation aurait divergé.
 */
export const parcelImportSourceSchema = parcelImportBase.refine(exactlyOneSource, {
  path: ['content'],
  error: 'required',
});

export type ParcelImportSourceInput = z.input<typeof parcelImportSourceSchema>;
export type ParcelImportSourcePayload = z.output<typeof parcelImportSourceSchema>;

/**
 * Application. `all-or-nothing` est le DÉFAUT : un fichier partiellement
 * importé laisse un lot de colis dont personne ne sait où il s'est arrêté.
 * `valid-only` existe parce qu'un opérateur qui a 3 lignes fausses sur 900
 * préfère parfois traiter les 897 — mais il doit le demander.
 */
export const parcelImportApplySchema = parcelImportBase
  .extend({ mode: z.enum(['all-or-nothing', 'valid-only']).default('all-or-nothing') })
  .refine(exactlyOneSource, { path: ['content'], error: 'required' });

export type ParcelImportApplyInput = z.input<typeof parcelImportApplySchema>;
export type ParcelImportApplyPayload = z.output<typeof parcelImportApplySchema>;

/**
 * ÉDITION du drapeau BL sur un colis existant — point 5, « à la création /
 * édition d'un colis ».
 *
 * Une route à elle seule, et non un `PATCH` général du colis : il n'en existe
 * pas, et en ouvrir un pour ce booléen aurait mis la description, le montant et
 * l'adresse à portée du même geste. Les frais ont déjà leur route séparée, pour
 * la même raison (C11, D-063).
 */
export const parcelBlRequestSchema = z.object({
  blRequested: z.boolean(),
});

export type ParcelBlRequestPayload = z.output<typeof parcelBlRequestSchema>;

/* -------------------------------------------------------------------------- */
/* Échange de colis — chantier « Finance », point 6                           */
/* -------------------------------------------------------------------------- */

/**
 * L'OUVERTURE d'un échange — point 6.
 *
 * **La destination est reprise du colis d'origine et MODIFIABLE.** Les trois
 * champs sont facultatifs : vides, le colis neuf repart à la même porte, ce qui
 * est le cas courant. Renseignés, ils décrivent la nouvelle adresse — le client
 * a déménagé, ou demande la livraison au bureau.
 *
 * **Le montant peut être NUL, et même négatif.** Un échange à somme nulle est
 * l'ordinaire ; un échange contre un article moins cher rend de l'argent. Zéro
 * n'est pas « rien » (D-101), et refuser le négatif aurait obligé l'agence à
 * saisir un remboursement ailleurs.
 */
export const exchangeCreateSchema = z.object({
  originalParcelCode: z.string().trim().min(1, 'required'),
  returnedProduct: z.string().trim().min(PARCEL_TEXT_MIN, 'required').max(255),
  newProduct: z.string().trim().min(PARCEL_TEXT_MIN, 'required').max(255),

  /** La nouvelle destination. Vide = celle du colis d'origine. */
  destinationCityId: z.string().trim().optional(),
  destinationDistrictId: z.string().trim().optional(),
  /** Quartier en texte libre — chantier « Quartier en saisie libre ». */
  destinationDistrictName: z.string().trim().max(DISTRICT_NAME_MAX).optional(),
  deliveryAddress: z.string().trim().max(255).optional(),
  recipientName: z.string().trim().max(120).optional(),
  recipientPhone: recipientPhonesSchema.optional().or(z.literal('')),

  /** En centimes. Négatif : c'est la société qui rend la différence. */
  amount: z.number().int().default(0),
  fees: z.number().int().min(0).default(0),

  courierId: z.string().trim().optional(),
  note: z.string().trim().max(500).optional(),
});

export type ExchangeCreateInput = z.input<typeof exchangeCreateSchema>;
export type ExchangeCreatePayload = z.output<typeof exchangeCreateSchema>;

/**
 * ÉCHANGER DEPUIS LE COLIS LIVRÉ — chantier « Étiquette orange », point 2.
 *
 * Le geste de Wimo, et ce n'est PAS `exchangeCreateSchema` : celui-là décrit
 * un dossier d'échange saisi au bureau, avec son livreur, ses produits et son
 * appoint. Celui-ci décrit ce que le VENDEUR remplit devant un colis déjà
 * livré : « par où revient l'ancien, d'où part le neuf, combien vaut-il ».
 *
 * Les deux coexistent et n'ont pas le même appelant. Les fondre aurait donné
 * un objet dont la moitié des champs est facultative selon qui appelle — la
 * forme qu'aucun typage ne rattrape.
 *
 * **Deux décisions, deux groupes de champs :**
 *
 *  - le COLIS DE RETOUR : où le vendeur récupère sa marchandise. En agence —
 *    il faut alors dire laquelle — ou chez lui.
 *  - le COLIS D'ÉCHANGE : d'où part le neuf. De l'adresse d'enlèvement
 *    habituelle, ou de l'adresse de retour qu'on vient de choisir.
 */
export const RETURN_TARGETS = ['agency', 'home'] as const;
export type ReturnTarget = (typeof RETURN_TARGETS)[number];

export const EXCHANGE_PICKUPS = ['initial', 'return-address'] as const;
export type ExchangePickup = (typeof EXCHANGE_PICKUPS)[number];

export const parcelExchangeSchema = z
  .object({
    /** Où le vendeur récupère ce qui revient. */
    returnTarget: z.enum(RETURN_TARGETS),
    /** L'agence de retrait — exigée quand `returnTarget` vaut `agency`. */
    returnAgencyId: z.string().trim().optional(),

    /** D'où part le colis neuf. */
    exchangePickup: z.enum(EXCHANGE_PICKUPS),

    /** Prix du colis neuf, en centimes. Zéro est un prix (D-101). */
    price: z.number().int().min(0).default(0),
    /** « Colis déjà payé » — rien à encaisser au pas de porte. */
    alreadyPaid: z.boolean().default(false),
    /** Ignoré : les frais sont toujours inclus (dernières corrections, point 3). */
    feeIncluded: z.boolean().default(true),

    /** Ce qui revient et ce qui repart, en clair. */
    returnedProduct: z.string().trim().max(255).optional(),
    newProduct: z.string().trim().max(255).optional(),
    note: z.string().trim().max(500).optional(),
  })
  .refine((value) => value.returnTarget !== 'agency' || Boolean(value.returnAgencyId), {
    /*
     * « Récupérer dans l'une de nos agences » sans dire laquelle n'est pas une
     * demande : le livreur ne saurait pas où déposer. Le contrôle vit ici et
     * pas seulement à l'écran — l'API s'appelle aussi sans écran.
     */
    path: ['returnAgencyId'],
    message: 'required',
  })
  .refine((value) => !(value.alreadyPaid && value.price > 0), {
    /*
     * LE PRIX SAISI EST LE COD DE L'ECH- — lot C, point C4 (D-178).
     * ECH-CAS110012610KEN : prix 20 ET « déjà payé » — le tableau lisait 20,
     * le téléphone, la caisse et la facture 0. Un colis déjà payé n'a rien à
     * encaisser : son prix est 0. La combinaison est refusée, ici, pour l'API
     * comme pour l'écran, plutôt que l'une des deux valeurs n'écrase l'autre
     * en silence.
     */
    path: ['price'],
    message: 'paid-with-price',
  });

export type ParcelExchangeInput = z.input<typeof parcelExchangeSchema>;
export type ParcelExchangePayload = z.output<typeof parcelExchangeSchema>;

/** Une transition d'échange — le motif est libre, la date vient du serveur. */
export const exchangeAdvanceSchema = z.object({
  comment: z.string().trim().max(500).optional(),
});

export type ExchangeAdvancePayload = z.output<typeof exchangeAdvanceSchema>;

/* -------------------------------------------------------------------------- */
/* Changement de destination — chantier « Finance », point 7                  */
/* -------------------------------------------------------------------------- */

/**
 * CHANGER LA DESTINATION D'UN COLIS — point 7.
 *
 * **Le motif est OBLIGATOIRE**, et dix caractères au moins — la même exigence
 * que pour un changement de frais (D-063) et pour la même raison : une adresse
 * qui change sans justification est indéfendable devant l'e-commerçant dont le
 * colis a fait deux jours de plus.
 *
 * **Tous les champs sont facultatifs, mais il en faut un.** On corrige souvent
 * un seul élément — un numéro de téléphone mal relevé, un étage manquant — et
 * imposer de tout ressaisir aurait fabriqué des erreurs de recopie.
 */
export const changeDestinationSchema = z
  .object({
    recipientName: z.string().trim().min(PARCEL_TEXT_MIN, 'required').max(120).optional(),
    recipientPhone: recipientPhonesSchema.optional(),
    deliveryAddress: z.string().trim().min(DELIVERY_ADDRESS_MIN, 'required').max(255).optional(),
    destinationCityId: z.string().trim().optional(),
    destinationDistrictId: z.string().trim().optional(),
    /**
     * Quartier en texte libre. `''` le RETIRE ; absent, il ne change pas —
     * chantier « Quartier en saisie libre ».
     */
    destinationDistrictName: z.string().trim().max(DISTRICT_NAME_MAX).optional(),
    /** Instructions de livraison — « sonner chez le voisin », « après 18 h ». */
    instructions: z.string().trim().max(500).optional(),
    reason: z.string().trim().min(1, 'required').max(500),
  })
  .refine(
    (value) =>
      value.recipientName !== undefined ||
      value.recipientPhone !== undefined ||
      value.deliveryAddress !== undefined ||
      value.destinationCityId !== undefined ||
      value.destinationDistrictId !== undefined ||
      value.destinationDistrictName !== undefined ||
      value.instructions !== undefined,
    { error: 'nothing-changed', path: ['deliveryAddress'] },
  );

export type ChangeDestinationInput = z.input<typeof changeDestinationSchema>;
export type ChangeDestinationPayload = z.output<typeof changeDestinationSchema>;
