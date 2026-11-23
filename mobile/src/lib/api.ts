import { ACTION_TIMEOUT_MS } from '@delivery/shared';
import { apiBaseUrl, readSlug } from './tenant';
import {
  clearTokens,
  isExpired,
  isRefreshExpired,
  readTokens,
  saveTokens,
  type Tokens,
} from './vault';

/**
 * Le client HTTP de l'application — M1, docs/DECISIONS.md D-069.
 *
 * **Un seul endroit sait parler à l'API.** Chaque écran passe par `api()` :
 * l'en-tête `X-Client: mobile`, le jeton, son renouvellement et la traduction
 * des erreurs métier vivent ici et nulle part ailleurs.
 *
 * **Le renouvellement est SÉRIALISÉ.** Un écran de tournée lance cinq requêtes
 * d'un coup ; si le jeton vient d'expirer, cinq renouvellements partiraient en
 * parallèle et quatre d'entre eux présenteraient un refresh token déjà tourné —
 * donc quatre déconnexions. La promesse en cours est partagée, et les quatre
 * autres l'attendent.
 */

/** Ce que l'API renvoie sur un refus : un motif MACHINE, et un message humain. */
export interface ApiFailure {
  status: number;
  message: string;
  /** `parcel-other-route`, `parcel-wrong-city`… L'écran traduit, il ne devine pas. */
  reason?: string;
  details?: Record<string, unknown>;
  /**
   * L'identifiant que l'API a rendu en `x-request-id` (D-134) — Recette 5,
   * point 4 a. L'écran l'affiche en « Référence » : le livreur le lit à
   * l'agence, la console « Erreurs » retrouve la ligne.
   */
  requestId?: string;
}

export class ApiError extends Error {
  constructor(readonly failure: ApiFailure) {
    super(failure.message);
    this.name = 'ApiError';
  }

  get status(): number {
    return this.failure.status;
  }

  get reason(): string | undefined {
    return this.failure.reason;
  }

  get requestId(): string | undefined {
    return this.failure.requestId;
  }

  /** Vrai quand l'appel a échoué SANS réponse : c'est un problème de réseau. */
  static isOffline(error: unknown): boolean {
    return error instanceof ApiError && error.status === 0;
  }

  /**
   * LE GESTE EST BON, CE N'EST PAS LE MOMENT — C15 bloc 3.
   *
   * La file ne se déclenchait que sur une absence de réseau. Un `503` pendant
   * un redéploiement de l'API, un `429` de limitation, un `408` de délai
   * dépassé : aucun des trois ne dit que le geste est mauvais, et tous les
   * trois faisaient pourtant **perdre le scan** — le livreur voyait une erreur,
   * debout dans l'agence, et devait recommencer.
   *
   * Un refus MÉTIER, lui, remonte toujours : « déjà pris par Youssef » est une
   * information dont il a besoin tout de suite, pas le soir au rejeu.
   *
   * Le `401` n'est pas ici, et c'est délibéré : quand un renouvellement est
   * vraiment refusé, la session est finie, et la file se vide avec elle
   * (D-077). Un `401` provoqué par le RÉSEAU, lui, ne parvient jamais jusqu'ici
   * — `api()` le rend en `status: 0`, donc en hors-ligne.
   */
  static isDeferrable(error: unknown): boolean {
    if (!(error instanceof ApiError)) return false;

    return (
      error.status === 0 || error.status === 408 || error.status === 429 || error.status >= 500
    );
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PATCH' | 'DELETE';
  body?: unknown;
  /** Coupe le renouvellement : `refresh` lui-même, et la connexion. */
  anonymous?: boolean;
  signal?: AbortSignal;
  /**
   * Le délai maximal d'UN envoi — Recette 5, point 4 a. 15 s par défaut ; plus
   * long pour une photo en base64 sur une 3G d'agence.
   */
  timeoutMs?: number;
  /**
   * L'en-tête `Idempotency-Key` — audit du 03/10, C2 : la MÊME clé au premier
   * envoi et à chaque rejeu de la file, pour qu'une création ne se fasse qu'une fois.
   */
  idempotencyKey?: string;
  /** En-têtes de plus, propres à un appel (lot W2, D-239 : `x-bulk-jobs`). */
  headers?: Record<string, string>;
}

/** Une photo de preuve, un chèque, une pièce jointe : la 3G d'agence est lente. */
export const UPLOAD_TIMEOUT_MS = 60_000;

/** La promesse de renouvellement en cours, partagée par tous les appelants. */
let refreshing: Promise<RefreshOutcome> | null = null;

/** Prévenu quand la session tombe : la racine redirige alors vers la connexion. */
type SessionListener = () => void;
const listeners = new Set<SessionListener>();

export function onSessionLost(listener: SessionListener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function announceSessionLost(): void {
  for (const listener of listeners) listener();
}

async function baseUrl(): Promise<string> {
  const slug = await readSlug();
  if (!slug) throw new ApiError({ status: 0, message: 'Aucune société choisie.' });

  return apiBaseUrl(slug);
}

/**
 * L'issue d'un renouvellement — C15 bloc 1.
 *
 * **Trois issues, et c'est tout l'enjeu.** Jusqu'ici il n'y en avait que deux :
 * un couple, ou `null`. Un `null` effaçait le coffre — donc un livreur qui
 * scannait dans un sous-sol d'agence, où le `fetch` ne part même pas, voyait
 * « Session expirée » avec une session parfaitement valide en poche.
 *
 * Un REFUS et une PANNE ne se ressemblent pas : le premier est définitif et
 * demande de se reconnecter, la seconde demande d'attendre. Les confondre,
 * c'est déconnecter quelqu'un pour un ascenseur.
 */
type RefreshOutcome =
  | { kind: 'renewed'; tokens: Tokens }
  /** Le serveur a dit non : jeton mort, tourné, révoqué, ou compte fermé. */
  | { kind: 'refused' }
  /** Personne n'a répondu, ou le serveur est cassé. La session reste ouverte. */
  | { kind: 'unreachable' };

/**
 * Renouvelle le couple, une seule fois à la fois.
 *
 * La promesse en cours est partagée : un écran de tournée lance cinq requêtes
 * d'un coup, et cinq renouvellements en parallèle présenteraient quatre fois un
 * refresh token déjà tourné — donc quatre refus.
 */
async function refreshTokens(current: Tokens): Promise<RefreshOutcome> {
  /*
   * L'échéance est connue et dépassée : inutile d'aller demander. On coupe
   * court, ce qui évite un aller-retour sur une 3G d'agence pour une réponse
   * qu'on connaît déjà.
   */
  if (isRefreshExpired(current)) {
    await clearTokens();
    announceSessionLost();
    return { kind: 'refused' };
  }

  refreshing ??= (async () => {
    try {
      const response = await fetch(`${await baseUrl()}/auth/refresh`, {
        method: 'POST',
        headers: { 'content-type': 'application/json', 'x-client': 'mobile' },
        body: JSON.stringify({ refreshToken: current.refreshToken }),
      });

      /*
       * `401` et `403` : le serveur REFUSE, la session est finie. Tout autre
       * code — `500`, `502`, `503` — est une panne : l'API redémarre, le proxy
       * ne trouve pas son amont. Effacer le coffre pour ça, ce serait
       * reconnecter tous les livreurs à chaque déploiement.
       */
      if (response.status === 401 || response.status === 403) {
        await clearTokens();
        announceSessionLost();
        return { kind: 'refused' } as const;
      }

      if (!response.ok) return { kind: 'unreachable' } as const;

      const session = (await response.json()) as {
        accessToken: string;
        refreshToken: string;
        expiresIn: number;
        refreshExpiresIn?: number;
      };

      await saveTokens(session);
      const tokens = await readTokens();

      return tokens ? ({ kind: 'renewed', tokens } as const) : ({ kind: 'unreachable' } as const);
    } catch {
      // Le `fetch` n'est jamais parti : c'est le réseau, pas le serveur. Le
      // coffre est intact, on réessaiera au prochain appel.
      return { kind: 'unreachable' } as const;
    } finally {
      // Libéré dans le `finally` : sans lui, un échec réseau laisserait la
      // promesse en place et TOUS les appels suivants attendraient un
      // renouvellement qui n'aura jamais lieu.
      refreshing = null;
    }
  })();

  return refreshing;
}

async function authorization(anonymous: boolean): Promise<string | null> {
  if (anonymous) return null;

  const tokens = await readTokens();
  if (!tokens) return null;
  if (!isExpired(tokens)) return `Bearer ${tokens.accessToken}`;

  const outcome = await refreshTokens(tokens);
  if (outcome.kind === 'renewed') return `Bearer ${outcome.tokens.accessToken}`;

  /*
   * Renouvellement INJOIGNABLE : on lève une erreur hors-ligne plutôt que de
   * partir sans jeton. Partir sans jeton rendrait un `401`, que l'écran lit
   * comme « session expirée » — alors que c'est le réseau qui manque, et que
   * la file hors-ligne, elle, sait garder le geste (`ApiError.isOffline`).
   */
  if (outcome.kind === 'unreachable') {
    throw new ApiError({ status: 0, message: 'Réseau indisponible.' });
  }

  return null;
}

/**
 * Un appel à l'API. Toutes les requêtes de l'application passent par là.
 *
 * Le 401 est retenté UNE fois après renouvellement : le jeton a pu expirer
 * entre la vérification locale et l'arrivée de la requête, et faire retomber
 * l'utilisateur sur l'écran de connexion pour une seconde de décalage serait
 * insupportable au bout de la dixième fois.
 */
export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const {
    method = 'GET',
    body,
    anonymous = false,
    signal,
    timeoutMs = ACTION_TIMEOUT_MS,
    idempotencyKey,
    headers: extraHeaders,
  } = options;

  const send = async (auth: string | null): Promise<Response> => {
    /*
     * LE DÉLAI — Recette 5, point 4 a. Un `fetch` sans délai attend une réponse
     * qui ne viendra jamais sur une liaison 3G figée : le bouton tournait sans
     * fin, et l'écran restait « bloqué ». Passé le délai, l'envoi est abandonné
     * et rendu en `408` : la file hors-ligne sait déjà garder ce geste (C15).
     */
    const controller = new AbortController();
    let timedOut = false;
    const timer = setTimeout(() => {
      timedOut = true;
      controller.abort();
    }, timeoutMs);
    const forward = () => controller.abort();
    signal?.addEventListener('abort', forward);

    const headers: Record<string, string> = {
      accept: 'application/json',
      // L'en-tête qui fait partir les jetons dans le corps (D-069).
      'x-client': 'mobile',
    };

    if (body !== undefined) headers['content-type'] = 'application/json';
    if (auth) headers.authorization = auth;
    if (idempotencyKey) headers['idempotency-key'] = idempotencyKey;
    if (extraHeaders) Object.assign(headers, extraHeaders);

    try {
      return await fetch(`${await baseUrl()}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: controller.signal,
      });
    } catch (error) {
      if (timedOut) {
        throw new ApiError({
          status: 408,
          reason: 'timeout',
          message: `Pas de réponse du serveur après ${Math.round(timeoutMs / 1000)} s.`,
        });
      }
      /*
       * `status: 0` veut dire « pas de réponse », et c'est ce que la file
       * hors-ligne attend pour savoir qu'elle doit garder le geste. Une erreur
       * de réseau et un 500 ne se traitent pas pareil : le premier se rejoue,
       * le second se signale.
       */
      throw new ApiError({
        status: 0,
        message: error instanceof Error ? error.message : 'Réseau indisponible.',
      });
    } finally {
      clearTimeout(timer);
      signal?.removeEventListener('abort', forward);
    }
  };

  let response = await send(await authorization(anonymous));

  if (response.status === 401 && !anonymous) {
    const tokens = await readTokens();
    const outcome = tokens ? await refreshTokens(tokens) : ({ kind: 'refused' } as const);

    /*
     * Le renouvellement n'a pas abouti FAUTE DE RÉSEAU : c'est une erreur
     * hors-ligne, pas une fin de session. Le scan qui vient d'échouer part en
     * file et sera rejoué — `refreshTokens` a laissé le coffre intact.
     */
    if (outcome.kind === 'unreachable') {
      throw new ApiError({ status: 0, message: 'Réseau indisponible.' });
    }

    if (outcome.kind === 'refused') {
      /*
       * `refreshTokens` a déjà vidé le coffre et prévenu la racine — sauf
       * quand il n'y avait plus de jeton du tout, auquel cas la racine a déjà
       * redirigé. Ici on ne fait que rendre la main à l'appelant.
       */
      if (!tokens) announceSessionLost();
      throw new ApiError({ status: 401, message: 'Session expirée.' });
    }

    response = await send(`Bearer ${outcome.tokens.accessToken}`);
  }

  if (!response.ok) throw new ApiError(await toFailure(response));

  // 204 : une suppression ne rend rien, et `json()` lèverait sur un corps vide.
  if (response.status === 204) return undefined as T;

  return (await response.json()) as T;
}

async function toFailure(response: Response): Promise<ApiFailure> {
  const requestId = response.headers.get('x-request-id') ?? undefined;

  try {
    const payload = (await response.json()) as {
      message?: string;
      details?: Record<string, unknown>;
    };

    return {
      status: response.status,
      message: payload.message ?? 'Une erreur est survenue.',
      reason: typeof payload.details?.reason === 'string' ? payload.details.reason : undefined,
      details: payload.details,
      requestId,
    };
  } catch {
    // Une réponse sans JSON — un 502 du proxy, par exemple. Le code suffit à
    // distinguer « refusé » de « cassé ».
    return { status: response.status, message: `Erreur ${response.status}.`, requestId };
  }
}
