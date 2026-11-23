import type { ColumnDef, RowData } from '@tanstack/react-table';
import {
  columnFilteringFeature,
  createColumnHelper,
  createFilteredRowModel,
  createPaginatedRowModel,
  createSortedRowModel,
  filterFn_equalsString,
  filterFn_includesString,
  globalFilteringFeature,
  rowSortingFeature,
  rowPaginationFeature,
  sortFn_alphanumeric,
  sortFn_basic,
  sortFn_datetime,
  sortFn_text,
  tableFeatures,
} from '@tanstack/react-table';

/**
 * Jeu de fonctionnalités unique pour TOUTES les tables de l'application.
 * TanStack Table v9 exige l'enregistrement explicite des features et des
 * fonctions de tri / filtre : les déclarer ici évite de les redéclarer par page
 * et garde le bundle minimal.
 */
export interface DataTableColumnMeta {
  /** Masque la colonne sous ce point de rupture (mobile-first). */
  hideBelow?: 'sm' | 'md' | 'lg' | 'xl';
  align?: 'start' | 'end' | 'center';
  /** Colonne d'actions : rendue au survol de la ligne sur pointeur fin. */
  isActions?: boolean;
  /** Largeur fixe en classes Tailwind, ex. `w-40`. */
  width?: string;
}

export const dataTableFeatures = tableFeatures({
  columnFilteringFeature,
  globalFilteringFeature,
  rowSortingFeature,
  rowPaginationFeature,
  filteredRowModel: createFilteredRowModel(),
  sortedRowModel: createSortedRowModel(),
  paginatedRowModel: createPaginatedRowModel(),
  filterFns: {
    includesString: filterFn_includesString,
    equalsString: filterFn_equalsString,
  },
  sortFns: {
    alphanumeric: sortFn_alphanumeric,
    basic: sortFn_basic,
    datetime: sortFn_datetime,
    text: sortFn_text,
  },
  columnMeta: {} as DataTableColumnMeta,
});

export type DataTableFeatures = typeof dataTableFeatures;

/** Helper typé à utiliser dans chaque `features/<domaine>/columns.tsx`. */
export function createDataTableColumnHelper<TData extends RowData>() {
  return createColumnHelper<DataTableFeatures, TData>();
}

/**
 * Type des colonnes acceptées par <DataTable>. Le `any` est imposé par la
 * signature de `ColumnHelper.columns` en v9, qui préserve le TValue de chaque
 * colonne dans un tuple — il ne se propage pas aux données.
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type DataTableColumns<TData extends RowData> = ColumnDef<DataTableFeatures, TData, any>[];
