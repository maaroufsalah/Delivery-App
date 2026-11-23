import AsyncStorage from '@react-native-async-storage/async-storage';
import { useQueryClient } from '@tanstack/react-query';
import { router } from 'expo-router';
import { createContext, type ReactNode, useCallback, useContext, useEffect, useState } from 'react';
import { AppState } from 'react-native';
import { api, ApiError, onSessionLost } from './api';
import { forgetDevice } from './notifications';
import { reset as resetQueue } from './queue';
import { clearTokens, readTokens, saveTokens } from './vault';

/**
 * La SESSION, et le rôle qui décide de l'espace — M1, D-072.
 *
 * L'application accueillera trois rôles en trois phases (livreur, e-commerçant,
 * staff). C'est le profil rendu par l'API qui oriente, et non un choix de
 * l'utilisateur : un compte n'a pas à savoir dans quel groupe de routes il
 * atterrit, et le laisser choisir ouvrirait un écran vide à qui se trompe.
 *
 * **Le rattachement prime sur le rôle.** Un compte porte `courierId` ou
 * `clientId` ; c'est LUI qui rend un espace utile, parce que c'est lui dont la
 * portée serveur se sert. Un compte étiqueté `COURIER` sans fiche n'aurait que
 * des listes vides.
 */

export interface Profile {
  id: string;
  /** `null` pour un livreur connecté par son nom d'utilisateur (point 5). */
  email: string | null;
  /** Absent d'une API antérieure au point 5. */
  username?: string | null;
  firstName: string;
  lastName: string;
  roles: string[];
  clientId: string | null;
  courierId: string | null;
  /**
   * Permissions EFFECTIVES, lues dans la matrice en base — pas déduites du rôle.
   *
   * La matrice est éditable (D-019) : un STAFF privé de `finance.pay` ne doit
   * pas voir le bouton qui confirme une remise. Le recalculer depuis le rôle
   * utiliserait la matrice PAR DÉFAUT et proposerait des gestes que l'API
   * refuse — un écran qui ment est pire qu'un écran incomplet.
   */
  permissions: string[];
  mustChangePassword: boolean;
}

/** A-t-on cette clé ? La seule façon de décider d'afficher un geste. */
export function can(profile: Profile | null, permission: string): boolean {
  return profile?.permissions.includes(permission) ?? false;
}

export type Space = 'courier' | 'merchant' | 'staff' | 'none';

interface Session {
  profile: Profile | null;
  space: Space;
  /** `true` tant qu'on n'a pas tranché entre « connecté » et « pas connecté ». */
  loading: boolean;
  /** `true` quand `/auth/me` n'a pas répondu (réseau, 5xx) : profil connu, nouvel essai en cours. */
  degraded: boolean;
  signIn: (email: string, password: string) => Promise<Profile>;
  signOut: () => Promise<void>;
  /** Anonymise le compte côté serveur, puis se comporte comme `signOut` (D-071). */
  deleteAccount: () => Promise<void>;
}

const SessionContext = createContext<Session | null>(null);

/** Le dernier profil servi par `/auth/me` : ce qu'on montre quand le serveur ne répond pas. */
const PROFILE_KEY = 'session.profile';

async function rememberProfile(profile: Profile): Promise<void> {
  await AsyncStorage.setItem(PROFILE_KEY, JSON.stringify(profile)).catch(() => undefined);
}

async function recallProfile(): Promise<Profile | null> {
  try {
    const raw = await AsyncStorage.getItem(PROFILE_KEY);
    return raw ? (JSON.parse(raw) as Profile) : null;
  } catch {
    return null;
  }
}

export function useSession(): Session {
  const value = useContext(SessionContext);
  if (!value) throw new Error('useSession hors de SessionProvider.');
  return value;
}

/** L'espace d'un profil. Le RATTACHEMENT d'abord, le rôle ensuite. */
export function spaceOf(profile: Profile | null): Space {
  if (!profile) return 'none';
  if (profile.courierId) return 'courier';
  if (profile.clientId) return 'merchant';
  if (profile.roles.some((role) => role === 'STAFF' || role === 'SUPER_ADMIN')) return 'staff';

  return 'none';
}

export function SessionProvider({ children }: { children: ReactNode }) {
  const [profile, setProfile] = useState<Profile | null>(null);
  const [loading, setLoading] = useState(true);
  /** `/auth/me` n'a pas répondu au lancement : session gardée, nouvel essai en cours. */
  const [degraded, setDegraded] = useState(false);

  const queryClient = useQueryClient();

  /**
   * Purge locale — coffre, file, cache de requêtes — PUIS retour à la racine.
   *
   * **C'est la navigation qui manquait (C15 bloc 2).** `forget()` vidait tout
   * et posait `profile` à `null`, mais aucun écran ne réagissait : l'aiguillage
   * ne vit qu'à la racine (`app/index.tsx`), et le livreur restait sur « Plus »
   * à regarder un bouton qui n'avait, de son point de vue, rien fait.
   *
   * `replace` et non `push` : l'historique ne doit pas ramener dans l'espace
   * qu'on vient de quitter d'un geste de retour.
   *
   * **Le cache de requêtes part aussi.** Sans lui, le compte suivant ouvrirait
   * l'application sur la tournée du précédent — une fraction de seconde, le
   * temps du premier chargement, mais avec ses colis et ses montants à l'écran.
   */
  const forget = useCallback(async () => {
    await clearTokens();
    await AsyncStorage.removeItem(PROFILE_KEY).catch(() => undefined);
    setDegraded(false);
    // Les gestes en attente d'un compte ne suivent pas le suivant : un scan de
    // Hamid ne doit pas partir sous le jeton de Youssef.
    await resetQueue();
    queryClient.clear();
    setProfile(null);
    router.replace('/');
  }, [queryClient]);

  useEffect(() => {
    // Le client HTTP prévient quand un renouvellement a échoué : la session est
    // finie, et la racine doit renvoyer vers la connexion sans qu'un écran ait
    // à s'en apercevoir tout seul.
    return onSessionLost(() => void forget());
  }, [forget]);

  /*
   * AU LANCEMENT — Recette 7, point 7 (audit MOB-01, critique).
   *
   * Un jeton en coffre ne prouve rien : il peut être révoqué. C'est `/auth/me`
   * qui tranche, et il renouvelle au passage si besoin. Mais toute erreur
   * déconnectait : un livreur qui ouvrait l'application en sous-sol, ou pendant
   * un redéploiement de l'API, perdait sa session ET sa file hors-ligne — ses
   * livraisons de la matinée. Désormais :
   *
   *  - **401 confirmé** (le client a déjà tenté le renouvellement, D-077) : la
   *    session est finie, on oublie tout, comme avant ;
   *  - **tout le reste** (réseau, délai, 5xx, 429) : la session est GARDÉE sur
   *    le dernier profil connu, la file reste, le bandeau dit « Hors ligne,
   *    nouvel essai… » et `/auth/me` est retenté jusqu'à réponse.
   *
   * Sans profil connu (première ouverture, hors ligne) : on reste sur la
   * connexion, sans vider le coffre — le prochain lancement en ligne tranchera.
   */
  const probe = useCallback(async (): Promise<'ok' | 'lost' | 'degraded' | 'anonymous'> => {
    if (!(await readTokens())) return 'anonymous';
    try {
      const me = await api<Profile>('/auth/me');
      setProfile(me);
      await rememberProfile(me);
      setDegraded(false);
      return 'ok';
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        await forget();
        return 'lost';
      }
      const known = await recallProfile();
      if (known) setProfile((current) => current ?? known);
      setDegraded(true);
      return 'degraded';
    }
  }, [forget]);

  useEffect(() => {
    // La règle ne voit pas que `probe` est asynchrone : rien n'est posé pendant le rendu.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void probe().finally(() => setLoading(false));
  }, [probe]);

  useEffect(() => {
    if (!degraded) return;
    // Nouvel essai automatique : toutes les 20 s, et dès que l'application
    // revient au premier plan.
    const timer = setInterval(() => void probe(), 20_000);
    const subscription = AppState.addEventListener('change', (state) => {
      if (state === 'active') void probe();
    });
    return () => {
      clearInterval(timer);
      subscription.remove();
    };
  }, [degraded, probe]);

  const signIn = useCallback(async (email: string, password: string) => {
    /*
     * Deux appels, et c'est VOULU.
     *
     * `login` rend les jetons, et un profil tiré du jeton : il y manque le nom
     * et les permissions, que le jeton ne porte pas. `/auth/me` rend le profil
     * complet. Se contenter du premier laissait l'application avec deux formes
     * de profil selon le chemin d'entrée — connexion ou relancement — et c'est
     * exactement ce qui faisait rebondir un livreur vers l'écran Société au
     * second lancement.
     *
     * Une seule source, donc, pour les deux chemins. Le coût est un aller-retour
     * à la connexion, une fois par jour au plus.
     */
    const tokens = await api<{
      accessToken: string;
      refreshToken: string;
      expiresIn: number;
      /** Échéance du rafraîchissement — C15 bloc 1. Absente d'une API ancienne. */
      refreshExpiresIn?: number;
      // `identifier` : l'e-mail OU le nom d'utilisateur (point 5).
    }>('/auth/login', {
      method: 'POST',
      body: { identifier: email, password },
      anonymous: true,
    });

    await saveTokens(tokens);

    const me = await api<Profile>('/auth/me');
    setProfile(me);
    await rememberProfile(me);

    return me;
  }, []);

  const signOut = useCallback(async () => {
    /*
     * LE SERVEUR NE RETIENT PERSONNE — C15 bloc 2.
     *
     * Les deux appels sont plafonnés à quatre secondes : un réseau d'agence
     * qui traîne ne doit pas laisser l'utilisateur devant un bouton qui n'a
     * l'air de rien faire. Leur échec ne change rien à la suite — la purge
     * locale a lieu de toute façon, et la révocation partira au prochain
     * renouvellement refusé.
     *
     * L'appareil AVANT la déconnexion : la route qui l'oublie demande une
     * session. Les notifications d'un compte ne suivent pas le suivant, pour la
     * même raison que la file se vide (D-077) — un téléphone d'agence qui
     * change de main ne doit pas continuer à sonner pour l'ancien.
     */
    await Promise.race([
      (async () => {
        await forgetDevice();
        await api('/auth/logout', { method: 'POST' }).catch(() => undefined);
      })(),
      new Promise((resolve) => setTimeout(resolve, 4_000)),
    ]).catch(() => undefined);

    await forget();
  }, [forget]);

  const deleteAccount = useCallback(async () => {
    await api('/auth/me', { method: 'DELETE' });
    await forget();
  }, [forget]);

  return (
    <SessionContext.Provider
      value={{
        profile,
        space: spaceOf(profile),
        loading,
        degraded,
        signIn,
        signOut,
        deleteAccount,
      }}
    >
      {children}
    </SessionContext.Provider>
  );
}
