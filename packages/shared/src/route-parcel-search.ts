import { textMatchesQuery } from './city-search';

/**
 * LA RECHERCHE DANS LES EXPÉDITIONS D'UN CIRCUIT — chantier « Affichage web
 * et étiquette », point 2.
 *
 * Un circuit porte jusqu'à quelques centaines de colis, tous déjà chargés par
 * la fiche : la recherche se fait donc à l'écran, sans requête, avec la règle
 * unique des menus (`textMatchesQuery` : sans casse ni accents, morceau au
 * milieu accepté).
 *
 * On cherche sur ce que l'agent a sous les yeux ou au téléphone : le code, la
 * référence du vendeur, le destinataire, son téléphone, la ville, et, pour un
 * ramassage ou un retour, l'expéditeur. Le téléphone se cherche aussi sans
 * ses espaces : « 0612 34 » trouve 0612345678.
 */
export interface RouteParcelSearchable {
  code: string;
  externalCode?: string | null;
  recipientName: string;
  recipientPhone: string;
  destinationCityName: string;
  senderName?: string;
  senderPhone?: string;
  originCityName?: string;
}

export function routeParcelMatchesQuery(parcel: RouteParcelSearchable, query: string): boolean {
  if (
    textMatchesQuery(
      [
        parcel.code,
        parcel.externalCode,
        parcel.recipientName,
        parcel.recipientPhone,
        parcel.destinationCityName,
        parcel.senderName,
        parcel.senderPhone,
        parcel.originCityName,
      ],
      query,
    )
  ) {
    return true;
  }

  const digits = query.replace(/\D/g, '');
  if (digits.length < 3 || digits.length !== query.replace(/\s/g, '').length) return false;
  return [parcel.recipientPhone, parcel.senderPhone].some(
    (phone) => phone != null && phone.replace(/\D/g, '').includes(digits),
  );
}

/**
 * L'ADRESSE COMPLÈTE D'UN COLIS EN UNE LIGNE — chantier « Affichage web et
 * étiquette », point 3 : la bulle au survol d'une ligne de la liste des colis.
 * Adresse, quartier s'il existe, ville ; rien d'inventé quand un morceau
 * manque. Les données viennent avec la liste : aucune requête par ligne.
 */
export function parcelAddressLine(parcel: {
  deliveryAddress?: string | null;
  destinationDistrictName?: string | null;
  destinationCityName?: string | null;
}): string | undefined {
  const parts = [parcel.deliveryAddress, parcel.destinationDistrictName, parcel.destinationCityName]
    .map((part) => part?.trim())
    .filter((part): part is string => Boolean(part));
  return parts.length > 0 ? parts.join(' · ') : undefined;
}

/**
 * Les lignes affichées : recherche ET statut. `statusId` absent = tous.
 * L'ordre reçu est gardé, aucune ligne n'est coupée.
 */
export function filterRouteParcels<T extends RouteParcelSearchable & { statusId: number }>(
  parcels: readonly T[],
  query: string,
  statusId?: number,
): T[] {
  return parcels.filter(
    (parcel) =>
      (statusId === undefined || parcel.statusId === statusId) &&
      routeParcelMatchesQuery(parcel, query),
  );
}
