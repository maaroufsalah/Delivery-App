'use server';

import {
  type CityPayload,
  citySchema,
  type DeliveryTypePayload,
  deliveryTypeSchema,
  type DistrictPayload,
  districtSchema,
  type PricingOriginImportPreview,
  type PricingOriginSavePayload,
  pricingOriginSaveSchema,
  type PricingRulePayload,
  pricingRuleSchema,
  type ZonePayload,
  type ZoneRatePayload,
  zoneRateSchema,
  zoneSchema,
} from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import {
  createCity,
  createDeliveryType,
  createDistrict,
  createPricingRule,
  createZone,
  deleteCity,
  deleteDeliveryType,
  deleteDistrict,
  deletePricingRule,
  deleteZone,
  deleteZoneRate,
  exportPricingOrigin,
  importPricingOrigin,
  setCityActive,
  setDeliveryTypeActive,
  setDistrictActive,
  setZoneActive,
  updateCity,
  updateDeliveryType,
  updateDistrict,
  savePricingOrigin,
  updatePricingRule,
  updateZone,
  upsertZoneRate,
} from '@/lib/api/referential';

/**
 * Écritures du référentiel.
 *
 * Un composant client ne peut pas appeler `src/lib/api/*`, qui vit côté serveur
 * (D-018) : ces actions sont le passage. Elles REVALIDENT la saisie — une
 * Server Action est un endpoint POST public, et la validation du formulaire
 * n'engage que les navigateurs bien élevés.
 *
 * Le refus de suppression (409) ne se perd pas en route : `references` remonte
 * tel quel, pour que l'écran puisse dire « 412 colis utilisent cette ville »
 * et proposer la désactivation plutôt qu'un message d'échec générique.
 */

/** Les listes du référentiel alimentent aussi les filtres des écrans colis. */
function revalidateReferential(route: string): void {
  revalidatePath(`/[locale]/(app)/${route}`, 'page');
  revalidatePath('/[locale]/(app)/parcels', 'page');
}

/* --- Villes ------------------------------------------------------------- */

export async function saveCityAction(
  id: string | null,
  input: unknown,
): Promise<ReferentialResult> {
  const parsed = citySchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const body = parsed.data as CityPayload;
    if (id) await updateCity(id, body);
    else await createCity(body);
    revalidateReferential('cities');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function setCityActiveAction(
  id: string,
  isActive: boolean,
): Promise<ReferentialResult> {
  try {
    await setCityActive(id, isActive);
    revalidateReferential('cities');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteCityAction(id: string): Promise<ReferentialResult> {
  try {
    await deleteCity(id);
    revalidateReferential('cities');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Zones -------------------------------------------------------------- */

export async function saveZoneAction(
  id: string | null,
  input: unknown,
): Promise<ReferentialResult> {
  const parsed = zoneSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const body = parsed.data as ZonePayload;
    if (id) await updateZone(id, body);
    else await createZone(body);
    revalidateReferential('zones');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function setZoneActiveAction(
  id: string,
  isActive: boolean,
): Promise<ReferentialResult> {
  try {
    await setZoneActive(id, isActive);
    revalidateReferential('zones');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteZoneAction(id: string): Promise<ReferentialResult> {
  try {
    await deleteZone(id);
    revalidateReferential('zones');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function saveZoneRateAction(input: unknown): Promise<ReferentialResult> {
  const parsed = zoneRateSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    await upsertZoneRate(parsed.data as ZoneRatePayload);
    revalidateReferential('zones');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteZoneRateAction(id: string): Promise<ReferentialResult> {
  try {
    await deleteZoneRate(id);
    revalidateReferential('zones');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Quartiers ---------------------------------------------------------- */

export async function saveDistrictAction(
  id: string | null,
  input: unknown,
): Promise<ReferentialResult> {
  const parsed = districtSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const body = parsed.data as DistrictPayload;
    if (id) await updateDistrict(id, body);
    else await createDistrict(body);
    revalidateReferential('districts');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function setDistrictActiveAction(
  id: string,
  isActive: boolean,
): Promise<ReferentialResult> {
  try {
    await setDistrictActive(id, isActive);
    revalidateReferential('districts');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteDistrictAction(id: string): Promise<ReferentialResult> {
  try {
    await deleteDistrict(id);
    revalidateReferential('districts');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Tarifs ------------------------------------------------------------- */

export async function savePricingRuleAction(
  id: string | null,
  input: unknown,
): Promise<ReferentialResult> {
  const parsed = pricingRuleSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const body = parsed.data as PricingRulePayload;
    if (id) await updatePricingRule(id, body);
    else await createPricingRule(body);
    revalidateReferential('pricing');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Lignes modifiées de la grille d'UNE origine — par ligne ou en bloc, un seul
 * aller-retour dans les deux cas.
 */
export async function savePricingOriginAction(
  originCityId: string,
  input: unknown,
): Promise<ReferentialResult<{ priced: number; cleared: number; delays: number }>> {
  const parsed = pricingOriginSaveSchema.safeParse(input);
  if (!parsed.success || typeof originCityId !== 'string' || originCityId === '') {
    return { ok: false, error: 'invalid' };
  }

  try {
    const result = await savePricingOrigin(originCityId, parsed.data as PricingOriginSavePayload);
    revalidateReferential('pricing');
    return { ok: true, data: result };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deletePricingRuleAction(id: string): Promise<ReferentialResult> {
  try {
    await deletePricingRule(id);
    revalidateReferential('pricing');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Import pour UNE origine. `apply: false` ne touche à rien et sert l'aperçu ;
 * `apply: true` n'écrit que les lignes reconnues — aucune ville n'est créée.
 */
export async function importPricingOriginAction(
  originCityId: string,
  content: string,
  apply: boolean,
  filename?: string,
): Promise<ReferentialResult<PricingOriginImportPreview>> {
  if (typeof content !== 'string' || content.length === 0 || !originCityId) {
    return { ok: false, error: 'invalid' };
  }

  try {
    const preview = await importPricingOrigin(originCityId, content, apply, filename);
    if (apply) revalidateReferential('pricing');
    return { ok: true, data: preview };
  } catch (error) {
    return toFailure(error);
  }
}

/** Le classeur d'une origine revient en base64 ; le navigateur en fait un fichier. */
export async function exportPricingOriginAction(
  originCityId: string,
): Promise<ReferentialResult<string>> {
  if (!originCityId) return { ok: false, error: 'invalid' };

  try {
    return { ok: true, data: await exportPricingOrigin(originCityId) };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Types de livraison -------------------------------------------------- */

export async function saveDeliveryTypeAction(
  id: string | null,
  input: unknown,
): Promise<ReferentialResult> {
  const parsed = deliveryTypeSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const body = parsed.data as DeliveryTypePayload;
    if (id) await updateDeliveryType(id, body);
    else await createDeliveryType(body);
    revalidateReferential('delivery-types');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function setDeliveryTypeActiveAction(
  id: string,
  isActive: boolean,
): Promise<ReferentialResult> {
  try {
    await setDeliveryTypeActive(id, isActive);
    revalidateReferential('delivery-types');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteDeliveryTypeAction(id: string): Promise<ReferentialResult> {
  try {
    await deleteDeliveryType(id);
    revalidateReferential('delivery-types');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}
