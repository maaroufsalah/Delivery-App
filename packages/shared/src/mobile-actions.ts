/**
 * LES GESTES DU LIVREUR SUR LE TÉLÉPHONE — Recette 5, points 3 et 4 a.
 *
 * Deux mécaniques pures, sans React ni React Native, pour qu'une suite Jest les
 * tienne (l'appli n'a pas de runner de tests) : le VERROU DU SCANNER et le
 * GARDE D'UNE ACTION. Les écrans les enveloppent dans des hooks
 * (`apps/mobile/src/lib/scan-gate.ts`, `use-action.ts`) ; la règle vit ici.
 */

/* --- Le verrou du scanner (point 3) ---------------------------------------- */

/** Après une réponse, la caméra se tait ce temps-là : le livreur lit la carte. */
export const SCAN_PAUSE_MS = 1_500;

/**
 * Un même code relu dans cette fenêtre est ignoré EN SILENCE — ni appel, ni son.
 * La fenêtre GLISSE : un colis resté devant l'objectif ne repart jamais tout
 * seul ; il faut l'écarter trois secondes, puis le représenter.
 */
export const SCAN_SAME_CODE_MS = 3_000;

export interface ScanGate {
  /**
   * Une lecture de la caméra. `true` : elle est EFFECTIVE, l'écran la traite
   * et appellera `release()` quand il aura sa réponse. `false` : ignorée.
   */
  tryAcquire(raw: string, now?: number): boolean;
  /** La réponse est là (succès, refus, délai, réseau) : la pause commence. */
  release(now?: number): void;
  /** Saisie manuelle : on veut pouvoir retaper le même code tout de suite. */
  forget(): void;
  readonly busy: boolean;
}

/** La forme comparée : sans espaces autour, en capitales — comme le serveur. */
export function normalizeScannedCode(raw: string): string {
  return raw.trim().toUpperCase();
}

/**
 * LE VERROU. Trois raisons d'ignorer une lecture, dans cet ordre :
 *
 *  1. une lecture est EN COURS (l'API n'a pas répondu) — la caméra lit le même
 *     code dix fois par seconde, et c'était l'origine du « déjà scanné » ;
 *  2. la PAUSE qui suit une réponse n'est pas finie ;
 *  3. c'est le MÊME code que la dernière lecture effective, relu dans les trois
 *     secondes — la fenêtre repart à chaque relecture ignorée.
 */
export function createScanGate(options: { pauseMs?: number; sameCodeMs?: number } = {}): ScanGate {
  const pauseMs = options.pauseMs ?? SCAN_PAUSE_MS;
  const sameCodeMs = options.sameCodeMs ?? SCAN_SAME_CODE_MS;

  let busy = false;
  let pausedUntil = 0;
  let last: { code: string; at: number } = { code: '', at: -Infinity };

  return {
    tryAcquire(raw, now = Date.now()) {
      const code = normalizeScannedCode(raw);
      if (!code || busy) return false;

      if (code === last.code && now - last.at < sameCodeMs) {
        last = { code, at: now };
        return false;
      }

      if (now < pausedUntil) return false;

      busy = true;
      last = { code, at: now };
      return true;
    },
    release(now = Date.now()) {
      busy = false;
      pausedUntil = now + pauseMs;
      // La fenêtre du même code part de la RÉPONSE : c'est là que le livreur
      // apprend l'issue et écarte le colis.
      if (last.code) last = { code: last.code, at: now };
    },
    forget() {
      last = { code: '', at: -Infinity };
      pausedUntil = 0;
    },
    get busy() {
      return busy;
    },
  };
}

/* --- Le garde d'une action (point 4 a) -------------------------------------- */

/*
 * Ce que le garde demande au runtime. `packages/shared` ne charge ni la lib DOM
 * ni les types Node ; le téléphone (Hermes) et Jest (Node) fournissent tous
 * deux ces quatre globaux. Déclarés ici, ils ne sortent pas du module.
 */
/** Le signal remis à la tâche : celui d'un `AbortController`, réduit à l'usage. */
export interface ActionSignal {
  readonly aborted: boolean;
  addEventListener(type: 'abort', listener: () => void): void;
}
declare const AbortController: new () => { readonly signal: ActionSignal; abort(): void };
declare function setTimeout(callback: () => void, ms: number): unknown;
declare function clearTimeout(handle: unknown): void;

/** Le délai au-delà duquel une action rend la main, réponse ou pas. */
export const ACTION_TIMEOUT_MS = 15_000;

/** L'erreur levée quand le délai est dépassé — reconnaissable sans `instanceof`. */
export class ActionTimeoutError extends Error {
  readonly kind = 'timeout' as const;

  constructor(readonly timeoutMs: number) {
    super(`Pas de réponse après ${Math.round(timeoutMs / 1000)} s.`);
    this.name = 'ActionTimeoutError';
  }
}

export function isActionTimeout(error: unknown): error is ActionTimeoutError {
  return error instanceof Error && (error as { kind?: unknown }).kind === 'timeout';
}

export type ActionOutcome<T> =
  | { kind: 'done'; value: T }
  | { kind: 'failed'; error: unknown }
  | { kind: 'timeout'; error: ActionTimeoutError }
  /** Un second appui pendant que le premier tourne : rien n'est parti. */
  | { kind: 'ignored' };

export interface ActionGuard {
  run<T>(task: (signal: ActionSignal) => Promise<T>): Promise<ActionOutcome<T>>;
  readonly running: boolean;
}

/**
 * UN APPUI, UNE REQUÊTE, UNE ISSUE.
 *
 * - **appui unique** : le verrou se pose AVANT le premier `await`, de façon
 *   synchrone ; le second appui d'un double appui le trouve fermé. Un
 *   `isPending` de React Query ne suffit pas : il arrive au rendu suivant ;
 * - **délai** : passé `timeoutMs`, la tâche est abandonnée (son `signal`
 *   avorte le `fetch`) et l'issue est `timeout` ;
 * - **retour garanti** : le verrou tombe dans un `finally`, quoi qu'il arrive —
 *   le défaut du bouton qui tourne sans fin ne peut plus se produire ;
 * - **jamais de rejet** : l'issue est une valeur, l'écran la lit toujours.
 */
export function createActionGuard(options: { timeoutMs?: number } = {}): ActionGuard {
  const timeoutMs = options.timeoutMs ?? ACTION_TIMEOUT_MS;
  let running = false;

  return {
    async run<T>(task: (signal: ActionSignal) => Promise<T>): Promise<ActionOutcome<T>> {
      if (running) return { kind: 'ignored' };
      running = true;

      const controller = new AbortController();
      let timer: unknown;

      const timeout = new Promise<never>((_, reject) => {
        timer = setTimeout(() => {
          controller.abort();
          reject(new ActionTimeoutError(timeoutMs));
        }, timeoutMs);
      });

      try {
        const value = await Promise.race([task(controller.signal), timeout]);
        return { kind: 'done', value };
      } catch (error) {
        if (isActionTimeout(error)) return { kind: 'timeout', error };
        return { kind: 'failed', error };
      } finally {
        if (timer !== undefined) clearTimeout(timer);
        running = false;
      }
    },
    get running() {
      return running;
    },
  };
}
