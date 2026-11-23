import type {
  DeliveryNoteDetail,
  MerchantCompany,
  MerchantSummary,
  SelectionPayload,
} from '@delivery/shared';
import { selectionBody } from '@/lib/api/selection';
import { apiFetch } from '@/lib/http';

/**
 * L'ESPACE DU VENDEUR — V2 vendeur bloc 2.
 *
 * **Une fonction, un endpoint** (règle n°1 de CLAUDE.md). `GET
 * /merchant/me/summary` ne prend aucun paramètre et n'en prendra pas : le
 * client est celui de la session, et le passer dans l'URL rouvrirait la
 * question de savoir qui a le droit de demander les chiffres de qui.
 *
 * Le même endpoint sert l'application mobile depuis M2. Deux calculs séparés
 * auraient fini par se contredire sur l'écran qui fait entrer l'argent.
 */
export async function getMerchantSummary(): Promise<MerchantSummary> {
  return apiFetch<MerchantSummary>('/merchant/me/summary');
}

/**
 * LE VENDEUR DEMANDE UN BON — V2 vendeur bloc 4, spécification §5 et §6.
 *
 * **Deux endpoints dédiés, et non `POST /delivery-notes`.** Celui-ci prend un
 * `clientId` dans le corps sans le confronter à la portée : un vendeur qui
 * aurait `notes.write` ouvrirait un bon pour n'importe quel autre client. C'est
 * la forme de fuite trouvée sur `routes.write` en M1 (D-072), et le remède est
 * le même — une route qui LIT la portée, sous une clé étroite (`notes.request`).
 *
 * Le bon naît **en attente** : sa validation reste à l'agence, qui atteste être
 * passée prendre la marchandise. Un vendeur qui pourrait valider certifierait
 * lui-même que le transporteur est venu.
 */
export async function requestPickupNote(
  selection: SelectionPayload | readonly string[],
): Promise<DeliveryNoteDetail> {
  return apiFetch<DeliveryNoteDetail>('/merchant/me/pickups', {
    method: 'POST',
    body: selectionBody(selection, 'parcelIds'),
  });
}

export async function requestDropoffNote(
  selection: SelectionPayload | readonly string[],
): Promise<DeliveryNoteDetail> {
  return apiFetch<DeliveryNoteDetail>('/merchant/me/dropoffs', {
    method: 'POST',
    body: selectionBody(selection, 'parcelIds'),
  });
}

/**
 * LE VENDEUR RETIRE SA DEMANDE — point 0 du chantier.
 *
 * `DELETE /merchant/me/notes/:code`, et non `DELETE /delivery-notes/:code` :
 * la première lit la portée sous `notes.request`, la seconde exige
 * `notes.write`, c'est-à-dire la composition et la validation des bons de
 * toute la société. Même partage que pour la demande elle-même.
 */
export async function cancelMerchantNote(code: string): Promise<{ code: string }> {
  return apiFetch<{ code: string }>(`/merchant/me/notes/${encodeURIComponent(code)}`, {
    method: 'DELETE',
  });
}

/** La fiche de SA société — V2 vendeur bloc 9. Aucun identifiant à passer. */
export async function getMerchantCompany(): Promise<MerchantCompany> {
  return apiFetch<MerchantCompany>('/merchant/me/company');
}
