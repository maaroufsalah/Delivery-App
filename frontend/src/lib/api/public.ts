import type { ParcelStatusGroup, PublicCity } from '@delivery/shared';
import { ApiError, publicFetch } from '@/lib/http';

/**
 * Endpoints publics — `GET /public/rates`, `GET /public/track/:code`.
 *
 * Servis sans session. L'API replie les 34 statuts internes sur leur FAMILLE et
 * n'expose ni note interne, ni auteur, ni montant, ni nom de client.
 */

export interface PublicRate {
  cityId: string;
  cityName: string;
  zoneName: string;
  /** Tarif quartier au départ du siège, en centimes. */
  price: number;
  deliveryDays: number;
}

export async function getPublicRates(): Promise<PublicRate[]> {
  try {
    // La grille change rarement : une minute de cache suffit, et permet à la
    // landing de rester pré-rendue puis rafraîchie (ISR).
    return await publicFetch<PublicRate[]>('/public/rates', { revalidate: 60 });
  } catch (error) {
    /*
     * L'API injoignable ne doit pas faire échouer `next build` : la landing est
     * pré-rendue, et une construction en intégration continue n'a pas forcément
     * de base sous la main. La grille repart vide, la page reste servie, et le
     * premier rendu qui suit la revalidation la repeuple. Toute autre erreur
     * remonte.
     */
    if (error instanceof ApiError && error.code === 'NETWORK') return [];
    throw error;
  }
}

/**
 * Villes proposées au formulaire d'inscription — C13 bloc 1.
 *
 * Même traitement que la grille tarifaire : une API injoignable ne doit pas
 * faire échouer `next build`. La liste repart vide, la page reste servie, et le
 * formulaire affiche alors qu'aucune ville n'est disponible plutôt qu'un écran
 * d'erreur.
 */
export async function getPublicCities(): Promise<PublicCity[]> {
  try {
    return await publicFetch<PublicCity[]>('/public/cities', { revalidate: 300 });
  } catch (error) {
    if (error instanceof ApiError && error.code === 'NETWORK') return [];
    throw error;
  }
}

export interface PublicTrackingStep {
  group: ParcelStatusGroup;
  at: string;
  cityName: string | null;
}

export interface PublicTracking {
  code: string;
  /** Famille du statut courant. */
  group: ParcelStatusGroup;
  destinationCityName: string;
  steps: PublicTrackingStep[];
}

export async function trackParcel(code: string): Promise<PublicTracking> {
  return publicFetch<PublicTracking>(`/public/track/${encodeURIComponent(code)}`);
}
