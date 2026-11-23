import { useRouter } from 'expo-router';
import * as Notifications from 'expo-notifications';
import { useEffect, useRef } from 'react';
import { useI18n } from '@/i18n';
import { registerDevice } from '@/lib/notifications';
import { useSession } from '@/lib/session';

/**
 * L'inscription aux notifications, au BON MOMENT — M2/M3 bloc 9.
 *
 * **Après la connexion, pas au lancement.** Une invite système qui tombe avant
 * que l'application ait servi à quoi que ce soit se fait refuser — et sur iOS,
 * le refus est définitif : la demande ne se repropose jamais, il faut aller
 * dans les réglages du téléphone. On attend donc qu'un espace soit ouvert, où
 * l'utilisateur sait déjà ce que l'application fait pour lui.
 *
 * **Une seule fois par session.** Le jeton ne change pas d'un écran à l'autre,
 * et le réenregistrer à chaque rendu ferait un appel par navigation.
 *
 * Ce composant ne rend RIEN. Il vit dans la racine parce qu'il doit survivre
 * aux changements d'espace, pas parce qu'il a quelque chose à afficher.
 */
export function PushRegistration() {
  const { profile } = useSession();
  const { locale } = useI18n();
  const router = useRouter();

  const registered = useRef<string | null>(null);

  useEffect(() => {
    if (!profile) {
      // Déconnecté : on réarme, pour que le compte suivant s'inscrive.
      registered.current = null;
      return;
    }

    if (registered.current === profile.id) return;
    registered.current = profile.id;

    void registerDevice(locale);
  }, [profile, locale]);

  /**
   * Ouvrir l'écran que la notification annonce.
   *
   * `PushService` joint le sujet et les codes utiles aux données du message.
   * Sans ça, toucher une notification ouvrirait l'accueil, et l'utilisateur
   * devrait retrouver à la main ce qu'on venait de lui signaler.
   */
  useEffect(() => {
    const subscription = Notifications.addNotificationResponseReceivedListener((response) => {
      const data = response.notification.request.content.data as Record<string, string>;
      const destination = routeFor(data);
      if (destination) router.push(destination);
    });

    return () => subscription.remove();
  }, [router]);

  return null;
}

/**
 * Où mène un sujet.
 *
 * La table est ici et non dans le message : le serveur nomme un ÉVÉNEMENT, pas
 * un écran. Un chemin embarqué dans la charge utile deviendrait faux au premier
 * renommage de route, sur des téléphones qu'on ne met pas à jour.
 */
function routeFor(data: Record<string, string>): string | null {
  const { topic, parcelCode, claimCode } = data;

  switch (topic) {
    case 'parcel-delivered':
    case 'parcel-failed':
    case 'parcel-postponed':
    case 'parcel-delivery-cancelled':
      return parcelCode ? `/(merchant)/parcel/${encodeURIComponent(parcelCode)}` : null;

    case 'invoice-issued':
      return '/(merchant)/finance';

    case 'claim-answered':
      return claimCode ? `/(merchant)/claim/${encodeURIComponent(claimCode)}` : null;

    case 'route-assigned':
    // Lot I (D-210, D-211) : sa tournée a changé — un colis parti, reçu, ou à relivrer.
    case 'parcel-transferred-out':
    case 'parcel-transferred-in':
    case 'delivery-cancelled':
      return '/(courier)/circuits';

    case 'remittance-confirmed':
      return '/(courier)/remittances';

    case 'cash-variance':
      return '/(staff)/remittances';

    case 'claim-opened':
      return claimCode ? `/(staff)/claim/${encodeURIComponent(claimCode)}` : '/(staff)/claims';

    case 'stale-route':
      return '/(staff)/home';

    default:
      return null;
  }
}
