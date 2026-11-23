'use client';

import { phonesOf, routeTargetsSender } from '@delivery/shared';
import { Navigation, Phone } from 'lucide-react';
import { useTranslations } from 'next-intl';

/**
 * L'ARRÊT : qui on va voir, et où — C12 bloc 4.
 *
 * Un circuit de livraison va chez le destinataire ; un circuit de ramassage ou
 * de retour va chez l'expéditeur. C'était le second défaut du retour client :
 * l'écran affichait le destinataire quel que soit le type, et un livreur en
 * ramassage se rendait à l'adresse de livraison — souvent à l'autre bout de la
 * ville, parfois dans une autre ville.
 *
 * **Une seule fonction, partagée par les trois écrans** qui montrent un colis
 * de circuit : la composition, le détail, la boîte d'issue. La recopier trois
 * fois aurait garanti qu'un des trois reste sur le destinataire.
 */

/** Ce qu'un colis de circuit porte des deux côtés du trajet. */
export interface RouteStopSource {
  recipientName: string;
  recipientPhone: string;
  deliveryAddress: string;
  destinationDistrictName?: string | null;
  destinationCityName?: string | null;
  senderName: string;
  senderPhone: string;
  pickupAddress: string;
  originCityName: string;
}

export interface RouteStop {
  name: string;
  phone: string;
  /** L'adresse, quartier ou ville compris, telle qu'on la lit sur une ligne. */
  place: string;
  /** Ce qu'on donne à une carte : l'adresse et sa ville, jamais le quartier seul. */
  mapsQuery: string;
}

export function routeStopOf(kind: string, parcel: RouteStopSource): RouteStop {
  if (routeTargetsSender(kind)) {
    return {
      name: parcel.senderName,
      phone: parcel.senderPhone,
      place: [parcel.pickupAddress, parcel.originCityName].filter(Boolean).join(' · '),
      mapsQuery: `${parcel.pickupAddress}, ${parcel.originCityName}`,
    };
  }

  return {
    name: parcel.recipientName,
    phone: parcel.recipientPhone,
    place: [parcel.destinationDistrictName, parcel.destinationCityName ?? parcel.deliveryAddress]
      .filter(Boolean)
      .join(' · '),
    mapsQuery: `${parcel.deliveryAddress}, ${parcel.destinationCityName ?? ''}`,
  };
}

/**
 * Appeler, et ouvrir l'itinéraire.
 *
 * Deux liens, pas deux boutons qui exécutent du JavaScript : un agent qui
 * prépare une tournée depuis un poste fixe veut souvent les COPIER, et un
 * `tel:` se copie. Sur un portable, ils composent et ouvrent la carte.
 *
 * `maps.google.com/?q=` plutôt qu'un lien natif : le back-office s'ouvre dans
 * un navigateur, où `geo:` ne mène nulle part. L'application mobile, elle,
 * utilise `geo:` sur Android — c'est elle qui est dans la rue.
 */
export function RouteStopActions({ stop }: { stop: RouteStop }) {
  const t = useTranslations('routes.detail');
  /*
   * D-244 : jusqu'à trois numéros dans le champ du destinataire, rangés
   * « a / b / c ». UN lien par numéro, chacun avec SON `tel:` : la valeur
   * entière dans un seul lien ne compose rien et ne se copie pas.
   */
  const phones = phonesOf(stop.phone);

  return (
    <div className="flex shrink-0 items-center gap-1">
      {phones.map((phone) => (
        <a
          key={phone}
          href={`tel:${phone}`}
          aria-label={
            phones.length > 1
              ? t('callNumber', { name: stop.name, phone })
              : t('call', { name: stop.name })
          }
          title={phone}
          className="text-muted-foreground hover:text-foreground hover:bg-muted focus-visible:ring-ring flex size-8 items-center justify-center rounded-md outline-none focus-visible:ring-2"
        >
          <Phone className="size-4" aria-hidden />
        </a>
      ))}

      <a
        href={`https://maps.google.com/?q=${encodeURIComponent(stop.mapsQuery)}`}
        target="_blank"
        rel="noreferrer"
        aria-label={t('directions', { name: stop.name })}
        className="text-muted-foreground hover:text-foreground hover:bg-muted focus-visible:ring-ring flex size-8 items-center justify-center rounded-md outline-none focus-visible:ring-2"
      >
        <Navigation className="flip-rtl size-4" aria-hidden />
      </a>
    </div>
  );
}
