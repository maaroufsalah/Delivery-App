'use client';

import {
  FINANCIAL_STATUS_COLORS,
  type FinancialStatus,
  MERCHANT_FINANCIAL_STATUS_COLORS,
  type MerchantFinancialStatus,
} from '@delivery/shared';
import { useTranslations } from 'next-intl';
import { CellBadge } from '@/components/data-table/cells';

/**
 * Badge du statut FINANCIER — chantier « Finance », point 1, D-105.
 *
 * Jumeau de `ParcelStatusBadge`, et volontairement distinct de lui : les deux
 * axes ont chacun leur badge, leur palette et leur espace de noms. Les fondre
 * dans un composant unique paramétré aurait invité, un jour, à en afficher un
 * seul « puisque c'est le même composant ».
 *
 * **Il rend `null` quand le champ est absent**, et ce n'est pas un oubli :
 * l'API ne sérialise pas `financialStatus` pour un e-commerçant (D-060). Une
 * colonne vide vaut mieux qu'un badge « inconnu » qui laisserait croire à une
 * donnée manquante.
 *
 * Lot O, D-227 : l'e-commerçant reçoit à la place `merchantFinancialStatus`
 * (Non encaissé, Encaissé, Facturé, Payé), rendu par le même badge. L'API ne
 * sert jamais les deux à la même portée.
 */
export function FinancialStatusBadge({
  status,
  merchantStatus,
}: {
  status: FinancialStatus | undefined;
  merchantStatus?: MerchantFinancialStatus | undefined;
}) {
  const t = useTranslations('financialStatus');
  const tMerchant = useTranslations('merchantFinancialStatus');

  if (status) return <CellBadge label={t(status)} color={FINANCIAL_STATUS_COLORS[status]} />;
  if (merchantStatus) {
    return (
      <CellBadge
        label={tMerchant(merchantStatus)}
        color={MERCHANT_FINANCIAL_STATUS_COLORS[merchantStatus]}
      />
    );
  }

  return null;
}
