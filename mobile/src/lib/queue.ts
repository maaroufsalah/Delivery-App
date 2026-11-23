import AsyncStorage from '@react-native-async-storage/async-storage';
import { api, ApiError } from './api';

/**
 * La file HORS-LIGNE — M1, docs/DECISIONS.md D-077.
 *
 * Un livreur marocain descend dans un parking, entre dans un immeuble, traverse
 * une zone sans couverture. Il ne doit jamais attendre le réseau pour dire
 * « livré » : le geste part dans cette file, l'écran avance, et la file se vide
 * quand le réseau revient.
 *
 * **Dans l'ORDRE, et une seule à la fois.** Les gestes d'une tournée ne sont
 * pas indépendants : scanner puis démarrer, livrer puis photographier. Les
 * rejouer en parallèle ferait arriver la photo avant l'événement qu'elle
 * documente — et l'API la refuserait, à juste titre (D-074).
 *
 * **Le premier échec ARRÊTE le drain.** Si le réseau est retombé, les suivants
 * échoueront aussi ; insister ne ferait que vider la batterie. La file reprend
 * au prochain retour de réseau, depuis l'entrée qui a échoué.
 *
 * **Un refus MÉTIER, lui, retire l'entrée.** Un colis déjà scanné par un autre
 * ne le sera jamais moins : garder le geste ferait bloquer la file pour
 * toujours sur une requête qui ne passera pas. Il part dans les « rejetés »,
 * que l'écran montre — un geste perdu en silence est pire qu'un geste refusé.
 *
 * **Conflit : dernier événement gagne.** Deux téléphones, ou un rejeu tardif :
 * l'API applique ce qui arrive, dans l'ordre d'arrivée. C'est la règle que le
 * PO a posée, et elle est la seule qui ne demande pas d'arbitrage humain sur un
 * trottoir.
 */

const QUEUE_KEY = 'offline.queue';
const REJECTED_KEY = 'offline.rejected';

/**
 * CE QUI N'EST PAS UN REFUS MÉTIER — C15 bloc 3.
 *
 * **Le défaut corrigé : un scan perdu pour une histoire de jeton.** Le drain ne
 * connaissait que deux cas — réseau absent, ou refus définitif. Un `401` arrivé
 * pendant le rejeu, parce que le jeton venait d'expirer, tombait donc dans le
 * second : le scan partait dans les « rejetés », c'est-à-dire **perdu**, alors
 * qu'il aurait parfaitement abouti trois secondes plus tard.
 *
 * Même raisonnement pour un `503` de redéploiement, un `429` de limitation et
 * un `408` de délai dépassé : aucun de ces trois-là ne dit que le geste est
 * mauvais, ils disent que ce n'est pas le moment. La file les garde.
 *
 * Un refus MÉTIER, lui, sort de la file — « déjà pris par un autre » ne se
 * démentira pas, et le garder bloquerait la file pour toujours.
 */
function isTransient(error: unknown): boolean {
  if (!(error instanceof ApiError)) return false;

  return (
    error.status === 401 ||
    error.status === 403 ||
    error.status === 408 ||
    error.status === 429 ||
    error.status >= 500
  );
}

/**
 * Ce qu'on met en file : les gestes de terrain, et eux seuls.
 *
 * `parcel` a rejoint la liste en M2. Un vendeur saisit ses colis le soir, chez
 * lui, parfois sans réseau — et la saisie est longue : trois étapes, un
 * destinataire, une adresse. La perdre parce que le wifi a sauté serait le
 * geste le plus coûteux de l'application à refaire.
 *
 * **Ce qui N'ENTRE PAS en file, et pourquoi.** Tout ce qui demande une réponse
 * du serveur pour que l'écran suivant ait un sens : un tarif, une facture, la
 * validation d'un bon de ramassage. Une file ne rend rien — elle promet. On ne
 * met en file que les gestes dont l'auteur sait déjà le résultat.
 */
export type QueuedKind = 'scan' | 'status' | 'proof' | 'parcel';

export interface QueuedItem {
  id: string;
  kind: QueuedKind;
  path: string;
  body: unknown;
  /** Quand le livreur a fait le geste — pas quand il est parti. */
  at: number;
  /** Ce que l'écran affiche en attente : « CAS…RAB », « Livré ». */
  label: string;
  attempts: number;
  /**
   * La clé d'idempotence du PREMIER envoi, rejouée telle quelle — audit du
   * 03/10, C2. Sans elle, une création partie mais dont la réponse s'est
   * perdue était refaite au rejeu : un second colis, un autre code.
   */
  idempotencyKey?: string;
}

export interface RejectedItem extends QueuedItem {
  reason: string;
  message: string;
}

async function read<T>(key: string): Promise<T[]> {
  try {
    const raw = await AsyncStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T[]) : [];
  } catch {
    // Un magasin illisible ne doit pas empêcher l'application de démarrer : on
    // repart d'une file vide, ce qui perd au pire un geste non parti.
    return [];
  }
}

async function write<T>(key: string, items: T[]): Promise<void> {
  await AsyncStorage.setItem(key, JSON.stringify(items));
}

export async function pending(): Promise<QueuedItem[]> {
  return read<QueuedItem>(QUEUE_KEY);
}

export async function rejected(): Promise<RejectedItem[]> {
  return read<RejectedItem>(REJECTED_KEY);
}

export async function clearRejected(): Promise<void> {
  await AsyncStorage.removeItem(REJECTED_KEY);
}

/**
 * UNE SEULE ÉCRITURE À LA FOIS SUR LA FILE — audit du 03/10, C1.
 *
 * `AsyncStorage` n'a pas de transaction : lire, modifier, réécrire depuis deux
 * endroits à la fois (un scan mis en file PENDANT un rejeu) faisait gagner le
 * dernier, et le geste de l'autre disparaissait — ni en file, ni rejeté. Toute
 * modification passe par cette chaîne, l'une après l'autre.
 */
let storageChain: Promise<unknown> = Promise.resolve();

function exclusive<T>(work: () => Promise<T>): Promise<T> {
  const next = storageChain.then(work, work);
  storageChain = next.catch(() => undefined);
  return next;
}

/** Une clé unique : la milliseconde, et un suffixe aléatoire. */
export function newKey(prefix = 'mob'): string {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

/** Met un geste en file. Rend l'entrée, pour que l'écran l'affiche aussitôt. */
export async function enqueue(
  item: Omit<QueuedItem, 'id' | 'at' | 'attempts'>,
): Promise<QueuedItem> {
  const queued: QueuedItem = {
    ...item,
    // `Date.now()` plus un suffixe aléatoire : deux scans dans la même
    // milliseconde sont possibles quand on vide un bac en rafale.
    id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    at: Date.now(),
    attempts: 0,
  };

  await exclusive(async () => write(QUEUE_KEY, [...(await pending()), queued]));
  return queued;
}

/** Retire UNE entrée, par son identifiant, en relisant la file sous verrou. */
async function removeQueued(id: string): Promise<void> {
  await exclusive(async () =>
    write(
      QUEUE_KEY,
      (await pending()).filter((row) => row.id !== id),
    ),
  );
}

export interface DrainResult {
  sent: number;
  rejected: number;
  /** Ce qui reste en file : zéro veut dire que tout est parti. */
  remaining: number;
}

/** Le rejeu en cours : un second appel le REJOINT au lieu d'en lancer un autre. */
let draining: Promise<DrainResult> | null = null;

/**
 * Vide la file, dans l'ordre, jusqu'au premier échec RÉSEAU.
 *
 * **Un seul rejeu à la fois** — audit du 03/10, C2. Le retour au premier plan et
 * l'appui sur le bandeau lançaient deux rejeux qui envoyaient les mêmes
 * entrées : deux colis pour une création. Le second appel attend le premier.
 *
 * **Chaque entrée sort par son identifiant**, en relisant la file sous verrou
 * (C1) : ce qui a été mis en file PENDANT le rejeu reste en file, au lieu
 * d'être écrasé par l'instantané de départ.
 */
export function drain(): Promise<DrainResult> {
  if (!draining) {
    draining = drainOnce().finally(() => {
      draining = null;
    });
  }
  return draining;
}

async function drainOnce(): Promise<DrainResult> {
  const items = await pending();
  const result: DrainResult = { sent: 0, rejected: 0, remaining: items.length };

  for (const item of items) {
    try {
      await api(item.path, {
        method: 'POST',
        body: item.body,
        // La clé du premier envoi : une création déjà faite rend le même colis.
        idempotencyKey: item.idempotencyKey ?? item.id,
      });
      await removeQueued(item.id);
      result.sent += 1;
    } catch (error) {
      if (ApiError.isOffline(error) || isTransient(error)) {
        /*
         * Le réseau est retombé, OU le serveur n'a pas pu répondre. On s'arrête
         * ICI : les suivants échoueraient aussi, et insister ne ferait que
         * vider la batterie. La file reprend au prochain retour, depuis cette
         * entrée.
         */
        await exclusive(async () =>
          write(
            QUEUE_KEY,
            (await pending()).map((row) =>
              row.id === item.id ? { ...row, attempts: row.attempts + 1 } : row,
            ),
          ),
        );
        return { ...result, remaining: (await pending()).length };
      }

      /*
       * Un refus MÉTIER. Le garder bloquerait la file pour toujours : un colis
       * pris par un autre ne se libérera pas tout seul. Il part dans les
       * rejetés, que l'écran montre — un geste perdu en silence est pire qu'un
       * geste refusé.
       */
      const failure = error instanceof ApiError ? error.failure : undefined;

      await exclusive(async () => {
        await write(REJECTED_KEY, [
          ...(await rejected()),
          {
            ...item,
            reason: failure?.reason ?? 'unknown',
            message: failure?.message ?? 'Refusé par le serveur.',
          },
        ]);
        await write(
          QUEUE_KEY,
          (await pending()).filter((row) => row.id !== item.id),
        );
      });

      result.rejected += 1;
    }
  }

  return { ...result, remaining: (await pending()).length };
}

/** Vide tout — à la déconnexion. Les gestes d'un compte ne suivent pas le suivant. */
export async function reset(): Promise<void> {
  // Deux appels plutôt que `multiRemove`, retiré de l'API en v3.
  await Promise.all([AsyncStorage.removeItem(QUEUE_KEY), AsyncStorage.removeItem(REJECTED_KEY)]);
}
