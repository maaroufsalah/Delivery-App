'use client';

import { useTranslations } from 'next-intl';
import type { ReferentialResult } from '@/lib/actions/action-result';

/**
 * Les motifs machine que l'API renvoie dans `details.reason`.
 *
 * La liste suit `returns.errors` de `messages/fr.json`, clé pour clé : un motif
 * absent d'ici retombe sur le message français de l'API, ce qui donne une
 * phrase française dans une interface arabe. Elle avait pris du retard de
 * treize motifs — remise à jour au chantier « Bon de retour livreur ».
 */
const REASONS = [
  'parcel-unknown',
  'parcel-not-returnable',
  'parcel-other-courier',
  'parcel-already-handed-over',
  'parcel-already-listed',
  'parcel-not-in-agency',
  'parcel-other-client',
  'parcel-already-returned',
  'parcel-moved',
  'handover-confirmed',
  'handover-already-confirmed',
  'slip-delivered',
  'slip-already-delivered',
  'parcel-already-reserved',
  'parcel-not-in-handover',
  'handover-locked',
  'handover-admin-locked',
  'handover-already-picked-up',
  'handover-not-picked-up',
  'handover-not-confirmed',
  'handover-not-draft',
  'handover-cancelled',
  'other-courier',
  'client-not-pro',
  'bl-not-requested',
  'bl-already-collected',
  'bl-not-collected',
  'bl-other-client',
  'bl-other-courier',
  'bl-already-reserved',
  'bl-note-locked',
  'bl-note-wrong-status',
  'handover-empty',
  'handover-own',
  'handover-not-requested',
  'receipt-is-agency-only',
  'bl-note-empty',
  'courier-without-agency',
] as const;

type ReturnErrorKey = (typeof REASONS)[number];

/**
 * Traduit le refus d'une action de retour.
 *
 * L'API répond 409 avec un motif MACHINE (`parcel-other-courier`) et un message
 * humain en français. L'écran préfère le motif, qu'il sait traduire dans la
 * langue de l'agent ; le message de l'API reste le secours, parce qu'un motif
 * inconnu ne doit pas se transformer en message vide.
 *
 * Devant une douchette, cette distinction n'est pas cosmétique : « ce colis
 * n'est pas affecté à ce livreur » se corrige, « échec » fait rescanner le même
 * colis dix fois.
 */
export function useReturnError(): (result: Extract<ReferentialResult, { ok: false }>) => string {
  const tError = useTranslations('returns.errors');
  const tValidation = useTranslations('validation');

  return (result) => {
    const reason = result.reason;

    if (reason && (REASONS as readonly string[]).includes(reason)) {
      return tError(reason as ReturnErrorKey);
    }

    return result.error === 'invalid' ? tValidation('invalid') : result.error;
  };
}
