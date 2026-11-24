import { backOfficeRedirect, requiresSession, withoutLocale } from '@delivery/shared';
import createMiddleware from 'next-intl/middleware';
import type { NextRequest } from 'next/server';
import { NextResponse } from 'next/server';
import { LOCALE_COOKIE, routing } from './i18n/routing';
import { slugFromHost } from './lib/platform-domain';

/**
 * Next 16 : `middleware.ts` est devenu `proxy.ts`, runtime nodejs imposé
 * (l'edge n'y est pas supporté).
 *
 * Cinq responsabilités :
 *  1. rendre au visiteur la langue qu'il a CHOISIE, s'il en a choisi une ;
 *  2. résoudre la locale et préfixer l'URL (next-intl) ;
 *  3. renouveler la session quand l'access token a expiré, et **poser le jeton
 *     neuf sur la REQUÊTE transmise au rendu** autant que sur la réponse ;
 *  4. décider SEUL des renvois vers `/login` — les pages ne le font plus ;
 *  5. renvoyer chez lui un compte qui ouvre une route qui n'est pas la sienne
 *     (B-20, C14 bloc 2).
 *
 * **Le renouvellement est ICI et nulle part ailleurs**, parce que c'est le seul
 * point du cycle de vie Next où un cookie peut être écrit avant le rendu. Un
 * composant serveur ne le peut pas : le nouveau jeton serait obtenu, jamais
 * posé.
 *
 * **Et il doit être écrit DEUX FOIS — C17, la troisième tentative.** Poser le
 * `Set-Cookie` sur la réponse ne sert qu'au PROCHAIN aller-retour : le rendu de
 * la requête en cours, lui, lit les cookies ENTRANTS. Un jeton renouvelé mais
 * non réinjecté laissait donc le rendu partir avec l'ancien — cinq appels API,
 * cinq `401`, et la page renvoyait vers la connexion alors que la session
 * venait d'être renouvelée. Voir `attachRenewedCookies()`.
 */
const intl = createMiddleware(routing);

/** L'URL porte-t-elle déjà `/fr`, `/ar` ou `/en` ? */
function hasLocalePrefix(pathname: string): boolean {
  return routing.locales.some(
    (locale) => pathname === `/${locale}` || pathname.startsWith(`/${locale}/`),
  );
}

/**
 * La langue CHOISIE par ce visiteur, ou `null` — C9 bloc 1.
 *
 * `localeDetection: false` empêche next-intl de lire `Accept-Language`, ce qui
 * est le but : la langue du navigateur ne décide plus. Mais le même drapeau lui
 * fait aussi ignorer son propre cookie, et celui-là porte un choix EXPLICITE,
 * fait dans Paramètres > Apparence. L'ignorer renverrait un utilisateur
 * arabophone au français à chaque fois qu'il ouvre la racine du site.
 *
 * Le cookie continue d'être ÉCRIT par next-intl (`syncCookie` ne regarde que
 * `localeCookie`) : il n'y a donc rien à poser ici, seulement à relire.
 */
function chosenLocale(request: NextRequest): string | null {
  const value = request.cookies.get(LOCALE_COOKIE)?.value;

  return value && routing.locales.includes(value as (typeof routing.locales)[number])
    ? value
    : null;
}

const ACCESS_COOKIE = 'dp.at';
const REFRESH_COOKIE = 'dp.rt';

/**
 * Ce que le proxy lit dans l'access token, SANS vérifier la signature.
 *
 * **Et c'est assumé, parce que ce que ça décide n'est pas un droit.** Le proxy
 * s'en sert pour choisir une REDIRECTION, c'est-à-dire du confort. Ce qui garde
 * réellement la donnée est ailleurs et n'a pas bougé : l'API refuse par
 * permission et par portée (D-082), et elle refuse un jeton d'un autre espace
 * par `TenantInterceptor`.
 *
 * Un jeton forgé ne gagne donc rien — au mieux il évite une redirection, et se
 * heurte ensuite à une API qui ne lui répond pas. Vérifier la signature ici
 * demanderait le secret d'accès dans le processus web, ce qui coûterait bien
 * plus que ça ne rapporterait.
 */
interface Claims {
  clientId: string | null;
  courierId: string | null;
  /** `cid` — la société du compte. `null` pour un administrateur de plateforme. */
  companyId: string | null;
  /** `exp`, en secondes. `null` quand le jeton n'en porte pas. */
  expiresAt: number | null;
}

/**
 * La charge utile du jeton, ou `null` s'il est illisible.
 *
 * Aucune dépendance : un JWT est trois segments base64url, et seul celui du
 * milieu nous intéresse.
 */
function claimsOf(token: string | undefined): Claims | null {
  if (!token) return null;

  try {
    const payload = token.split('.')[1];
    if (!payload) return null;

    const json = Buffer.from(payload.replace(/-/g, '+').replace(/_/g, '/'), 'base64').toString(
      'utf8',
    );
    const claims = JSON.parse(json) as {
      clid?: string | null;
      crid?: string | null;
      cid?: string | null;
      exp?: number;
    };

    return {
      clientId: claims.clid ?? null,
      courierId: claims.crid ?? null,
      companyId: claims.cid ?? null,
      expiresAt: typeof claims.exp === 'number' ? claims.exp : null,
    };
  } catch {
    // Jeton illisible : on ne redirige pas. Le layout et l'API décideront.
    return null;
  }
}

/**
 * Ce jeton vaut-il encore la peine d'être présenté ? — C17.
 *
 * Le cookie d'accès meurt normalement une minute AVANT son jeton (C15 bloc 1),
 * donc cette question ne devrait jamais se poser. Elle se pose quand même : une
 * société qui raccourcit `session.accessMinutes` (D-019) laisse derrière elle
 * des cookies plus longs que leurs jetons, et l'horloge d'un poste peut dériver.
 *
 * Un jeton expiré est alors traité comme ABSENT : on renouvelle, au lieu de
 * laisser le rendu partir avec lui et récolter cinq `401`.
 */
function isUsable(claims: Claims | null): claims is Claims {
  if (!claims) return false;
  if (claims.expiresAt === null) return true;

  return claims.expiresAt * 1000 > Date.now();
}

const API_URL = (
  process.env.API_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  'http://localhost:3001/api'
).replace(/\/$/, '');

export default async function proxy(request: NextRequest) {
  /*
   * Une URL sans préfixe de langue + un choix en cookie = on honore le choix.
   * Sans cette redirection, next-intl appliquerait `defaultLocale` et le
   * français écraserait une préférence explicite.
   */
  const gone = await unknownSpaceResponse(request);
  if (gone) return gone;

  const { pathname } = request.nextUrl;

  if (!hasLocalePrefix(pathname)) {
    const chosen = chosenLocale(request);

    if (chosen !== null && chosen !== routing.defaultLocale) {
      const target = request.nextUrl.clone();
      target.pathname = `/${chosen}${pathname === '/' ? '' : pathname}`;
      return NextResponse.redirect(target);
    }
  }

  /*
   * LA SESSION SE RÈGLE AVANT LE RENDU, PAS PENDANT — C17.
   *
   * L'ordre compte et il a changé : `resolveSession` peut RÉÉCRIRE les cookies
   * de `request`, et `intl(request)` recopie ces en-têtes-là dans la requête
   * qu'il transmet au rendu (`NextResponse.next({ request: { headers } })`).
   * Appeler `intl` d'abord, comme avant, figeait les anciens cookies.
   */
  const session = await resolveSession(request);
  if (session.redirect) return session.redirect;

  /*
   * L'IDENTIFIANT DE LA REQUÊTE — recette SpeedyTrans 4, point 5 c.
   *
   * Posé ICI, sur chaque page et chaque Server Action, et TOUJOURS neuf : celui
   * qu'un navigateur enverrait est écrasé, sinon il pourrait fondre ses gestes
   * dans la ligne de journal d'un autre. next-intl recopie les en-têtes de la
   * requête dans le rendu ; `lib/http.ts` le transmet à l'API, qui le reprend.
   */
  request.headers.set('x-request-id', crypto.randomUUID());

  const response = intl(request);

  for (const cookie of session.setCookies) {
    response.headers.append('set-cookie', cookie);
  }

  if (session.cleared) {
    response.cookies.delete(ACCESS_COOKIE);
    response.cookies.delete(REFRESH_COOKIE);
  }

  /*
   * LA ROUTE EST-ELLE LA SIENNE ? — B-20, C14 bloc 2.
   *
   * Ici et non dans un layout, contrairement à la règle de D-091, pour une
   * raison de forme : un layout ne reçoit pas le chemin. Il ne se re-rend même
   * pas d'une navigation à l'autre — c'est tout son intérêt — et Next n'expose
   * donc aucune façon stable de lui dire « quelle page ». Le proxy, lui, voit
   * chaque requête avec son URL.
   *
   * La règle elle-même reste PARTAGÉE et testée (`backOfficeRedirect`), comme
   * `landingPathFor` : ce fichier ne décide de rien, il applique.
   *
   * `roles: []` n'est pas un oubli — la règle ne lit que le RATTACHEMENT, parce
   * que c'est lui qui décide de l'espace et non l'étiquette (D-072).
   *
   * **Le jeton lu est celui d'APRÈS renouvellement** : `request.cookies` a été
   * réécrit au-dessus. Le lire avant renvoyait un compte sur son atterrissage
   * avec un jeton périmé, et perdait au passage le `Set-Cookie` tout neuf.
   */
  const claims = claimsOf(request.cookies.get(ACCESS_COOKIE)?.value);

  if (claims) {
    const path = withoutLocale(request.nextUrl.pathname, routing.locales);
    const target = backOfficeRedirect(path, { ...claims, roles: [] });

    if (target) {
      const url = request.nextUrl.clone();
      url.pathname = `${localePrefix(request)}${target}`;
      url.search = '';

      const redirect = NextResponse.redirect(url);

      /*
       * Les cookies renouvelés SUIVENT la redirection. Sans cette recopie, un
       * livreur qui ouvre `/fr/parcels` au moment où sa session se renouvelle
       * partirait vers `/fr/me` en ayant perdu son jeton neuf — et paierait un
       * second renouvellement.
       */
      for (const cookie of session.setCookies) {
        redirect.headers.append('set-cookie', cookie);
      }

      return redirect;
    }
  }

  return response;
}

/* -------------------------------------------------------------------------- */
/*  La session                                                                 */
/* -------------------------------------------------------------------------- */

/** Ce que `resolveSession` a décidé, et ce qu'il reste à poser sur la réponse. */
interface SessionDecision {
  /** Réponse TERMINALE — `/login`. `null` : la navigation continue. */
  redirect: NextResponse | null;
  /** Les `Set-Cookie` de l'API, à recopier tels quels sur la réponse finale. */
  setCookies: string[];
  /** Les deux cookies de session sont morts : les effacer sur la réponse. */
  cleared: boolean;
}

const CONTINUE: SessionDecision = { redirect: null, setCookies: [], cleared: false };

/**
 * Décide de la session, et RÉÉCRIT `request.cookies` en conséquence — C17.
 *
 * Quatre issues, et c'est ici que toutes les décisions de session se prennent :
 * les pages n'en prennent plus aucune (voir `requireSession()` côté web).
 *
 *  1. jeton d'accès valable et du bon espace → on ne touche à rien ;
 *  2. jeton d'un AUTRE espace → cookies effacés, `/login` avec le message ;
 *  3. plus de jeton d'accès mais un rafraîchissement → renouvellement ;
 *  4. plus rien du tout → `/login` si la page exige une session.
 */
async function resolveSession(request: NextRequest): Promise<SessionDecision> {
  const access = request.cookies.get(ACCESS_COOKIE)?.value;
  const claims = claimsOf(access);
  const refreshToken = request.cookies.get(REFRESH_COOKIE)?.value;

  if (isUsable(claims)) {
    /*
     * LE JETON D'UN AUTRE ESPACE — C17, le second écart.
     *
     * Les cookies portent le domaine de la plateforme : un `dp.at` obtenu sur
     * `wimo.…` est donc ENVOYÉ à `speedy.…`. L'API le refuse — `403`,
     * `reason: tenant-mismatch` — mais ce refus tombait au milieu du rendu
     * d'un layout, là où la frontière d'erreur ne peut plus rien pour lui :
     * `/fr/dashboard` rendait `500`.
     *
     * Le proxy tranche donc AVANT le rendu. C'est un refus de session, il se
     * traite comme un `401` : les deux cookies partent, et l'utilisateur
     * arrive sur la connexion avec la phrase qui explique pourquoi.
     */
    if (await belongsElsewhere(request, claims)) {
      return forget(request, loginRedirect(request, 'other-space'));
    }

    return CONTINUE;
  }

  if (!refreshToken) {
    /*
     * PLUS AUCUNE SESSION — et c'est le proxy qui le dit, plus la page.
     *
     * Avant C17, les trois coques appelaient `redirect('/login')` depuis le
     * serveur. Ce `307`-là remplace la réponse du proxy, `Set-Cookie` compris :
     * une session renouvelée à l'instant même se perdait dans le renvoi. Ici,
     * rien n'a été renouvelé — mais la règle doit vivre à un seul endroit pour
     * qu'on cesse de se poser la question.
     */
    return forget(request, loginRedirect(request));
  }

  const outcome = await renewSession(request, refreshToken);

  switch (outcome.kind) {
    case 'renewed':
      /*
       * LE CŒUR DE C17. Les cookies neufs sont posés sur la REQUÊTE — donc lus
       * par `cookies()` dans chaque composant serveur, donc envoyés par
       * `lib/http.ts` à chacun de ses appels — ET rendus au navigateur.
       */
      attachRenewedCookies(request, outcome.cookies);

      return { redirect: null, setCookies: outcome.cookies, cleared: false };

    /*
     * PLUS JAMAIS « UNE ERREUR EST SURVENUE » POUR UNE SESSION FINIE —
     * C15 bloc 3.
     *
     * La session est bel et bien terminée : on renvoie vers la connexion TOUT
     * DE SUITE, en gardant le chemin. Les pages publiques, elles, passent — la
     * landing et le suivi de colis n'ont que faire d'une session.
     */
    case 'refused':
      return forget(request, loginRedirect(request));

    /*
     * UN 4xx QUI N'EST PAS UN REFUS EST UN DÉFAUT DE CHEZ NOUS — C16.
     *
     * `422`, `400`, `415` : le serveur a compris la requête et la rejette pour
     * sa FORME. Aucune patience ne l'arrangera, et « garder les cookies et
     * continuer » est exactement ce qui a caché le bug pendant un déploiement
     * entier. On le JOURNALISE — c'est la seule trace qu'un exploitant aura —
     * et on renvoie vers la connexion, qui est un état propre et terminal.
     */
    case 'rejected':
      console.error(
        `[proxy] /auth/refresh a rejeté la requête (${outcome.status}). ` +
          'Ce n’est ni un refus de session ni une panne : le contrat de la ' +
          'route a changé, ou le corps envoyé ne lui convient plus.',
      );

      return forget(request, loginRedirect(request));

    /*
     * PANNE — `5xx` ou API injoignable, après une seconde tentative.
     *
     * On ne touche À RIEN : les cookies restent, la navigation continue. Une
     * API qui redémarre dure trois secondes ; déconnecter pour ça, c'est
     * déconnecter tout le monde à chaque déploiement. Le prochain passage
     * retentera, et le bandeau « Réessayer » (C15 bloc 3) rend la panne lisible
     * sans perdre l'écran.
     */
    case 'unreachable':
      return CONTINUE;
  }
}

/**
 * Les cookies renouvelés, posés sur la REQUÊTE — C17, le correctif.
 *
 * `request.cookies.set()` réécrit l'en-tête `cookie` de la requête, et
 * next-intl le recopie dans `NextResponse.next({ request: { headers } })` : le
 * rendu voit alors le jeton neuf dès CETTE requête. Sans cela, `cookies()` —
 * donc `lib/http.ts`, donc les vingt appels d'un tableau de bord — repartait
 * avec l'ancien, récoltait autant de `401`, et la page renvoyait vers la
 * connexion une seconde après que la session eut été renouvelée.
 *
 * Seuls les deux cookies de session sont recopiés : le reste du `Set-Cookie`
 * (durée, `httpOnly`, `domain`) ne regarde que le navigateur, et l'API en reste
 * seule maîtresse.
 */
function attachRenewedCookies(request: NextRequest, setCookies: readonly string[]): void {
  for (const raw of setCookies) {
    const pair = raw.split(';')[0];
    if (!pair) continue;

    const separator = pair.indexOf('=');
    if (separator === -1) continue;

    const name = pair.slice(0, separator).trim();
    if (name !== ACCESS_COOKIE && name !== REFRESH_COOKIE) continue;

    const value = pair.slice(separator + 1).trim();
    if (value === '') continue;

    request.cookies.set(name, decodeURIComponent(value));
  }
}

/**
 * La session est finie : l'oublier des deux côtés.
 *
 * Sur la REQUÊTE, pour que le rendu ne parte pas avec un jeton mort — une page
 * publique servie à quelqu'un dont la session vient de tomber doit s'afficher,
 * pas récolter des `401`. Et sur la RÉPONSE, pour que le navigateur cesse de
 * le renvoyer.
 */
function forget(request: NextRequest, redirect: NextResponse | null): SessionDecision {
  request.cookies.delete(ACCESS_COOKIE);
  request.cookies.delete(REFRESH_COOKIE);

  return { redirect, setCookies: [], cleared: true };
}

/* -------------------------------------------------------------------------- */
/*  Le renouvellement                                                          */
/* -------------------------------------------------------------------------- */

/** Ce qu'un renouvellement peut donner — et rien d'autre. */
type Renewal =
  | { kind: 'renewed'; cookies: string[] }
  /** `401`/`403` : la session est FINIE. Expirée, tournée, révoquée. */
  | { kind: 'refused' }
  /** Autre `4xx` : la requête est mal formée. C'est notre défaut, pas le sien. */
  | { kind: 'rejected'; status: number }
  /** `5xx` ou pas de réponse : la session est intacte, le serveur non. */
  | { kind: 'unreachable' };

/**
 * Deux tentatives au plus, et seule une PANNE en vaut une seconde — C16.
 *
 * Un refus et un rejet sont des réponses définitives : les rejouer ne ferait
 * que doubler la latence de chaque navigation. Une panne, elle, est souvent
 * l'instant précis où l'API bascule sur ses images neuves ; un second essai la
 * traverse sans que personne ne s'en aperçoive.
 */
async function renewSession(request: NextRequest, refreshToken: string): Promise<Renewal> {
  let last: Renewal = { kind: 'unreachable' };

  for (let attempt = 0; attempt < 2; attempt += 1) {
    last = await attemptRenewal(request, refreshToken);
    if (last.kind !== 'unreachable') return last;
  }

  return last;
}

async function attemptRenewal(request: NextRequest, refreshToken: string): Promise<Renewal> {
  let refreshed: Response;

  try {
    refreshed = await fetch(`${API_URL}/auth/refresh`, {
      method: 'POST',
      headers: {
        /*
         * LE CORPS EST OBLIGATOIRE — C16.
         *
         * `/auth/refresh` est validée par un schéma zod qui attend un OBJET.
         * Sans `content-type: application/json`, Express ne parse rien,
         * `request.body` reste `undefined`, et la route rendait `422` — à
         * cookie strictement identique, `{}` rend `200`. Le corps est VIDE à
         * dessein : le refresh token voyage dans le cookie, comme pour tout
         * navigateur ; le champ `refreshToken` du schéma ne sert qu'au mobile,
         * qui n'a pas de cookie (D-069).
         */
        'content-type': 'application/json',
        cookie: `${REFRESH_COOKIE}=${encodeURIComponent(refreshToken)}`,
        ...hostHeader(request),
      },
      body: '{}',
      cache: 'no-store',
    });
  } catch {
    // Le `fetch` n'est jamais parti : c'est le réseau, pas une réponse.
    return { kind: 'unreachable' };
  }

  if (refreshed.ok) return { kind: 'renewed', cookies: refreshed.headers.getSetCookie() };

  if (refreshed.status === 401 || refreshed.status === 403) return { kind: 'refused' };
  if (refreshed.status < 500) return { kind: 'rejected', status: refreshed.status };

  return { kind: 'unreachable' };
}

/**
 * L'HÔTE DU VISITEUR EST OBLIGATOIRE — C15 bloc 1.
 *
 * Le proxy appelle l'API serveur à serveur : `Host` porte alors l'adresse
 * interne de l'API, qui n'est le domaine d'aucun tenant. Depuis C8, l'API
 * résout la société PAR L'HÔTE ; sans cet en-tête, une route publique
 * s'exécute hors contexte de tenant et la première requête Prisma lève.
 */
function hostHeader(request: NextRequest): Record<string, string> {
  const host = request.headers.get('x-forwarded-host') ?? request.headers.get('host');

  return host ? { 'x-forwarded-host': host } : {};
}

/* -------------------------------------------------------------------------- */
/*  L'espace du jeton                                                          */
/* -------------------------------------------------------------------------- */

/** Une minute : un slug ou un domaine propre ne change pas tous les jours. */
const TENANT_CACHE_TTL = 60_000;

/**
 * Ce que l'API dit d'un hôte : `companyId` quand il sert un espace, `null`
 * sinon, et `unknown: true` seulement quand elle a RÉPONDU « espace inconnu »
 * (404) — une API muette ne prouve rien, et ne doit rien fermer.
 */
const tenantByHost = new Map<string, { at: number; companyId: string | null; unknown: boolean }>();

/**
 * Ce jeton appartient-il à un AUTRE espace que l'hôte demandé ? — C17.
 *
 * La comparaison porte sur des identifiants, jamais sur des noms d'hôtes : un
 * tenant peut apporter son propre domaine (`suivi.wimo.ma`), et deviner la
 * société à partir du sous-domaine se tromperait sur ceux-là. Le jeton porte
 * `cid` ; l'hôte, lui, est résolu par l'API — c'est elle qui tient la table.
 *
 * **Une lecture par hôte et par minute**, mise en cache dans le processus.
 * `/public/company` est publique, sans session, et déjà appelée par le layout
 * racine sur toutes les routes : ce n'est pas un appel de plus dans la nature,
 * c'est le même, une fois par minute.
 *
 * `false` dès que quelque chose manque — jeton sans société (administrateur de
 * plateforme), hôte qui ne résout aucun tenant (le domaine nu), API muette.
 * **Le doute ne déconnecte pas** : l'API, elle, refusera de toute façon.
 */
async function belongsElsewhere(request: NextRequest, claims: Claims): Promise<boolean> {
  if (!claims.companyId) return false;

  const host = request.headers.get('x-forwarded-host') ?? request.headers.get('host');
  if (!host) return false;

  const { companyId } = await tenantOfHost(host, request);

  return companyId !== null && companyId !== claims.companyId;
}

async function tenantOfHost(
  host: string,
  request: NextRequest,
): Promise<{ companyId: string | null; unknown: boolean }> {
  const key = host.toLowerCase();
  const cached = tenantByHost.get(key);

  if (cached && Date.now() - cached.at < TENANT_CACHE_TTL) return cached;

  let companyId: string | null = null;
  let unknown = false;

  try {
    const response = await fetch(`${API_URL}/public/company`, {
      headers: { accept: 'application/json', ...hostHeader(request) },
      cache: 'no-store',
    });

    if (response.ok) {
      const body = (await response.json()) as { id?: string };
      companyId = body.id ?? null;
    } else if (response.status === 404) {
      unknown = true;
    }
  } catch {
    // API muette : on ne conclut rien, et surtout on ne déconnecte personne.
    return { companyId: null, unknown: false };
  }

  const entry = { at: Date.now(), companyId, unknown };
  tenantByHost.set(key, entry);

  return entry;
}

/**
 * UN SOUS-DOMAINE SANS ESPACE — chantier « Purge de speedy ».
 *
 * `wimo.delivery.nocido.com` a servi un tenant qui n'existe plus ; son DNS, son
 * vhost et son certificat, eux, existent encore. Sans ce garde, la landing
 * rendait un 500 : elle lit la grille tarifaire d'un espace que l'API ne
 * connaît pas.
 *
 * `UNKNOWN_TENANT_REDIRECT` (une origine, lue à l'exécution) renvoie le
 * visiteur vers l'espace qui l'a remplacé ; sans elle, un 404 net. Rien n'est
 * nommé ici : le code reste multi-tenant, la décision est dans la config.
 *
 * Seuls les sous-domaines de la PLATEFORME sont concernés — le domaine nu sert
 * la landing Nocido, et un domaine propre inconnu n'arrive pas jusqu'ici.
 */
async function unknownSpaceResponse(request: NextRequest): Promise<NextResponse | null> {
  const host = request.headers.get('x-forwarded-host') ?? request.headers.get('host');
  if (!slugFromHost(host)) return null;

  const { unknown } = await tenantOfHost(host!, request);
  if (!unknown) return null;

  const target = process.env.UNKNOWN_TENANT_REDIRECT?.trim();
  if (target) {
    try {
      return NextResponse.redirect(new URL('/', target), 307);
    } catch {
      // Origine mal saisie : le 404 vaut mieux qu'un 500.
    }
  }

  return new NextResponse('Espace inconnu — Unknown space — فضاء غير معروف\n', {
    status: 404,
    headers: { 'content-type': 'text/plain; charset=utf-8', 'cache-control': 'no-store' },
  });
}

/* -------------------------------------------------------------------------- */
/*  Le renvoi vers la connexion                                                */
/* -------------------------------------------------------------------------- */

/** Pourquoi la session s'est arrêtée, quand ce n'est pas une simple expiration. */
type LoginReason = 'other-space';

/**
 * Vers la connexion, en gardant la page demandée — C15 bloc 3, C17.
 *
 * `null` quand la page ne demande pas de session : la navigation continue alors
 * normalement. **C'est le SEUL endroit du web qui renvoie vers `/login`** — les
 * trois coques le faisaient elles-mêmes, et leur `307` écrasait la réponse du
 * proxy, `Set-Cookie` compris (C17).
 *
 * Le paramètre s'appelle `next` et porte le chemin AVEC son préfixe de langue :
 * c'est ce que l'écran de connexion poussera après coup, et un chemin sans
 * langue ferait repasser par la redirection de `proxy` — deux sauts au lieu
 * d'un. Il est ré-assaini à la lecture (`safeReturnPath`), parce que ce qui
 * arrive dans une URL n'a pas forcément été posé par nous.
 */
function loginRedirect(request: NextRequest, reason?: LoginReason): NextResponse | null {
  const path = withoutLocale(request.nextUrl.pathname, routing.locales);
  if (!requiresSession(path)) return null;

  const url = request.nextUrl.clone();
  url.pathname = `${localePrefix(request)}/login`;
  url.search = '';
  url.searchParams.set('next', `${request.nextUrl.pathname}${request.nextUrl.search}`);
  if (reason) url.searchParams.set('reason', reason);

  const redirect = NextResponse.redirect(url);
  redirect.cookies.delete(ACCESS_COOKIE);
  redirect.cookies.delete(REFRESH_COOKIE);

  return redirect;
}

/** Le préfixe de langue de l'URL courante, RECONSTRUIT — jamais deviné. */
function localePrefix(request: NextRequest): string {
  const locale = routing.locales.find(
    (code) =>
      request.nextUrl.pathname === `/${code}` || request.nextUrl.pathname.startsWith(`/${code}/`),
  );

  return locale ? `/${locale}` : '';
}

export const config = {
  // Tout sauf les routes d'API, les assets Next et les fichiers à extension.
  matcher: '/((?!api|_next|_vercel|.*\\..*).*)',
};

/** Réexporté pour les Server Actions qui posent la session à la connexion. */
export { ACCESS_COOKIE, REFRESH_COOKIE };

export type ProxyResponse = NextResponse;
