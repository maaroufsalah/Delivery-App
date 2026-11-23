import type { ParcelFilters } from '@delivery/shared';

/**
 * LES FILTRES DE LA LISTE DES COLIS, lus en UN seul endroit — lot D, D4.
 *
 * La page (liste et compteurs du haut) et la table (étiquettes de « toute la
 * liste ») recopiaient chacune leur liste de clés : le « statut financier »
 * manquait aux deux, et le filtre restait sans effet alors que l'URL le
 * portait. L'export, lui, recopie toute l'URL et filtrait déjà. Un filtre
 * ajouté à la barre s'ajoute ICI, et les trois lecteurs le reçoivent.
 *
 * La période n'y est pas : la page la résout (URL, puis mémoire, puis défaut)
 * et la table la tient de la fenêtre que l'API a rendue.
 */
export function parcelListFilters(read: (key: string) => string | undefined): ParcelFilters {
  const statusId = read('statusId');
  const merchantFinancial = read('merchantFinancialStatus');

  return {
    search: read('search'),
    // D-255 : `4` ou `4,5` — passé tel quel, l'API lit la liste.
    statusId: statusId || undefined,
    statusGroup: read('statusGroup'),
    statusIds: read('statusIds'),
    clientId: read('clientId'),
    destinationCityId: read('destinationCityId'),
    financialStatus: read('financialStatus') as ParcelFilters['financialStatus'],
    /* Le filtre financier du vendeur — lot O, D-227. */
    merchantFinancialStatus: merchantFinancial as ParcelFilters['merchantFinancialStatus'],
    /* Le filtre « Type » — chantier « Étiquette orange », point 2. */
    kind: read('kind') as ParcelFilters['kind'],
    /* D-257 : les colis supprimés (l'API ne l'honore que pour l'administrateur). */
    deleted: read('deleted') === 'true' ? 'true' : undefined,
  };
}
