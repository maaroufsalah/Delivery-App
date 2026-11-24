/**
 * LA MARQUE SERVIE PAR CE BINAIRE — C15 bloc 4.
 *
 * **Pourquoi une constante de build et non un écran.** L'application a été
 * conçue mono-binaire multi-client (D-075) : elle demandait son « nom court »
 * au premier lancement et appelait `https://<slug>.delivery.nocido.com`. C'est
 * la bonne architecture pour la plateforme — un seul compte développeur, une
 * seule fiche de magasin, une seule revue.
 *
 * Ce n'est pas la bonne EXPÉRIENCE pour le livreur qui l'installe. Il ouvre
 * l'application de son transporteur et tombe sur un formulaire qui lui demande
 * un mot qu'il ne connaît pas, avant même d'avoir vu un logo. Tant qu'un seul
 * transporteur est servi, cet écran ne fait que coûter des appels à l'agence.
 *
 * **Le multi-tenant reste entier dans le code** — `tenant.ts`, l'écran Société,
 * la résolution par hôte. Poser `null` ici le rallume en une ligne, sans rien
 * réécrire : c'est ce qui sera fait quand un deuxième transporteur arrivera,
 * ou dans la variante en marque blanche qui portera son propre binaire.
 *
 * **Elle vit dans le bundle JavaScript, donc elle part en OTA.** La changer ne
 * demande pas de nouvel APK — contrairement au nom de l'application et à son
 * icône, qui sont, eux, natifs. C'est ce qui permettra de rallumer le
 * multi-client sans passer par un magasin.
 */

/** Le sous-domaine servi, ou `null` pour redemander la société au lancement. */
export const BRAND_TENANT: string | null = 'wimo';

/** L'application sert-elle un seul transporteur ? */
export const IS_SINGLE_TENANT = BRAND_TENANT !== null;

/**
 * Le logo de la marque servie — dernières corrections, point 5.
 *
 * Une IMAGE DU BUNDLE, comme l'icône et l'écran de démarrage le sont du
 * binaire : l'écran de connexion l'affiche avant toute requête, hors ligne
 * compris. Il est BLANC sur transparence (généré par
 * `scripts/mobile-brand-assets.py` depuis `docs/reference/`) : il se pose
 * toujours sur le dégradé de la marque, jamais sur un fond clair.
 */
export const BRAND_LOGO = require('../../assets/brand-logo.png') as number;

/** Largeur / hauteur de `brand-logo.png` (480 × 167). */
export const BRAND_LOGO_RATIO = 480 / 364;

/**
 * LE NOM COURT DE REPLI — lot D, D2 (D-189).
 *
 * « Reçu par Speedy », « Remis à Speedy » : les libellés lisent le nom court
 * réglé dans Paramètres > Société, que `/public/company` rend. Avant la
 * première réponse, et hors ligne sans cache, c'est celui-ci. Une constante du
 * binaire du tenant, comme `BRAND_TENANT` : elle part en OTA.
 */
export const BRAND_SHORT_NAME = 'Wimo';
