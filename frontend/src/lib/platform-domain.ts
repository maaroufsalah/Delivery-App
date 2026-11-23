/**
 * Le domaine de la plateforme — C8 bloc 5, D-048.
 *
 * `delivery.nocido.com` en production. Il sert à deux choses, et à deux
 * seulement :
 *
 *  - **composer l'hôte d'un tenant** — `<slug>.<domaine>` — pour l'afficher
 *    dans la console et y emmener un administrateur ;
 *  - **reconnaître le domaine NU**, qui n'appartient à aucun client et sert la
 *    landing de la plateforme.
 *
 * **Ce n'est pas un mécanisme d'isolation.** C'est l'API qui résout le tenant à
 * partir de l'hôte réel de la requête et qui filtre tout ce qui suit (D-049).
 * Une valeur fausse ici produit un lien qui ne mène nulle part, jamais la
 * donnée d'un autre.
 *
 * Le repli sur `localhost:3000` garde le développement fonctionnel sans
 * fichier d'environnement, comme le fait déjà l'API pour la même variable.
 */

const FALLBACK = 'localhost:3000';

export function platformDomain(): string {
  const value = process.env.NEXT_PUBLIC_PLATFORM_DOMAIN?.trim();

  return value && value !== '' ? value.toLowerCase() : FALLBACK;
}

/**
 * Le slug porté par un hôte, ou `null`.
 *
 * `wimo.delivery.nocido.com` → `wimo`. Le domaine nu rend `null` : c'est la
 * plateforme elle-même, pas un client.
 *
 * **Un seul niveau de sous-domaine**, comme côté API : accepter `a.b.domaine`
 * ouvrirait la résolution à des hôtes qu'aucun certificat ne couvre.
 */
export function slugFromHost(host: string | null | undefined): string | null {
  const hostname = (host ?? '').split(':')[0]?.trim().toLowerCase() ?? '';
  if (hostname === '') return null;

  const domain = platformDomain().split(':')[0] ?? '';
  const suffix = `.${domain}`;

  if (!hostname.endsWith(suffix)) return null;

  const slug = hostname.slice(0, -suffix.length);
  if (slug === '' || slug.includes('.')) return null;

  return slug;
}

/** L'hôte est-il le domaine NU de la plateforme ? */
export function isPlatformHost(host: string | null | undefined): boolean {
  const hostname = (host ?? '').split(':')[0]?.trim().toLowerCase() ?? '';
  const domain = platformDomain().split(':')[0] ?? '';

  return hostname !== '' && hostname === domain;
}
