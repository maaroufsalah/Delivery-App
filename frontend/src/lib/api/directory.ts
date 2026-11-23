import type {
  Agency,
  City,
  Client,
  Courier,
  District,
  PendingCounts,
  Zone,
} from '@delivery/shared';
import { apiFetch } from '@/lib/http';

/** Référentiel — `GET /directory/*`. */

export async function getCities(): Promise<City[]> {
  return apiFetch<City[]>('/directory/cities');
}

export async function getAllCities(): Promise<City[]> {
  return apiFetch<City[]>('/directory/cities', { query: { includeInactive: true } });
}

export async function getZones(): Promise<Zone[]> {
  return apiFetch<Zone[]>('/directory/zones');
}

export async function getDistricts(cityId?: string): Promise<District[]> {
  return apiFetch<District[]>('/directory/districts', { query: { cityId } });
}

export async function getClients(): Promise<Client[]> {
  return apiFetch<Client[]>('/directory/clients');
}

export async function getCouriers(): Promise<Courier[]> {
  return apiFetch<Courier[]>('/directory/couriers');
}

export async function getAgencies(): Promise<Agency[]> {
  return apiFetch<Agency[]>('/directory/agencies');
}

/**
 * Fiches à valider — C13 bloc 2, pour les pastilles du menu.
 *
 * Sans `revalidate` : `apiFetch` ne met jamais en cache par défaut, et c'est
 * ce qu'il faut ici. Un compteur qui traîne d'une minute annonce du travail
 * déjà fait — et il serait de surcroît partagé entre deux agents d'agences
 * différentes, qui ne voient pas les mêmes fiches.
 */
export async function getPendingCounts(): Promise<PendingCounts> {
  return apiFetch<PendingCounts>('/directory/pending-counts');
}
