import * as Updates from 'expo-updates';

/**
 * QUEL ENVIRONNEMENT CE BINAIRE SERT — chantier UAT, D-200.
 *
 * Deux applications coexistent sur un même téléphone : « SpeedyTrans »
 * (`ma.nocido.delivery`, canal EAS `production`) parle à l'UAT, future
 * production ; « SpeedyTrans Recette » (`ma.nocido.delivery.recette`, canal
 * `preview`) parle à la recette.
 *
 * **Le canal décide, pas le bundle.** `Updates.channel` est gravé dans l'APK
 * au build et ne change pas avec une OTA. Une variable du bundle
 * (`EXPO_PUBLIC_API_URL`) suivrait au contraire l'OTA : une mise à jour
 * publiée avec le mauvais environnement EAS ferait appeler la recette par
 * l'application de production. Ici, quelle que soit l'OTA reçue, chaque
 * application garde SA pile.
 *
 * Sans canal (Expo Go, build de développement), c'est la recette — et
 * seulement là, `EXPO_PUBLIC_API_URL` peut la remplacer pour viser une API
 * locale par un tunnel.
 */
export type AppEnvironment = 'production' | 'recette';

const CHANNEL = Updates.channel;

export const APP_ENVIRONMENT: AppEnvironment = CHANNEL === 'production' ? 'production' : 'recette';

/**
 * L'API de PRODUCTION — un nom définitif, gardé au go-live : le vhost nginx
 * y pose lui-même l'hôte du tenant (`X-Forwarded-Host`), l'application n'a
 * donc rien d'autre à connaître. Changer le nom du web, au go-live, ne
 * demande ni APK ni OTA.
 */
export const PRODUCTION_API_ORIGIN = 'https://api.wimo.ma';

/** Surcharge de DÉVELOPPEMENT seulement : jamais lue par un binaire qui porte un canal. */
export function developmentApiOverride(): string | null {
  if (CHANNEL && CHANNEL !== 'development') return null;
  const value = process.env.EXPO_PUBLIC_API_URL?.trim();
  return value ? value.replace(/\/$/, '') : null;
}
