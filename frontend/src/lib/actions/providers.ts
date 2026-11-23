'use server';

import {
  type ProviderTokenListItem,
  type ProviderTokenSecret,
  providerStatusMapSchema,
  providerTokenCreateSchema,
  providerTokenModeSchema,
} from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import {
  createProvider,
  resolveProviderAlert,
  revokeProvider,
  rotateProvider,
  setProviderMode,
  setProviderStatusMap,
} from '@/lib/api/providers';

/**
 * Écritures de Paramètres > Prestataires — chantier sync-wimo-speedy.
 *
 * Même forme que partout : la saisie est revalidée, l'endpoint appelé, la page
 * invalidée. Le jeton en clair revient UNE fois, dans la réponse.
 */

function revalidateProviders(): void {
  revalidatePath('/[locale]/(app)/settings/providers', 'page');
}

export async function createProviderAction(
  input: unknown,
): Promise<ReferentialResult<ProviderTokenSecret>> {
  const parsed = providerTokenCreateSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const created = await createProvider(parsed.data);
    revalidateProviders();
    return { ok: true, data: created };
  } catch (error) {
    return toFailure(error);
  }
}

export async function rotateProviderAction(
  id: string,
): Promise<ReferentialResult<ProviderTokenSecret>> {
  try {
    const rotated = await rotateProvider(id);
    revalidateProviders();
    return { ok: true, data: rotated };
  } catch (error) {
    return toFailure(error);
  }
}

export async function revokeProviderAction(
  id: string,
): Promise<ReferentialResult<ProviderTokenListItem>> {
  try {
    const revoked = await revokeProvider(id);
    revalidateProviders();
    return { ok: true, data: revoked };
  } catch (error) {
    return toFailure(error);
  }
}

export async function setProviderModeAction(
  id: string,
  input: unknown,
): Promise<ReferentialResult<ProviderTokenListItem>> {
  const parsed = providerTokenModeSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const updated = await setProviderMode(id, parsed.data.mode);
    revalidateProviders();
    return { ok: true, data: updated };
  } catch (error) {
    return toFailure(error);
  }
}

export async function setProviderStatusMapAction(
  id: string,
  input: unknown,
): Promise<ReferentialResult<ProviderTokenListItem>> {
  const parsed = providerStatusMapSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const updated = await setProviderStatusMap(id, parsed.data);
    revalidateProviders();
    return { ok: true, data: updated };
  } catch (error) {
    return toFailure(error);
  }
}

/** Une correction refusée, reprise à la main : l'alerte est marquée traitée. */
export async function resolveProviderAlertAction(
  id: string,
  callId: string,
): Promise<ReferentialResult<{ id: string }>> {
  try {
    const resolved = await resolveProviderAlert(id, callId);
    revalidateProviders();
    return { ok: true, data: resolved };
  } catch (error) {
    return toFailure(error);
  }
}
