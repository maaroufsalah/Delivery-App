'use client';

import { useTranslations } from 'next-intl';
import type { ReferentialResult } from '@/lib/actions/action-result';

/** Les motifs machine que l'API renvoie dans `details.reason` pour le terrain. */
const REASONS = [
  'parcel-other-city',
  'parcel-not-ready',
  'parcel-in-route',
  'parcel-not-dispatchable',
  'parcel-other-client',
  'parcel-wrong-status',
  'parcel-in-note',
  'route-not-draft',
  // Lot W2 (D-239) : une tâche de masse travaille sur l'objet ; un colis bougé pendant la tâche.
  'bulk-job-running',
  'parcel-status-changed',
  'route-not-empty',
  'route-dispatched',
  'route-closed',
  'route-not-dispatched',
  'note-validated',
  'note-picked-up',
  'note-in-route',
  // Lot I, I1 (D-211) : les refus du transfert.
  'parcel-delivered',
  'parcel-finished',
  'parcel-on-return-handover',
  'parcel-wrong-city',
  'parcel-not-on-route',
  'transfer-delivery-only',
  'courier-inactive',
  // Lot L, L1 (D-219) : le transfert vers une autre ville.
  'courier-no-city',
  'transfer-city-required',
  'transfer-city-not-served',
  'parcel-invoiced',
  'parcel-remitted',
  'parcel-destination-locked',
] as const;

type RouteErrorKey = (typeof REASONS)[number];

/**
 * Traduit le refus d'une action de circuit ou de bon.
 *
 * Même principe que `useReturnError` : l'API répond 409 avec un motif MACHINE,
 * l'écran le traduit dans la langue de l'agent, et le message français de l'API
 * ne sert que de secours pour un motif qu'on ne connaîtrait pas encore.
 *
 * Composer un circuit, c'est se faire refuser des colis : un déjà pris par une
 * autre tournée, un qui n'est pas arrivé en ville. « Échec » ferait recocher la
 * même ligne ; « ce colis est déjà sur CIR-2609-0004 » la fait abandonner.
 */
export function useRouteError(): (result: Extract<ReferentialResult, { ok: false }>) => string {
  const tError = useTranslations('routes.errors');
  const tValidation = useTranslations('validation');

  return (result) => {
    const reason = result.reason;

    if (reason && (REASONS as readonly string[]).includes(reason)) {
      return tError(reason as RouteErrorKey);
    }

    return result.error === 'invalid' ? tValidation('invalid') : result.error;
  };
}
