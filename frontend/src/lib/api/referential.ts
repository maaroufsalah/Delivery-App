import type {
  CityListItem,
  CityPayload,
  DeliveryTypeListItem,
  DeliveryTypePayload,
  DistrictListItem,
  DistrictPayload,
  ListQuery,
  Paginated,
  PricingOriginImportPreview,
  PricingOriginPage,
  PricingOriginSavePayload,
  PricingRuleListItem,
  PricingRulePayload,
  ZoneListItem,
  ZonePayload,
  ZoneRateItem,
  ZoneRateMatrix,
  ZoneRatePayload,
} from '@delivery/shared';
import { apiFetch, apiFetchBase64 } from '@/lib/http';

/**
 * Référentiel — administration des villes, zones, quartiers, tarifs et types.
 *
 * Une fonction = un endpoint, comme partout dans `src/lib/api`. Ces listes
 * sont PAGINÉES PAR LE SERVEUR : la requête part avec `page`, `pageSize`,
 * `sort` et `order`, et la réponse `Paginated<T>` porte le total. Les listes
 * légères de `directory.ts` (villes pour un menu déroulant) restent distinctes
 * — elles n'ont ni pagination ni compteur de références.
 */

type Query = ListQuery & { isActive?: 'true' | 'false' };

/** Retire les valeurs absentes : `?search=` vide fausserait le filtre serveur. */
function toSearch(query: object): Record<string, string | number> {
  const search: Record<string, string | number> = {};

  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue;
    search[key] = value as string | number;
  }

  return search;
}

/* --- Villes ------------------------------------------------------------- */

export async function getCityPage(
  query: Query & { zoneId?: string },
): Promise<Paginated<CityListItem>> {
  return apiFetch<Paginated<CityListItem>>('/cities', { query: toSearch(query) });
}

export async function createCity(body: CityPayload): Promise<CityListItem> {
  return apiFetch<CityListItem>('/cities', { method: 'POST', body });
}

export async function updateCity(id: string, body: CityPayload): Promise<CityListItem> {
  return apiFetch<CityListItem>(`/cities/${id}`, { method: 'PATCH', body });
}

export async function setCityActive(id: string, isActive: boolean): Promise<CityListItem> {
  return apiFetch<CityListItem>(`/cities/${id}/active`, { method: 'PATCH', body: { isActive } });
}

export async function deleteCity(id: string): Promise<{ id: string }> {
  return apiFetch<{ id: string }>(`/cities/${id}`, { method: 'DELETE' });
}

/* --- Zones -------------------------------------------------------------- */

export async function getZonePage(query: Query): Promise<Paginated<ZoneListItem>> {
  return apiFetch<Paginated<ZoneListItem>>('/zones', { query: toSearch(query) });
}

export async function createZone(body: ZonePayload): Promise<ZoneListItem> {
  return apiFetch<ZoneListItem>('/zones', { method: 'POST', body });
}

export async function updateZone(id: string, body: ZonePayload): Promise<ZoneListItem> {
  return apiFetch<ZoneListItem>(`/zones/${id}`, { method: 'PATCH', body });
}

export async function setZoneActive(id: string, isActive: boolean): Promise<ZoneListItem> {
  return apiFetch<ZoneListItem>(`/zones/${id}/active`, { method: 'PATCH', body: { isActive } });
}

export async function deleteZone(id: string): Promise<{ id: string }> {
  return apiFetch<{ id: string }>(`/zones/${id}`, { method: 'DELETE' });
}

export async function getZoneRates(): Promise<ZoneRateMatrix> {
  return apiFetch<ZoneRateMatrix>('/zones/rates');
}

export async function upsertZoneRate(body: ZoneRatePayload): Promise<ZoneRateItem> {
  return apiFetch<ZoneRateItem>('/zones/rates', { method: 'POST', body });
}

export async function deleteZoneRate(id: string): Promise<{ id: string }> {
  return apiFetch<{ id: string }>(`/zones/rates/${id}`, { method: 'DELETE' });
}

/* --- Quartiers ---------------------------------------------------------- */

export async function getDistrictPage(
  query: Query & { cityId?: string },
): Promise<Paginated<DistrictListItem>> {
  return apiFetch<Paginated<DistrictListItem>>('/districts', { query: toSearch(query) });
}

export async function createDistrict(body: DistrictPayload): Promise<DistrictListItem> {
  return apiFetch<DistrictListItem>('/districts', { method: 'POST', body });
}

export async function updateDistrict(id: string, body: DistrictPayload): Promise<DistrictListItem> {
  return apiFetch<DistrictListItem>(`/districts/${id}`, { method: 'PATCH', body });
}

export async function setDistrictActive(id: string, isActive: boolean): Promise<DistrictListItem> {
  return apiFetch<DistrictListItem>(`/districts/${id}/active`, {
    method: 'PATCH',
    body: { isActive },
  });
}

export async function deleteDistrict(id: string): Promise<{ id: string }> {
  return apiFetch<{ id: string }>(`/districts/${id}`, { method: 'DELETE' });
}

/* --- Tarifs ------------------------------------------------------------- */

export async function getPricingRulePage(
  query: Query & { originCityId?: string; destinationCityId?: string },
): Promise<Paginated<PricingRuleListItem>> {
  return apiFetch<Paginated<PricingRuleListItem>>('/pricing-rules', { query: toSearch(query) });
}

/**
 * La grille d'UNE origine — chantier « Tarifs par origine ». Sans
 * `originCityId`, l'API part de la ville du siège. Une page à la fois, une
 * origine à la fois : la matrice pleine ne descend plus jamais.
 */
export async function getPricingByOrigin(
  query: ListQuery & { originCityId?: string; zoneId?: string },
): Promise<PricingOriginPage> {
  return apiFetch<PricingOriginPage>('/pricing-rules/by-origin', { query: toSearch(query) });
}

export async function createPricingRule(body: PricingRulePayload): Promise<PricingRuleListItem> {
  return apiFetch<PricingRuleListItem>('/pricing-rules', { method: 'POST', body });
}

export async function updatePricingRule(
  id: string,
  body: PricingRulePayload,
): Promise<PricingRuleListItem> {
  return apiFetch<PricingRuleListItem>(`/pricing-rules/${id}`, { method: 'PATCH', body });
}

/** Lignes modifiées de la grille d'une origine. `rate: null` = tarif de zone. */
export async function savePricingOrigin(
  originCityId: string,
  body: PricingOriginSavePayload,
): Promise<{ priced: number; cleared: number; delays: number }> {
  return apiFetch<{ priced: number; cleared: number; delays: number }>(
    `/pricing-rules/by-origin/${encodeURIComponent(originCityId)}`,
    { method: 'PUT', body },
  );
}

export async function deletePricingRule(id: string): Promise<{ id: string }> {
  return apiFetch<{ id: string }>(`/pricing-rules/${id}`, { method: 'DELETE' });
}

/**
 * Aperçu (`apply: false`) ou application d'un import pour UNE origine. Le
 * classeur voyage en base64 dans le corps JSON — cf. docs/DECISIONS.md D-022.
 */
export async function importPricingOrigin(
  originCityId: string,
  content: string,
  apply: boolean,
  filename?: string,
): Promise<PricingOriginImportPreview> {
  return apiFetch<PricingOriginImportPreview>(
    `/pricing-rules/by-origin/${encodeURIComponent(originCityId)}/import`,
    { method: 'POST', body: { content, apply, filename } },
  );
}

/** Export de la grille d'une origine, rendu en base64 : le navigateur en fera un fichier. */
export async function exportPricingOrigin(originCityId: string): Promise<string> {
  return apiFetchBase64(`/pricing-rules/by-origin/${encodeURIComponent(originCityId)}/export`);
}

/* --- Types de livraison -------------------------------------------------- */

export async function getDeliveryTypePage(query: Query): Promise<Paginated<DeliveryTypeListItem>> {
  return apiFetch<Paginated<DeliveryTypeListItem>>('/delivery-types', { query: toSearch(query) });
}

export async function createDeliveryType(body: DeliveryTypePayload): Promise<DeliveryTypeListItem> {
  return apiFetch<DeliveryTypeListItem>('/delivery-types', { method: 'POST', body });
}

export async function updateDeliveryType(
  id: string,
  body: DeliveryTypePayload,
): Promise<DeliveryTypeListItem> {
  return apiFetch<DeliveryTypeListItem>(`/delivery-types/${id}`, { method: 'PATCH', body });
}

export async function setDeliveryTypeActive(
  id: string,
  isActive: boolean,
): Promise<DeliveryTypeListItem> {
  return apiFetch<DeliveryTypeListItem>(`/delivery-types/${id}/active`, {
    method: 'PATCH',
    body: { isActive },
  });
}

export async function deleteDeliveryType(id: string): Promise<{ id: string }> {
  return apiFetch<{ id: string }>(`/delivery-types/${id}`, { method: 'DELETE' });
}
