import { z } from 'zod';
import {
  BILLING_TYPES,
  CLIENT_DOCUMENT_KINDS,
  CLIENT_TYPES,
  LABEL_TEMPLATES,
  LOCALES,
  PAY_MODELS,
  PAY_PERIODS,
  USER_ROLES,
  VEHICLE_TYPES,
} from '../enums';
import { SETTLEMENT_MODES } from '../courier-pay';
import { isHexColor } from '../themes';
import { PASSWORD_MIN_LENGTH } from '../password';
import { PERMISSION_KEYS } from '../permissions';
import { madAmountSchema, phoneSchema } from './parcel';
import { PRO_VAT_RATES, type ProVatRate } from '../utils/money';

/**
 * Schémas d'écriture des COMPTES, partagés par le formulaire web et l'API.
 *
 * Même convention que le référentiel : un montant se SAISIT en dirhams et se
 * LIT en centimes ; la conversion vit à la frontière, jamais dans un composant.
 */

const nameSchema = z.string().trim().min(2, 'too-short').max(80);
const optionalText = (max: number) => z.string().trim().max(max).optional().or(z.literal(''));

/* --- Validation d'une demande d'inscription — C13 bloc 2 ----------------- */

/**
 * « Valider » : UN geste, deux effets.
 *
 * La fiche s'ouvre et le compte s'ouvre. Les séparer en deux boutons
 * garantirait le cas qui ne se répare pas tout seul — une fiche validée dont le
 * compte reste fermé, et une personne qui attend en croyant que son dossier
 * traîne alors qu'il est accepté depuis une semaine.
 *
 * L'agence est FACULTATIVE ici parce qu'elle l'est dans la moitié des cas : la
 * fiche la porte déjà. Elle devient obligatoire quand la fiche n'en a pas, et
 * c'est l'API qui tranche — elle seule connaît l'état de la fiche.
 */
export const validateAccountSchema = z.object({
  agencyId: z.string().min(1).optional().or(z.literal('')),
});

export type ValidateAccountInput = z.input<typeof validateAccountSchema>;
export type ValidateAccountPayload = z.output<typeof validateAccountSchema>;

/**
 * « Refuser » : le motif est OBLIGATOIRE.
 *
 * Il n'est pas là pour la forme. La personne rappellera, et l'agent qui
 * décroche doit pouvoir dire autre chose que « je vois juste refusé ». Un motif
 * facultatif serait vide neuf fois sur dix, c'est-à-dire exactement quand on en
 * a besoin.
 */
export const rejectAccountSchema = z.object({
  // Obligatoire, sans longueur minimale (remarques Badr 12/11) : seul le vide est refusé.
  reason: z.string().trim().min(1, 'required').max(500),
});

export type RejectAccountInput = z.input<typeof rejectAccountSchema>;
export type RejectAccountPayload = z.output<typeof rejectAccountSchema>;

/** Ce qui reste à examiner, pour les pastilles du menu — C13 bloc 2. */
export interface PendingCounts {
  clients: number;
  couriers: number;
}

/* --- Utilisateurs -------------------------------------------------------- */

/**
 * Création d'un compte par un administrateur — C13 bloc 3, D-086.
 *
 * **Le mot de passe est SAISI ici**, et c'est le renversement du chantier.
 * Jusqu'à C13, l'API en tirait un temporaire, le montrait une fois et posait
 * `mustChangePassword` — au nom du principe qu'un administrateur ne doit pas
 * connaître le mot de passe d'autrui.
 *
 * Le principe est juste et le terrain l'a démenti : nos utilisateurs ne sont
 * pas techniques, l'écran de changement forcé est la marche sur laquelle ils
 * trébuchent, et l'administrateur dicte de toute façon le mot de passe au
 * téléphone. Le secret était donc partagé dans les faits, avec en prime un
 * écran de plus à franchir. Le bouton « Générer » reste pour qui veut un mot de
 * passe solide sans l'inventer.
 */
/**
 * LE NOM D'UTILISATEUR — recette SpeedyTrans 3, point 5.
 *
 * Un livreur n'a pas toujours d'adresse e-mail, et on lui en inventait une
 * pour qu'il puisse se connecter. Il se connecte désormais par un nom simple :
 * lettres, chiffres, point, tiret, souligné. En MINUSCULES une fois pour toutes
 * — « Hamid.B » et « hamid.b » sont le même compte —, unique par société
 * (index partiel en base, sur `lower(username)`).
 */
export const USERNAME_PATTERN = /^[a-z0-9._-]{3,40}$/;

export const usernameSchema = z
  .string()
  .trim()
  .toLowerCase()
  .regex(USERNAME_PATTERN, 'invalid-username');

/**
 * Un champ laissé vide vaut `null`, jamais une chaîne vide.
 *
 * `nullish()` et pas seulement `optional()` : le formulaire valide, puis la
 * Server Action et l'API REVALIDENT ce qu'il a produit — c'est-à-dire `null`.
 * Refuser ici sa propre sortie ferait échouer en 422 un formulaire correct.
 */
export const optionalUsernameSchema = usernameSchema
  .nullish()
  .or(z.literal(''))
  .transform((value) => value || null);

const optionalLoginEmailSchema = z
  .email({ error: 'invalid-email' })
  .transform((value) => value.trim().toLowerCase())
  .nullish()
  .or(z.literal(''))
  .transform((value) => value || null);

/**
 * UN IDENTIFIANT DE CONNEXION, AU MOINS — point 5.
 *
 * L'e-mail n'est facultatif que pour un compte PUREMENT livreur qui porte un
 * nom d'utilisateur. Tout autre compte (équipe, vendeur) garde son e-mail
 * obligatoire : c'est lui qui reçoit la réinitialisation du mot de passe.
 */
function requireLoginIdentity(
  value: { email: string | null; username: string | null; roles: readonly string[] },
  ctx: z.RefinementCtx,
): void {
  if (value.email) return;
  const courierOnly = value.roles.length === 1 && value.roles[0] === 'COURIER';
  if (courierOnly && value.username) return;
  ctx.addIssue({
    code: 'custom',
    path: ['email'],
    message: courierOnly ? 'email-or-username' : 'required',
  });
}

const userFieldsSchema = z.object({
  email: optionalLoginEmailSchema,
  username: optionalUsernameSchema,
  firstName: nameSchema,
  lastName: nameSchema,
  /*
   * Chiffres et « + », tel que saisi : lot D, D1 (D-188), après D-172 pour les
   * colis. La normalisation marocaine de C13 réécrivait le numéro en silence.
   */
  phone: phoneSchema.optional().or(z.literal('')),
  jobTitle: optionalText(80),
  nationalId: optionalText(30),
  roles: z.array(z.enum(USER_ROLES)).min(1, 'required'),
  /** Agence principale : elle cadre la portée des requêtes du compte. */
  agencyId: z.string().min(1).optional().or(z.literal('')),
  /** Agences additionnelles — pivot `UserAgency`, remplaçant du CSV legacy. */
  agencyIds: z.array(z.string().min(1)).default([]),
});

export const createUserSchema = userFieldsSchema
  .extend({
    /**
     * `PASSWORD_MIN_LENGTH`, comme partout ailleurs : l'administrateur qui pose
     * le secret de quelqu'un d'autre est tenu par la même borne que la personne
     * qui choisit le sien à l'inscription. Deux bornes, c'était un mot de passe
     * accepté à l'inscription et refusé au profil.
     */
    password: z.string().min(PASSWORD_MIN_LENGTH, 'password-too-short').max(200),
  })
  .superRefine(requireLoginIdentity);

export type CreateUserInput = z.input<typeof createUserSchema>;
export type CreateUserPayload = z.output<typeof createUserSchema>;

/**
 * Modification — l'adresse COMPRISE, depuis C13 (D-086).
 *
 * Elle en était exclue au motif que « changer l'e-mail d'un compte, c'est
 * changer de personne ». C'était confondre l'identité et le login : un compte
 * est identifié par son `id`, l'e-mail n'est qu'un identifiant de connexion
 * unique PAR SOCIÉTÉ, et il change quand quelqu'un change de fournisseur de
 * messagerie. Le refuser obligeait à supprimer puis recréer — c'est-à-dire à
 * perdre l'historique pour corriger une faute de frappe.
 *
 * Le mot de passe, lui, reste hors du formulaire de modification : il a son
 * propre geste, et le poser par mégarde en corrigeant un numéro de téléphone
 * déconnecterait la personne sans prévenir.
 */
export const updateUserSchema = userFieldsSchema.superRefine(requireLoginIdentity);

/**
 * Le formulaire de MODIFICATION, qui porte un champ mot de passe inerte (le
 * même composant sert à la création). Construit ici, et pas par un `.extend()`
 * côté web : zod refuse d'étendre un schéma déjà raffiné.
 */
export const editUserFormSchema = userFieldsSchema
  .extend({ password: z.string().optional() })
  .superRefine(requireLoginIdentity);

export type UpdateUserInput = z.input<typeof updateUserSchema>;
export type UpdateUserPayload = z.output<typeof updateUserSchema>;

/**
 * « Mon profil » — ce que le TITULAIRE peut changer lui-même (C13 bloc 4).
 *
 * Quatre champs, et pas un de plus. Ni rôles, ni agences, ni activation : un
 * compte qui pourrait s'attribuer un rôle n'aurait plus de rôle du tout, au
 * sens où plus rien ne le contraindrait. Le mot de passe a son propre geste,
 * avec l'ancien à redonner (`changePasswordSchema`).
 *
 * L'e-mail EST modifiable par son titulaire : c'est son identifiant de
 * connexion, il en change quand il change de fournisseur de messagerie, et lui
 * faire demander ça à un administrateur pour une adresse qui n'appartient qu'à
 * lui serait une politesse inutile. L'unicité par société reste vérifiée.
 */
export const updateOwnProfileSchema = z.object({
  firstName: nameSchema,
  lastName: nameSchema,
  phone: phoneSchema.optional().or(z.literal('')),
  /*
   * Facultatif depuis le point 5 : un livreur connecté par son nom
   * d'utilisateur n'en a pas forcément. Le service refuse de vider l'adresse
   * d'un compte qui n'a pas d'autre identifiant.
   */
  email: optionalLoginEmailSchema,
});

export type UpdateOwnProfileInput = z.input<typeof updateOwnProfileSchema>;
export type UpdateOwnProfilePayload = z.output<typeof updateOwnProfileSchema>;

/**
 * Changement de mot de passe par le titulaire du compte.
 *
 * L'ancien mot de passe est exigé même sous `mustChangePassword` : un poste
 * resté ouvert ne doit pas suffire à s'approprier le compte.
 */
export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, 'required'),
    password: z.string().min(PASSWORD_MIN_LENGTH, 'password-too-short').max(200),
    confirmation: z.string().min(1, 'required'),
  })
  .refine((value) => value.password === value.confirmation, {
    path: ['confirmation'],
    error: 'password-mismatch',
  })
  .refine((value) => value.password !== value.currentPassword, {
    path: ['password'],
    error: 'password-unchanged',
  });

export type ChangePasswordPayload = z.output<typeof changePasswordSchema>;

/**
 * Mot de passe POSÉ par un administrateur — C12 bloc 2a.
 *
 * Le pendant de `resetPassword`, qui en TIRE un au hasard. Ici c'est
 * l'administrateur qui le choisit, parce qu'il va le dicter au téléphone à un
 * livreur debout dans la rue : un mot de passe de quatorze caractères tirés au
 * sort ne se dicte pas, il se recopie de travers.
 *
 * `mustChangePassword` est **faux par défaut**, contrairement à
 * `resetPassword`. Le retour du client est explicite : ses utilisateurs ne sont
 * pas techniques, et un écran de changement de mot de passe au premier accès
 * est la marche sur laquelle ils trébuchent. La case reste offerte à qui veut
 * l'exiger.
 *
 * La longueur minimale est `PASSWORD_MIN_LENGTH`, celle de tous les autres
 * gestes : une borne qui dépendrait de QUI tape serait une politique de mot de
 * passe à deux vitesses, et c'est le compte du livreur qui la paierait.
 */
export const setUserPasswordSchema = z
  .object({
    password: z.string().min(PASSWORD_MIN_LENGTH, 'password-too-short').max(200),
    /**
     * FACULTATIVE depuis C13 bloc 3.
     *
     * Elle servait à rattraper une faute de frappe dans un champ masqué. Le
     * champ ne l'est plus — on le lit, on le dicte, on le copie — et redemander
     * la même chaîne à l'aveugle ne vérifierait plus rien d'autre que la
     * patience de qui la tape. Quand elle est fournie, elle doit toujours
     * correspondre : les appels existants n'ont pas changé de sens.
     */
    confirmation: z.string().optional(),
    mustChangePassword: z.boolean().default(false),
  })
  .refine((value) => !value.confirmation || value.password === value.confirmation, {
    path: ['confirmation'],
    error: 'password-mismatch',
  });

export type SetUserPasswordInput = z.input<typeof setUserPasswordSchema>;
export type SetUserPasswordPayload = z.output<typeof setUserPasswordSchema>;

/**
 * « Mot de passe oublié » — C12 bloc 2b.
 *
 * Une adresse, rien d'autre. Pas de captcha, pas de question secrète : la
 * route ne rend aucune information, donc il n'y a rien à protéger derrière un
 * obstacle. Le débit, lui, est borné côté serveur.
 */
export const forgotPasswordSchema = z.object({
  email: z.email({ error: 'invalid-email' }),
});

export type ForgotPasswordInput = z.input<typeof forgotPasswordSchema>;
export type ForgotPasswordPayload = z.output<typeof forgotPasswordSchema>;

/* --- Rôles et permissions ------------------------------------------------ */

/**
 * Matrice complète, envoyée en UN bloc.
 *
 * Envoyer des deltas (« ajoute ceci, retire cela ») sur une matrice éditée à
 * la case rendrait l'état final dépendant de l'ordre des requêtes. Ici l'écran
 * poste ce qu'il affiche, et c'est ce qui est écrit.
 */
export const roleMatrixSchema = z.object({
  granted: z.record(z.enum(USER_ROLES), z.array(z.enum(PERMISSION_KEYS))),
});

export type RoleMatrixPayload = z.output<typeof roleMatrixSchema>;

/**
 * Les droits EFFECTIFS voulus pour un compte — point 1 des dernières
 * corrections. Même parti pris que la matrice : l'écran poste ce qu'il
 * affiche, et l'API en déduit les octrois et les retraits par différence avec
 * le rôle.
 */
export const userPermissionsSchema = z.object({
  effective: z.array(z.enum(PERMISSION_KEYS)),
});

export type UserPermissionsPayload = z.output<typeof userPermissionsSchema>;

/* --- Agences ------------------------------------------------------------- */

export const agencySchema = z.object({
  name: nameSchema,
  cityId: z.string().min(1, 'required'),
  phone: phoneSchema.optional().or(z.literal('')),
  email: z.email({ error: 'invalid-email' }).optional().or(z.literal('')),
  address: optionalText(255),
  /** Vide = hérite de la société. La surcharge est l'exception, pas la règle. */
  currency: optionalText(3),
  timezone: optionalText(64),
  /**
   * LES VILLES QU'ELLE DESSERT — retouches Anass, point 4.
   *
   * `cityId` dit où l'agence SE TROUVE ; celles-ci disent ce qu'elle COUVRE.
   * La distinction décide du rattachement d'un nouvel e-commerçant : une
   * agence de Casablanca qui dessert Mohammedia le sait, nous non.
   *
   * VIDE par défaut, et c'est un défaut tenable : la résolution retombe alors
   * sur l'agence située dans la ville, puis sur le siège — exactement ce qui se
   * faisait avant. Remplir la couverture affine, elle ne débloque rien.
   *
   * La ville de l'agence n'a pas à y figurer : elle est couverte d'office.
   */
  coveredCityIds: z.array(z.string().min(1)).default([]),
  isActive: z.boolean().default(true),
});

export type AgencyInput = z.input<typeof agencySchema>;
export type AgencyPayload = z.output<typeof agencySchema>;

/* --- E-commerçants ------------------------------------------------------- */

export const clientSchema = z.object({
  name: nameSchema,
  legalName: optionalText(120),
  type: z.enum(CLIENT_TYPES).default('merchant'),
  /** ICE marocain : 15 chiffres. Facultatif pour un particulier. */
  taxId: z
    .string()
    .trim()
    .regex(/^\d{15}$/, 'invalid-tax-id')
    .optional()
    .or(z.literal('')),
  phone: phoneSchema,
  email: z.email({ error: 'invalid-email' }),
  cityId: z.string().min(1, 'required'),
  agencyId: z.string().min(1).optional().or(z.literal('')),
  address: optionalText(255),
  notes: optionalText(1000),
  /**
   * C11 bloc 3, D-061 — le régime de facturation.
   *
   * `normal` par défaut : ajouter le champ ne doit changer le comportement
   * d'aucun client existant. Passer un client en `professional` est une
   * décision de gestion, elle se prend explicitement.
   */
  billingType: z.enum(BILLING_TYPES).default('normal'),
  monthlyBilling: z.boolean().default(false),
  /**
   * CLIENT PRO — chantier « Retours, BL Clients Pro », point 4.
   *
   * L'interrupteur du BL : « aucun champ, aucune question, aucun workflow BL
   * pour un client normal ». `false` par défaut, et réservé à l'admin — le
   * vendeur lit sa fiche (`/merchant/me/company`) mais n'écrit pas ce champ.
   *
   * À ne pas confondre avec `billingType: 'professional'`, qui décide de la
   * FACTURE et pas des bons de livraison (D-061).
   */
  isPro: z.boolean().default(false),
  isActive: z.boolean().default(true),
});

export type ClientInput = z.input<typeof clientSchema>;
export type ClientPayload = z.output<typeof clientSchema>;

/**
 * MISE À JOUR PARTIELLE d'une fiche e-commerçant — chantier « Bon de retour
 * livreur », point 0.
 *
 * **`PATCH` n'acceptait pas la sortie de son propre `GET`.** Il exigeait
 * l'objet ENTIER : un `{"isPro": true}` seul rendait `422` sur `name`, `phone`,
 * `email`… et il refusait les `null` que le `GET` rend sur `legalName`,
 * `taxId`, `address` et `notes`. Pour basculer un interrupteur, il fallait
 * relire la fiche, recopier douze champs ET en retirer les nuls. Le rapport de
 * production du 2026-09-22 l'a relevé, et la même famille de piège était déjà
 * notée sur `settings/finance`.
 *
 * Deux règles, et elles suffisent :
 *
 *  1. **un champ ABSENT n'est pas touché** — c'est la définition d'un `PATCH`,
 *     et `.partial()` la donne ;
 *  2. **`null` est accepté là où la colonne est nullable**, et vaut « efface ».
 *     Le `GET` rend `null`, le `PATCH` doit savoir le relire : un contrat qui
 *     ne sait pas ingérer ce qu'il produit oblige l'appelant à le nettoyer.
 *
 * Le `POST` de création, lui, ne bouge pas : ouvrir une fiche demande un nom,
 * un téléphone et une ville, et les rendre optionnels ferait naître des fiches
 * qu'on ne peut ni appeler ni rattacher.
 */
export const clientPatchSchema = clientSchema.partial().extend({
  /*
   * LES DÉFAUTS SONT RETIRÉS, un par un, et ce n'est pas du zèle.
   *
   * `.partial()` rend la clé facultative mais GARDE le `.default()` qui vit
   * dessous : un `PATCH {"isPro": true}` ressortait avec `isActive: true`,
   * `billingType: 'normal'` et `type: 'merchant'`. Basculer un interrupteur
   * aurait donc RÉACTIVÉ un client suspendu et lui aurait repris son régime de
   * facturation — sans que rien ne le dise, et sans que personne ne l'ait
   * demandé.
   *
   * Les redéclarer en `.optional()` nu coupe la racine : absent reste absent.
   */
  type: z.enum(CLIENT_TYPES).optional(),
  billingType: z.enum(BILLING_TYPES).optional(),
  monthlyBilling: z.boolean().optional(),
  isPro: z.boolean().optional(),
  isActive: z.boolean().optional(),

  legalName: optionalText(120).nullable(),
  taxId: z
    .string()
    .trim()
    .regex(/^\d{15}$/, 'invalid-tax-id')
    .optional()
    .or(z.literal(''))
    .nullable(),
  agencyId: z.string().min(1).optional().or(z.literal('')).nullable(),
  address: optionalText(255).nullable(),
  notes: optionalText(1000).nullable(),
});

export type ClientPatchInput = z.input<typeof clientPatchSchema>;
export type ClientPatchPayload = z.output<typeof clientPatchSchema>;

/**
 * « Taux de TVA applicable » d'un Client Pro — lot R (D-230). En CENTIÈMES,
 * une des cinq valeurs de `PRO_VAT_RATES` ; `null` = le défaut société. Hors du
 * `PATCH` de la fiche : la route dédiée est réservée à l'administration.
 */
export const clientVatRateSchema = z.object({
  vatRate: z
    .number()
    .int()
    .refine((rate) => PRO_VAT_RATES.includes(rate as ProVatRate), 'invalid')
    .nullable(),
});

export type ClientVatRatePayload = z.output<typeof clientVatRateSchema>;

/**
 * Conditions de ramassage d'un client — lot T (D-232). Frais en CENTIMES,
 * seuil en colis ; `null` = la valeur globale, `0` autorisé (gratuit). Hors du
 * `PATCH` de la fiche : la route dédiée est réservée à l'administration.
 */
export const clientPickupTermsSchema = z.object({
  pickupFee: z.number().int().min(0, 'negative').max(100_000_000, 'amount-too-large').nullable(),
  freePickupThreshold: z.number().int().min(0, 'negative').max(1000, 'too-large').nullable(),
});

export type ClientPickupTermsPayload = z.output<typeof clientPickupTermsSchema>;

export const clientDiscountSchema = z.object({
  /** Vide, avec `cityIds` vide = toutes les zones. */
  zoneIds: z.array(z.string().min(1)).default([]),
  /** Lot K (D-218) — villes de destination visées ; passent avant les zones. */
  cityIds: z.array(z.string().min(1)).default([]),
  flatRate: madAmountSchema.nullable().default(null),
  sameCityRate: madAmountSchema.nullable().default(null),
  pickupFee: madAmountSchema.nullable().default(null),
  isActive: z.boolean().default(true),
});

export type ClientDiscountInput = z.input<typeof clientDiscountSchema>;
export type ClientDiscountPayload = z.output<typeof clientDiscountSchema>;

export const clientPickupAddressSchema = z.object({
  label: z.string().trim().min(2, 'too-short').max(60),
  address: z.string().trim().min(5, 'too-short').max(255),
  cityId: z.string().min(1, 'required'),
  districtId: z.string().min(1).optional().or(z.literal('')),
  phone: phoneSchema.optional().or(z.literal('')),
  isDefault: z.boolean().default(false),
});

export type ClientPickupAddressInput = z.input<typeof clientPickupAddressSchema>;
export type ClientPickupAddressPayload = z.output<typeof clientPickupAddressSchema>;

/** Types de fichiers acceptés en pièce jointe d'une fiche client. */
export const ACCEPTED_DOCUMENT_TYPES = [
  'application/pdf',
  'image/jpeg',
  'image/png',
  'image/webp',
] as const;

export const MAX_DOCUMENT_BYTES = 8 * 1024 * 1024;

/**
 * Dépôt d'un document. Le fichier voyage en base64 dans le corps JSON, comme
 * l'import tarifaire — les écritures partent d'une Server Action (D-018).
 */
export const clientDocumentSchema = z.object({
  name: z.string().trim().min(1, 'required').max(120),
  kind: z.enum(CLIENT_DOCUMENT_KINDS).default('other'),
  mimeType: z.enum(ACCEPTED_DOCUMENT_TYPES, { error: 'unsupported-file-type' }),
  content: z.string().min(1, 'required'),
});

export type ClientDocumentPayload = z.output<typeof clientDocumentSchema>;

/* --- Livreurs ------------------------------------------------------------ */

export const courierSchema = z.object({
  firstName: nameSchema,
  lastName: nameSchema,
  phone: phoneSchema,
  /** CNI marocaine. */
  nationalId: optionalText(30),
  cityId: z.string().min(1, 'required'),
  agencyId: z.string().min(1).optional().or(z.literal('')),
  vehicleType: z.enum(VEHICLE_TYPES).default('motorbike'),
  bankName: optionalText(80),
  rib: z
    .string()
    .trim()
    .regex(/^\d{24}$/, 'invalid-rib')
    .optional()
    .or(z.literal('')),
  /**
   * D-020 et D-058 — les DEUX axes, indépendants.
   *
   * Les défauts décrivent le livreur « normal » du PO : à la commission,
   * déduite à chaque remise de fonds. Un salarié se règle sur sa fiche.
   */
  payModel: z.enum(PAY_MODELS).default('commission'),
  settlementMode: z.enum(SETTLEMENT_MODES).default('at-remittance'),
  payPeriod: z.enum(PAY_PERIODS).default('monthly'),
  fixedSalary: madAmountSchema.nullable().default(null),
  isActive: z.boolean().default(true),
});

export type CourierInput = z.input<typeof courierSchema>;
export type CourierPayload = z.output<typeof courierSchema>;

/**
 * Grille de paie par ville — remplacée en bloc, comme la matrice des rôles.
 *
 * Lot F, F1 (D-206) : elle porte aussi les villes SERVIES par le livreur, et
 * « Tout cocher » envoie toutes les villes de la société (325 aujourd'hui). La
 * borne de 200 refusait ce geste ; la vraie borne est au serveur, qui exige des
 * villes connues de la société et sans doublon — donc au plus leur nombre.
 * Celle-ci ne garde qu'un plafond technique.
 */
export const courierCitiesSchema = z.object({
  cities: z
    .array(
      z.object({
        cityId: z.string().min(1, 'required'),
        pickupFee: madAmountSchema,
        deliveryFee: madAmountSchema,
      }),
    )
    .max(5_000, 'too-many'),
});

export type CourierCitiesPayload = z.output<typeof courierCitiesSchema>;

/** Création du compte lié à une fiche client ou livreur. */
export const linkAccountSchema = z.object({
  email: z.email({ error: 'invalid-email' }).transform((value) => value.trim().toLowerCase()),
});

export type LinkAccountPayload = z.output<typeof linkAccountSchema>;

/* --- Réglages métier (D-019) --------------------------------------------- */

export const businessSettingsSchema = z.object({
  parcelCodePrefix: z
    .string()
    .trim()
    .toUpperCase()
    .max(6, 'too-large')
    .regex(/^[A-Z0-9-]*$/, 'invalid-prefix'),
  localesEnabled: z.array(z.enum(LOCALES)).min(1, 'at-least-one-locale'),
  localesDefault: z.enum(LOCALES),
  /** Jours avant qu'une réclamation soit « en retard ». `0` désactive le badge. */
  claimResolutionDays: z.coerce.number().int().min(0, 'negative-amount').max(365, 'too-large'),

  /*
   * L'ÉTIQUETTE — chantier « Étiquette orange », point 1.
   *
   * Trois réglages, et c'est ce qui rend vraie la dernière phrase de la
   * mission : « gabarit paramétré par tenant (couleurs, logo, bandeau) : wimo
   * garde son rendu actuel ». Le logo, lui, est déjà celui de Paramètres >
   * Société ; on ne le redemande pas ici.
   */
  labelTemplate: z.enum(LABEL_TEMPLATES),
  /** Vide = la couleur de marque de la société. */
  labelBrandColor: z
    .string()
    .trim()
    .refine((value) => value === '' || isHexColor(value), 'invalid-color'),
  /** Vide = aucun bandeau. La phrase nomme la société : pas de défaut. */
  labelBanner: z.string().trim().max(200, 'too-large'),
});

export type BusinessSettingsInput = z.input<typeof businessSettingsSchema>;
export type BusinessSettingsPayload = z.output<typeof businessSettingsSchema>;

/**
 * Ce qu'un vendeur a le droit de changer sur SA fiche — V2 vendeur bloc 9.
 *
 * Quatre champs, et pas un de plus : sa raison sociale, son téléphone, sa
 * ville et son adresse. Le régime de facturation, le compte bancaire et
 * l'agence de rattachement n'y sont pas — ce sont des termes commerciaux
 * négociés, pas des coordonnées, et les ouvrir laisserait un vendeur changer
 * seul la façon dont son argent lui revient (D-061).
 *
 * **L'e-mail non plus.** Il identifie le COMPTE autant que la fiche, et le
 * changer demande de vérifier la nouvelle adresse — c'est le travail de
 * « Mon profil », pas celui de cet écran.
 */
export const merchantCompanySchema = z.object({
  name: z.string().trim().min(2, 'too-short').max(160),
  // Lot D, D1 (D-188) : la même règle que la fiche tenue par l'agence.
  phone: phoneSchema,
  cityId: z.string().min(1, 'required'),
  address: z.string().trim().max(240).optional(),
});

export type MerchantCompanyInput = z.input<typeof merchantCompanySchema>;
export type MerchantCompanyPayload = z.output<typeof merchantCompanySchema>;
