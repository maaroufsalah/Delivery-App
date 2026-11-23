import { z } from 'zod';
import { LOCALES } from './enums';
import { type ThemeOverrides, themeOverridesSchema } from './theme-overrides';
import { isHexColor } from './themes';

/**
 * Configuration de la société — DOMAIN.md §1.21 (`general_configs` + `app_configs`).
 *
 * UNE société par installation (docs/DECISIONS.md D-013) : chaque client a son
 * déploiement, son serveur et sa base. Il n'y a donc ni sélecteur de marque, ni
 * résolution de tenant. `companyId` reste porté par les types pour que l'API
 * NestJS puisse l'exiger, mais il n'est jamais exposé en UI.
 *
 * Cet objet est la source de vérité du white-label : le nom, le logo et les
 * deux couleurs de marque en sortent, et `buildThemeTokens` en dérive les
 * variables CSS au SSR.
 */

export const SUPPORTED_CURRENCIES = ['MAD', 'EUR', 'USD', 'AED', 'SAR', 'TND', 'DZD'] as const;
export type SupportedCurrency = (typeof SUPPORTED_CURRENCIES)[number];

export const SUPPORTED_COUNTRIES = ['MA', 'DZ', 'TN', 'FR', 'ES', 'AE', 'SA'] as const;
export type SupportedCountry = (typeof SUPPORTED_COUNTRIES)[number];

/** Fuseaux proposés à la saisie. La liste IANA complète serait ingérable en select. */
export const SUPPORTED_TIME_ZONES = [
  'Africa/Casablanca',
  'Africa/Algiers',
  'Africa/Tunis',
  'Europe/Paris',
  'Europe/Madrid',
  'Asia/Dubai',
  'Asia/Riyadh',
] as const;
export type SupportedTimeZone = (typeof SUPPORTED_TIME_ZONES)[number];

/** Lot D, D2 (D-189) : la longueur d'un nom court, celle d'un libellé de statut. */
export const SHORT_NAME_MAX = 20;

/**
 * LE MARQUEUR DU NOM COURT dans les catalogues de traduction — D-189.
 *
 * Pas un argument ICU (`{shortName}`) : le typage des messages l'exigerait à
 * chaque appel, et les libellés de statut se lisent par clé calculée. Le
 * marqueur est remplacé UNE fois, au chargement du catalogue (web :
 * `i18n/request.ts` ; mobile : `t()`), par le nom court de la société.
 */
export const SHORT_NAME_TOKEN = '%shortName%';

/** Le nom court à imprimer : le réglage, sinon le nom de la société tronqué. */
export function companyShortName(company: { shortName?: string | null; name: string }): string {
  const short = company.shortName?.trim();
  return short ? short : company.name.trim().slice(0, SHORT_NAME_MAX);
}

/** Remplace le marqueur dans un libellé. Sans marqueur, le texte revient intact. */
export function withShortName(text: string, shortName: string): string {
  return text.includes(SHORT_NAME_TOKEN) ? text.split(SHORT_NAME_TOKEN).join(shortName) : text;
}

export interface CompanySettings {
  /** Dormant : une seule société par installation, jamais affiché. */
  id: string;
  name: string;
  /**
   * LE NOM COURT — lot D, D2 (D-189). Ce que les libellés impriment là où ils
   * disaient « Speedy » en dur ou « l'administration » : « Reçu par Speedy »,
   * « Remis à Speedy », « Créé par Speedy ». 20 caractères au plus, réglé dans
   * Paramètres > Société ; vide en base, il vaut le nom tronqué
   * (`companyShortName`).
   */
  shortName: string;
  /** Chemin servi par `public/`, ou data URL issue de l'upload admin. */
  logo: string;
  /**
   * Le fond du logo — dernières corrections, point 5. `null` : le logo se
   * pose tel quel. Renseigné (SpeedyTrans : l'orange de la marque), il est
   * dessiné sur une pastille de cette couleur partout où il s'affiche, pour
   * qu'un logo clair ne disparaisse pas sur un fond clair. Jamais recoloré.
   */
  logoBackground: string | null;
  /** SEC-01, D-166 : l'icone de marque fabriquee a l'upload (fichier du volume), ou `null` tant qu'aucun logo n'a ete televerse depuis. */
  icon: string | null;
  primaryColor: string;
  secondaryColor: string;
  currency: SupportedCurrency;
  country: SupportedCountry;
  timeZone: SupportedTimeZone;
  /** Langues proposées dans l'interface. Sous-ensemble de `LOCALES`. */
  locales: string[];
  defaultLocale: string;

  /**
   * Profil de la société — C8 bloc 3.
   *
   * Tout est facultatif : un tenant en essai n'a pas encore rempli sa fiche, et
   * l'interface doit s'afficher quand même. Chaque consommateur décide de ce
   * qu'il fait d'un champ vide — le pied de facture l'omet, la landing ne
   * montre pas de ligne « téléphone : ».
   */
  legalName: string | null;
  /** ICE marocain. Imprimé au pied des documents. */
  taxId: string | null;
  /** Registre du commerce. */
  rc: string | null;
  /**
   * L'identité des FACTURES PRO — D-175 (lot B, point B4). Vides : la facture
   * d'un Client Pro reprend `legalName` / `taxId`. Jamais lues ailleurs.
   */
  proLegalName: string | null;
  proTaxId: string | null;
  address: string | null;
  city: string | null;
  phone: string | null;
  email: string | null;
  website: string | null;

  /**
   * Thème AVANCÉ, par zone d'interface — facultatif (D-033).
   *
   * Vide dans l'immense majorité des installations : les deux couleurs de
   * marque suffisent. Renseigné, il se pose PAR-DESSUS la dérivation, sans
   * jamais la remplacer.
   */
  theme?: ThemeOverrides;

  /**
   * LE LIBELLÉ D'ENVIRONNEMENT — chantier UAT, D-199. « RECETTE », « UAT »,
   * ou `null` en production. Rendu par `GET /public/company` seulement (il
   * vient de `APP_ENV`, pas de la société) ; absent partout ailleurs, et le
   * formulaire des paramètres ne le connaît pas.
   */
  environmentLabel?: string | null;
}

/**
 * Repli NEUTRE, servi quand l'API est injoignable (D-016).
 *
 * Il ne porte plus de marque (C8 bloc 3) : en multi-tenant, afficher
 * « SpeedyTrans » pendant un incident montrerait la marque d'un client aux
 * visiteurs d'un autre. Un nom générique et des couleurs sobres disent « le
 * service est là, sa configuration ne l'est pas », ce qui est exactement le
 * cas.
 */
export const DEFAULT_COMPANY_SETTINGS: CompanySettings = {
  id: 'cmp-unknown',
  name: 'Livraison',
  shortName: 'Livraison',
  logo: '/brands/placeholder.svg',
  logoBackground: null,
  icon: null,
  primaryColor: '#3A3A3A',
  secondaryColor: '#111111',
  currency: 'MAD',
  country: 'MA',
  timeZone: 'Africa/Casablanca',
  locales: ['fr', 'ar', 'en'],
  defaultLocale: 'fr',
  legalName: null,
  taxId: null,
  rc: null,
  proLegalName: null,
  proTaxId: null,
  address: null,
  city: null,
  phone: null,
  email: null,
  website: null,
  theme: {},
};

const hexColorSchema = z
  .string()
  .trim()
  .refine((value) => isHexColor(value), 'invalid-color');

/**
 * Le logo accepte un chemin servi par l'app ou une data URL d'image : l'upload
 * admin n'a pas de stockage objet tant que l'API n'existe pas.
 */
const logoSchema = z
  .string()
  .trim()
  .min(1, 'required')
  .refine((value) => value.startsWith('/') || value.startsWith('data:image/'), 'invalid-logo');

/**
 * UN CHAMP DE PROFIL FACULTATIF — chantier « Audit des paramètres société ».
 *
 * Trois écritures désignent « pas renseigné », et elles circulent toutes les
 * trois : la **chaîne vide** que poste le formulaire, le **`null`** que rend le
 * `GET` (le service range `''` comme `null`), et l'**absence** dans un PATCH
 * partiel. Le schéma les accepte toutes et rend `''`.
 *
 * Sans cela, renvoyer au `PATCH` le corps de son propre `GET` rendait **422**
 * sur six champs — la classe de bug corrigée sur `/clients/:id`, et le premier
 * écart relevé au diagnostic de ce chantier.
 *
 * **Aucune `transform()` ici**, et c'est délibéré : elle rendrait le type
 * d'ENTRÉE différent du type de SORTIE, et le resolver du formulaire (qui
 * infère les deux) ne compilerait plus. Le service, lui, sait déjà ranger
 * `''` comme `null` — il n'a pas besoin qu'on normalise avant lui.
 */
const optionalProfileText = (max: number) => z.string().trim().max(max).nullable().optional();

/**
 * La forme NUE, avant le contrôle croisé — c'est elle que le PATCH rend
 * partielle. `z.object().partial()` ne s'applique pas après un `.refine()`.
 */
const companySettingsObject = z
  .object({
    name: z.string().trim().min(2, 'too-short').max(60),
    /** Lot D, D2 (D-189) : obligatoire, 20 caractères au plus. */
    shortName: z.string().trim().min(1, 'required').max(SHORT_NAME_MAX, 'too-long'),
    logo: logoSchema,
    /** Vide ou `null` : pas de fond, le logo s'affiche tel quel. */
    logoBackground: z.union([hexColorSchema, z.literal(''), z.null()]).optional(),
    primaryColor: hexColorSchema,
    secondaryColor: hexColorSchema,
    currency: z.enum(SUPPORTED_CURRENCIES),
    country: z.enum(SUPPORTED_COUNTRIES),
    timeZone: z.enum(SUPPORTED_TIME_ZONES),
    locales: z.array(z.enum(LOCALES)).min(1, 'at-least-one-locale'),
    defaultLocale: z.enum(LOCALES),
    // Le thème avancé voyage avec la configuration société, mais s'édite sur son
    // propre écran : le formulaire Société ne l'envoie pas, d'où l'option.
    /* --- Profil de la société (C8 bloc 3) — tout est facultatif -------------- */
    legalName: optionalProfileText(160),
    taxId: optionalProfileText(40),
    rc: optionalProfileText(60),
    /** D-175 — l'identité des factures pro, facultative. */
    proLegalName: optionalProfileText(160),
    proTaxId: optionalProfileText(40),
    address: optionalProfileText(200),
    city: optionalProfileText(80),
    phone: optionalProfileText(40),
    /** Vide accepté : un tenant en essai n'a pas encore d'adresse de contact. */
    email: z
      .union([z.string().trim().toLowerCase().email('invalid-email'), z.literal(''), z.null()])
      .optional(),
    website: z
      .union([z.string().trim().url('invalid-url'), z.literal(''), z.null()])
      .optional(),

    theme: themeOverridesSchema.optional(),
  });

export const companySettingsSchema = companySettingsObject
  // Une langue par défaut désactivée laisserait l'application sans point d'entrée.
  .refine((value) => value.locales.includes(value.defaultLocale), {
    path: ['defaultLocale'],
    error: 'default-locale-not-enabled',
  });

/**
 * CE QUE LE `PATCH` ACCEPTE : un objet PARTIEL.
 *
 * Un `PATCH` qui exige l'objet entier n'est pas un `PATCH` : il oblige chaque
 * appelant à relire, à recopier, et donc à réémettre des champs qu'il ne
 * voulait pas toucher — c'est ainsi qu'un enregistrement de couleurs renvoyait
 * le logo entier et heurtait la limite de corps.
 *
 * Le contrôle croisé ne s'applique plus qu'aux corps qui portent **les deux**
 * champs : demander « la langue par défaut est-elle activée ? » à un corps qui
 * n'en mentionne aucune n'a pas de sens.
 */
export const companySettingsPatchSchema = companySettingsObject
  .partial()
  .refine(
    (value) =>
      value.locales === undefined ||
      value.defaultLocale === undefined ||
      value.locales.includes(value.defaultLocale),
    { path: ['defaultLocale'], error: 'default-locale-not-enabled' },
  );

export type CompanySettingsInput = z.input<typeof companySettingsSchema>;
export type CompanySettingsPayload = z.output<typeof companySettingsSchema>;
export type CompanySettingsPatchPayload = z.output<typeof companySettingsPatchSchema>;

/** Taille maximale du logo uploadé, en octets. Au-delà, la data URL alourdit chaque page. */
export const MAX_LOGO_BYTES = 512 * 1024;

export const ACCEPTED_LOGO_TYPES = ['image/svg+xml', 'image/png', 'image/jpeg', 'image/webp'];

/**
 * L'ENVOI D'UN LOGO — chantier « Audit des paramètres société », bloc 3.
 *
 * Le logo ne voyage plus dans la configuration société : il a sa propre route,
 * et il est rangé comme un FICHIER sur le volume persistant, à côté des preuves
 * de règlement. Trois raisons, et aucune n'est cosmétique :
 *
 *  1. une data URL en base part dans le HTML de **chaque page** qui affiche la
 *     marque, à chaque rendu ;
 *  2. elle interdit le cache-busting — la valeur EST le contenu, donc l'URL ne
 *     change jamais et le navigateur garde l'ancien logo ;
 *  3. elle faisait porter au `PATCH` de la société un corps de 683 ko, qui
 *     heurtait la limite du body-parser (bloc 1).
 *
 * Même forme que `chequePhotoSchema` : un nom, un type, un contenu en base64.
 */
export const companyLogoSchema = z.object({
  name: z.string().trim().min(1, 'required').max(120),
  mimeType: z.enum(['image/svg+xml', 'image/png', 'image/jpeg', 'image/webp'], {
    error: 'unsupported-file-type',
  }),
  /** Le fichier, en base64. La TAILLE est contrôlée par le service, décodée. */
  content: z.string().min(1, 'required'),
});

export type CompanyLogoPayload = z.output<typeof companyLogoSchema>;

/**
 * Le préfixe des logos SERVIS depuis le stockage.
 *
 * `companies.logoUrl` garde soit un chemin livré avec l'application
 * (`/brands/wimo.svg`, les valeurs historiques, toujours valides), soit cette
 * route publique suivie du nom de fichier. Le nom change à chaque envoi — c'est
 * le cache-busting, obtenu sans paramètre de requête.
 */
export const LOGO_URL_PREFIX = '/api/public/logo/';
