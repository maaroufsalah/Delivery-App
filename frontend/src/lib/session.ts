import { getCurrentUser, type UserProfile } from '@/lib/api/auth';
import { ApiError } from '@/lib/http';

/**
 * Le compte connecté, ou une erreur `401` — C17.
 *
 * **Aucune page ne renvoie plus vers `/login` elle-même, et c'est le
 * correctif.** Les trois coques appelaient `redirect({ href: '/login' })`
 * depuis le serveur. Ce `307`-là REMPLACE la réponse construite par le proxy,
 * `Set-Cookie` compris : une session renouvelée à l'instant même repartait à la
 * poubelle avec le renvoi, et il fallait trois navigations pour retrouver son
 * écran — la première atterrissant sur la connexion.
 *
 * La décision vit donc à un seul endroit, `proxy.ts`, qui la prend AVANT le
 * rendu et sans rien écraser : plus de cookies du tout, un renouvellement
 * refusé, un jeton d'un autre espace — les trois renvoient vers `/login`, avec
 * la page demandée en `?next=`.
 *
 * Ce qui reste ici est le cas que le proxy ne PEUT pas voir : un jeton d'accès
 * encore valable dans le temps, mais révoqué depuis (une déconnexion sur un
 * autre appareil). L'API répond `401`, on lève, et la frontière d'erreur
 * (`app/[locale]/error.tsx`, C15 bloc 3) fait le saut vers la connexion côté
 * navigateur — sans `307`, donc sans rien écraser.
 */
export async function requireSession(): Promise<UserProfile> {
  const user = await getCurrentUser();
  if (user) return user;

  throw new ApiError('Session terminée.', 401, 'UNAUTHENTICATED');
}
