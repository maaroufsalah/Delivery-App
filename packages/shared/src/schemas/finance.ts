import { z } from 'zod';
import { PAY_PERIODS, PAYMENT_METHODS } from '../enums';
import { SELECTION_MAX } from './selection';
import { PRO_VAT_RATES, type ProVatRate } from '../utils/money';

/**
 * Schémas d'écriture du menu FACTURATION, partagés par les formulaires web et
 * l'API — même convention que les retours et le référentiel.
 *
 * Ce qui n'est PAS ici : les montants d'une ligne de facture. Ils ne se
 * saisissent pas. Ils sont CALCULÉS depuis le colis au moment de l'émission,
 * puis gelés (D-036). Un champ de saisie les rendrait négociables ligne par
 * ligne — c'est ce que le legacy laissait faire, et c'est pourquoi ses factures
 * changeaient de total entre deux affichages (§4 dette n°17).
 *
 * Le seul montant saisi est celui d'un RÈGLEMENT : là, c'est un fait extérieur
 * — un virement reçu, une espèce comptée — et personne d'autre que l'opérateur
 * ne le connaît.
 */

const optionalText = (max: number) => z.string().trim().max(max).optional().or(z.literal(''));

/**
 * Montant SAISI en dirhams, comme partout ailleurs dans les formulaires
 * (`madAmountSchema` du colis) : c'est ce que l'opérateur lit sur un reçu. La
 * conversion en centimes entiers se fait au bord, dans le service — jamais un
 * flottant n'entre en base (§4 dette n°2).
 *
 * Strictement positif : une régularisation à zéro n'est pas un règlement, et un
 * montant négatif serait une annulation déguisée.
 */
const madAmount = z
  .number({ error: 'required' })
  .positive('must-be-positive')
  .max(1_000_000, 'amount-too-large');

/**
 * Même unité, mais ZÉRO est permis : un montant de politique tarifaire peut
 * valoir zéro — un ramassage offert, un retour non rémunéré — là où un
 * règlement à zéro n'existe pas.
 */
const madAmount0 = z
  .number({ error: 'required' })
  .min(0, 'negative')
  .max(1_000_000, 'amount-too-large');

/**
 * Pièce jointe facultative : ordre de virement, reçu signé, IMAGE D'UN CHÈQUE.
 *
 * Déclarée ici, en tête, parce qu'elle sert désormais aux quatre documents
 * d'argent — règlement client, bon de versement, règlement livreur, remise —
 * et qu'une `const` n'est pas remontée : la déclarer plus bas ferait échouer
 * l'évaluation du module au premier schéma qui la référence.
 */
const proofSchema = z
  .object({
    name: z.string().trim().min(1, 'required').max(120),
    mimeType: z.enum(['application/pdf', 'image/jpeg', 'image/png', 'image/webp'], {
      error: 'unsupported-file-type',
    }),
    content: z.string().min(1, 'required'),
  })
  .nullable()
  .default(null);

/* --- Facture ------------------------------------------------------------- */

export const invoiceCreateSchema = z.object({
  clientId: z.string().min(1, 'required'),
  agencyId: z.string().min(1, 'required'),
  note: optionalText(500),
});

export type InvoiceCreateInput = z.input<typeof invoiceCreateSchema>;
export type InvoiceCreatePayload = z.output<typeof invoiceCreateSchema>;

/**
 * Ajout de colis à un brouillon, par lot.
 *
 * Par LOT et non un par un : une facture mensuelle porte couramment cent colis,
 * et cent allers-retours HTTP pour composer un brouillon feraient de l'écran un
 * sablier. Le service refuse le lot entier si un seul colis n'est pas
 * éligible — une facture à moitié composée est pire qu'un refus motivé.
 */
export const invoiceParcelsSchema = z.object({
  parcelIds: z.array(z.string().min(1)).min(1, 'required').max(SELECTION_MAX, 'too-many'),
});

export type InvoiceParcelsPayload = z.output<typeof invoiceParcelsSchema>;

/**
 * Émission : le geste qui fige la facture.
 *
 * L'échéance est facultative — le legacy n'en avait aucune (§1.15), et la lui
 * imposer aujourd'hui bloquerait la saisie sans que personne n'ait décidé d'un
 * délai de paiement.
 */
export const invoiceIssueSchema = z.object({
  /** Date d'échéance, `AAAA-MM-JJ`. */
  dueAt: z
    .string()
    .trim()
    .regex(/^\d{4}-\d{2}-\d{2}$/, 'invalid-date')
    .optional()
    .or(z.literal('')),
});

/**
 * LA LIGNE DE FACTURE HORS COLIS — lot Argent 2, A2-1 (D-181).
 *
 * Saisie à la main par l'administration : un AJUSTEMENT (régularisation D-169,
 * correction) ou AUTRE chose. Le frais de ramassage par bon (`pickup-note-fee`)
 * n'est jamais saisi : la validation du bon le pose (A2-5).
 *
 * `amount` en MAD, SIGNÉ, hors taxes : positif = dû par le vendeur, négatif =
 * dû au vendeur. Zéro n'est pas une ligne. Le motif est obligatoire : c'est lui
 * que le vendeur lit sur le PDF.
 */
export const INVOICE_ADJUSTMENT_KINDS = ['adjustment', 'pickup-note-fee', 'other'] as const;
export type InvoiceAdjustmentKind = (typeof INVOICE_ADJUSTMENT_KINDS)[number];

export const invoiceAdjustmentSchema = z.object({
  kind: z.enum(['adjustment', 'other']).default('adjustment'),
  amount: z
    .number()
    .finite()
    .refine((value) => value !== 0, 'required')
    .refine((value) => Math.abs(value) <= 10_000_000, 'too-large')
    .refine((value) => Number.isInteger(Math.round(value * 100)), 'invalid'),
  // Obligatoire, sans longueur minimale (remarques Badr 12/11) : seul le vide est refusé.
  reason: z.string().trim().min(1, 'required').max(300, 'too-long'),
  reference: optionalText(80),
});

export type InvoiceAdjustmentInput = z.input<typeof invoiceAdjustmentSchema>;
export type InvoiceAdjustmentPayload = z.output<typeof invoiceAdjustmentSchema>;

export type InvoiceIssueInput = z.input<typeof invoiceIssueSchema>;
export type InvoiceIssuePayload = z.output<typeof invoiceIssueSchema>;

/* --- Règlement ----------------------------------------------------------- */

/* --- Chèque — C11 bloc 7 ---------------------------------------------- */

/**
 * Les trois champs d'un chèque, partagés par les DEUX règlements — D-067.
 *
 * `reference` existe déjà mais ne suffit pas : un chèque se retrouve par son
 * numéro ET sa banque, et sa date d'émission n'est pas celle du règlement —
 * un chèque post-daté est reçu aujourd'hui et encaissable dans trois semaines.
 * Confondre les deux dates fait rapprocher une caisse qui ne tombera jamais.
 *
 * Déclarés une fois et repris des deux côtés : le règlement d'un client et la
 * paie d'un livreur ne se saisissent pas au même écran, mais un chèque est un
 * chèque.
 */
export const chequeFields = {
  chequeNumber: optionalText(40),
  chequeBank: optionalText(80),
  /** Date d'ÉMISSION du chèque, `AAAA-MM-JJ`. Pas celle du règlement. */
  chequeDate: z
    .string()
    .trim()
    .regex(/^\d{4}-\d{2}-\d{2}$/, 'invalid-date')
    .optional()
    .or(z.literal('')),
};

/**
 * Règlement d'un relevé LIVREUR — C11 bloc 7, D-067.
 *
 * Le relevé v1 n'avait aucun mode : « marqué payé » ne disait pas par quel
 * canal, et un livreur qui conteste n'avait rien à rapprocher. Aucun MONTANT
 * n'est saisi ici non plus — c'est le relevé qui dit ce qui est dû (D-065).
 */
export const statementPaySchema = z
  .object({
    method: z.enum(PAYMENT_METHODS).default('cash'),
    bankAccountId: z.string().trim().optional().or(z.literal('')),
    reference: optionalText(120),
    ...chequeFields,
    proof: proofSchema,
  })
  .refine((value) => value.method !== 'check' || Boolean(value.chequeNumber), {
    error: 'cheque-number-required',
    path: ['chequeNumber'],
  })
  .refine((value) => value.method === 'cash' || Boolean(value.bankAccountId), {
    error: 'bank-account-required',
    path: ['bankAccountId'],
  });

export type StatementPayInput = z.input<typeof statementPaySchema>;
export type StatementPayPayload = z.output<typeof statementPaySchema>;

/**
 * La PHOTO du chèque, téléversée par le livreur — C11 bloc 7, D-067.
 *
 * Le livreur reçoit son chèque au comptoir et le photographie depuis son
 * téléphone : c'est sa preuve à lui, et il n'a aucune raison de dépendre de
 * l'agence pour l'attacher. Aucun autre champ n'est modifiable par cette route
 * — il documente un règlement, il ne le change pas.
 */
export const chequePhotoSchema = z.object({
  // Requise, et pas seulement « non nulle par défaut » : la route N'EXISTE
  // que pour attacher une image. Sans elle, l'appel ne veut rien dire.
  proof: z.object({
    name: z.string().trim().min(1, 'required').max(120),
    mimeType: z.enum(['application/pdf', 'image/jpeg', 'image/png', 'image/webp'], {
      error: 'unsupported-file-type',
    }),
    content: z.string().min(1, 'required'),
  }),
});

export type ChequePhotoPayload = z.output<typeof chequePhotoSchema>;

export const paymentSchema = z
  .object({
    invoiceId: z.string().trim().optional().or(z.literal('')),
    clientId: z.string().min(1, 'required'),
    agencyId: z.string().min(1, 'required'),
    amount: madAmount,
    method: z.enum(PAYMENT_METHODS).default('cash'),
    bankAccountId: z.string().trim().optional().or(z.literal('')),
    reference: optionalText(120),
    note: optionalText(500),
    /** Date de règlement, `AAAA-MM-JJ`. Le jour même par défaut, côté service. */
    paidAt: z
      .string()
      .trim()
      .regex(/^\d{4}-\d{2}-\d{2}$/, 'invalid-date')
      .optional()
      .or(z.literal('')),
    ...chequeFields,
    /** L'image du chèque, photographiée au comptoir. Facultative. */
    proof: proofSchema,
  })
  .refine((value) => value.method !== 'check' || Boolean(value.chequeNumber), {
    /*
     * Un chèque sans numéro ne se retrouve pas — C11 bloc 7, D-067.
     *
     * C'est LE champ qui permet de le rapprocher d'un relevé bancaire trois
     * semaines plus tard, quand il revient impayé. La banque, elle, reste
     * facultative : elle est souvent lisible sur l'image.
     */
    error: 'cheque-number-required',
    path: ['chequeNumber'],
  })
  .refine((value) => value.method === 'cash' || Boolean(value.bankAccountId), {
    // Un virement ou un chèque arrive forcément SUR un compte. L'espèce, non :
    // elle est comptée au guichet, et le legacy laissait `compte_id` à NULL sur
    // ses 422 règlements en espèce.
    error: 'bank-account-required',
    path: ['bankAccountId'],
  });

export type PaymentInput = z.input<typeof paymentSchema>;
export type PaymentPayload = z.output<typeof paymentSchema>;

/* --- Retour de fonds ----------------------------------------------------- */

export const remittanceCreateSchema = z.object({
  courierId: z.string().min(1, 'required'),
  cityId: z.string().min(1, 'required'),
  agencyId: z.string().min(1, 'required'),
  note: optionalText(500),
  /**
   * LES COLIS, DÈS LA CRÉATION — chantier « Retour de fonds unique », point 5.
   *
   * Un bon ouvert et vide n'est la preuve de rien, et il occupe la place du
   * SEUL bon ouvert qu'un livreur peut avoir (D-116) : il empêche l'ouverture
   * automatique de fonctionner pour lui. La production en portait un depuis des
   * semaines — `RF-2609-0015` — et personne ne savait à quoi il servait.
   *
   * Facultatif dans le TYPE, exigé par le service : les écrans existants
   * composent encore en deux temps (créer, puis ajouter), et casser leur
   * contrat d'un coup aurait été une régression pour un gain de forme.
   */
  parcelIds: z.array(z.string().min(1)).max(SELECTION_MAX, 'too-many').optional(),
});

export type RemittanceCreateInput = z.input<typeof remittanceCreateSchema>;
export type RemittanceCreatePayload = z.output<typeof remittanceCreateSchema>;

export const remittanceParcelsSchema = z.object({
  parcelIds: z.array(z.string().min(1)).min(1, 'required').max(SELECTION_MAX, 'too-many'),
});

export type RemittanceParcelsPayload = z.output<typeof remittanceParcelsSchema>;

/**
 * Le comptage — C11 bloc 2.
 *
 * Le montant remis se SAISIT ; les trois attendus se calculent. Un écart entre
 * les deux exige un motif : une caisse qui ne tombe pas juste et que personne
 * n'explique redevient le legacy, où `recettes.montant` était un nombre sans
 * provenance.
 *
 * Saisi en dirhams, stocké en centimes, comme partout à la frontière.
 */
export const remittanceCountSchema = z
  .object({
    declaredAmount: madAmount0,
    varianceReason: optionalText(300),
    /** L'attendu, renvoyé par le client pour que la règle du motif se vérifie ici. */
    expectedNet: madAmount0,
  })
  .refine(
    (value) =>
      value.declaredAmount === value.expectedNet ||
      (value.varianceReason !== undefined && value.varianceReason !== ''),
    { path: ['varianceReason'], error: 'variance-needs-reason' },
  );

export type RemittanceCountInput = z.input<typeof remittanceCountSchema>;
export type RemittanceCountPayload = z.output<typeof remittanceCountSchema>;

/** Date seule, sans heure — les bornes de période et les dates de règlement. */
const isoDate = z
  .string()
  .trim()
  .regex(/^\d{4}-\d{2}-\d{2}$/, 'invalid-date');

/* --- Facture mensuelle (client PRO) — C11 bloc 3 --------------------------- */

/**
 * « Générer la facture du mois » — C11 bloc 3, D-061.
 *
 * Un seul geste, et AUCUNE ligne saisie : le service ramasse lui-même les colis
 * de la période qui sont facturables (D-061) et pas encore facturés. C'est ce
 * qui distingue cette route de la composition manuelle, où l'opérateur choisit
 * colis par colis.
 *
 * Les bornes sont INCLUSES et portent sur la date de passage au statut — le
 * jour où le colis a été livré ou a échoué, pas celui où il a été créé. Mêmes
 * bornes, même sens et même refus de période inversée que le relevé livreur :
 * deux écrans qui demandent une période doivent la demander pareil.
 */
export const invoiceMonthlySchema = z
  .object({
    clientId: z.string().min(1, 'required'),
    agencyId: z.string().min(1, 'required'),
    periodStart: isoDate,
    periodEnd: isoDate,
    note: optionalText(500),
  })
  .refine((value) => value.periodStart <= value.periodEnd, {
    error: 'period-reversed',
    path: ['periodEnd'],
  });

export type InvoiceMonthlyInput = z.input<typeof invoiceMonthlySchema>;
export type InvoiceMonthlyPayload = z.output<typeof invoiceMonthlySchema>;

/* --- Bon de versement (client PRO) — C11 bloc 3 ---------------------------- */

export const codPayoutCreateSchema = z.object({
  clientId: z.string().min(1, 'required'),
  agencyId: z.string().min(1, 'required'),
  note: optionalText(500),
});

export type CodPayoutCreateInput = z.input<typeof codPayoutCreateSchema>;
export type CodPayoutCreatePayload = z.output<typeof codPayoutCreateSchema>;

export const codPayoutParcelsSchema = z.object({
  parcelIds: z.array(z.string().min(1)).min(1, 'required').max(SELECTION_MAX, 'too-many'),
});

export type CodPayoutParcelsPayload = z.output<typeof codPayoutParcelsSchema>;

/**
 * BONS DE VERSEMENT EN MASSE — lot U (D-233). Période (date de LIVRAISON) et
 * agence filtrent ; `all` = tous les clients qui ont un solde à verser, sinon
 * les `clientIds` cochés.
 */
export const codPayoutBulkSchema = z
  .object({
    from: z.string().trim().optional(),
    to: z.string().trim().optional(),
    agencyId: z.string().trim().optional(),
    all: z.boolean().optional(),
    clientIds: z.array(z.string().min(1)).max(SELECTION_MAX, 'too-many').optional(),
    note: optionalText(500),
  })
  .refine((value) => value.all === true || (value.clientIds?.length ?? 0) > 0, {
    message: 'required',
    path: ['clientIds'],
  });

export type CodPayoutBulkInput = z.input<typeof codPayoutBulkSchema>;
export type CodPayoutBulkPayload = z.output<typeof codPayoutBulkSchema>;

/**
 * Le VERSEMENT — le geste qui fige le bon et marque les colis.
 *
 * Aucun montant ne s'y saisit, et c'est délibéré : le total est la somme des
 * COD retenus, sans déduction. Un champ « montant » inviterait à en retrancher
 * quelque chose, ce qui est exactement ce que ce document refuse de faire.
 *
 * Le mode et la référence se saisissent ICI plutôt qu'à l'ouverture : on ne
 * sait pas par quel canal on paiera tant qu'on n'a pas fini de composer.
 */
export const codPayoutPaySchema = z.object({
  method: z.enum(PAYMENT_METHODS),
  bankAccountId: z.string().trim().optional().or(z.literal('')),
  reference: optionalText(120),
  /** Date du versement. Vide = maintenant. */
  paidAt: isoDate.optional().or(z.literal('')),
  proof: proofSchema,
});

export type CodPayoutPayInput = z.input<typeof codPayoutPaySchema>;
export type CodPayoutPayPayload = z.output<typeof codPayoutPaySchema>;

/* --- Paramètres > Finance ------------------------------------------------- */

/** Préfixe de numéro : court, en capitales, sans séparateur — il en porte déjà un. */
const documentPrefix = z
  .string()
  .trim()
  .toUpperCase()
  .min(1, 'required')
  .max(5, 'too-large')
  .regex(/^[A-Z0-9]+$/, 'invalid-prefix');

export const financeSettingsSchema = z.object({
  /** Saisi en POURCENT (20 = 20 %), stocké en centièmes. */
  vatRate: z.number().min(0, 'negative').max(100, 'too-large'),
  /**
   * Lot R (D-230) — défaut des Clients Pro, en POURCENT : 20, 14, 10, 7 ou 0.
   * FACULTATIF : un appel qui ne le porte pas ne le touche pas (pas de
   * `.default()`, qui le ramènerait à 20 en silence).
   */
  proVatRate: z
    .number()
    .refine((rate) => PRO_VAT_RATES.includes(Math.round(rate * 100) as ProVatRate), 'invalid')
    .optional(),

  invoicePrefix: documentPrefix,
  remittancePrefix: documentPrefix,
  statementPrefix: documentPrefix,
  codPayoutPrefix: documentPrefix,
  numberingYearlyReset: z.boolean(),

  failureFeeSameCity: madAmount0,
  failureFeeCrossCity: madAmount0,
  pickupFee: madAmount0,
  freePickupThreshold: z.number().int().min(0, 'negative').max(1000, 'too-large'),

  courierReturnFee: madAmount0,
  courierFailedCommission: madAmount0,
  courierPayPeriod: z.enum(PAY_PERIODS),

  invoiceFooter: z.string().trim().max(300, 'too-large'),
  codPayoutFooter: z.string().trim().max(300, 'too-large'),
  /**
   * B-21 — la sortie de `GET` doit être une entrée valide de `PATCH`.
   *
   * `GET /settings/finance` rend `"defaultBankAccountId": null` quand aucun
   * compte n'est choisi. Reposter cet objet tel quel était refusé en 422 : le
   * schéma acceptait `string` et `undefined`, jamais `null`. Le champ devait
   * être RETIRÉ de la charge utile pour que l'écriture passe.
   *
   * L'écran ne tombait pas dedans — il n'envoie pas le champ quand il est vide
   * — mais tout appel direct oui : un script de reprise, un test
   * d'intégration, une correction en urgence un dimanche.
   *
   * C'est la famille de défaut que B-14 a fermée sur les unités. Celui-là
   * coûtait un `null` là où l'autre coûtait un facteur cent ; même cause.
   *
   * `nullish()` accepte les trois formes que la vie produit : la chaîne, le
   * `null` du `GET`, et l'absence.
   */
  defaultBankAccountId: z.string().trim().nullish(),

  /*
   * Zéro = illimité, et c'est le défaut. Borné à dix ans : au-delà, « illimité »
   * est le mot juste, et une saisie à 36 500 jours est plus sûrement une faute
   * de frappe qu'une politique de conservation.
   */
  auditRetentionDays: z.number().int().min(0, 'negative').max(3650, 'too-large'),
});

export type FinanceSettingsInput = z.input<typeof financeSettingsSchema>;
export type FinanceSettingsPayload = z.output<typeof financeSettingsSchema>;

/* --- Relevé de rémunération livreur --------------------------------------- */

/**
 * Ouverture d'un relevé : un livreur, une période.
 *
 * Aucune ligne n'est saisie — elles sont CALCULÉES depuis les courses
 * confirmées et la grille `CourierCity`. C'est tout l'intérêt : une paie qui
 * se saisit à la main est une paie qu'on discute, et le legacy n'avait
 * justement rien d'autre que la saisie à la main (`frais_livreur`, 58 lignes).
 *
 * Les bornes sont INCLUSES et portent sur la date de confirmation de la
 * course : c'est le jour où le livreur a travaillé, pas celui où le colis a été
 * créé.
 */
export const courierStatementSchema = z
  .object({
    courierId: z.string().min(1, 'required'),
    agencyId: z.string().min(1, 'required'),
    periodStart: isoDate,
    periodEnd: isoDate,
    note: optionalText(500),
  })
  .refine((value) => value.periodStart <= value.periodEnd, {
    error: 'period-reversed',
    path: ['periodEnd'],
  });

export type CourierStatementInput = z.input<typeof courierStatementSchema>;
export type CourierStatementPayload = z.output<typeof courierStatementSchema>;

/* --- Ordre de virement groupé --------------------------------------------- */

/**
 * Deux entrées, exclusives : des clients cochés (sans facture), ou les
 * factures que la génération en masse vient de créer. Le serveur ne choisit
 * pas à la place de l'écran.
 */
export const transferOrderCreateSchema = z
  .object({
    // Bornes relevées à SELECTION_MAX (chantier « Plafond de 200 »).
    clientIds: z.array(z.string().min(1)).max(SELECTION_MAX, 'too-many').default([]),
    invoiceCodes: z.array(z.string().min(1)).max(SELECTION_MAX, 'too-many').default([]),
  })
  .refine((value) => value.clientIds.length > 0 !== value.invoiceCodes.length > 0, {
    error: 'required',
    path: ['clientIds'],
  });

export type TransferOrderCreateInput = z.input<typeof transferOrderCreateSchema>;
export type TransferOrderCreatePayload = z.output<typeof transferOrderCreateSchema>;
