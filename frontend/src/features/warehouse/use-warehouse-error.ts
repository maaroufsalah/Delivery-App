'use client';

import { useTranslations } from 'next-intl';
import type { ReferentialResult } from '@/lib/actions/action-result';

/** Motifs machine que l'API renvoie dans `details.reason` pour l'entrepôt. */
const REASONS = [
  'insufficient-stock',
  'product-has-stock',
  'order-duplicate',
  'order-has-parcel',
  'order-not-confirmed',
  'order-not-draft',
  'storage-already-decided',
] as const;

type WarehouseErrorKey = (typeof REASONS)[number];

/**
 * Traduit le refus d'une action d'entrepôt.
 *
 * Deux motifs comptent plus que les autres, parce qu'ils se corrigent :
 * `insufficient-stock` dit d'ajuster l'inventaire avant d'expédier, et
 * `order-has-parcel` dit que le colis existe déjà — c'est-à-dire que le geste
 * a DÉJÀ réussi, ce qu'un « échec » générique laisserait croire l'inverse.
 */
export function useWarehouseError(): (result: Extract<ReferentialResult, { ok: false }>) => string {
  const tError = useTranslations('warehouse.errors');
  const tValidation = useTranslations('validation');

  return (result) => {
    const reason = result.reason;

    if (reason && (REASONS as readonly string[]).includes(reason)) {
      return tError(reason as WarehouseErrorKey);
    }

    return result.error === 'invalid' ? tValidation('invalid') : result.error;
  };
}
