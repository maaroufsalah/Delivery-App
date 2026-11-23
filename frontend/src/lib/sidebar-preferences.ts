import { NAV_SECTIONS, type NavSectionKey } from '@/lib/navigation-config';

/**
 * Préférences d'affichage de la barre latérale — C10 blocs 4 et 5.
 *
 * ⚠️ **Ce fichier ne doit importer AUCUNE API serveur** : il est chargé par un
 * composant client. La lecture des cookies vit dans
 * `sidebar-preferences.server.ts`, appelée par le layout.
 *
 * **Pourquoi un cookie et pas `localStorage`.** `localStorage` n'est lisible
 * qu'après hydratation : la barre se peindrait d'abord avec l'état par défaut,
 * puis sauterait à l'état retenu. Un cookie est présent dans la requête, donc
 * connu au rendu serveur, et le premier pixel est déjà le bon. C'est le même
 * raisonnement que D-006 pour la marque, et le même que pour le thème.
 */

/** Section dépliée. Vide = toutes repliées, ce qui est un état légitime. */
export const NAV_SECTION_COOKIE = 'dp.nav';

/** Barre réduite en icônes seules. */
export const NAV_RAIL_COOKIE = 'dp.rail';

/** Section ouverte quand rien n'a été choisi ni visité (mission, bloc 2). */
export const DEFAULT_SECTION: NavSectionKey = 'parcels';

/** Un an : une préférence d'affichage n'a pas de raison d'expirer plus tôt. */
const ONE_YEAR_SECONDS = 60 * 60 * 24 * 365;

/** `null` plutôt qu'une valeur inventée si le cookie a été bricolé à la main. */
export function parseSectionKey(value: string | undefined): NavSectionKey | null {
  if (!value) return null;
  return NAV_SECTIONS.some((section) => section.key === value) ? (value as NavSectionKey) : null;
}

/**
 * La section qui contient une route, ou `null`.
 *
 * Le préfixe compte : `/parcels/CAS0100124` doit ouvrir « Colis » comme
 * `/parcels`. On retient la correspondance la PLUS LONGUE — `/storage` et
 * `/storage-requests` vivent dans deux sections différentes, et un simple
 * `startsWith` ferait gagner la première déclarée.
 */
export function sectionOfPath(pathname: string): NavSectionKey | null {
  let best: { key: NavSectionKey; length: number } | null = null;

  for (const section of NAV_SECTIONS) {
    for (const item of section.items) {
      const matches = pathname === item.href || pathname.startsWith(`${item.href}/`);
      if (matches && (best === null || item.href.length > best.length)) {
        best = { key: section.key, length: item.href.length };
      }
    }
  }

  return best?.key ?? null;
}

/**
 * L'état d'ouverture au PREMIER rendu, serveur et client confondus.
 *
 * L'ordre n'est pas arbitraire. La section de la page courante l'emporte : une
 * barre qui n'indique pas où l'on se trouve a raté son travail principal, et
 * c'est ce que demande le bloc 3. Le cookie ne décide donc que lorsque la page
 * n'appartient à aucune section — Tableau de bord, Paramètres.
 *
 * Cette fonction doit rester PURE et déterministe : elle est évaluée des deux
 * côtés du rendu, et toute divergence produirait un écart d'hydratation.
 */
export function initialSection(
  pathname: string,
  cookieValue: NavSectionKey | null,
): NavSectionKey | null {
  return sectionOfPath(pathname) ?? cookieValue ?? DEFAULT_SECTION;
}

/**
 * Écriture depuis le client, en `document.cookie`.
 *
 * Pas de Server Action : replier une section est une préférence d'affichage,
 * pas une donnée. Un aller-retour serveur à chaque clic ferait payer une
 * latence réseau pour une animation, et invaliderait le cache de route pour
 * rien.
 *
 * `SameSite=Lax` suffit — le cookie ne porte aucune autorisation — et `Secure`
 * suit le protocole pour ne pas casser le développement en HTTP.
 */
export function writePreference(name: string, value: string): void {
  if (typeof document === 'undefined') return;

  const secure = window.location.protocol === 'https:' ? '; Secure' : '';
  document.cookie = `${name}=${encodeURIComponent(value)}; Path=/; Max-Age=${ONE_YEAR_SECONDS}; SameSite=Lax${secure}`;
}
