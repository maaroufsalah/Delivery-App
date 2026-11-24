'use server';

import {
  type ForgotPasswordResult,
  forgotPasswordSchema,
  type RegistrationResult,
  registrationSchema,
} from '@delivery/shared';
import { cookies, headers } from 'next/headers';
import { z } from 'zod';
import type { ActionResult } from '@/lib/actions/parcels';

/**
 * Connexion et déconnexion.
 *
 * Une Server Action est le seul contexte, avec `proxy.ts`, où Next autorise
 * l'écriture d'un cookie. L'appel à l'API est serveur → serveur : le navigateur
 * ne voit jamais son `Set-Cookie`, il faut donc le recopier. On préserve les
 * attributs posés par l'API (`httpOnly`, `sameSite`, durée) : elle reste seule
 * maîtresse de la politique de session.
 */

const API_URL = (
  process.env.API_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  'http://localhost:3001/api'
).replace(/\/$/, '');

/**
 * L'hôte du visiteur, transmis à l'API — C8 bloc 5, D-049.
 *
 * Cette action appelle l'API en direct, sans passer par `lib/http.ts` : elle
 * doit donc transmettre l'en-tête elle-même. Sans lui, l'API ne sait pas de
 * quel espace il s'agit, et la connexion sur `wimo.…` ne résout aucun tenant.
 */
async function hostHeaders(): Promise<Record<string, string>> {
  const store = await headers();
  const host = store.get('x-forwarded-host') ?? store.get('host');
  /*
   * L'ADRESSE DU NAVIGATEUR, relayée — recette SpeedyTrans 3 (frein des
   * connexions). Cette action appelle l'API depuis le conteneur web : sans ce
   * relais, TOUTES les connexions web porteraient l'adresse du conteneur, et
   * le frein par adresse les freinerait toutes ensemble. On transmet l'en-tête
   * tel que nginx l'a construit ; l'API ne fait confiance qu'aux sauts privés
   * et retient la dernière adresse publique — celle ajoutée par nginx.
   */
  const forwardedFor = store.get('x-forwarded-for');
  // L'identifiant de requête posé par `proxy.ts` (recette 4, point 5 c).
  const requestId = store.get('x-request-id');

  return {
    ...(host ? { 'x-forwarded-host': host } : {}),
    ...(forwardedFor ? { 'x-forwarded-for': forwardedFor } : {}),
    ...(requestId ? { 'x-request-id': requestId } : {}),
  };
}

const loginSchema = z.object({
  // L'e-mail OU le nom d'utilisateur (point 5) : l'API tranche à la forme.
  identifier: z.string().trim().min(1, 'required').max(254),
  password: z.string().min(1, 'required'),
  /**
   * « Se souvenir de moi » — C15 bloc 1.
   *
   * Transmis tel quel à l'API, qui en déduit la durée du rafraîchissement.
   * Le front ne connaît AUCUNE durée : elles vivent dans les `Setting` de la
   * société (D-019), et un chiffre écrit ici finirait par mentir.
   */
  remember: z.boolean().optional(),
});

interface ParsedCookie {
  name: string;
  value: string;
  maxAge?: number;
  path: string;
  httpOnly: boolean;
  secure: boolean;
  sameSite: 'lax' | 'strict' | 'none';
  domain?: string;
}

/** Analyse d'un en-tête `Set-Cookie`. Suffisant pour nos deux cookies. */
function parseSetCookie(raw: string): ParsedCookie | null {
  const [pair, ...attributes] = raw.split(';');
  if (!pair) return null;

  const separator = pair.indexOf('=');
  if (separator === -1) return null;

  const cookie: ParsedCookie = {
    name: pair.slice(0, separator).trim(),
    value: decodeURIComponent(pair.slice(separator + 1).trim()),
    path: '/',
    httpOnly: false,
    secure: false,
    sameSite: 'lax',
  };

  for (const attribute of attributes) {
    const [rawKey, rawValue] = attribute.split('=');
    const key = rawKey?.trim().toLowerCase();
    const value = rawValue?.trim();

    if (key === 'max-age' && value) cookie.maxAge = Number(value);
    else if (key === 'path' && value) cookie.path = value;
    else if (key === 'domain' && value) cookie.domain = value;
    else if (key === 'httponly') cookie.httpOnly = true;
    else if (key === 'secure') cookie.secure = true;
    else if (key === 'samesite' && value) {
      cookie.sameSite = value.toLowerCase() as ParsedCookie['sameSite'];
    }
  }

  return cookie;
}

async function applySetCookies(response: Response): Promise<void> {
  const store = await cookies();

  for (const raw of response.headers.getSetCookie()) {
    const cookie = parseSetCookie(raw);
    if (!cookie) continue;

    store.set(cookie.name, cookie.value, {
      maxAge: cookie.maxAge,
      path: cookie.path,
      domain: cookie.domain,
      httpOnly: cookie.httpOnly,
      secure: cookie.secure,
      sameSite: cookie.sameSite,
    });
  }
}

/**
 * L'API a-t-elle refusé pour cause de compte EN ATTENTE ?
 *
 * Le corps d'erreur est lu défensivement : une réponse non-JSON (502 d'un
 * reverse proxy, page d'erreur HTML) ne doit pas faire échouer la connexion
 * autrement que par le message ordinaire.
 */
async function pendingReason(response: Response): Promise<boolean> {
  try {
    const body = (await response.json()) as { details?: { reason?: string } };
    return body?.details?.reason === 'pending-validation';
  } catch {
    return false;
  }
}

export async function loginAction(input: unknown): Promise<ActionResult> {
  const parsed = loginSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  let response: Response;

  try {
    response = await fetch(`${API_URL}/auth/login`, {
      method: 'POST',
      headers: { 'content-type': 'application/json', ...(await hostHeaders()) },
      body: JSON.stringify(parsed.data),
      cache: 'no-store',
    });
  } catch {
    return { ok: false, error: 'unreachable' };
  }

  if (!response.ok) {
    /*
     * Un SEUL cas est distingué : le compte en attente de validation — C13
     * bloc 1. L'API ne le dit qu'à qui a donné le bon mot de passe, et le
     * `reason` qu'elle joint est ce qui permet d'afficher le message qui
     * explique l'attente plutôt qu'un « identifiants incorrects » trompeur.
     *
     * Pour tout le reste, e-mail inconnu, mot de passe faux ou compte fermé,
     * l'API ne distingue rien : le front n'a donc rien à en déduire non plus.
     */
    // Le frein par adresse (recette SpeedyTrans 3) : il faut attendre, pas
    // corriger sa saisie — le dire autrement ferait retaper un bon mot de passe.
    if (response.status === 429) return { ok: false, error: 'login-throttled' };

    const reason = await pendingReason(response);
    if (reason) return { ok: false, error: 'pending-validation' };

    return { ok: false, error: 'invalid-credentials' };
  }

  await applySetCookies(response);
  return { ok: true, data: undefined };
}

/**
 * « Mot de passe oublié » — C12 bloc 2b.
 *
 * Comme la connexion, en direct sur l'API plutôt que par `lib/http.ts` : il n'y
 * a pas de session, et c'est l'en-tête d'hôte qui désigne l'espace.
 *
 * **Aucune erreur ne remonte à l'écran quand l'API répond.** La route rend la
 * même chose pour une adresse connue et une inconnue ; si le front distinguait
 * les deux cas par un message d'échec, il réintroduirait par la porte de
 * derrière ce que l'API refuse de dire. Seule une API injoignable est signalée,
 * parce qu'alors rien n'a été enregistré.
 */
export async function forgotPasswordAction(
  input: unknown,
): Promise<ActionResult<ForgotPasswordResult>> {
  const parsed = forgotPasswordSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid-email' };

  try {
    const response = await fetch(`${API_URL}/auth/forgot-password`, {
      method: 'POST',
      headers: { 'content-type': 'application/json', ...(await hostHeaders()) },
      body: JSON.stringify(parsed.data),
      cache: 'no-store',
    });

    if (!response.ok) return { ok: false, error: 'unreachable' };

    return { ok: true, data: (await response.json()) as ForgotPasswordResult };
  } catch {
    return { ok: false, error: 'unreachable' };
  }
}

/**
 * Ouvre une session depuis le lien « se connecter en tant que » — C8, D-051.
 *
 * Le jeton arrive dans l'URL parce qu'il franchit une frontière de DOMAINE : la
 * console vit sur celui de la plateforme, la session doit naître sur celui du
 * tenant, et les cookies sont liés à l'hôte. Il ne vaut que deux minutes, et
 * que pour l'espace qu'il nomme — l'API vérifie les deux.
 */
export async function impersonateAction(token: string): Promise<ActionResult> {
  if (token.trim() === '') return { ok: false, error: 'invalid' };

  let response: Response;

  try {
    response = await fetch(`${API_URL}/auth/impersonate`, {
      method: 'POST',
      headers: { 'content-type': 'application/json', ...(await hostHeaders()) },
      body: JSON.stringify({ token }),
      cache: 'no-store',
    });
  } catch {
    return { ok: false, error: 'unreachable' };
  }

  if (!response.ok) return { ok: false, error: 'invalid-credentials' };

  await applySetCookies(response);
  return { ok: true, data: undefined };
}

export async function logoutAction(): Promise<ActionResult> {
  const store = await cookies();
  const header = store
    .getAll()
    .map((cookie) => `${cookie.name}=${encodeURIComponent(cookie.value)}`)
    .join('; ');

  try {
    // Révoque les refresh côté serveur : se contenter d'effacer les cookies
    // laisserait un jeton valide utilisable ailleurs.
    const response = await fetch(`${API_URL}/auth/logout`, {
      method: 'POST',
      /*
       * UN CORPS VIDE, MAIS UN CORPS — C16, le même défaut que `/auth/refresh`.
       *
       * La route est validée par un schéma zod qui attendait un OBJET. Sans
       * `content-type: application/json`, Express ne parse rien, le corps reste
       * `undefined` et la route rendait `422` : `response.ok` était faux,
       * `applySetCookies` n'était jamais appelé, et les cookies survivaient
       * avec leur `domain` — « Se déconnecter ne fait rien », de nouveau.
       *
       * Les deux côtés sont corrigés (le schéma accepte l'absence de corps),
       * pour qu'un seul des deux déployé ne casse rien.
       */
      headers: {
        'content-type': 'application/json',
        cookie: header,
        ...(await hostHeaders()),
      },
      body: '{}',
      cache: 'no-store',
    });
    /*
     * `applySetCookies` est ce qui efface VRAIMENT les cookies — C15 bloc 2.
     *
     * Le `Set-Cookie` de l'API porte le `domain` avec lequel les cookies ont
     * été posés, et c'est le seul moyen de les effacer en production : un
     * `store.delete('dp.at')` sans domaine ne touche pas un cookie posé sur
     * `.speedytrans.ma`.
     *
     * La route rend désormais `204` même sans session valide (elle est
     * `@Public()`), donc ce chemin est celui de tous les cas nominaux. Avant,
     * un jeton d'accès expiré — le cas le plus fréquent au moment où l'on
     * clique « Se déconnecter » — rendait `401`, et le cookie survivait.
     */
    if (response.ok) await applySetCookies(response);
  } catch {
    // API injoignable : on efface quand même la session locale.
  }

  // Filet, pour le cas où l'API n'a pas répondu. Sans `domain`, donc sans effet
  // sur un cookie posé sur un domaine parent — d'où l'appel serveur ci-dessus.
  store.delete('dp.at');
  store.delete('dp.rt');

  return { ok: true, data: undefined };
}

/**
 * Inscription publique — C13 bloc 1, D-086.
 *
 * En direct sur l'API comme la connexion, et pour la même raison : il n'y a pas
 * de session, et c'est l'en-tête d'hôte qui désigne l'espace. Aucun cookie
 * n'est posé au retour — l'inscription n'ouvre rien, elle demande.
 *
 * Le schéma est rejoué ici alors que le formulaire l'a déjà validé : une
 * Server Action est une frontière réseau, et ce qui lui arrive n'a pas
 * forcément traversé le formulaire.
 */
export async function registerAction(input: unknown): Promise<ActionResult<RegistrationResult>> {
  const parsed = registrationSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  let response: Response;

  try {
    response = await fetch(`${API_URL}/public/register`, {
      method: 'POST',
      headers: { 'content-type': 'application/json', ...(await hostHeaders()) },
      body: JSON.stringify(parsed.data),
      cache: 'no-store',
    });
  } catch {
    return { ok: false, error: 'unreachable' };
  }

  if (!response.ok) {
    /*
     * Trois issues, trois messages. L'adresse déjà prise est la seule que
     * l'écran rattache à un CHAMP — les deux autres parlent de l'espace, pas de
     * ce qui a été tapé.
     */
    if (response.status === 409) {
      // L'adresse OU le nom d'utilisateur (point 5) : l'API dit lequel.
      const body = (await response.json().catch(() => null)) as {
        details?: { field?: string };
      } | null;
      return {
        ok: false,
        error: body?.details?.field === 'username' ? 'username-taken' : 'email-taken',
      };
    }
    if (response.status === 403) return { ok: false, error: 'registration-closed' };
    return { ok: false, error: 'invalid' };
  }

  return { ok: true, data: (await response.json()) as RegistrationResult };
}
