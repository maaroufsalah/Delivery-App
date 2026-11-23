'use client';

import { useTranslations } from 'next-intl';
import type { ReferentialResult } from '@/lib/actions/action-result';

/** Les motifs machine que l'API renvoie dans `details.reason`. */
const REASONS = [
  'parcel-unknown',
  'parcel-not-billable',
  'parcel-other-client',
  'parcel-already-invoiced',
  'parcel-not-delivered',
  'parcel-other-courier',
  'parcel-already-remitted',
  // C11 bloc 3 — les deux régimes de facturation (D-061).
  'parcel-not-reimbursed',
  'parcel-already-paid-out',
  // Lot Q : la part chèque d'un COD n'est jamais versée.
  'parcel-paid-by-cheque',
  'payout-paid',
  'payout-already-paid',
  'client-not-professional',
  'no-billable-parcel',
  'invoice-issued',
  'invoice-already-issued',
  'invoice-draft',
  'invoice-already-paid',
  'invoice-other-client',
  // Lot D, D5 et D6 (D-186) : un seul brouillon par vendeur ; ses lignes hors colis le protègent.
  'draft-exists',
  'invoice-has-adjustments',
  'remittance-confirmed',
  'remittance-already-confirmed',
  // D-243 : clôture par le livreur, réouverture par l'agence.
  'courier-has-newer-remittance',
  'remittance-not-closed',
  'no-open-remittance',
  'remittance-empty',
  'remittance-already-closed',
  'statement-validated',
  'statement-already-validated',
  'statement-draft',
  'statement-already-paid',
  'course-already-settled',
] as const;

type FinanceErrorKey = (typeof REASONS)[number];

/**
 * Traduit le refus d'une action de facturation.
 *
 * L'API répond 409 avec un motif MACHINE (`parcel-already-invoiced`) et un
 * message humain en français. L'écran préfère le motif, qu'il sait traduire
 * dans la langue de l'opérateur ; le message de l'API reste le secours.
 *
 * Ici la distinction porte de l'argent : « ce colis est déjà sur une facture
 * émise » se corrige en le retirant du lot, alors qu'un « échec » laisse
 * l'opérateur relancer la même émission jusqu'à croire à une panne.
 */
export function useFinanceError(): (result: Extract<ReferentialResult, { ok: false }>) => string {
  const tError = useTranslations('finance.errors');
  const tValidation = useTranslations('validation');

  return (result) => {
    const reason = result.reason;

    if (reason && (REASONS as readonly string[]).includes(reason)) {
      return tError(reason as FinanceErrorKey);
    }

    return result.error === 'invalid' ? tValidation('invalid') : result.error;
  };
}
