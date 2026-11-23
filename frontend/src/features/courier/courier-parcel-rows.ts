import { codToCollect, type ParcelListItem } from '@delivery/shared';
import type { CourierParcelRow } from '@/features/courier/courier-parcel-list';
import { getParcels, type ParcelListQuery } from '@/lib/api/parcels';

/*
 * LES LIGNES DE « MES COLIS », côté SERVEUR — 21/11/2026.
 *
 * Lu par la page (composant serveur), jamais par un composant client
 * (D-018) : un seul endroit dit ce qu'une ligne peut recevoir.
 */

/** Les filtres de l'écran, ceux de l'URL. */
export type CourierParcelFilters = Pick<ParcelListQuery, 'search' | 'statusId' | 'statusGroup'>;

/** Ce que « Charger plus » ajoute, et la première page. */
export const COURIER_PARCELS_STEP = 25;

/** La page lue à l'API : sous le plafond de `GET /parcels` (200), assez pour peu d'allers-retours. */
const API_PAGE = 100;

/**
 * Les `limit` premiers colis du livreur (tous si `limit` est `Infinity`), et
 * le total. `GET /parcels` borne déjà la lecture au livreur (D-082).
 */
export async function loadCourierParcels(
  filters: CourierParcelFilters,
  limit: number,
): Promise<{ items: ParcelListItem[]; total: number }> {
  const items: ParcelListItem[] = [];
  let total = 0;

  for (let page = 1; ; page += 1) {
    const response = await getParcels({ ...filters, page, pageSize: API_PAGE });
    total = response.total;
    items.push(...response.items);
    if (response.items.length < API_PAGE || items.length >= Math.min(limit, total)) break;
  }

  return { items: items.slice(0, limit), total };
}

/**
 * La ligne d'un colis. **Ses issues viennent de la liste elle-même**
 * (`courierOutcomes`, 21/11/2026) : `GET /parcels` les calcule pour toute la
 * page, avec la règle de `GET /courier/parcels/:code/outcomes`. Plus aucun
 * appel colis par colis.
 */
function toRow(parcel: ParcelListItem): CourierParcelRow {
  return {
    id: parcel.id,
    code: parcel.code,
    recipientName: parcel.recipientName,
    destinationCityName: parcel.destinationCityName,
    deliveryAddress: parcel.deliveryAddress,
    statusId: parcel.statusId,
    codAmount: parcel.codAmount,
    // DOMAIN.md §3.8 : le dû au pas de porte, la fonction partagée, rien de neuf.
    dueInCents: codToCollect(parcel),
    // Attendu en chèque : un par un, depuis la fiche (règle `bulk-cheque-parcels`).
    cheque: parcel.expectedPayment === 'cheque',
    outcomes: parcel.courierOutcomes ?? [],
  };
}

export function toCourierRows(parcels: ParcelListItem[]): CourierParcelRow[] {
  return parcels.map(toRow);
}
