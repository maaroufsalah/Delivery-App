import type { ReferenceCount, TransferCityChoice } from '@delivery/shared';
import { ApiError } from '@/lib/http';

/** Une ville bien formée, rien d'autre ne passe dans le menu (lot L). */
function isCityChoice(value: unknown): value is TransferCityChoice {
  const city = value as Partial<TransferCityChoice> | null;
  return typeof city?.id === 'string' && typeof city.name === 'string';
}

/**
 * Résultat d'une Server Action d'administration.
 *
 * Ce module n'est PAS un fichier `'use server'` : il n'exporte qu'un type et
 * une fonction pure, et un fichier `'use server'` ne peut exporter que des
 * fonctions asynchrones. Le mettre à part est ce qui permet aux deux domaines
 * (référentiel et comptes) de partager la même forme de réponse.
 *
 * `references` est le point important : quand l'API refuse une suppression en
 * 409, elle dit CE qui bloque et en quelle quantité. L'écran peut alors
 * afficher « 412 colis utilisent cette ville » et proposer la désactivation,
 * au lieu d'un « échec » qui n'apprend rien (DOMAIN.md §4 dette n°8).
 */
export type ReferentialResult<T = undefined> =
  | { ok: true; data: T }
  | {
      ok: false;
      error: string;
      code?: string;
      references?: ReferenceCount[];
      field?: string;
      /**
       * Motif MACHINE d'un refus, quand l'API en donne un : `parcel-moved`,
       * `parcel-other-courier`… L'écran le traduit ; `error` reste le message
       * humain de secours. C'est ce qui permet de dire POURQUOI un scan est
       * refusé dans la langue de l'agent.
       */
      reason?: string;
      /**
       * La facture que nomme le refus, quand l'API la donne — finitions recette 8,
       * F1 : `draft-exists` désigne le brouillon déjà ouvert, que l'écran ouvre.
       */
      invoiceCode?: string;
      /**
       * La remise que nomme un refus de doublon, quand l'API la donne — lot K
       * (D-218) : l'écran la décrit dans la langue de l'agent.
       */
      discountId?: string;
      /**
       * Les villes proposées par un refus `transfer-city-required` — lot L
       * (D-219) : la boîte de transfert les met dans son menu.
       */
      cities?: TransferCityChoice[];
      /** … et combien de colis changeraient de destination. */
      cityCount?: number;
    };

/**
 * Traduit une exception en échec d'action — ET LA JOURNALISE.
 *
 * **Le journal n'est pas un ornement.** Avant le chantier « Audit des
 * paramètres société », les actions rendaient `'unexpected'` sans écrire une
 * ligne : `docker compose logs web` ne contenait que le démarrage de Next, et
 * un 413 arrivé en production était indiscernable d'une panne réseau. On
 * cherchait la cause dans les journaux de l'API en devinant l'heure.
 *
 * `context` nomme le geste — `settings.company`, `settings.theme` — pour que la
 * ligne soit lisible sans remonter la pile.
 */
export function toFailure(
  error: unknown,
  context?: string,
): Extract<ReferentialResult<never>, { ok: false }> {
  const where = context ? `[${context}] ` : '';

  if (error instanceof ApiError) {
    const details = error.details as
      | {
          references?: ReferenceCount[];
          field?: string;
          reason?: string;
          invoiceCode?: unknown;
          discountId?: unknown;
          cities?: unknown;
          count?: unknown;
        }
      | undefined;

    /*
     * Une ligne JSON, avec l'identifiant de requête que l'API a renvoyé —
     * recette SpeedyTrans 4, point 5 c : on la retrouve à côté de la ligne de
     * l'API, et dans la console « Erreurs » pour une 500. `console` et non un
     * logger maison : c'est ce que `docker compose logs web` rend, sans
     * dépendance de plus. Un refus (4xx) est un avertissement, pas une panne.
     */
    const line = JSON.stringify({
      level: error.status >= 500 || error.status === 0 ? 'error' : 'warn',
      source: 'web-action',
      context: context ?? null,
      requestId: error.requestId,
      status: error.status,
      code: error.code,
      msg: error.message,
      ...(details ? { details } : {}),
    });
    if (error.status >= 500 || error.status === 0) console.error(line);
    else console.warn(line);

    return {
      ok: false,
      error: error.message,
      code: error.code,
      ...(details?.references ? { references: details.references } : {}),
      ...(details?.field ? { field: details.field } : {}),
      ...(details?.reason ? { reason: details.reason } : {}),
      ...(typeof details?.invoiceCode === 'string' ? { invoiceCode: details.invoiceCode } : {}),
      ...(typeof details?.discountId === 'string' ? { discountId: details.discountId } : {}),
      ...(Array.isArray(details?.cities)
        ? {
            cities: (details.cities as unknown[]).filter(isCityChoice),
            ...(typeof details.count === 'number' ? { cityCount: details.count } : {}),
          }
        : {}),
    };
  }

  console.error(
    JSON.stringify({
      level: 'error',
      source: 'web-action',
      context: context ?? null,
      msg: `${where}erreur inattendue`,
      err: error instanceof Error ? { message: error.message, stack: error.stack } : String(error),
    }),
  );

  return { ok: false, error: 'unexpected' };
}
