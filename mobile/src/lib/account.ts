import type { ForgotPasswordResult } from '@delivery/shared';
import { api } from './api';

/**
 * Les deux gestes du COMPTE — C12 bloc 2b et 2c.
 *
 * **Aucun des deux ne passe par la file hors-ligne.** Ce n'est pas un oubli :
 * rejouer une demande de mot de passe oublié trois heures plus tard n'a aucun
 * sens, et différer un changement de mot de passe laisserait l'utilisateur
 * croire qu'il a changé alors que l'ancien fonctionne encore — c'est exactement
 * le genre de mensonge qui coûte un accès le lendemain matin. Les deux exigent
 * le réseau, et le disent quand il manque.
 */

/**
 * « Mot de passe oublié » — C12 bloc 2b.
 *
 * `anonymous` : il n'y a justement pas de session, et présenter un jeton
 * périmé ferait partir un renouvellement pour rien avant l'appel.
 *
 * La réponse est la MÊME que l'adresse existe ou non : l'écran n'a donc rien à
 * en déduire, il affiche ce qu'elle porte — le téléphone de l'agence.
 */
export async function forgotPassword(email: string): Promise<ForgotPasswordResult> {
  return api<ForgotPasswordResult>('/auth/forgot-password', {
    method: 'POST',
    body: { email: email.trim() },
    anonymous: true,
  });
}

/**
 * Changer SON mot de passe — C12 bloc 2c.
 *
 * L'ancien est exigé, même sous `mustChangePassword` : un téléphone resté
 * déverrouillé sur un comptoir ne doit pas suffire à s'approprier le compte.
 *
 * **Le serveur révoque toutes les sessions.** Le jeton courant tombe donc avec
 * les autres, et l'appelant doit se déconnecter derrière — sinon l'application
 * continue avec un jeton mort et le découvre au premier écran suivant, sous la
 * forme d'une erreur qui n'explique rien.
 */
export async function changeOwnPassword(input: {
  currentPassword: string;
  password: string;
  confirmation: string;
}): Promise<void> {
  await api<{ ok: true }>('/users/me/password', { method: 'POST', body: input });
}
