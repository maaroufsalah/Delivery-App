'use server';

import {
  type AgencyPayload,
  agencySchema,
  type BusinessSettings,
  type BusinessSettingsPayload,
  businessSettingsSchema,
  changePasswordSchema,
  type ClientDiscountPayload,
  clientDiscountSchema,
  type ClientDocumentPayload,
  clientDocumentSchema,
  type ClientPayload,
  type ClientPickupAddressPayload,
  type ClientPickupTermsPayload,
  clientPickupTermsSchema,
  clientPickupAddressSchema,
  clientSchema,
  type CourierCitiesPayload,
  courierCitiesSchema,
  type CourierPayload,
  courierSchema,
  createUserSchema,
  type RoleMatrix,
  type RoleMatrixPayload,
  roleMatrixSchema,
  setUserPasswordSchema,
  type UpdateUserPayload,
  updateOwnProfileSchema,
  updateUserSchema,
  type UserInvitation,
  type UserPermissions,
  userPermissionsSchema,
} from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import {
  addClientDiscount,
  addClientDocument,
  addClientPickupAddress,
  changeOwnPassword,
  createAgency,
  createClient,
  createClientAccount,
  createCourier,
  createCourierAccount,
  deleteAgency,
  deleteClient,
  deleteClientDiscount,
  deleteClientDocument,
  deleteClientPickupAddress,
  downloadClientDocument,
  deleteCourier,
  deleteUser,
  createUser,
  setAgencyActive,
  setClientActive,
  setClientPro,
  setClientVatRate,
  setClientInternal,
  setClientPickupTerms,
  setClientValidated,
  validateClient,
  rejectClient,
  validateCourier,
  rejectCourier,
  setCourierActive,
  setCourierCities,
  setCourierValidated,
  setUserActive,
  setUserPassword,
  updateAgency,
  updateBusinessSettings,
  updateClient,
  updateClientDiscount,
  updateCourier,
  updateRoleMatrix,
  updateOwnProfile,
  updateUser,
  updateUserPermissions,
  resetUserPermissions,
} from '@/lib/api/accounts';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';

/**
 * Écritures du menu COMPTES.
 *
 * Même forme que le référentiel : la Server Action revalide l'entrée, appelle
 * l'endpoint, et laisse remonter le décompte des références en cas de 409.
 * Un composant client n'appelle jamais `src/lib/api/*` directement (D-018).
 */

function revalidateAccounts(route: string): void {
  revalidatePath(`/[locale]/(app)/${route}`, 'page');
  // Le menu est filtré par les permissions, et l'en-tête porte l'identité :
  // une modification de compte ou de matrice peut les changer.
  revalidatePath('/[locale]/(app)', 'layout');
}

/* --- Utilisateurs -------------------------------------------------------- */

/**
 * Création d'un compte — C13 bloc 3, D-086.
 *
 * Rien n'est renvoyé : il n'y a plus de mot de passe temporaire à montrer une
 * fois, puisque celui qui crée le compte vient de le taper.
 */
export async function createUserAction(input: unknown): Promise<ReferentialResult> {
  const parsed = createUserSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await createUser(parsed.data);
    revalidateAccounts('users');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function updateUserAction(id: string, input: unknown): Promise<ReferentialResult> {
  const parsed = updateUserSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await updateUser(id, parsed.data as UpdateUserPayload);
    revalidateAccounts('users');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * « Mon profil » — C13 bloc 4.
 *
 * `revalidateAccounts('profile')` revalide la page ET le layout : le nom et
 * l'adresse s'affichent dans le menu du bas, sur toutes les pages. Ne
 * revalider que la page laisserait l'ancien nom sous les yeux de qui vient de
 * le corriger.
 */
export async function updateOwnProfileAction(input: unknown): Promise<ReferentialResult> {
  const parsed = updateOwnProfileSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await updateOwnProfile(parsed.data);
    revalidateAccounts('profile');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function setUserActiveAction(
  id: string,
  isActive: boolean,
): Promise<ReferentialResult> {
  try {
    await setUserActive(id, isActive);
    revalidateAccounts('users');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Mot de passe POSÉ par l'administrateur — C12 bloc 2a.
 *
 * `revalidateAccounts` couvre `users`, `couriers` et `clients` : le bouton vit
 * sur les fiches, et c'est `mustChangePassword` affiché dans la liste des
 * comptes qui change.
 */
export async function setUserPasswordAction(
  id: string,
  input: unknown,
): Promise<ReferentialResult<UserInvitation>> {
  // Le mot de passe arrive seul depuis l'onglet « Compte » : le champ est en
  // clair, il n'y a donc rien à confirmer (voir `setUserPasswordSchema`).
  const parsed = setUserPasswordSchema.safeParse(
    typeof input === 'string' ? { password: input } : input,
  );
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const invitation = await setUserPassword(id, parsed.data);
    revalidateAccounts('users');
    revalidateAccounts('couriers');
    revalidateAccounts('clients');
    return { ok: true, data: invitation };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteUserAction(id: string): Promise<ReferentialResult> {
  try {
    await deleteUser(id);
    revalidateAccounts('users');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function changeOwnPasswordAction(input: unknown): Promise<ReferentialResult> {
  const parsed = changePasswordSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await changeOwnPassword(parsed.data);
    // Le profil porte `mustChangePassword` : tout le shell doit se relire.
    revalidatePath('/[locale]/(app)', 'layout');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Rôles --------------------------------------------------------------- */

export async function updateRoleMatrixAction(
  input: unknown,
): Promise<ReferentialResult<RoleMatrix>> {
  const parsed = roleMatrixSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const matrix = await updateRoleMatrix(parsed.data as RoleMatrixPayload);
    revalidateAccounts('roles');
    return { ok: true, data: matrix };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Permissions d'un compte — dernières corrections, point 1 ------------- */

export async function updateUserPermissionsAction(
  id: string,
  input: unknown,
): Promise<ReferentialResult<UserPermissions>> {
  const parsed = userPermissionsSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const permissions = await updateUserPermissions(id, parsed.data);
    revalidateAccounts('users/[id]');
    return { ok: true, data: permissions };
  } catch (error) {
    return toFailure(error);
  }
}

export async function resetUserPermissionsAction(
  id: string,
): Promise<ReferentialResult<UserPermissions>> {
  try {
    const permissions = await resetUserPermissions(id);
    revalidateAccounts('users/[id]');
    return { ok: true, data: permissions };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Agences ------------------------------------------------------------- */

export async function saveAgencyAction(
  id: string | null,
  input: unknown,
): Promise<ReferentialResult> {
  const parsed = agencySchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const body = parsed.data as AgencyPayload;
    if (id) await updateAgency(id, body);
    else await createAgency(body);
    revalidateAccounts('agencies');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function setAgencyActiveAction(
  id: string,
  isActive: boolean,
): Promise<ReferentialResult> {
  try {
    await setAgencyActive(id, isActive);
    revalidateAccounts('agencies');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteAgencyAction(id: string): Promise<ReferentialResult> {
  try {
    await deleteAgency(id);
    revalidateAccounts('agencies');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- E-commerçants ------------------------------------------------------- */

export async function saveClientAction(
  id: string | null,
  input: unknown,
): Promise<ReferentialResult<{ id: string }>> {
  const parsed = clientSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const body = parsed.data as ClientPayload;
    const saved = id ? await updateClient(id, body) : await createClient(body);
    revalidateAccounts('clients');
    return { ok: true, data: { id: saved.id } };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * « CLIENT PRO », DEPUIS LA FICHE — chantier « Bon de retour livreur »,
 * point 0.
 *
 * Un seul champ part sur le fil. C'est tout ce que le `PATCH` partiel a rendu
 * possible, et c'est ce qui change le geste : l'interrupteur qui décide si un
 * vendeur voit le workflow BL se trouve là où on le cherche — sur la fiche —
 * et non derrière un formulaire de douze champs qu'il faut rouvrir, remplir et
 * valider.
 */
export async function setClientProAction(id: string, isPro: boolean): Promise<ReferentialResult> {
  try {
    await setClientPro(id, isPro);
    revalidateAccounts('clients');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/** Client interne — exclu du report des factures négatives (administration, `finance.validate`). */
export async function setClientInternalAction(
  id: string,
  isInternal: boolean,
): Promise<ReferentialResult> {
  try {
    await setClientInternal(id, isInternal);
    revalidateAccounts('clients');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/** Lot R (D-230) — « Taux de TVA applicable » d'un Client Pro (administration). */
export async function setClientVatRateAction(
  id: string,
  vatRate: number | null,
): Promise<ReferentialResult> {
  try {
    await setClientVatRate(id, vatRate);
    revalidateAccounts('clients');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/** Lot T (D-232) — conditions de ramassage d'un client (administration). */
export async function setClientPickupTermsAction(
  id: string,
  terms: ClientPickupTermsPayload,
): Promise<ReferentialResult> {
  const parsed = clientPickupTermsSchema.safeParse(terms);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await setClientPickupTerms(id, parsed.data);
    revalidateAccounts('clients');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function setClientActiveAction(
  id: string,
  isActive: boolean,
): Promise<ReferentialResult> {
  try {
    await setClientActive(id, isActive);
    revalidateAccounts('clients');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function setClientValidatedAction(
  id: string,
  validated: boolean,
): Promise<ReferentialResult> {
  try {
    await setClientValidated(id, validated);
    revalidateAccounts('clients');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * « Valider » et « Refuser » — C13 bloc 2.
 *
 * `toFailure` conserve le `field` renvoyé par l'API : c'est `agencyId` qui dit
 * à l'écran d'ouvrir le sélecteur d'agence, plutôt que de faire deviner au
 * front l'état de la fiche qu'il n'a pas.
 */
export async function validateClientAction(
  id: string,
  agencyId?: string,
): Promise<ReferentialResult> {
  try {
    await validateClient(id, agencyId);
    revalidateAccounts('clients');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function rejectClientAction(id: string, reason: string): Promise<ReferentialResult> {
  try {
    await rejectClient(id, reason);
    revalidateAccounts('clients');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function validateCourierAction(
  id: string,
  agencyId?: string,
): Promise<ReferentialResult> {
  try {
    await validateCourier(id, agencyId);
    revalidateAccounts('couriers');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function rejectCourierAction(id: string, reason: string): Promise<ReferentialResult> {
  try {
    await rejectCourier(id, reason);
    revalidateAccounts('couriers');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteClientAction(id: string): Promise<ReferentialResult> {
  try {
    await deleteClient(id);
    revalidateAccounts('clients');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Crée le compte lié à la fiche — C13 bloc 3.
 *
 * Le formulaire complet, mot de passe compris. La fiche est revalidée ET la
 * liste des comptes : le nouveau compte doit apparaître dans les deux.
 */
export async function createClientAccountAction(
  id: string,
  input: unknown,
): Promise<ReferentialResult> {
  const parsed = createUserSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await createClientAccount(id, parsed.data);
    revalidateAccounts('clients');
    revalidateAccounts('users');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function addClientDiscountAction(
  id: string,
  input: unknown,
): Promise<ReferentialResult> {
  const parsed = clientDiscountSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await addClientDiscount(id, parsed.data as ClientDiscountPayload);
    revalidateAccounts('clients');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Modification d'une remise existante.
 *
 * Retirer puis recréer aurait laissé un intervalle sans remise, pendant lequel
 * un colis enregistré se serait tarifé au plein tarif.
 */
export async function updateClientDiscountAction(
  id: string,
  discountId: string,
  input: unknown,
): Promise<ReferentialResult> {
  const parsed = clientDiscountSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await updateClientDiscount(id, discountId, parsed.data as ClientDiscountPayload);
    revalidateAccounts('clients');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteClientDiscountAction(
  id: string,
  discountId: string,
): Promise<ReferentialResult> {
  try {
    await deleteClientDiscount(id, discountId);
    revalidateAccounts('clients');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function addClientPickupAddressAction(
  id: string,
  input: unknown,
): Promise<ReferentialResult> {
  const parsed = clientPickupAddressSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await addClientPickupAddress(id, parsed.data as ClientPickupAddressPayload);
    revalidateAccounts('clients');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteClientPickupAddressAction(
  id: string,
  addressId: string,
): Promise<ReferentialResult> {
  try {
    await deleteClientPickupAddress(id, addressId);
    revalidateAccounts('clients');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function addClientDocumentAction(
  id: string,
  input: unknown,
): Promise<ReferentialResult> {
  const parsed = clientDocumentSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await addClientDocument(id, parsed.data as ClientDocumentPayload);
    revalidateAccounts('clients');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/** Le contenu revient en base64 ; le fichier est fabriqué par le navigateur. */
export async function downloadClientDocumentAction(
  id: string,
  documentId: string,
): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await downloadClientDocument(id, documentId) };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteClientDocumentAction(
  id: string,
  documentId: string,
): Promise<ReferentialResult> {
  try {
    await deleteClientDocument(id, documentId);
    revalidateAccounts('clients');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Livreurs ------------------------------------------------------------ */

export async function saveCourierAction(
  id: string | null,
  input: unknown,
): Promise<ReferentialResult<{ id: string }>> {
  const parsed = courierSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const body = parsed.data as CourierPayload;
    const saved = id ? await updateCourier(id, body) : await createCourier(body);
    revalidateAccounts('couriers');
    return { ok: true, data: { id: saved.id } };
  } catch (error) {
    return toFailure(error);
  }
}

export async function setCourierActiveAction(
  id: string,
  isActive: boolean,
): Promise<ReferentialResult> {
  try {
    await setCourierActive(id, isActive);
    revalidateAccounts('couriers');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function setCourierValidatedAction(
  id: string,
  validated: boolean,
): Promise<ReferentialResult> {
  try {
    await setCourierValidated(id, validated);
    revalidateAccounts('couriers');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/** Grille de paie remplacée en bloc — D-020, stockée et pas encore calculée. */
export async function setCourierCitiesAction(
  id: string,
  input: unknown,
): Promise<ReferentialResult> {
  const parsed = courierCitiesSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await setCourierCities(id, parsed.data as CourierCitiesPayload);
    revalidateAccounts('couriers');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Crée le compte lié à la fiche — C13 bloc 3.
 *
 * Le formulaire complet, mot de passe compris. La fiche est revalidée ET la
 * liste des comptes : le nouveau compte doit apparaître dans les deux.
 */
export async function createCourierAccountAction(
  id: string,
  input: unknown,
): Promise<ReferentialResult> {
  const parsed = createUserSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await createCourierAccount(id, parsed.data);
    revalidateAccounts('couriers');
    revalidateAccounts('users');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteCourierAction(id: string): Promise<ReferentialResult> {
  try {
    await deleteCourier(id);
    revalidateAccounts('couriers');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Réglages métier ------------------------------------------------------ */

export async function updateBusinessSettingsAction(
  input: unknown,
): Promise<ReferentialResult<BusinessSettings>> {
  const parsed = businessSettingsSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const saved = await updateBusinessSettings(parsed.data as BusinessSettingsPayload);
    // La tarification lit ces valeurs : les écrans de colis doivent se relire.
    revalidatePath('/[locale]/(app)', 'layout');
    return { ok: true, data: saved };
  } catch (error) {
    return toFailure(error);
  }
}
