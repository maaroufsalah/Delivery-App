/**
 * Client HTTP unique de l'application.
 *
 * Toutes les fonctions de `src/lib/api/*` passent par ici, et par ici seulement.
 * Il porte trois responsabilités :
 *
 *  1. **Les cookies.** Les jetons vivent dans des cookies `httpOnly` posés par
 *     l'API — inaccessibles au JavaScript, donc hors de portée d'un XSS. Un
 *     composant serveur n'a pas de cookies attachés d'office : ils sont
 *     transférés explicitement depuis `next/headers`.
 *
 *  2. **Le renouvellement.** Il est fait en amont, dans `proxy.ts` : c'est le
 *     seul endroit où Next autorise l'écriture d'un cookie avant le rendu. Un
 *     401 qui arrive quand même signifie que la session est bel et bien
 *     terminée, et il remonte tel quel.
 *
 *  3. **Le typage des erreurs.** L'API répond toujours `{ code, message,
 *     details }` ; `ApiError` en est la projection, et `code` est ce sur quoi
 *     l'appelant s'accroche — jamais le message, qui est destiné à l'humain.
 *
 * `next/headers` est importé DYNAMIQUEMENT, et seulement par `apiFetch` : le
 * module reste ainsi chargeable depuis la configuration next-intl, qui est
 * évaluée hors du contexte App Router. Les écrans interactifs, eux, passent par
 * des Server Actions — aucun composant client n'importe ce fichier.
 */

import { apiErrorDigest } from '@delivery/shared';

/** URL vue par le serveur. En déploiement dédié, les deux se confondent. */
const BASE_URL = (
  process.env.API_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  'http://localhost:3001/api'
).replace(/\/$/, '');

export type ApiErrorCode =
  | 'UNAUTHENTICATED'
  | 'FORBIDDEN'
  | 'NOT_FOUND'
  | 'CONFLICT'
  | 'VALIDATION_FAILED'
  | 'TRANSITION_FORBIDDEN'
  | 'INTERNAL'
  | 'NETWORK';

/** Le `digest` que pose `forbidden()` de `next/navigation` (`experimental.authInterrupts`). */
const FORBIDDEN_DIGEST = 'NEXT_HTTP_ERROR_FALLBACK;403';

export class ApiError extends Error {
  /**
   * Le SEUL champ qui traverse la frontière serveur — C15 bloc 3.
   *
   * Next remplace le message d'une erreur serveur par un identifiant avant de
   * l'envoyer au navigateur : sans cela, `error.tsx` ne saurait pas distinguer
   * une session expirée d'une base en panne, et afficherait « Une erreur est
   * survenue » pour les deux. Next respecte le `digest` qu'on pose soi-même —
   * c'est ainsi que `redirect()` fonctionne.
   */
  readonly digest: string;

  constructor(
    message: string,
    readonly status: number,
    readonly code: ApiErrorCode = 'INTERNAL',
    readonly details?: unknown,
    /** L'identifiant que l'API a renvoyé en `x-request-id` — recette 4, point 5 c. */
    readonly requestId: string | null = null,
  ) {
    super(message);
    this.name = 'ApiError';
    this.digest = apiErrorDigest(status, requestId);

    /*
     * UN REFUS DE L'API REND UN 403, PAS UN 500 — lot W1 (E2E-009), D-236.
     *
     * Un 403 non rattrapé pendant le rendu d'une page (lien direct, marque-page)
     * remontait à `error.tsx` : « Accès refusé » juste, mais servi en HTTP 500,
     * hors de la coque, avec une erreur React en console. Le `digest` de
     * `forbidden()` le fait traiter par Next comme un refus : `forbidden.tsx`,
     * dans la coque, en HTTP 403. Le code qui RATTRAPE un `ApiError` (statut
     * 403 → 404 d'une fiche d'un autre compte, `toFailure` des actions) n'en
     * voit aucune différence : la classe et le statut sont inchangés.
     */
    if (status === 403) this.digest = FORBIDDEN_DIGEST;
  }

  get isUnauthenticated(): boolean {
    return this.status === 401;
  }
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PATCH' | 'PUT' | 'DELETE';
  body?: unknown;
  query?: Record<string, string | number | boolean | undefined | null>;
  /**
   * Durée de cache Next, en secondes. `0` (défaut) : jamais de cache — une
   * liste de colis vue par deux agents d'agences différentes n'est pas la
   * même réponse, la mettre en cache partagé serait une fuite.
   */
  revalidate?: number;
  /** En-têtes de plus, propres à un appel (lot W2 : `x-bulk-jobs`). */
  headers?: Record<string, string>;
}

function buildUrl(path: string, query?: RequestOptions['query']): string {
  const url = new URL(`${BASE_URL}${path.startsWith('/') ? path : `/${path}`}`);

  for (const [key, value] of Object.entries(query ?? {})) {
    if (value === undefined || value === null || value === '') continue;
    url.searchParams.set(key, String(value));
  }

  return url.toString();
}

/**
 * L'hôte VU PAR LE VISITEUR, transmis à l'API — C8 bloc 5, D-049.
 *
 * C'est lui qui désigne le tenant sur les routes publiques : `wimo.…` sert
 * Wimo, `speedy.…` sert SpeedyTrans. Or un appel serveur-à-serveur porte
 * l'hôte de l'API (`localhost:3001`), pas celui du navigateur — sans ce
 * transfert, la landing et le suivi public ne résoudraient AUCUN tenant.
 *
 * On le transmet en `x-forwarded-host`, l'en-tête standard, celui que le proxy
 * pose déjà. **L'API ne doit donc jamais être exposée directement** : nginx
 * écrase cet en-tête à l'entrée, ce qui ferme la seule façon de le forger
 * (procédure dans docs/DEPLOY.md).
 *
 * L'absence n'est pas une erreur : au build, ou hors contexte de requête, il
 * n'y a pas d'hôte. L'API refusera alors proprement, plutôt que de deviner.
 */
async function forwardedHost(): Promise<string | undefined> {
  try {
    const { headers } = await import('next/headers');
    const store = await headers();

    return store.get('x-forwarded-host') ?? store.get('host') ?? undefined;
  } catch {
    return undefined;
  }
}

async function parseError(response: Response): Promise<ApiError> {
  let code: ApiErrorCode = 'INTERNAL';
  let message = `Erreur ${response.status}`;
  let details: unknown;

  try {
    const body = (await response.json()) as {
      code?: ApiErrorCode;
      message?: string;
      details?: unknown;
    };
    if (body.code) code = body.code;
    if (body.message) message = body.message;
    details = body.details;
  } catch {
    // Réponse non JSON : on garde le message par défaut.
  }

  return new ApiError(
    message,
    response.status,
    code,
    details,
    response.headers.get(REQUEST_ID_HEADER),
  );
}

/**
 * L'IDENTIFIANT DE LA REQUÊTE EN COURS — recette SpeedyTrans 4, point 5 c.
 *
 * `proxy.ts` en pose un sur chaque requête de page ou d'action ; il part vers
 * l'API avec chaque appel, qui le reprend, l'écrit dans ses journaux et le
 * renvoie. Les écritures d'un même geste se lisent alors ensemble, et le
 * client qui lit « Référence : … » sur une page d'erreur nous donne la clé.
 */
const REQUEST_ID_HEADER = 'x-request-id';

async function currentRequestId(): Promise<string | undefined> {
  try {
    const { headers } = await import('next/headers');
    return (await headers()).get(REQUEST_ID_HEADER) ?? undefined;
  } catch {
    return undefined;
  }
}

/**
 * L'ADRESSE DU NAVIGATEUR, relayée à l'API — recette SpeedyTrans 4, point 4.
 *
 * Même relais que les actions de connexion (D-130) : sans lui, chaque geste
 * fait depuis le web portait au journal l'adresse du CONTENEUR web
 * (`::ffff:172.25.0.4`). On transmet l'en-tête tel que nginx l'a construit ;
 * l'API ne fait confiance qu'aux sauts privés et retient la dernière adresse
 * publique — celle ajoutée par nginx, qu'un client ne peut pas forger.
 */
async function forwardedFor(): Promise<string | undefined> {
  try {
    const { headers } = await import('next/headers');
    return (await headers()).get('x-forwarded-for') ?? undefined;
  } catch {
    return undefined;
  }
}

/** Appel authentifié. Les cookies de la requête courante sont transférés. */
export async function apiFetch<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { cookies } = await import('next/headers');
  const store = await cookies();
  const cookieHeader = store
    .getAll()
    .map((cookie) => `${cookie.name}=${encodeURIComponent(cookie.value)}`)
    .join('; ');

  return request<T>(path, options, cookieHeader);
}

/**
 * Appel authentifié rendant du BINAIRE, encodé en base64.
 *
 * Un export `.xlsx` ne passe pas par `apiFetch`, qui parse du JSON. Le base64
 * traverse ensuite une Server Action jusqu'au navigateur, qui en fait un Blob :
 * aucun composant client n'a besoin d'un jeton, et il n'y a pas de route de
 * proxy à maintenir (D-018).
 */
export async function apiFetchBase64(
  path: string,
  options: RequestOptions = {},
  /** Lit les en-têtes de la réponse — le nombre de lignes d'un export (Recette 5, point 8). */
  onHeaders?: (headers: Headers) => void,
): Promise<string> {
  const { cookies } = await import('next/headers');
  const store = await cookies();
  const cookieHeader = store
    .getAll()
    .map((cookie) => `${cookie.name}=${encodeURIComponent(cookie.value)}`)
    .join('; ');

  const headers: Record<string, string> = { accept: '*/*' };
  if (cookieHeader) headers.cookie = cookieHeader;

  const host = await forwardedHost();
  if (host) headers['x-forwarded-host'] = host;
  const id = await currentRequestId();
  if (id) headers[REQUEST_ID_HEADER] = id;
  const client = await forwardedFor();
  if (client) headers['x-forwarded-for'] = client;
  // D-176 : un document peut se demander par un CORPS (cinq mille codes).
  if (options.body !== undefined) headers['content-type'] = 'application/json';

  let response: Response;

  try {
    response = await fetch(buildUrl(path, options.query), {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      cache: 'no-store',
    });
  } catch (error) {
    throw new ApiError(
      `API injoignable : ${error instanceof Error ? error.message : 'erreur réseau'}`,
      0,
      'NETWORK',
    );
  }

  if (!response.ok) throw await parseError(response);

  onHeaders?.(response.headers);
  return Buffer.from(await response.arrayBuffer()).toString('base64');
}

/** Appel non authentifié : suivi public, grille tarifaire, landing. */
export async function publicFetch<T>(path: string, options: RequestOptions = {}): Promise<T> {
  return request<T>(path, options, undefined);
}

/**
 * Un fichier PUBLIC de l'API, en octets — dernières corrections, point 5.
 *
 * Sert au favicon généré, qui pose le logo téléversé (`/api/public/logo/…`)
 * sur son fond. Même hôte transmis que `publicFetch` : c'est lui qui désigne le
 * tenant, et donc SON logo.
 */
export async function publicFetchBytes(
  path: string,
): Promise<{ bytes: ArrayBuffer; type: string }> {
  const headers: Record<string, string> = {};
  const host = await forwardedHost();
  if (host) headers['x-forwarded-host'] = host;

  const response = await fetch(buildUrl(path), { headers, next: { revalidate: 60 } });
  if (!response.ok) throw await parseError(response);

  return {
    bytes: await response.arrayBuffer(),
    type: response.headers.get('content-type') ?? 'image/png',
  };
}

async function request<T>(
  path: string,
  options: RequestOptions,
  cookieHeader: string | undefined,
): Promise<T> {
  const headers: Record<string, string> = { accept: 'application/json' };
  if (cookieHeader) headers.cookie = cookieHeader;
  if (options.body !== undefined) headers['content-type'] = 'application/json';
  // Lot W2 (D-239) : `x-bulk-jobs: 1` — ce client sait suivre une tâche de masse (202).
  Object.assign(headers, options.headers);

  const host = await forwardedHost();
  if (host) headers['x-forwarded-host'] = host;
  /*
   * PAS d'identifiant sur une lecture MISE EN CACHE — recette 4, point 5 c.
   *
   * Les en-têtes entrent dans la clé du cache de données de Next : un
   * identifiant neuf à chaque requête la rendait unique, et `/public/company`
   * (60 s de cache) repartait à CHAQUE rendu. Déployé tel quel, il a rempli en
   * quelques minutes le débit public de la société (60 par minute, partagé par
   * tout le web) et rendu 429. Une lecture publique en cache n'a rien à
   * corréler de toute façon.
   */
  const id = options.revalidate ? undefined : await currentRequestId();
  if (id) headers[REQUEST_ID_HEADER] = id;
  // L'adresse du navigateur (D-130) — jamais sur une lecture en cache, même raison.
  const client = options.revalidate ? undefined : await forwardedFor();
  if (client) headers['x-forwarded-for'] = client;

  let response: Response;

  try {
    response = await fetch(buildUrl(path, options.query), {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      // `no-store` par défaut : cf. `revalidate` ci-dessus.
      cache: options.revalidate ? undefined : 'no-store',
      next: options.revalidate ? { revalidate: options.revalidate } : undefined,
    });
  } catch (error) {
    throw new ApiError(
      `API injoignable : ${error instanceof Error ? error.message : 'erreur réseau'}`,
      0,
      'NETWORK',
    );
  }

  if (!response.ok) throw await parseError(response);
  if (response.status === 204) return undefined as T;

  return (await response.json()) as T;
}
