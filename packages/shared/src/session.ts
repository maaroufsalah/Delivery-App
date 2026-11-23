/**
 * DURÉES DE SESSION — C15 bloc 1, D-019.
 *
 * Quatre durées, et une règle : **l'utilisateur ne voit « session expirée » que
 * si son jeton de rafraîchissement est réellement mort.** Tout le reste se
 * renouvelle en silence.
 *
 * **Pourquoi quatre et pas une.** Un navigateur partagé au comptoir d'une
 * agence et un téléphone de livreur qui ne quitte pas sa poche ne courent pas
 * le même risque. Une seule durée obligerait à choisir entre déconnecter le
 * livreur trois fois par jour et laisser une session ouverte un mois sur un
 * poste commun. Le « Se souvenir de moi » est la troisième voie : c'est le
 * titulaire qui dit que le poste est le sien.
 *
 * **Pourquoi en `Setting` et pas en constante.** Une société qui manipule des
 * remises de fonds voudra des sessions courtes ; une autre, dont les livreurs
 * sont en zone blanche la moitié de la journée, voudra l'inverse. C'est une
 * politique, pas une règle du domaine (D-019) — elle se règle dans Paramètres,
 * sans déploiement.
 */

/** Le genre de session, tel qu'il est signé dans le jeton de rafraîchissement. */
export type SessionKind = 'web' | 'remember' | 'mobile';

export const SESSION_KINDS: readonly SessionKind[] = ['web', 'remember', 'mobile'];

export interface SessionDurations {
  /** Jeton d'accès, en SECONDES. Court : il ne se révoque pas. */
  accessSeconds: number;
  /** Rafraîchissement d'un navigateur ordinaire. */
  webSeconds: number;
  /** Rafraîchissement d'un navigateur où « Se souvenir de moi » a été coché. */
  rememberSeconds: number;
  /** Rafraîchissement de l'application mobile. */
  mobileSeconds: number;
}

const MINUTE = 60;
const DAY = 24 * 60 * 60;

export const DEFAULT_SESSION_DURATIONS: SessionDurations = {
  accessSeconds: 15 * MINUTE,
  webSeconds: 7 * DAY,
  rememberSeconds: 30 * DAY,
  mobileSeconds: 30 * DAY,
};

/**
 * De combien le COOKIE d'accès meurt avant le JETON qu'il porte.
 *
 * **C'est tout le renouvellement silencieux du web.** Le proxy ne renouvelle
 * que lorsqu'il ne voit plus de cookie d'accès ; si ce cookie vivait aussi
 * longtemps que le jeton, la dernière requête avant l'échéance partirait avec
 * un jeton expiré et recevrait un `401` — une erreur visible pour une session
 * parfaitement valide.
 *
 * En faisant disparaître le cookie une minute plus tôt, le renouvellement se
 * déclenche pendant que le jeton est encore bon : aucune requête ne porte
 * jamais un jeton mort, et l'utilisateur ne voit rien.
 */
export const RENEWAL_MARGIN_SECONDS = 60;

/** Durée de vie du cookie d'accès : celle du jeton, moins la marge. */
export function accessCookieSeconds(accessSeconds: number): number {
  return Math.max(30, accessSeconds - RENEWAL_MARGIN_SECONDS);
}

/**
 * Bornes de sécurité.
 *
 * Une valeur mal saisie dans Paramètres ne doit pouvoir produire **ni** une
 * session d'une seconde — qui rendrait le back-office inutilisable sans que
 * personne comprenne pourquoi — **ni** une session éternelle, qui viderait la
 * déconnexion globale de son sens.
 */
const BOUNDS = {
  accessSeconds: { min: 5 * MINUTE, max: 60 * MINUTE },
  refreshSeconds: { min: 1 * DAY, max: 90 * DAY },
} as const;

function clamp(value: unknown, fallback: number, bounds: { min: number; max: number }): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) return fallback;

  return Math.min(bounds.max, Math.max(bounds.min, Math.round(value)));
}

/**
 * Les durées d'une société, à partir de valeurs lues en base.
 *
 * Les clés sont exprimées en **minutes** et en **jours** parce que c'est ce
 * qu'un administrateur saisit ; la conversion en secondes se fait ici, une
 * seule fois, plutôt que dans chaque appelant.
 */
export function sessionDurationsFrom(values: {
  accessMinutes?: unknown;
  webDays?: unknown;
  rememberDays?: unknown;
  mobileDays?: unknown;
}): SessionDurations {
  const seconds = (value: unknown, unit: number): unknown =>
    typeof value === 'number' && Number.isFinite(value) ? value * unit : undefined;

  return {
    accessSeconds: clamp(
      seconds(values.accessMinutes, MINUTE),
      DEFAULT_SESSION_DURATIONS.accessSeconds,
      BOUNDS.accessSeconds,
    ),
    webSeconds: clamp(
      seconds(values.webDays, DAY),
      DEFAULT_SESSION_DURATIONS.webSeconds,
      BOUNDS.refreshSeconds,
    ),
    rememberSeconds: clamp(
      seconds(values.rememberDays, DAY),
      DEFAULT_SESSION_DURATIONS.rememberSeconds,
      BOUNDS.refreshSeconds,
    ),
    mobileSeconds: clamp(
      seconds(values.mobileDays, DAY),
      DEFAULT_SESSION_DURATIONS.mobileSeconds,
      BOUNDS.refreshSeconds,
    ),
  };
}

/** Combien de secondes vaut un rafraîchissement de ce genre, chez cette société. */
export function refreshSecondsFor(kind: SessionKind, durations: SessionDurations): number {
  if (kind === 'mobile') return durations.mobileSeconds;
  if (kind === 'remember') return durations.rememberSeconds;

  return durations.webSeconds;
}

/** Normalise une valeur venue d'un jeton ancien ou d'un corps de requête. */
export function sessionKindOf(value: unknown): SessionKind {
  return SESSION_KINDS.includes(value as SessionKind) ? (value as SessionKind) : 'web';
}

/**
 * LE CODE D'ERREUR QUI TRAVERSE LA FRONTIÈRE SERVEUR — C15 bloc 3.
 *
 * **Le problème que ça résout.** Next remplace le message d'une erreur serveur
 * par un identifiant avant de l'envoyer au navigateur : en production, la
 * frontière d'erreur ne sait rien de ce qui a échoué, et affiche donc « Une
 * erreur est survenue » aussi bien pour une session expirée que pour une base
 * en panne. C'est exactement ce que voyaient les utilisateurs.
 *
 * **Le seul champ qui passe est `digest`**, et Next respecte celui qu'on a
 * posé soi-même — c'est ainsi que `redirect()` et `notFound()` fonctionnent. On
 * y écrit donc le statut HTTP, et la frontière peut enfin distinguer les trois
 * cas : se reconnecter, expliquer un refus, ou proposer de réessayer.
 *
 * Le format est volontairement pauvre — un préfixe et un nombre. Rien de ce qui
 * y transite n'est confidentiel, et il finit dans le HTML.
 */
const DIGEST_PREFIX = 'DP_API';

export function apiErrorDigest(status: number, requestId?: string | null): string {
  /*
   * L'IDENTIFIANT DE REQUÊTE voyage avec le statut — recette SpeedyTrans 4,
   * point 5 c : la page d'erreur affiche « Référence : <requestId> », que le
   * client nous lit et que la console « Erreurs » retrouve. Il n'a rien de
   * confidentiel ; il est borné à sa forme sûre (lettres, chiffres, tirets).
   */
  const reference = requestId && /^[A-Za-z0-9-]{8,64}$/.test(requestId) ? `;${requestId}` : '';
  return `${DIGEST_PREFIX};${status}${reference}`;
}

/** Le statut porté par un `digest`, ou `null` si ce n'en est pas un des nôtres. */
export function apiErrorStatus(digest: string | undefined): number | null {
  if (!digest?.startsWith(`${DIGEST_PREFIX};`)) return null;

  const status = Number(digest.slice(DIGEST_PREFIX.length + 1).split(';')[0]);
  return Number.isFinite(status) ? status : null;
}

/**
 * La RÉFÉRENCE à montrer au client — recette 4, point 5 c. Pour une erreur de
 * l'API : son identifiant de requête. Pour une erreur de rendu : le `digest`
 * que Next pose et journalise lui-même côté serveur.
 */
export function errorReference(digest: string | undefined): string | null {
  if (!digest) return null;
  if (!digest.startsWith(`${DIGEST_PREFIX};`)) return digest;
  return digest.slice(DIGEST_PREFIX.length + 1).split(';')[1] ?? null;
}

/**
 * Le chemin de retour après connexion, ASSAINI — C15 bloc 3.
 *
 * **Une redirection ouverte est une vraie faille** : `?next=https://…` posé
 * dans un lien envoyé par courriel renverrait l'utilisateur, fraîchement
 * authentifié, sur un site qui n'est pas le nôtre. On n'accepte donc qu'un
 * chemin relatif à une seule barre oblique.
 *
 * `//evil.example` et `/\evil.example` sont des URL absolues pour un
 * navigateur : les deux sont refusées explicitement, parce que les oublier est
 * l'erreur classique de ce contrôle.
 */
/**
 * `URL` existe partout où ce paquet tourne (Node ≥ 10, navigateurs), mais la
 * bibliothèque de types de `packages/shared` ne charge pas le DOM : on nomme
 * ici ce qu'on lit, et rien d'autre. Déclaration ambiante, aucun code émis.
 */
interface ResolvedUrl {
  origin: string;
  pathname: string;
  search: string;
  hash: string;
  username: string;
  password: string;
}
declare const URL: new (url: string, base?: string) => ResolvedUrl;

export function safeReturnPath(value: unknown): string | null {
  if (typeof value !== 'string') return null;

  const path = value.trim();
  if (!path.startsWith('/')) return null;

  /*
   * SEC-23 (audit GO-TO-PROD, confirmé en Node) : `?next=/%09/evil.com` passait.
   * Le paramètre arrive décodé une fois (`"/\t/evil.com"`), et l'analyseur
   * d'URL du navigateur RETIRE tabulations et sauts de ligne avant de lire :
   * `new URL("/\t/evil.com", origin).href === "https://evil.com/"`. Même
   * famille : la barre inverse (`/\evil.com`), les caractères de contrôle, et
   * les formes encodées une fois de plus (`/%2F%2Fevil.com`), qu'un second
   * décodage révèle. On décode donc une copie pour le CONTRÔLE, et on ne rend
   * jamais autre chose que le chemin RENORMALISÉ par l'analyseur lui-même.
   */
  let decoded: string;
  try {
    decoded = decodeURIComponent(path);
  } catch {
    return null;
  }
  for (const candidate of [path, decoded]) {
    // eslint-disable-next-line no-control-regex -- c'est précisément ce qu'on cherche
    if (/[\u0000-\u001f\u007f\s\\]/.test(candidate)) return null;
    if (candidate.startsWith('//')) return null;
    if (candidate.includes('://')) return null;
  }

  // Résolution contre une origine témoin : ce qui en sort doit y rester.
  const origin = 'https://return-path.invalid';
  let url: ResolvedUrl;
  try {
    url = new URL(path, origin);
  } catch {
    return null;
  }
  if (url.origin !== origin || url.username || url.password) return null;
  if (!url.pathname.startsWith('/') || url.pathname.startsWith('//')) return null;

  return `${url.pathname}${url.search}${url.hash}`;
}
