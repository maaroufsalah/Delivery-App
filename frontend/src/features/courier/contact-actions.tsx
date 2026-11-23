'use client';

import { phonesOf } from '@delivery/shared';
import { MessageCircle, Navigation, Phone } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';

/**
 * LES TROIS GESTES DU TERRAIN — L1 bloc 3, les mêmes que sur le mobile.
 *
 * **Des liens, pas des bibliothèques.** `tel:`, `wa.me` et Google Maps
 * s'ouvrent par une URL : sur un téléphone, l'application prend la main ; sur
 * un poste fixe, le navigateur ou le client de messagerie. Aucun paquet ne
 * ferait mieux, et chacun serait une dépendance de plus à mettre à jour.
 *
 * Au Maroc, WhatsApp est souvent le seul canal auquel un destinataire répond —
 * c'est la raison pour laquelle il a sa place à côté de l'appel, et pas dans un
 * menu.
 */
export function ContactActions({ phone, address }: { phone: string; address: string }) {
  const t = useTranslations('courier.parcel');
  /*
   * D-244 : le champ du destinataire porte jusqu'à trois numéros, rangés
   * « a / b / c ». UN lien d'appel par numéro : un `tel:` qui contiendrait la
   * valeur entière ne compose rien. Avec plusieurs numéros, chaque bouton
   * montre le sien. WhatsApp : le premier numéro seulement.
   */
  const phones = phonesOf(phone);
  const first = phones[0];

  return (
    <div className="flex flex-wrap gap-2">
      {phones.map((number) => (
        <Button key={number} asChild variant="outline" className="gap-2">
          <a
            href={`tel:${number}`}
            aria-label={phones.length > 1 ? t('callNumber', { phone: number }) : undefined}
          >
            <Phone className="size-4" aria-hidden />
            {phones.length > 1 ? <span dir="ltr">{number}</span> : t('call')}
          </a>
        </Button>
      ))}

      {first ? (
        <Button asChild variant="outline" className="gap-2">
          <a href={`https://wa.me/${waNumber(first)}`} target="_blank" rel="noreferrer">
            <MessageCircle className="size-4" aria-hidden />
            {t('whatsapp')}
          </a>
        </Button>
      ) : null}

      <Button asChild variant="outline" className="gap-2">
        <a
          href={`https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(address)}`}
          target="_blank"
          rel="noreferrer"
        >
          <Navigation className="size-4" aria-hidden />
          {t('directions')}
        </a>
      </Button>
    </div>
  );
}

/**
 * `0612345678` → `212612345678`, ce que `wa.me` attend.
 *
 * WhatsApp veut un numéro international sans `+` ni séparateur. Un numéro
 * marocain saisi en local commence par `0` : on le remplace par l'indicatif.
 * La même fonction que sur le mobile — elle tient en cinq lignes, et la
 * partager dans `packages/shared` pour cinq lignes coûterait plus cher en
 * lecture qu'en duplication.
 */
function waNumber(phone: string): string {
  const digits = phone.replace(/[^\d+]/g, '');
  if (digits.startsWith('+')) return digits.slice(1);
  if (digits.startsWith('0')) return `212${digits.slice(1)}`;
  return digits;
}
