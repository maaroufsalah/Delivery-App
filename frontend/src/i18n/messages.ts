import { getMessages } from 'next-intl/server';

/**
 * Découpage du catalogue de traductions par groupe de routes — C7 bloc 4.
 *
 * **La dette.** `<NextIntlClientProvider>` sans prop `messages` appelle
 * `getMessages()` et sérialise le catalogue ENTIER dans le HTML. Posé au layout
 * racine, il faisait voyager les deux mille clés du back-office sur la page
 * d'accueil, qui en utilise une vingtaine. Le visiteur d'une landing page
 * téléchargeait le vocabulaire de la facturation, des réclamations et de
 * l'entrepôt.
 *
 * **Le remède.** Un fournisseur PAR groupe de routes, chacun avec les seuls
 * espaces de noms que ses composants CLIENT réclament. Les composants serveur
 * n'en ont pas besoin : ils lisent la configuration de la requête, pas le
 * contexte React.
 *
 * Les fournisseurs de `use-intl` ne fusionnent pas : celui d'un groupe REMPLACE
 * celui de la racine pour son sous-arbre. Chaque liste doit donc être complète
 * pour son groupe, `error` compris là où une frontière d'erreur peut tomber.
 */

/** Un catalogue de messages, tel que next-intl le manipule. */
type Catalogue = Record<string, unknown>;

/**
 * Espaces de noms du layout RACINE.
 *
 * `error.tsx` vit au niveau `[locale]`, hors des groupes : sa frontière rend
 * dans le fournisseur racine, et c'est le seul composant client à le faire.
 */
export const ROOT_NAMESPACES = ['error'] as const;

/**
 * Site public. Le tracking et la grille tarifaire sont les deux seuls écrans
 * interactifs ; l'en-tête porte le sélecteur de langue et le mode sombre.
 */
export const PUBLIC_NAMESPACES = [
  'error',
  'public',
  'locale',
  'theme',
  'common',
  /*
   * `table` — recette SpeedyTrans 4, point 3 : la grille des tarifs de la
   * landing passe par LA DataTable (`features/public/rates-table.tsx`). Sans
   * lui, chaque visite de la page d'accueil écrivait
   * `MISSING_MESSAGE: table (fr)` dans les journaux du web.
   */
  'table',
] as const;

/** Connexion : un formulaire, ses messages de validation, et la même en-tête. */
export const AUTH_NAMESPACES = [
  'error',
  'auth',
  'common',
  'validation',
  'locale',
  'theme',
] as const;

/**
 * Console plateforme — C8 bloc 4.
 *
 * La liste la plus courte de toutes, et c'est voulu : cette console ne partage
 * RIEN avec le back-office d'un tenant. Elle ne parle ni de colis, ni de
 * tournées, ni de facturation — elle parle d'espaces clients. Lui donner le
 * catalogue métier ferait voyager deux mille clés pour un écran qui en utilise
 * quarante, et brouillerait la frontière que tout le reste s'emploie à tenir.
 */
export const PLATFORM_NAMESPACES = [
  'error',
  'platform',
  'common',
  'validation',
  'locale',
  'theme',
  'table',
] as const;

/**
 * Espace LIVREUR — C11 bloc 7, D-068.
 *
 * Un livreur consulte sa journée sur un téléphone, souvent en 3G et souvent
 * dehors. Il reçoit donc le strict nécessaire : son propre vocabulaire, celui
 * des statuts de colis, et rien du back-office — ni tarifs, ni entrepôt, ni
 * réclamations, ni référentiel.
 */
export const COURIER_NAMESPACES = [
  'error',
  'courier',
  // La coque du livreur porte ses propres entrées de menu (L1 bloc 1) : elles
  // vivent sous `courier.nav`, donc rien à ajouter ici — mais `accounts` l'est,
  // parce que « Mon profil » réutilise les deux formulaires du back-office.
  'accounts',
  /*
   * `claims` depuis le complément de L2 : le livreur ouvre et suit les
   * réclamations de ses colis (B-19), et les trois écrans réutilisent les
   * composants du back-office — donc ses clés.
   */
  'claims',
  'common',
  'validation',
  'locale',
  'theme',
  'status',
  'statusGroup',
  'finance',
  /*
   * `returns` depuis le chantier « Bon de retour livreur » : le livreur ne
   * CONSULTE plus seulement ses retours, il en ouvre. L'assistant, le bouton
   * flottant et la fiche d'un bon sont des composants CLIENT, et ils lisent le
   * même vocabulaire que le back-office — c'est la demande du point 7 du
   * chantier précédent, « les mêmes libellés dans les trois espaces ».
   *
   * Sans lui, ces écrans rendraient leurs clés brutes : un fournisseur de
   * next-intl REMPLACE celui de la racine pour son sous-arbre, il ne le
   * complète pas.
   */
  'returns',
  /*
   * `table` — recette SpeedyTrans 4, point 3. Relevés et remises du livreur
   * passent par LA DataTable (pagination, barre d'outils) : sans lui, les
   * journaux du web portaient `MISSING_MESSAGE: table (fr)` à chaque ouverture.
   */
  'table',
] as const;

/**
 * Back-office. Tout SAUF `public` : la landing n'a rien à faire dans le bundle
 * d'un agent, et c'est la moitié du gain dans l'autre sens.
 */
export const APP_EXCLUDED_NAMESPACES = ['public', 'platform'] as const;

/** Ne garde que les espaces demandés. Un espace absent est ignoré en silence. */
export function pickNamespaces(messages: Catalogue, namespaces: readonly string[]): Catalogue {
  const picked: Catalogue = {};

  for (const namespace of namespaces) {
    if (namespace in messages) picked[namespace] = messages[namespace];
  }

  return picked;
}

/** Tout sauf les espaces nommés. */
export function omitNamespaces(messages: Catalogue, namespaces: readonly string[]): Catalogue {
  const excluded = new Set<string>(namespaces);
  const kept: Catalogue = {};

  for (const [key, value] of Object.entries(messages)) {
    if (!excluded.has(key)) kept[key] = value;
  }

  return kept;
}

export async function messagesFor(namespaces: readonly string[]): Promise<Catalogue> {
  return pickNamespaces((await getMessages()) as Catalogue, namespaces);
}

export async function messagesWithout(namespaces: readonly string[]): Promise<Catalogue> {
  return omitNamespaces((await getMessages()) as Catalogue, namespaces);
}
