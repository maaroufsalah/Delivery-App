import type {
  CreateUserPayload,
  AgencyListItem,
  AgencyPayload,
  BusinessSettings,
  BusinessSettingsPayload,
  ClientDetail,
  ClientPickupTermsPayload,
  ClientDiscountPayload,
  ClientDocumentPayload,
  ClientListItem,
  ClientPayload,
  ClientPickupAddressPayload,
  CourierCitiesPayload,
  CourierDetail,
  CourierListItem,
  CourierPayload,
  ListQuery,
  Paginated,
  RoleMatrix,
  RoleMatrixPayload,
  SetUserPasswordPayload,
  UpdateOwnProfilePayload,
  UpdateUserPayload,
  UserDetail,
  UserInvitation,
  UserListItem,
  UserPermissions,
  UserPermissionsPayload,
} from '@delivery/shared';
import { apiFetch, apiFetchBase64 } from '@/lib/http';

/**
 * Comptes — utilisateurs, rôles, agences, e-commerçants, livreurs.
 *
 * Une fonction = un endpoint. Les listes sont paginées PAR LE SERVEUR : la
 * requête part avec `page`, `pageSize`, `sort`, `order`, et la réponse porte
 * le total.
 */

type Query = ListQuery & { isActive?: 'true' | 'false' };

function toSearch(query: object): Record<string, string | number> {
  const search: Record<string, string | number> = {};

  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue;
    search[key] = value as string | number;
  }

  return search;
}

/* --- Utilisateurs -------------------------------------------------------- */

export async function getUserPage(
  query: Query & { role?: string; agencyId?: string },
): Promise<Paginated<UserListItem>> {
  return apiFetch<Paginated<UserListItem>>('/users', { query: toSearch(query) });
}

export async function getTeamPage(query: Query): Promise<Paginated<UserListItem>> {
  return apiFetch<Paginated<UserListItem>>('/users/team', { query: toSearch(query) });
}

export async function getUser(id: string): Promise<UserDetail> {
  return apiFetch<UserDetail>(`/users/${id}`);
}

export async function createUser(body: CreateUserPayload): Promise<UserDetail> {
  return apiFetch<UserDetail>('/users', { method: 'POST', body });
}

/** « Mon profil » — C13 bloc 4. `me`, jamais un identifiant : c'est le sien. */
export async function updateOwnProfile(body: UpdateOwnProfilePayload): Promise<UserDetail> {
  return apiFetch<UserDetail>('/users/me', { method: 'PATCH', body });
}

export async function updateUser(id: string, body: UpdateUserPayload): Promise<UserDetail> {
  return apiFetch<UserDetail>(`/users/${id}`, { method: 'PATCH', body });
}

export async function setUserActive(id: string, isActive: boolean): Promise<UserDetail> {
  return apiFetch<UserDetail>(`/users/${id}/active`, { method: 'PATCH', body: { isActive } });
}

export async function resetUserPassword(id: string): Promise<UserInvitation> {
  return apiFetch<UserInvitation>(`/users/${id}/reset-password`, { method: 'POST' });
}

/** Mot de passe CHOISI par l'administrateur — C12 bloc 2a. */
export async function setUserPassword(
  id: string,
  body: SetUserPasswordPayload,
): Promise<UserInvitation> {
  return apiFetch<UserInvitation>(`/users/${id}/set-password`, { method: 'POST', body });
}

export async function deleteUser(id: string): Promise<{ id: string }> {
  return apiFetch<{ id: string }>(`/users/${id}`, { method: 'DELETE' });
}

/** Changement par le titulaire — seule route ouverte à un compte invité. */
export async function changeOwnPassword(body: unknown): Promise<{ ok: true }> {
  return apiFetch<{ ok: true }>('/users/me/password', { method: 'POST', body });
}

/* --- Rôles et permissions ------------------------------------------------ */

export async function getRoleMatrix(): Promise<RoleMatrix> {
  return apiFetch<RoleMatrix>('/roles');
}

export async function updateRoleMatrix(body: RoleMatrixPayload): Promise<RoleMatrix> {
  return apiFetch<RoleMatrix>('/roles', { method: 'PUT', body });
}

/* --- Permissions d'un compte — dernières corrections, point 1 ------------- */

export async function getUserPermissions(id: string): Promise<UserPermissions> {
  return apiFetch<UserPermissions>(`/users/${id}/permissions`);
}

export async function updateUserPermissions(
  id: string,
  body: UserPermissionsPayload,
): Promise<UserPermissions> {
  return apiFetch<UserPermissions>(`/users/${id}/permissions`, { method: 'PUT', body });
}

export async function resetUserPermissions(id: string): Promise<UserPermissions> {
  return apiFetch<UserPermissions>(`/users/${id}/permissions`, { method: 'DELETE' });
}

/* --- Agences ------------------------------------------------------------- */

export async function getAgencyPage(query: Query): Promise<Paginated<AgencyListItem>> {
  return apiFetch<Paginated<AgencyListItem>>('/agencies', { query: toSearch(query) });
}

export async function createAgency(body: AgencyPayload): Promise<AgencyListItem> {
  return apiFetch<AgencyListItem>('/agencies', { method: 'POST', body });
}

export async function updateAgency(id: string, body: AgencyPayload): Promise<AgencyListItem> {
  return apiFetch<AgencyListItem>(`/agencies/${id}`, { method: 'PATCH', body });
}

export async function setAgencyActive(id: string, isActive: boolean): Promise<AgencyListItem> {
  return apiFetch<AgencyListItem>(`/agencies/${id}/active`, {
    method: 'PATCH',
    body: { isActive },
  });
}

export async function deleteAgency(id: string): Promise<{ id: string }> {
  return apiFetch<{ id: string }>(`/agencies/${id}`, { method: 'DELETE' });
}

/* --- E-commerçants ------------------------------------------------------- */

export async function getClientPage(
  query: Query & { agencyId?: string; validated?: 'true' | 'false' | 'rejected' },
): Promise<Paginated<ClientListItem>> {
  return apiFetch<Paginated<ClientListItem>>('/clients', { query: toSearch(query) });
}

export async function getClient(id: string): Promise<ClientDetail> {
  return apiFetch<ClientDetail>(`/clients/${id}`);
}

export async function createClient(body: ClientPayload): Promise<ClientDetail> {
  return apiFetch<ClientDetail>('/clients', { method: 'POST', body });
}

export async function updateClient(id: string, body: ClientPayload): Promise<ClientDetail> {
  return apiFetch<ClientDetail>(`/clients/${id}`, { method: 'PATCH', body });
}

/**
 * LE PATCH D'UN SEUL CHAMP — chantier « Bon de retour livreur », point 0.
 *
 * `PATCH /clients/:id` accepte désormais un corps partiel : basculer
 * « Client Pro » ne demande plus de relire la fiche, de la recopier et d'en
 * retirer les nuls. C'est ce qui permet à l'interrupteur de vivre sur la
 * FICHE plutôt que dans la boîte « Modifier ».
 */
export async function setClientPro(id: string, isPro: boolean): Promise<ClientDetail> {
  return apiFetch<ClientDetail>(`/clients/${id}`, { method: 'PATCH', body: { isPro } });
}

/** Lot R (D-230) — taux de TVA du Client Pro, en centièmes ; `null` = défaut société. */
export async function setClientVatRate(id: string, vatRate: number | null): Promise<ClientDetail> {
  return apiFetch<ClientDetail>(`/clients/${id}/vat-rate`, { method: 'PATCH', body: { vatRate } });
}

/** Lot T (D-232) — conditions de ramassage du client ; `null` = valeur globale. */
export async function setClientPickupTerms(
  id: string,
  terms: ClientPickupTermsPayload,
): Promise<ClientDetail> {
  return apiFetch<ClientDetail>(`/clients/${id}/pickup-terms`, { method: 'PATCH', body: terms });
}

export async function setClientActive(id: string, isActive: boolean): Promise<ClientDetail> {
  return apiFetch<ClientDetail>(`/clients/${id}/active`, { method: 'PATCH', body: { isActive } });
}

export async function setClientValidated(id: string, validated: boolean): Promise<ClientDetail> {
  return apiFetch<ClientDetail>(`/clients/${id}/validation`, {
    method: 'PATCH',
    body: { isActive: validated },
  });
}

/**
 * « Valider » — C13 bloc 2. Un seul appel ouvre la fiche ET le compte.
 *
 * `agencyId` n'est envoyé que lorsque la fiche n'en porte pas : l'API refuse
 * avec `field: 'agencyId'` dans ce cas, et c'est ce refus qui fait ouvrir le
 * sélecteur à l'écran.
 */
export async function validateClient(id: string, agencyId?: string): Promise<ClientDetail> {
  return apiFetch<ClientDetail>(`/clients/${id}/validate`, {
    method: 'POST',
    body: { agencyId: agencyId ?? '' },
  });
}

export async function rejectClient(id: string, reason: string): Promise<ClientDetail> {
  return apiFetch<ClientDetail>(`/clients/${id}/reject`, { method: 'POST', body: { reason } });
}

export async function createClientAccount(
  id: string,
  body: CreateUserPayload,
): Promise<UserDetail> {
  return apiFetch<UserDetail>(`/clients/${id}/account`, { method: 'POST', body });
}

export async function deleteClient(id: string): Promise<{ id: string }> {
  return apiFetch<{ id: string }>(`/clients/${id}`, { method: 'DELETE' });
}

export async function addClientDiscount(
  id: string,
  body: ClientDiscountPayload,
): Promise<ClientDetail> {
  return apiFetch<ClientDetail>(`/clients/${id}/discounts`, { method: 'POST', body });
}

export async function updateClientDiscount(
  id: string,
  discountId: string,
  body: ClientDiscountPayload,
): Promise<ClientDetail> {
  return apiFetch<ClientDetail>(`/clients/${id}/discounts/${discountId}`, {
    method: 'PATCH',
    body,
  });
}

export async function deleteClientDiscount(id: string, discountId: string): Promise<ClientDetail> {
  return apiFetch<ClientDetail>(`/clients/${id}/discounts/${discountId}`, { method: 'DELETE' });
}

export async function addClientPickupAddress(
  id: string,
  body: ClientPickupAddressPayload,
): Promise<ClientDetail> {
  return apiFetch<ClientDetail>(`/clients/${id}/pickup-addresses`, { method: 'POST', body });
}

export async function deleteClientPickupAddress(
  id: string,
  addressId: string,
): Promise<ClientDetail> {
  return apiFetch<ClientDetail>(`/clients/${id}/pickup-addresses/${addressId}`, {
    method: 'DELETE',
  });
}

export async function addClientDocument(
  id: string,
  body: ClientDocumentPayload,
): Promise<ClientDetail> {
  return apiFetch<ClientDetail>(`/clients/${id}/documents`, { method: 'POST', body });
}

/**
 * Contenu d'une pièce jointe, en base64.
 *
 * L'endpoint rend du binaire : il ne passe donc pas par `apiFetch`, qui parse
 * du JSON. Le base64 traverse une Server Action jusqu'au navigateur, qui en
 * fait un Blob — aucun composant client n'a besoin d'un jeton, et il n'y a pas
 * de route de proxy à maintenir (D-018).
 */
export async function downloadClientDocument(id: string, documentId: string): Promise<string> {
  return apiFetchBase64(`/clients/${id}/documents/${documentId}`);
}

export async function deleteClientDocument(id: string, documentId: string): Promise<ClientDetail> {
  return apiFetch<ClientDetail>(`/clients/${id}/documents/${documentId}`, { method: 'DELETE' });
}

/* --- Livreurs ------------------------------------------------------------ */

export async function getCourierPage(
  query: Query & { agencyId?: string; cityId?: string; validated?: 'true' | 'false' | 'rejected' },
): Promise<Paginated<CourierListItem>> {
  return apiFetch<Paginated<CourierListItem>>('/couriers', { query: toSearch(query) });
}

export async function getCourier(id: string): Promise<CourierDetail> {
  return apiFetch<CourierDetail>(`/couriers/${id}`);
}

export async function createCourier(body: CourierPayload): Promise<CourierDetail> {
  return apiFetch<CourierDetail>('/couriers', { method: 'POST', body });
}

export async function updateCourier(id: string, body: CourierPayload): Promise<CourierDetail> {
  return apiFetch<CourierDetail>(`/couriers/${id}`, { method: 'PATCH', body });
}

export async function setCourierActive(id: string, isActive: boolean): Promise<CourierDetail> {
  return apiFetch<CourierDetail>(`/couriers/${id}/active`, { method: 'PATCH', body: { isActive } });
}

export async function setCourierValidated(id: string, validated: boolean): Promise<CourierDetail> {
  return apiFetch<CourierDetail>(`/couriers/${id}/validation`, {
    method: 'PATCH',
    body: { isActive: validated },
  });
}

export async function setCourierCities(
  id: string,
  body: CourierCitiesPayload,
): Promise<CourierDetail> {
  return apiFetch<CourierDetail>(`/couriers/${id}/cities`, { method: 'PUT', body });
}

export async function validateCourier(id: string, agencyId?: string): Promise<CourierDetail> {
  return apiFetch<CourierDetail>(`/couriers/${id}/validate`, {
    method: 'POST',
    body: { agencyId: agencyId ?? '' },
  });
}

export async function rejectCourier(id: string, reason: string): Promise<CourierDetail> {
  return apiFetch<CourierDetail>(`/couriers/${id}/reject`, { method: 'POST', body: { reason } });
}

export async function createCourierAccount(
  id: string,
  body: CreateUserPayload,
): Promise<UserDetail> {
  return apiFetch<UserDetail>(`/couriers/${id}/account`, { method: 'POST', body });
}

export async function deleteCourier(id: string): Promise<{ id: string }> {
  return apiFetch<{ id: string }>(`/couriers/${id}`, { method: 'DELETE' });
}

/* --- Réglages métier (D-019) --------------------------------------------- */

export async function getBusinessSettings(): Promise<BusinessSettings> {
  return apiFetch<BusinessSettings>('/settings/business');
}

export async function updateBusinessSettings(
  body: BusinessSettingsPayload,
): Promise<BusinessSettings> {
  return apiFetch<BusinessSettings>('/settings/business', { method: 'PATCH', body });
}
