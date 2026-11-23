import type { NotificationSettings } from '@delivery/shared';
import Constants from 'expo-constants';
import * as Notifications from 'expo-notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Platform } from 'react-native';
import { api } from './api';

/**
 * Les notifications, côté appareil — M2/M3 bloc 9.
 *
 * **La permission se demande AU BON MOMENT**, pas au lancement. Une invite
 * système qui tombe avant que l'application ait servi à quoi que ce soit se
 * fait refuser, et iOS ne la repropose jamais : le refus est définitif, et il
 * faut alors aller dans les réglages du téléphone. On attend donc que le compte
 * soit connecté et que l'espace soit ouvert — à ce moment, l'utilisateur sait
 * ce que l'application fait pour lui.
 *
 * **Un refus n'est pas une panne.** Tout continue de fonctionner ; seules les
 * notifications manquent. C'est ce que les magasins attendent, et ce que M1
 * avait déjà posé pour la caméra et la position.
 */

/** Comment une notification se présente quand l'application est OUVERTE. */
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    /*
     * On l'affiche quand même. Un agent qui consulte une fiche pendant qu'un
     * écart de caisse tombe doit le voir : l'application ouverte ne veut pas
     * dire que l'utilisateur regarde l'écran qui concerne l'alerte.
     */
    shouldShowBanner: true,
    shouldShowList: true,
    shouldPlaySound: false,
    shouldSetBadge: false,
  }),
});

/**
 * Le jeton de cet appareil, si tout se prête.
 *
 * Rend `null` — jamais une exception — dans les trois cas où c'est normal :
 * permission refusée, simulateur, projet EAS pas encore créé. Aucun n'est une
 * erreur de l'application, et aucun ne doit interrompre une connexion.
 */
export async function obtainPushToken(): Promise<string | null> {
  try {
    const existing = await Notifications.getPermissionsAsync();

    let granted = existing.granted;

    if (!granted && existing.canAskAgain) {
      const asked = await Notifications.requestPermissionsAsync();
      granted = asked.granted;
    }

    if (!granted) return null;

    /*
     * Android exige un CANAL, sinon la notification arrive sans son ni
     * priorité — et `PushService` envoie `channelId: 'default'`.
     */
    if (Platform.OS === 'android') {
      await Notifications.setNotificationChannelAsync('default', {
        name: 'Nocido Delivery',
        importance: Notifications.AndroidImportance.HIGH,
      });
    }

    /*
     * L'identifiant du projet EAS. Il n'existe qu'après `eas init` : tant que
     * le compte Expo n'est pas créé, il n'y a pas de jeton à obtenir, et ce
     * n'est pas une panne — juste une étape de déploiement qui reste à faire.
     */
    const projectId = Constants.expoConfig?.extra?.eas?.projectId ?? Constants.easConfig?.projectId;

    if (!projectId) return null;

    const token = await Notifications.getExpoPushTokenAsync({ projectId });
    return token.data;
  } catch {
    /*
     * Simulateur, service Google Play absent, réseau coupé. Aucun de ces cas
     * n'empêche l'application de servir : on repart sans notifications.
     *
     * **`expo-device` n'est PAS utilisé** pour détecter le simulateur : la
     * mission interdit une dépendance de plus, et ce `catch` donne exactement
     * le même résultat — pas de jeton, pas d'erreur visible.
     */
    return null;
  }
}

/** Enregistre l'appareil auprès de l'API. Silencieux en cas d'échec. */
export async function registerDevice(locale: string): Promise<string | null> {
  const token = await obtainPushToken();
  if (!token) return null;

  try {
    await api('/me/push-token', {
      method: 'POST',
      body: {
        token,
        platform: Platform.OS === 'ios' ? 'ios' : 'android',
        locale,
        appVersion: Constants.expoConfig?.version,
      },
    });

    return token;
  } catch {
    // Une inscription ratée n'est pas une panne de session : on réessaiera au
    // prochain lancement.
    return null;
  }
}

/**
 * Oublie l'appareil — à la déconnexion.
 *
 * Le pendant de la purge du coffre et de la file (D-077) : les notifications
 * d'un compte ne suivent pas le suivant. Un téléphone d'agence qui change de
 * main ne doit pas continuer à sonner pour l'ancien.
 */
export async function forgetDevice(): Promise<void> {
  try {
    const token = await obtainPushToken();
    if (!token) return;

    await api('/me/push-token', { method: 'DELETE', body: { token } });
  } catch {
    // Le serveur transfère de toute façon la ligne au prochain compte qui
    // enregistre ce jeton : l'échec ici ne laisse pas de fuite durable.
  }
}

/* --- Les préférences, pour l'écran Profil --------------------------------- */

export function useNotificationSettings() {
  return useQuery({
    queryKey: ['notifications', 'settings'] as const,
    queryFn: () => api<NotificationSettings>('/me/notifications'),
  });
}

export function useSetNotificationPreference() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (input: { topic: string; enabled: boolean }) =>
      api<NotificationSettings>('/me/notifications', { method: 'PATCH', body: input }),
    onSuccess: (settings) => {
      // Le serveur rend l'état complet : on le pose plutôt que de réinterroger.
      client.setQueryData(['notifications', 'settings'], settings);
    },
  });
}
