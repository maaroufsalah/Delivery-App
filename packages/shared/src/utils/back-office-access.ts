import { landingPathFor, type LandingIdentity } from './landing';

/**
 * QUI A LE DROIT D'OUVRIR QUELLE ROUTE DU BACK-OFFICE — B-20, C14 bloc 2.
 *
 * Relevé en production le 2026-09-19, avec `client@gmail.com` : un
 * e-commerçant qui tape `/fr/zones`, `/fr/couriers`, `/fr/clients` ou
 * `/fr/remittances` reçoit `200` et l'écran d'administration. Les listes sont
 * vides — la portée serveur fait son travail — mais l'écran est là, avec ses
 * boutons « Nouveau livreur » et « Exporter ».
 *
 * **La cause est le bon choix de L1 bloc 7.** La coque e-commerçant ENVELOPPE
 * les écrans du back-office au lieu d'en dupliquer des copies (D-090) : deux
 * formulaires de création de colis à tenir d'accord auraient divergé sur
 * l'écran qui fait entrer l'argent. La conséquence est que les URL restent
 * celles du back-office, et que seule la barre change. L1 a retiré le menu,
 * pas l'accès.
 *
 * **Ce que cette règle N'EST PAS** : une garantie de confidentialité. Celle-là
 * est serveur, et elle tient déjà — les écritures sont refusées par
 * `directory.write`, les lectures sensibles par leur clé, les listes bornées
 * par la portée (D-082), et la grille tarifaire depuis C14 bloc 1. Ce qu'on
 * corrige ici est **la surface et la confusion** : un vendeur ne doit pas
 * tomber sur un écran qui ne lui est pas destiné, même vide.
 */

/**
 * Les routes du groupe `(app)` — le back-office, premier segment par premier
 * segment.
 *
 * **Écrite à la main, et vérifiée par un test** qui la compare aux dossiers
 * réels de `app/[locale]/(app)`. La dériver du système de fichiers aurait été
 * séduisant et faux : cette règle tourne aussi dans le proxy, où il n'y a pas
 * de dossier à lire, et dans un bundle où il n'y en a plus du tout.
 *
 * Ce qui n'est PAS dedans passe sans être examiné : la connexion, le site
 * public, le suivi de colis, la console plateforme, et l'espace livreur `/me`.
 * C'est ce qui évite la boucle — un livreur renvoyé vers `/me` par une règle
 * qui interdirait aussi `/me` tournerait en rond jusqu'à l'erreur du
 * navigateur.
 */
export const BACK_OFFICE_ROUTES = [
  '/agencies',
  '/cheques',
  '/cities',
  '/claims',
  '/clients',
  '/cod-payouts',
  '/couriers',
  // Stock Livreur — lot M (D-222). Back-office seul : absent de MERCHANT_ROUTES.
  '/courier-stock',
  '/dashboard',
  '/delivery-types',
  '/districts',
  // Les échanges de colis — chantier « Finance », point 6. Back-office ET
  // vendeur : la mission demande qu'il les VOIE (cf. MERCHANT_ROUTES).
  '/exchanges',
  '/dropoff-notes',
  '/feed',
  '/finance',
  '/invoices',
  // « Factures clients pro » — recette 3, point 4. Back-office seul : absente
  // de MERCHANT_ROUTES, le vendeur lit les siennes sur /invoices.
  '/invoices-pro',
  '/news',
  '/notifications',
  '/orders',
  '/parcels',
  '/payments',
  // La petite caisse — chantier « Caisse, OV, Petite caisse », point 3. Back-office
  // seul : absente de MERCHANT_ROUTES, un vendeur n'y entre pas.
  '/petty-cash',
  '/pickup-notes',
  '/pricing',
  '/products',
  '/profile',
  '/remittances',
  '/returns',
  '/roles',
  '/routes',
  '/settings',
  '/stock',
  '/storage',
  '/storage-requests',
  '/team',
  // Les ordres de virement déjà générés — consultation seule, back-office seul.
  '/transfer-orders',
  '/users',
  '/zones',
] as const;

/**
 * Ce qu'un compte E-COMMERÇANT peut ouvrir dans le back-office.
 *
 * C'est exactement la navigation de sa coque (`merchant-shell.tsx`), plus ce
 * qui en découle — un détail de colis, une facture, son mot de passe. Les
 * préfixes sont comparés sur une frontière de SEGMENT : `/parcels` ouvre
 * `/parcels/CAS-2609-0001`, jamais `/parcels-internes`.
 *
 * **Une liste blanche, pas une liste noire.** Un écran d'administration ajouté
 * demain sera fermé au marchand sans que personne y pense ; l'inverse aurait
 * rouvert la porte au premier écran oublié.
 */
export const MERCHANT_ROUTES = [
  '/dashboard',
  '/parcels',
  /*
   * SES chèques — chantier « Chèques », point 2. Le chèque lui appartient ; la
   * société ne fait que le transporter. Il en suit le trajet, en lecture
   * seule : la portée du service ne lui donne que les siens, et les trois
   * gestes lui répondent 403.
   */
  '/cheques',
  // Il VOIT les échanges de ses colis — point 6 — et n'en ouvre aucun : c'est
  // le transport de sa marchandise, pas sa décision. Le service refuse
  // l'écriture ; la route ne fait qu'ouvrir la lecture.
  '/exchanges',
  '/returns/pending',
  '/invoices',
  '/cod-payouts',
  '/claims',
  '/feed',
  '/profile',
  /*
   * V2 vendeur bloc 1 — ce que la spécification d'Anass demande en plus, et
   * que le vendeur avait DÉJÀ le droit de lire sans pouvoir l'atteindre.
   *
   * Les quatre écrans existent, servis par des endpoints que `MERCHANT` porte
   * depuis toujours : `notes.read` pour les bons, `warehouse.read` pour les
   * produits et le stock. La liste blanche, elle, ne les nommait pas — donc le
   * proxy renvoyait le vendeur sur son tableau de bord, et personne ne voyait
   * l'entrée de menu parce qu'il n'y en avait pas.
   *
   * **Ce qui s'ouvre s'ouvre par permission, jamais par portée** : chaque
   * écran reste borné à ses propres lignes par `scopeWhere()` (D-082), et ce
   * chantier n'y touche pas.
   */
  '/pickup-notes',
  '/dropoff-notes',
  '/products',
  '/stock',
  /*
   * Le centre de notifications — V2 vendeur bloc 8. Ouvert à TOUS les comptes,
   * vendeur compris : ses propres notifications ne sont pas une ressource du
   * domaine, et il n'y a rien à borner qu'un `userId` que le serveur lit
   * lui-même.
   */
  '/notifications',
] as const;

/**
 * Ce qu'un LIVREUR peut ouvrir dans le back-office : rien.
 *
 * Son espace est `(courier)/me` tout entier (D-068), et le layout l'y renvoie
 * déjà depuis L1 bloc 0 — mais après avoir été atteint, c'est-à-dire une fois
 * la requête rendue. Ici, il ne l'atteint plus.
 */
export const COURIER_ROUTES: readonly string[] = [];

/** `/fr/parcels/CAS-1` → `/parcels/CAS-1`. Le préfixe de langue ne décide rien. */
export function withoutLocale(pathname: string, locales: readonly string[]): string {
  for (const locale of locales) {
    if (pathname === `/${locale}`) return '/';
    if (pathname.startsWith(`/${locale}/`)) return pathname.slice(locale.length + 1);
  }

  return pathname;
}

/** Le chemin est-il ce préfixe, ou dessous ? Comparé par SEGMENT. */
function isUnder(pathname: string, prefix: string): boolean {
  return pathname === prefix || pathname.startsWith(`${prefix}/`);
}

/** Cette route appartient-elle au back-office ? Le reste ne nous regarde pas. */
export function isBackOfficeRoute(pathname: string): boolean {
  return BACK_OFFICE_ROUTES.some((route) => isUnder(pathname, route));
}

/**
 * Les espaces qui EXIGENT une session, hors back-office — C15 bloc 3.
 *
 * `/me` est l'espace livreur (D-068), `/console` celui de la plateforme. Ni
 * l'un ni l'autre n'est dans `BACK_OFFICE_ROUTES`, et les deux tombent pourtant
 * sur une page vide quand la session meurt.
 */
const SIGNED_IN_ROUTES = ['/me', '/console'] as const;

/**
 * Cette route demande-t-elle une session ? — C15 bloc 3.
 *
 * Elle sert au proxy pour décider, quand un renouvellement est refusé, s'il
 * faut renvoyer vers la connexion en gardant le chemin demandé — ou laisser
 * passer. La landing, le suivi public et les mentions légales restent servis
 * sans session : y renvoyer quelqu'un vers `/login` serait absurde.
 *
 * `pathname` est attendu SANS préfixe de langue.
 */
export function requiresSession(pathname: string): boolean {
  return isBackOfficeRoute(pathname) || SIGNED_IN_ROUTES.some((route) => isUnder(pathname, route));
}

/**
 * Ce compte a-t-il le droit d'ouvrir cette route ?
 *
 * `pathname` est attendu SANS préfixe de langue — `withoutLocale()` le retire.
 * Un compte qui n'est rattaché ni à une fiche client ni à une fiche livreur est
 * un compte interne : le back-office est son écran, et ce sont ses permissions
 * qui bornent ce qu'il y trouve.
 */
export function canOpenRoute(pathname: string, identity: LandingIdentity): boolean {
  if (!isBackOfficeRoute(pathname)) return true;

  if (identity.courierId) return COURIER_ROUTES.some((route) => isUnder(pathname, route));
  if (identity.clientId) return MERCHANT_ROUTES.some((route) => isUnder(pathname, route));

  return true;
}

/**
 * Où renvoyer ce compte, ou `null` s'il est chez lui.
 *
 * Le repli est son ATTERRISSAGE, pas la racine : un marchand qui tape `/zones`
 * revient sur son tableau de bord, un livreur sur sa tournée. Les renvoyer
 * tous deux à `/` les ferait repasser par la redirection de la page d'accueil,
 * donc deux sauts au lieu d'un.
 */
export function backOfficeRedirect(pathname: string, identity: LandingIdentity): string | null {
  return canOpenRoute(pathname, identity) ? null : landingPathFor(identity);
}
