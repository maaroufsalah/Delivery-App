import * as SecureStore from 'expo-secure-store';

/**
 * Le coffre à jetons — M1, docs/DECISIONS.md D-069.
 *
 * `expo-secure-store` s'appuie sur le Keychain iOS et le Keystore Android :
 * chiffré au repos, lié à l'appareil, hors de portée d'une sauvegarde en clair.
 * C'est ce qui justifie que l'API envoie les jetons dans le corps plutôt qu'en
 * cookie `httpOnly` — le coffre est mieux gardé qu'un cookie ne l'a jamais été.
 *
 * **`WHEN_UNLOCKED_THIS_DEVICE_ONLY`** : le jeton ne suit pas une restauration
 * de sauvegarde sur un autre téléphone. Un livreur qui change d'appareil se
 * reconnecte, ce qui est une gêne de dix secondes ; l'inverse aurait laissé une
 * session ouverte sur un appareil dont on ne sait rien.
 *
 * **Rien d'autre n'entre ici.** Pas de profil, pas de tournée en cache : le
 * coffre est lent par construction, et y ranger des données d'écran ferait
 * payer un déchiffrement à chaque rendu.
 */

const ACCESS_KEY = 'auth.access';
const REFRESH_KEY = 'auth.refresh';
const EXPIRY_KEY = 'auth.expiresAt';
/**
 * Échéance du RAFRAÎCHISSEMENT — C15 bloc 1.
 *
 * Sans elle, l'application ne peut pas distinguer « ma session est finie » de
 * « le serveur ne répond pas » : les deux se présentent comme un
 * renouvellement qui échoue. Avec elle, un refus réseau ne coûte rien — on
 * sait que le jeton est encore bon, on réessaiera.
 */
const REFRESH_EXPIRY_KEY = 'auth.refreshExpiresAt';

const OPTIONS: SecureStore.SecureStoreOptions = {
  keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
};

export interface Tokens {
  accessToken: string;
  refreshToken: string;
  /** Instant d'expiration, en millisecondes epoch. */
  expiresAt: number;
  /**
   * Échéance du rafraîchissement, en millisecondes epoch.
   *
   * `0` = inconnue, et c'est le cas des installations antérieures à C15 : on
   * ne sait pas, donc on ne conclut pas. Le serveur tranchera.
   */
  refreshExpiresAt: number;
}

export async function readTokens(): Promise<Tokens | null> {
  const [accessToken, refreshToken, expiresAt, refreshExpiresAt] = await Promise.all([
    SecureStore.getItemAsync(ACCESS_KEY, OPTIONS),
    SecureStore.getItemAsync(REFRESH_KEY, OPTIONS),
    SecureStore.getItemAsync(EXPIRY_KEY, OPTIONS),
    SecureStore.getItemAsync(REFRESH_EXPIRY_KEY, OPTIONS),
  ]);

  if (!accessToken || !refreshToken) return null;

  return {
    accessToken,
    refreshToken,
    expiresAt: Number(expiresAt ?? 0),
    refreshExpiresAt: Number(refreshExpiresAt ?? 0),
  };
}

/**
 * Range le couple, avec sa date d'expiration CALCULÉE ici.
 *
 * L'API rend une durée en secondes et non une date, parce que l'horloge d'un
 * téléphone peut être fausse de plusieurs heures (D-069). On la convertit sur
 * l'horloge locale, qui est juste par rapport à elle-même.
 */
export async function saveTokens(session: {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  /** Absente d'une API antérieure à C15 : l'échéance reste alors inconnue. */
  refreshExpiresIn?: number;
}): Promise<void> {
  const now = Date.now();

  await Promise.all([
    SecureStore.setItemAsync(ACCESS_KEY, session.accessToken, OPTIONS),
    SecureStore.setItemAsync(REFRESH_KEY, session.refreshToken, OPTIONS),
    SecureStore.setItemAsync(EXPIRY_KEY, String(now + session.expiresIn * 1000), OPTIONS),
    SecureStore.setItemAsync(
      REFRESH_EXPIRY_KEY,
      String(session.refreshExpiresIn ? now + session.refreshExpiresIn * 1000 : 0),
      OPTIONS,
    ),
  ]);
}

export async function clearTokens(): Promise<void> {
  await Promise.all([
    SecureStore.deleteItemAsync(ACCESS_KEY, OPTIONS),
    SecureStore.deleteItemAsync(REFRESH_KEY, OPTIONS),
    SecureStore.deleteItemAsync(EXPIRY_KEY, OPTIONS),
    SecureStore.deleteItemAsync(REFRESH_EXPIRY_KEY, OPTIONS),
  ]);
}

/**
 * Le jeton est-il à bout de souffle ?
 *
 * Une marge de trente secondes : une requête partie avec un jeton qui expire
 * dans deux secondes arrive expirée, et le 401 qui s'ensuit coûte un
 * aller-retour de plus sur une 3G d'agence.
 */
export function isExpired(tokens: Tokens, now = Date.now()): boolean {
  return tokens.expiresAt - 30_000 <= now;
}

/**
 * Le RAFRAÎCHISSEMENT est-il mort ? — C15 bloc 1.
 *
 * **`false` quand on ne sait pas**, et c'est délibéré : une échéance inconnue
 * — installation antérieure à C15, coffre partiellement écrit — ne doit pas
 * déconnecter. Le serveur reste seul juge ; cette fonction ne fait qu'éviter
 * un aller-retour dont on connaît déjà l'issue.
 */
export function isRefreshExpired(tokens: Tokens, now = Date.now()): boolean {
  return tokens.refreshExpiresAt > 0 && tokens.refreshExpiresAt <= now;
}
