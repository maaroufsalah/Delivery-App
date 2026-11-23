import * as Updates from 'expo-updates';
import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';

/**
 * LA MISE À JOUR OTA, VUE DE L'APPLICATION — C14 bloc 4.
 *
 * **Ce que le diagnostic a établi.** La configuration était juste de bout en
 * bout : `ENABLED = true`, `CHECK_ON_LAUNCH = ALWAYS`, la bonne URL, le bon
 * runtime, le bon canal. Le seul écart était `LAUNCH_WAIT_MS = 0`
 * (`fallbackToCacheTimeout` absent, donc zéro) : l'application ne PATIENTE
 * jamais. Elle démarre sur le bundle en cache, télécharge en arrière-plan, et
 * n'applique qu'au démarrage à froid SUIVANT — or un balayage depuis les
 * récents ne tue pas toujours le processus Android.
 *
 * **Mais le vrai défaut était qu'on ne pouvait pas le savoir.** Aucun appel à
 * `checkForUpdateAsync()`, aucun journal, rien à l'écran : impossible de
 * distinguer « téléchargé, pas encore appliqué » de « la requête n'est jamais
 * partie ». C'est ce trou-là que ce fichier bouche, et il est livrable par
 * OTA — `expo-updates` est déjà lié dans l'APK `ebb98c4b`.
 *
 * **Jamais de rechargement forcé.** `reloadAsync()` relance l'application
 * immédiatement : l'appeler tout seul couperait un livreur en train de
 * rapporter une issue devant une porte. Le téléchargement est automatique, le
 * REDÉMARRAGE est un geste — un bandeau, un bouton, et lui qui choisit quand.
 */

/** Où en est la mise à jour, du point de vue de quelqu'un qui regarde l'écran. */
export type UpdateStatus =
  | 'idle'
  /** La vérification est partie. */
  | 'checking'
  /** Une mise à jour existe, on la télécharge. */
  | 'downloading'
  /** Téléchargée : il ne manque qu'un redémarrage. */
  | 'ready'
  /** Vérifiée, rien de neuf. */
  | 'current'
  /** Indisponible ici : développement, ou OTA désactivées dans ce binaire. */
  | 'unavailable';

/** Ce que « À propos » affiche, et qui PROUVE quel bundle tourne. */
export interface UpdateInfo {
  channel: string | null;
  runtimeVersion: string | null;
  /** `null` quand l'application tourne sur le bundle embarqué dans l'APK. */
  updateId: string | null;
  embedded: boolean;
}

interface AppUpdate {
  status: UpdateStatus;
  info: UpdateInfo;
  /** Vérifie maintenant. `silent` : la vérification automatique du démarrage. */
  check: (silent?: boolean) => Promise<void>;
  /** Applique la mise à jour téléchargée. Le seul appel à `reloadAsync`. */
  restart: () => Promise<void>;
}

const UpdateContext = createContext<AppUpdate | null>(null);

export function useAppUpdate(): AppUpdate {
  const value = useContext(UpdateContext);
  if (!value) throw new Error('useAppUpdate hors de UpdateProvider.');
  return value;
}

export function UpdateProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<UpdateStatus>(Updates.isEnabled ? 'idle' : 'unavailable');

  const check = useCallback(async (silent = false) => {
    /*
     * `isEnabled` est faux en développement et dans un client de
     * développement : `checkForUpdateAsync()` y LÈVE. Sans cette garde,
     * l'application planterait au démarrage sur le poste de qui la développe,
     * ce qui est le meilleur moyen de faire retirer la fonctionnalité.
     */
    if (!Updates.isEnabled) {
      setStatus('unavailable');
      return;
    }

    try {
      setStatus('checking');
      const result = await Updates.checkForUpdateAsync();

      if (!result.isAvailable) {
        setStatus('current');
        return;
      }

      setStatus('downloading');
      await Updates.fetchUpdateAsync();

      /*
       * `isNew` est faux quand le bundle était déjà en cache — typiquement le
       * cas que le diagnostic décrivait : téléchargé à un lancement
       * précédent, jamais appliqué faute de démarrage à froid. On ne le
       * regarde donc PAS : dans les deux cas le bundle est là et il ne manque
       * qu'un redémarrage, ce qui est exactement ce qu'il faut dire.
       */
      setStatus('ready');
    } catch {
      /*
       * Réseau absent, serveur muet, jeton expiré : on retombe en silence.
       * Une vérification de mise à jour qui alerte sur son propre échec
       * apprend à ignorer les alertes — et le livreur n'y peut rien.
       */
      setStatus(silent ? 'idle' : 'current');
    }
  }, []);

  const restart = useCallback(async () => {
    if (!Updates.isEnabled) return;
    await Updates.reloadAsync();
  }, []);

  useEffect(() => {
    /*
     * UNE fois, au montage — et ce montage a lieu APRÈS le portail
     * (`AppGate`), donc après le splash, sur l'écran de connexion ou
     * l'accueil. Vérifier avant afficherait un bandeau par-dessus un écran que
     * personne n'a encore vu.
     *
     * DIFFÉRÉE d'une seconde et demie, et pour deux raisons qui vont dans le
     * même sens. La première est réseau : le premier écran lance déjà le thème
     * du tenant, la configuration mobile et la tournée, et une quatrième
     * requête en concurrence sur une 3G d'agence retarde ce que le livreur
     * attend vraiment. La seconde est de rendu : un `setState` synchrone dans un
     * effet déclenche une cascade, ce que le compilateur React refuse à juste
     * titre.
     *
     * Le délai n'est pas perdu : le bandeau n'a de sens qu'une fois l'écran
     * lisible, et personne ne redémarre l'application dans sa première seconde.
     */
    const timer = setTimeout(() => void check(true), 1_500);

    return () => clearTimeout(timer);
  }, [check]);

  const info: UpdateInfo = {
    channel: Updates.channel,
    runtimeVersion: Updates.runtimeVersion,
    updateId: Updates.updateId,
    embedded: Updates.isEmbeddedLaunch,
  };

  return (
    <UpdateContext.Provider value={{ status, info, check, restart }}>
      {children}
    </UpdateContext.Provider>
  );
}
