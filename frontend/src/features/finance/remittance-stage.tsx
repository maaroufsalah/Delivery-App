'use client';

import type { RemittanceStage } from '@delivery/shared';
import { useTranslations } from 'next-intl';
import { CellBadge } from '@/components/data-table/cells';

/**
 * LES QUATRE STATUTS D'UNE CAISSE, à un seul endroit — D-243.
 *
 * Le statut vient SERVI par l'API (`stage`) : aucun écran ne le recalcule à
 * partir de `status` et de la clôture du livreur. Ce fichier ne fait que lui
 * donner un libellé et une teinte, pour que la liste, la fiche, le tableau de
 * bord et l'espace livreur disent la même chose avec la même couleur.
 *
 * Les teintes sont des tokens, jamais une couleur de marque : « ouverte » et
 * « reçue » gardent celles qu'avaient « Ouvert » et « Confirmé » ; « fermée »
 * prend l'avertissement (de l'argent attend d'être réceptionné) et « validée »
 * le succès (plus rien à faire).
 */
export const REMITTANCE_STAGE_COLORS: Record<RemittanceStage, string> = {
  open: 'var(--chart-3)',
  closed: 'var(--warning)',
  received: 'var(--chart-4)',
  validated: 'var(--success)',
  cancelled: 'var(--muted-foreground)',
};

export function RemittanceStageBadge({
  stage,
  icon,
}: {
  stage: RemittanceStage;
  icon?: React.ComponentType<{ className?: string }>;
}) {
  const t = useTranslations('finance.remittances.stage');

  return <CellBadge label={t(stage)} color={REMITTANCE_STAGE_COLORS[stage]} icon={icon} />;
}
