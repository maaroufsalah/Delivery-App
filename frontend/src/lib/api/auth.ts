import type { PermissionKey } from '@delivery/shared';
import { ApiError, apiFetch } from '@/lib/http';

/** Session — `GET /auth/me`. La connexion passe par une Server Action. */

/**
 * État commercial de l'espace — C8 bloc 4, D-050.
 *
 * `null` pour un administrateur de plateforme : il n'appartient à aucun tenant.
 */
export interface TenantBanner {
  status: 'TRIAL' | 'ACTIVE' | 'SUSPENDED';
  trialEndsAt: string | null;
  /** Jours restants, arrondis vers le haut. Négatif = échéance dépassée. */
  trialDaysLeft: number | null;
  /** L'essai est terminé : l'API refuse les écritures, la lecture reste ouverte. */
  expired: boolean;
}

export interface UserProfile {
  id: string;
  /** `null` pour un livreur qui se connecte par son nom d'utilisateur (point 5). */
  email: string | null;
  username: string | null;
  firstName: string;
  lastName: string;
  /** C13 bloc 4 — « Mon profil » le modifie. */
  phone: string | null;
  roles: string[];
  agencyId: string | null;
  /**
   * Rattachements MÉTIER du compte, tels que l'API les sert depuis C8.
   *
   * Ils n'étaient pas déclarés ici, faute d'usage côté web. L'espace livreur
   * (C11 bloc 7, D-068) s'ouvre sur `courierId` : c'est le RATTACHEMENT qui
   * rend l'espace utile, pas le rôle — un compte portant le rôle sans fiche
   * n'aurait que des listes vides.
   */
  clientId: string | null;
  courierId: string | null;
  /** Compte invité : l'écran de changement de mot de passe s'impose. */
  mustChangePassword: boolean;
  /**
   * Permissions EFFECTIVES, lues dans la matrice en base. Le menu s'y filtre.
   * Les recalculer ici depuis les rôles utiliserait la matrice par défaut,
   * alors qu'elle est éditable : le menu proposerait des écrans interdits.
   */
  permissions: PermissionKey[];
  /** De quoi afficher le bandeau d'état sans un appel de plus. */
  tenant: TenantBanner | null;
  /**
   * Session ouverte depuis la console plateforme — C8, D-051.
   *
   * Elle n'ouvre aucun droit de plus : c'est une session ordinaire du tenant.
   * Le drapeau ne sert qu'à le DIRE, pour qu'on n'écrive pas chez un client en
   * croyant être chez soi.
   */
  impersonated?: boolean;
}

/**
 * Profil du compte connecté, ou `null` si la session est terminée.
 *
 * `null` plutôt qu'une exception : l'absence de session est un état ordinaire
 * du back-office (premier accès, jeton expiré), pas une panne. Le layout en
 * déduit une redirection.
 */
export async function getCurrentUser(): Promise<UserProfile | null> {
  try {
    return await apiFetch<UserProfile>('/auth/me');
  } catch (error) {
    if (error instanceof ApiError && error.isUnauthenticated) return null;
    throw error;
  }
}
