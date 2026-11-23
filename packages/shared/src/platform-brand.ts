import type { CompanySettings } from './settings';

/**
 * La marque de la PLATEFORME — C9 bloc 5.
 *
 * ⚠️ **PROVISOIRE — à remplacer par la charte Nocido définitive.** Ces deux
 * valeurs ont été arrêtées par le PO faute de charte publiée : `nocido.ma` ne
 * résolvait pas au moment de l'écriture, et le dépôt ne portait aucune couleur
 * Nocido. Les remplacer ici suffit : rien d'autre dans le code ne les connaît.
 *
 * **Pourquoi ce fichier existe.** Le domaine nu n'appartient à aucune société.
 * `GET /public/company` y répond donc 404 — c'est correct, et voulu depuis C8
 * bloc 5 : rendre la première société de la table ferait porter à la landing
 * de Nocido la marque d'un client. Le web retombait alors sur
 * `DEFAULT_COMPANY_SETTINGS`, dont les gris disent « configuration absente ».
 *
 * C'est l'origine exacte du rendu fade : la vitrine commerciale était peinte
 * avec la palette d'un INCIDENT. Le repli neutre reste juste pour un tenant
 * dont l'API ne répond pas ; il était faux pour un domaine qui n'a simplement
 * pas de tenant, et ces deux cas remontaient tous deux en 404.
 *
 * **Ce n'est pas une marque de tenant.** Aucun client ne peut la choisir, elle
 * ne vit pas en base, et elle ne passe pas par `Paramètres > Société`. La règle
 * « pas de couleur de marque en dur » (CLAUDE.md, règle 3) vise les marques
 * CLIENTES, qui varient d'une installation à l'autre ; celle de la plateforme
 * qui les héberge est fixe par nature — et elle est ici, en un seul endroit,
 * pas dispersée dans une feuille de style.
 */
export const PLATFORM_BRAND = {
  /** Bleu franc. */
  primaryColor: '#2563EB',
  /** Ardoise. */
  secondaryColor: '#0F172A',
} as const;

/** Le nom affiché, et celui de l'onglet du navigateur, sur le domaine nu. */
export const PLATFORM_NAME = 'Nocido';

/**
 * La configuration servie sur le domaine NU.
 *
 * Même forme qu'une société pour que le layout, `<CompanyStyle />` et les
 * composants publics n'aient aucun cas particulier à connaître : la landing
 * plateforme se peint par le même chemin qu'une landing de tenant.
 *
 * `locales` porte les trois langues et `defaultLocale` le français, comme un
 * tenant neuf (C9 bloc 1).
 */
export const PLATFORM_COMPANY_SETTINGS: CompanySettings = {
  id: 'cmp-platform',
  name: PLATFORM_NAME,
  shortName: PLATFORM_NAME,
  logo: '/brands/nocido.svg',
  logoBackground: null,
  icon: null,
  primaryColor: PLATFORM_BRAND.primaryColor,
  secondaryColor: PLATFORM_BRAND.secondaryColor,
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
  email: 'contact@nocido.com',
  website: null,
  theme: {},
};
