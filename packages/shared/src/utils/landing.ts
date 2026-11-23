import type { UserRole } from '../enums';

/**
 * OÙ ATTERRIT un compte après sa connexion — C13 bloc 6.
 *
 * Une seule fonction, partagée, parce que la question se pose à trois
 * endroits : le formulaire de connexion, le tableau de bord qui redirige ce
 * qui n'est pas pour lui, et le test qui vérifie les quatre rôles. Trois
 * réponses écrites séparément auraient divergé au premier rôle ajouté.
 *
 * **Le RATTACHEMENT d'abord, le rôle ensuite** — la règle de D-072, déjà celle
 * de `spaceOf()` côté mobile. Un compte lié à une fiche livreur est un livreur,
 * quelle que soit l'étiquette qu'on lui a posée ; l'inverse — un rôle `COURIER`
 * sans fiche — n'ouvre rien, parce qu'il n'y a aucune tournée à montrer.
 *
 * L'e-commerçant, lui, atterrit bien sur `/dashboard` : le tableau de bord est
 * borné à SES colis par `RequestScope.clientId`, et c'est donc déjà son propre
 * tableau de bord. Lui en construire un second serait le même écran sous un
 * autre nom.
 */
export interface LandingIdentity {
  clientId: string | null;
  courierId: string | null;
  roles: readonly UserRole[] | readonly string[];
}

export function landingPathFor(identity: LandingIdentity): string {
  /*
   * Le livreur d'abord : c'est le seul rôle dont le back-office n'est pas
   * l'écran. Le sien est `(courier)/me` — sa tournée, ses remises, ses relevés
   * (C11 bloc 7, D-068). L'envoyer sur `/dashboard` lui montrerait des KPI
   * bornés à ses propres colis, c'est-à-dire un écran vide avec des zéros là
   * où il attend la liste de ses courses.
   */
  if (identity.courierId) return '/me';

  return '/dashboard';
}

/** Vrai quand `/dashboard` n'est PAS l'écran de ce compte. */
export function belongsElsewhere(identity: LandingIdentity): boolean {
  return landingPathFor(identity) !== '/dashboard';
}
