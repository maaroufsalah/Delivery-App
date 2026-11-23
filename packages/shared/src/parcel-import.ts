import type { ParcelImportRow } from './types/parcel';

/**
 * LA RELECTURE D'UN IMPORT : ANOMALIES EN TÊTE — lot J, J4.
 *
 * Sur un fichier de 500 lignes, les sept lignes fausses se cherchaient page par
 * page. Ces trois fonctions décident de ce que l'écran montre ; elles ne
 * lisent QUE ce que le serveur a rendu (`errors`, `warnings`), jamais une
 * règle de leur cru : le validateur reste unique (`ParcelImportService`).
 */

/** Une ligne est en ERREUR (non importable), en AVERTISSEMENT (à vérifier) ou valide. */
export type ParcelImportSeverity = 'error' | 'warning' | 'valid';

/** Le filtre de l'écran : « Toutes », « Erreurs », « Avertissements ». */
export type ParcelImportFilter = 'all' | 'errors' | 'warnings';

export function importRowSeverity(item: ParcelImportRow): ParcelImportSeverity {
  if (item.errors.length > 0) return 'error';
  if ((item.warnings?.length ?? 0) > 0) return 'warning';
  return 'valid';
}

const RANK: Record<ParcelImportSeverity, number> = { error: 0, warning: 1, valid: 2 };

/**
 * L'ordre d'affichage : erreurs, puis avertissements, puis lignes valides ;
 * à l'intérieur de chaque groupe, l'ordre du FICHIER. Rend des numéros de
 * ligne, pas des lignes : l'écran fige cet ordre à chaque relecture du
 * serveur, et une ligne corrigée garde sa place jusqu'à « Vérifier à
 * nouveau » — elle ne saute pas sous le curseur.
 */
export function importDisplayOrder(items: readonly ParcelImportRow[]): number[] {
  return [...items]
    .sort((a, b) => RANK[importRowSeverity(a)] - RANK[importRowSeverity(b)] || a.row - b.row)
    .map((item) => item.row);
}

/** « 7 erreurs · 3 avertissements · 490 valides » — en LIGNES, leur somme fait le fichier. */
export function importSeverityCounts(
  items: readonly ParcelImportRow[],
): Record<'errors' | 'warnings' | 'valid', number> {
  const counts = { errors: 0, warnings: 0, valid: 0 };
  for (const item of items) {
    const severity = importRowSeverity(item);
    if (severity === 'error') counts.errors += 1;
    else if (severity === 'warning') counts.warnings += 1;
    else counts.valid += 1;
  }
  return counts;
}

export function importRowMatchesFilter(item: ParcelImportRow, filter: ParcelImportFilter): boolean {
  if (filter === 'all') return true;
  return importRowSeverity(item) === (filter === 'errors' ? 'error' : 'warning');
}
