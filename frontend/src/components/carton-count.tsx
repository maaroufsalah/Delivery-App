'use client';

import { cartonsOf, cartonTotal, showsCartons, type CartonLine } from '@delivery/shared';
import { Boxes } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { CellBadge } from '@/components/data-table/cells';

/**
 * LES CARTONS D'UN BON OU D'UN CIRCUIT — chantier « Nombre de cartons sur les
 * bons et circuits » (nuit du 11/11/2026).
 *
 * Deux pièces, pour les quatre fiches (bon BR/BD, circuit, RL-, BRT-) :
 *
 *  - `<CartonCell>` : le nombre de cartons d'UNE ligne. Une ligne reste un
 *    colis — rien n'est dédoublé ;
 *  - `<CartonTotal>` : « 3 expéditions · 6 cartons », en tête de la liste.
 *
 * La CELLULE ne rend rien quand le document ne porte aucun colis de plusieurs
 * cartons (`showsCartons`, la règle que le PDF lit aussi). Le TOTAL, lui,
 * s'affiche toujours depuis le libellé « expédition » (voir plus bas).
 */
export function CartonCell({
  line,
  lines,
}: {
  line: CartonLine;
  /** Toutes les lignes du document : c'est lui qui décide si la colonne existe. */
  lines: readonly CartonLine[];
}) {
  const t = useTranslations('common');
  if (!showsCartons(lines)) return null;

  return (
    <CellBadge
      icon={Boxes}
      label={t('cartonsCount', { count: cartonsOf(line) })}
      className="shrink-0 tabular-nums"
    />
  );
}

/**
 * LE TOTAL — libellé de Badr, 11/11/2026 : « 3 expéditions · 7 cartons », ou
 * « 3 expéditions » quand aucun colis n'a plusieurs cartons, « 1 expédition »
 * au singulier. Le MÊME libellé que le document ait des cartons ou non : le
 * total s'affiche donc toujours, seule sa seconde moitié dépend des cartons.
 */
export function CartonTotal({ lines }: { lines: readonly CartonLine[] }) {
  const t = useTranslations('common');
  if (lines.length === 0) return null;

  return (
    <span className="text-muted-foreground text-sm font-medium tabular-nums">
      {showsCartons(lines)
        ? t('cartonsTotal', { parcels: lines.length, cartons: cartonTotal(lines) })
        : t('shipmentsTotal', { count: lines.length })}
    </span>
  );
}
