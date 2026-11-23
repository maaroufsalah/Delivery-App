import type { useFormatter } from 'next-intl';
import type { KpiTrend } from '@/components/kpi-card';

/**
 * L'ÉVOLUTION D'UN KPI — chantier « Dashboard de pilotage », spec § 14.
 *
 * `(actuel − précédent) / précédent`, et les cas limites nommés plutôt que
 * devinés : pas de période précédente (`all`) → rien ; précédent nul et actuel
 * nul → « = » ; précédent nul et actuel non nul → « nouveau » (une division par
 * zéro n'est pas « +∞ % »). Les TAUX (0..1) se comparent en POINTS, pas en
 * pourcentage de pourcentage : passer de 80 % à 90 % est « +10 pts ».
 *
 * PURE : évaluée côté serveur comme côté client, avec le `format` de next-intl.
 */
/** Le formateur de next-intl, serveur (`getFormatter`) comme client (`useFormatter`). */
export type TrendFormat = Pick<ReturnType<typeof useFormatter>, 'number'>;

export interface TrendLabels {
  /** « nouveau » : rien avant, quelque chose maintenant. */
  fresh: string;
  /** « pts » : l'unité d'un écart de taux. */
  points: string;
  /** « vs 01/02 – 18/02 ». */
  hint: string;
}

export function kpiTrend(
  current: number,
  previous: number | null | undefined,
  options: {
    /** Vrai quand une HAUSSE est une mauvaise nouvelle (retours, échecs, délais). */
    invert?: boolean;
    /** Vrai pour un ratio 0..1 : l'écart s'affiche en points. */
    rate?: boolean;
    /** Neutre : ni bon ni mauvais (volume total). */
    neutral?: boolean;
  },
  format: TrendFormat,
  labels: TrendLabels,
): KpiTrend | undefined {
  if (previous === null || previous === undefined) return undefined;

  const delta = current - previous;
  const direction = delta > 0 ? 'up' : delta < 0 ? 'down' : 'flat';
  const positive = options.neutral || direction === 'flat' ? null : (delta > 0) !== !!options.invert;

  let text: string;
  if (options.rate) {
    // En points de pourcentage, avec les chiffres de la locale (l'arabe n'écrit
    // pas « % » de la même façon, donc pas de remplacement de texte).
    const pts = format.number(delta * 100, { maximumFractionDigits: 1 });
    text = `${delta > 0 ? '+' : ''}${pts} ${labels.points}`;
  } else if (previous === 0) {
    text = current === 0 ? '=' : labels.fresh;
  } else {
    const ratio = delta / Math.abs(previous);
    text = `${ratio > 0 ? '+' : ''}${format.number(ratio, 'percent')}`;
  }

  return { text, direction, positive, hint: labels.hint };
}
