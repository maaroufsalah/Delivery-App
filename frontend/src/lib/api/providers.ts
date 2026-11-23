import type {
  ProviderCallListItem,
  ProviderModeValue,
  ProviderStatusMapOverride,
  ProviderTokenCreatePayload,
  ProviderTokenListItem,
  ProviderTokenSecret,
} from '@delivery/shared';
import { apiFetch } from '@/lib/http';

/**
 * Paramètres > Prestataires — chantier sync-wimo-speedy.
 *
 * Le jeton en clair n'existe que dans la réponse de la création et de la
 * rotation : aucune de ces fonctions ne le relit, l'API ne le garde pas.
 */

export async function getProviders(): Promise<ProviderTokenListItem[]> {
  return apiFetch<ProviderTokenListItem[]>('/settings/providers');
}

export async function createProvider(input: ProviderTokenCreatePayload): Promise<ProviderTokenSecret> {
  return apiFetch<ProviderTokenSecret>('/settings/providers', { method: 'POST', body: input });
}

export async function rotateProvider(id: string): Promise<ProviderTokenSecret> {
  return apiFetch<ProviderTokenSecret>(`/settings/providers/${encodeURIComponent(id)}/rotate`, {
    method: 'POST',
  });
}

export async function revokeProvider(id: string): Promise<ProviderTokenListItem> {
  return apiFetch<ProviderTokenListItem>(`/settings/providers/${encodeURIComponent(id)}/revoke`, {
    method: 'POST',
  });
}

export async function setProviderMode(
  id: string,
  mode: ProviderModeValue,
): Promise<ProviderTokenListItem> {
  return apiFetch<ProviderTokenListItem>(`/settings/providers/${encodeURIComponent(id)}/mode`, {
    method: 'PATCH',
    body: { mode },
  });
}

export async function setProviderStatusMap(
  id: string,
  statusMap: ProviderStatusMapOverride,
): Promise<ProviderTokenListItem> {
  return apiFetch<ProviderTokenListItem>(
    `/settings/providers/${encodeURIComponent(id)}/status-map`,
    { method: 'PUT', body: { statusMap } },
  );
}

export async function getProviderCalls(id: string): Promise<ProviderCallListItem[]> {
  return apiFetch<ProviderCallListItem[]>(`/settings/providers/${encodeURIComponent(id)}/calls`);
}

export async function resolveProviderAlert(id: string, callId: string): Promise<{ id: string }> {
  return apiFetch<{ id: string }>(
    `/settings/providers/${encodeURIComponent(id)}/calls/${encodeURIComponent(callId)}/resolve`,
    { method: 'POST' },
  );
}
