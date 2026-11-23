import { cookies } from 'next/headers';
import type { DashboardPeriod } from '@delivery/shared';
import {
  DASHBOARD_FILTERS_COOKIE,
  type DashboardFilterValues,
  parseDashboardFilters,
  parsePeriod,
  parseRange,
  periodCookieNames,
  type PeriodMemory,
} from '@/lib/dashboard-period';

/**
 * Les filtres complémentaires mémorisés (agence, ville, livreur, client) —
 * chantier « Dashboard de pilotage ». Même frontière que la période : lus ici,
 * côté serveur, pour que le premier appel API porte déjà les bons filtres.
 */
export async function readDashboardFilterPreference(): Promise<DashboardFilterValues> {
  const store = await cookies();
  const raw = store.get(DASHBOARD_FILTERS_COOKIE)?.value;
  return parseDashboardFilters(raw ? safeDecode(raw) : undefined);
}

function safeDecode(value: string): string {
  try {
    return decodeURIComponent(value);
  } catch {
    return '';
  }
}

/**
 * Lecture serveur du dernier choix de période — chantier « Filtre de période ».
 *
 * Séparé de `dashboard-period.ts` parce que `next/headers` ne peut pas être
 * importé par un composant client, et que l'autre fichier l'est. Appelée par
 * les pages de tableau de bord, qui résolvent la période effective (URL ??
 * cookie ?? défaut) AVANT d'appeler l'API, pour que le premier pixel soit le
 * bon.
 */
export async function readPeriodPreference(memory: PeriodMemory = 'dashboard'): Promise<{
  period: DashboardPeriod | null;
  range: { from: string; to: string } | null;
}> {
  const store = await cookies();
  const names = periodCookieNames(memory);

  return {
    period: parsePeriod(store.get(names.period)?.value),
    range: parseRange(store.get(names.range)?.value),
  };
}
