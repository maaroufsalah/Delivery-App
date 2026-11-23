import type { CourierRouteParcel } from '@delivery/shared';
import { routeTargetsSender, waNumber } from '@delivery/shared';
import { Platform } from 'react-native';

/**
 * L'ARRÊT : qui le livreur va voir, et où — C12 bloc 4.
 *
 * Une tournée de livraison va chez le destinataire ; une tournée de ramassage
 * va chez l'expéditeur. L'application affichait le destinataire dans les deux
 * cas : un livreur en ramassage appelait la mauvaise personne et roulait vers
 * la mauvaise adresse, parfois dans une autre ville.
 *
 * **Une seule fonction pour les trois écrans** — la liste, la fiche, le
 * scanner. La recopier aurait garanti qu'un des trois reste sur le
 * destinataire, et celui-là serait resté faux longtemps.
 */
export interface RouteStop {
  name: string;
  phone: string;
  address: string;
  /** Quartier ou ville, selon le sens. La seconde ligne de l'adresse. */
  area: string | null;
  /** L'URL à ouvrir pour l'itinéraire, prête à passer à `Linking`. */
  directionsUrl: string;
}

export function routeStopOf(kind: string, parcel: CourierRouteParcel): RouteStop {
  if (routeTargetsSender(kind)) {
    return {
      name: parcel.senderName,
      phone: parcel.senderPhone,
      address: parcel.pickupAddress,
      area: parcel.originCityName,
      /*
       * L'ADRESSE, jamais les coordonnées du colis.
       *
       * `latitude`/`longitude` sont celles où le livreur a rapporté une issue
       * (D-074), c'est-à-dire le pas de porte du DESTINATAIRE. Les utiliser
       * pour un ramassage enverrait exactement là où il ne faut pas aller.
       */
      directionsUrl: directionsTo(`${parcel.pickupAddress}, ${parcel.originCityName}`),
    };
  }

  return {
    name: parcel.recipientName,
    phone: parcel.recipientPhone,
    address: parcel.deliveryAddress,
    area: parcel.destinationDistrictName,
    directionsUrl:
      parcel.latitude !== null && parcel.longitude !== null
        ? directionsTo(`${parcel.latitude},${parcel.longitude}`)
        : directionsTo(`${parcel.deliveryAddress}, ${parcel.destinationCityName}`),
  };
}

/**
 * L'itinéraire, confié au téléphone.
 *
 * `geo:` sur Android laisse l'utilisateur choisir entre Google Maps et Waze —
 * beaucoup de livreurs préfèrent Waze au Maroc. iOS n'a pas d'équivalent
 * neutre : `maps.google.com` ouvre l'application si elle est installée.
 */
function directionsTo(target: string): string {
  if (Platform.OS === 'android') return `geo:0,0?q=${encodeURIComponent(target)}`;
  return `https://maps.google.com/?q=${encodeURIComponent(target)}`;
}

/**
 * Le numéro WhatsApp, ré-exporté — L2 bloc 3.
 *
 * La règle vit dans `packages/shared` depuis qu'elle sert aussi au web. Les
 * écrans continuent de l'importer d'ici, avec `routeStopOf` : c'est le même
 * sujet — à qui on parle, et où il est.
 */
export { waNumber };
