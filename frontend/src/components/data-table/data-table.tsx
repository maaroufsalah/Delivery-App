'use client';

import { ArrowDown, ArrowUp, ChevronRight, ChevronsUpDown, Inbox } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { type RowData, type SortingState, useTable } from '@tanstack/react-table';
import { useState } from 'react';
import { Checkbox } from '@/components/ui/checkbox';
import { DataTablePagination } from '@/components/data-table/data-table-pagination';
import {
  type DataTableColumnMeta,
  type DataTableColumns,
  dataTableFeatures,
} from '@/components/data-table/table-features';
import { cn } from '@/lib/utils';

/**
 * LA table de l'application. Aucune page ne redéclare de `<table>`.
 *
 * Elle fournit : en-tête coloré, tri visible, masquage responsive par colonne,
 * survol de ligne avec actions, état vide et pagination compacte.
 * Les filtres et les KPI sont passés en `toolbar` / `kpis` pour rester
 * spécifiques au domaine sans dupliquer la mécanique.
 */

const HIDE_BELOW: Record<NonNullable<DataTableColumnMeta['hideBelow']>, string> = {
  sm: 'hidden sm:table-cell',
  md: 'hidden md:table-cell',
  lg: 'hidden lg:table-cell',
  xl: 'hidden xl:table-cell',
};

const ALIGN: Record<NonNullable<DataTableColumnMeta['align']>, string> = {
  start: 'text-start',
  center: 'text-center',
  end: 'text-end',
};

function cellClasses(meta: DataTableColumnMeta | undefined) {
  return cn(
    meta?.hideBelow ? HIDE_BELOW[meta.hideBelow] : undefined,
    meta?.align ? ALIGN[meta.align] : 'text-start',
    meta?.width,
  );
}

/**
 * Pagination et tri décidés par le SERVEUR.
 *
 * Tant que les données étaient mockées, la table chargeait tout et découpait
 * dans le navigateur. Sur les 10 164 quartiers de la prod (DOMAIN.md §1.7),
 * cela transfère dix mille lignes pour en montrer vingt-cinq. Quand cette
 * propriété est fournie, `data` contient DÉJÀ la page demandée : la table ne
 * trie plus, ne pagine plus, et `total` lui dit seulement où elle en est.
 *
 * Un seul `onChange` pour les quatre valeurs : elles vivent ensemble dans
 * l'URL, et les changer une par une provoquerait autant d'allers-retours.
 */
export interface DataTableServerState {
  /** 1-based, comme dans l'URL — l'index 0-based reste interne à la table. */
  page: number;
  pageSize: number;
  total: number;
  sort: string;
  order: 'asc' | 'desc';
  onChange: (next: {
    page?: number;
    pageSize?: number;
    sort?: string;
    order?: 'asc' | 'desc';
  }) => void;
}

/**
 * LA SÉLECTION DE LIGNES — V2 vendeur bloc 3.
 *
 * **Ici et pas dans une table de domaine.** La règle n°2 de CLAUDE.md ne
 * souffre pas d'exception : `data-table.tsx` est la seule table de
 * l'application. Une case à cocher écrite dans `parcels-table.tsx` aurait été
 * recopiée dans les bons au bloc suivant, puis dans le stock, et les trois
 * auraient divergé sur le détail qui compte — ce que « tout sélectionner »
 * sélectionne quand la liste est paginée par le serveur.
 *
 * **Elle ne porte QUE la page affichée**, et c'est le choix explicite. Cocher
 * l'en-tête sélectionne les vingt-cinq lignes visibles, pas les huit cents de
 * la sélection filtrée : promettre l'inverse demanderait d'aller chercher les
 * identifiants côté serveur, et l'utilisateur téléchargerait sans savoir quoi.
 * Le compteur dit toujours combien de lignes sont réellement retenues.
 *
 * La sélection est portée par l'APPELANT : c'est lui qui sait ce qu'il en fait,
 * et lui qui doit pouvoir la vider après un téléchargement réussi.
 */
export interface DataTableSelection<TData> {
  /** L'identifiant stable d'une ligne — un code, un id. */
  keyOf: (row: TData) => string;
  selected: readonly string[];
  onChange: (next: string[]) => void;
  /** Les gestes proposés, rendus dans la barre qui apparaît sous la sélection. */
  actions: React.ReactNode;
  /**
   * « X cochés / Y » — D-176 (lot B, point B5). `total` : la liste ENTIÈRE
   * (toutes pages) ; `count` remplace le nombre de clés quand l'écran a coché
   * « toute la liste », qui ne tient pas dans `selected`.
   */
  total?: number;
  count?: number;
}

/**
 * LES LIGNES REGROUPÉES — D-252 (petite caisse, tableau unique). Les lignes
 * de la page, dans l'ordre reçu, se rangent en groupes CONSÉCUTIFS de même
 * clé ; chaque groupe s'ouvre par une ligne d'en-tête pleine largeur, que
 * l'écran remplit (ses chiffres, ses boutons). Un clic sur l'en-tête plie ou
 * déplie le groupe. L'état plié appartient à l'écran : c'est lui qui sait
 * quel groupe s'ouvre par défaut.
 */
export interface DataTableGroups<TData> {
  keyOf: (row: TData) => string;
  /** Le contenu de l'en-tête du groupe, à droite du chevron. */
  header: (key: string, rows: TData[], open: boolean) => React.ReactNode;
  isOpen: (key: string) => boolean;
  onToggle: (key: string) => void;
  /** Libellé accessible du chevron. */
  toggleLabel: (key: string, open: boolean) => string;
}

export interface DataTableProps<TData extends RowData> {
  data: TData[];
  columns: DataTableColumns<TData>;
  /** Carte de filtres rendue au-dessus de la table. */
  toolbar?: React.ReactNode;
  /** Cartes KPI rendues en tête de page. */
  kpis?: React.ReactNode;
  initialSorting?: SortingState;
  pageSize?: number;
  /** Recherche globale pilotée par la barre de filtres du domaine. */
  globalFilter?: string;
  /** Fourni = pagination et tri côté serveur ; absent = tout reste local. */
  server?: DataTableServerState;
  onRowClick?: (row: TData) => void;
  emptyTitle?: string;
  emptyHint?: string;
  /**
   * Variante dense, sans pagination : pour un extrait posé dans une autre page
   * (les 10 derniers colis du tableau de bord). La pagination n'a pas de sens
   * sur un extrait, et le pas vertical y est plus serré.
   */
  compact?: boolean;
  /** Fourni = une colonne de cases à cocher s'ajoute en tête (V2 vendeur bloc 3). */
  selection?: DataTableSelection<TData>;
  /**
   * LA PAGE QUI NE SE PERD PLUS — lot J, J3. Pagination locale seulement.
   *
   * Absent, la table garde le comportement de TanStack : toute nouvelle
   * `data` ramène en page 1 (`autoResetPageIndex`), ce que veulent les écrans
   * qui FILTRENT en remplaçant `data` (grille des villes d'un livreur).
   *
   * Fourni, une nouvelle `data` est une MISE À JOUR des mêmes lignes — une
   * cellule corrigée, une relecture revenue du serveur — et la page reste ;
   * seuls un changement de cette clé (un filtre de l'écran), de la recherche
   * ou du tri ramènent en page 1. Une page devenue trop loin (moins de
   * lignes) recule à la dernière.
   */
  pageResetKey?: string;
  /** Identité stable d'une ligne : sans elle, c'est son rang. */
  getRowId?: (row: TData) => string;
  /**
   * LA LIGNE DES TOTAUX — lot M (stock livreur). Une cellule par id de
   * colonne, alignée et masquée comme sa colonne (`meta`) ; absente, rien ne
   * s'affiche. Les totaux sont ceux de la liste ENTIÈRE, calculés par l'API.
   */
  footer?: Partial<Record<string, React.ReactNode>>;
  /**
   * LA BULLE AU SURVOL DE LA LIGNE — chantier « Affichage web et étiquette »,
   * point 3. Un texte par ligne, calculé sur les données DÉJÀ chargées : aucune
   * requête au survol. Bulle native (`title`) : elle suit la ligne entière,
   * sans monter un composant flottant par ligne sur une page de 100 colis.
   */
  getRowTitle?: (row: TData) => string | undefined;
  /** Fourni = les lignes se rangent sous des en-têtes de groupe repliables. */
  groups?: DataTableGroups<TData>;
}

/**
 * LE PAS D'UNE LIGNE — chantier « Affichage web et étiquette », point 5. Sous
 * 1700 px (`dense:`), la cellule se resserre : de 63 à environ 44 px par ligne
 * sur un laptop, deux fois plus de lignes visibles sans défiler.
 */
const CELL_PADDING = 'px-4 py-3 dense:px-3 dense:py-1.5';

export function DataTable<TData extends RowData>({
  data,
  columns,
  toolbar,
  kpis,
  initialSorting = [],
  pageSize = 10,
  globalFilter = '',
  server,
  onRowClick,
  emptyTitle,
  emptyHint,
  compact = false,
  selection,
  pageResetKey,
  getRowId,
  footer,
  getRowTitle,
  groups,
}: DataTableProps<TData>) {
  const t = useTranslations('table');
  const [sorting, setSorting] = useState<SortingState>(initialSorting);

  /*
   * Lot J, J3 : la page tenue ICI quand l'écran donne `pageResetKey`. Le
   * retour en page 1 se décide pendant le rendu, sur la clé vue au rendu
   * précédent — pas dans un effet, qui montrerait d'abord la mauvaise page.
   */
  const stablePage = !server && pageResetKey !== undefined;
  const [pagination, setPagination] = useState({ pageIndex: 0, pageSize });
  const resetKey = `${pageResetKey ?? ''}\u0000${globalFilter}`;
  const [seenResetKey, setSeenResetKey] = useState(resetKey);
  if (stablePage && seenResetKey !== resetKey) {
    setSeenResetKey(resetKey);
    if (pagination.pageIndex !== 0) setPagination({ ...pagination, pageIndex: 0 });
  }

  const serverSorting: SortingState = server?.sort
    ? [{ id: server.sort, desc: server.order === 'desc' }]
    : [];

  const serverPagination = server
    ? { pageIndex: server.page - 1, pageSize: server.pageSize }
    : undefined;

  /*
   * v9 passe soit une valeur, soit une fonction de mise à jour : les deux
   * doivent être résolues, sinon un clic sur « suivant » ne fait rien.
   */
  const handleSortingChange = (updater: SortingState | ((old: SortingState) => SortingState)) => {
    if (!server) {
      setSorting(updater);
      // Même règle que côté serveur : un autre ordre repart du début.
      if (stablePage) setPagination((old) => ({ ...old, pageIndex: 0 }));
      return;
    }

    const next = typeof updater === 'function' ? updater(serverSorting) : updater;
    const first = next[0];
    if (!first) return;

    // Un changement de tri renvoie en page 1 : rester en page 7 d'un autre
    // ordre montrerait des lignes sans rapport avec ce qui vient d'être cliqué.
    server.onChange({ sort: first.id, order: first.desc ? 'desc' : 'asc', page: 1 });
  };

  const handlePaginationChange = (
    updater:
      | { pageIndex: number; pageSize: number }
      | ((old: { pageIndex: number; pageSize: number }) => { pageIndex: number; pageSize: number }),
  ) => {
    if (stablePage) {
      setPagination((old) => (typeof updater === 'function' ? updater(old) : updater));
      return;
    }

    if (!server || !serverPagination) return;

    const next = typeof updater === 'function' ? updater(serverPagination) : updater;

    server.onChange(
      next.pageSize === serverPagination.pageSize
        ? { page: next.pageIndex + 1 }
        : // Changer la taille de page en restant en page 12 ferait sauter
          // au-delà du total : on repart du début.
          { pageSize: next.pageSize, page: 1 },
    );
  };

  const table = useTable({
    features: dataTableFeatures,
    data,
    columns,
    state: server
      ? { sorting: serverSorting, globalFilter: '', pagination: serverPagination }
      : stablePage
        ? { sorting, globalFilter, pagination }
        : { sorting, globalFilter },
    onSortingChange: handleSortingChange,
    ...(server
      ? {
          onPaginationChange: handlePaginationChange,
          rowCount: server.total,
          // `data` est déjà la page triée : re-trier et re-découper ici
          // produirait un tri de 25 lignes au lieu du tri de la table entière.
          manualPagination: true,
          manualSorting: true,
          manualFiltering: true,
        }
      : stablePage
        ? { onPaginationChange: handlePaginationChange, autoResetPageIndex: false }
        : { initialState: { pagination: { pageIndex: 0, pageSize } } }),
    ...(getRowId ? { getRowId: (row: TData) => getRowId(row) } : {}),
    globalFilterFn: 'includesString',
    enableSortingRemoval: false,
  });

  // Moins de lignes qu'avant (un filtre, une relecture) : la page recule à la
  // dernière qui existe, au lieu d'afficher « aucun résultat » en page 9.
  if (stablePage) {
    const count = table.getPrePaginatedRowModel().rows.length;
    const last = Math.max(0, Math.ceil(count / pagination.pageSize) - 1);
    if (pagination.pageIndex > last) setPagination({ ...pagination, pageIndex: last });
  }

  const rows = table.getRowModel().rows;
  const columnCount = table.getAllLeafColumns().length + (selection ? 1 : 0);

  /*
   * Les groupes consécutifs de la page : `[clé, lignes]`, dans l'ordre reçu.
   * Sans `groups`, un seul groupe sans en-tête — le rendu d'avant.
   */
  const sections: Array<{ key: string | null; rows: typeof rows }> = [];
  for (const row of rows) {
    const key = groups ? groups.keyOf(row.original) : null;
    const last = sections[sections.length - 1];
    if (last && last.key === key) last.rows.push(row);
    else sections.push({ key, rows: [row] });
  }

  /*
   * « Tout sélectionner » ne parle que de la PAGE. Un `onChange` qui ajoute ou
   * retire les clés visibles préserve ce qui a été coché sur une autre page :
   * remplacer la liste entière ferait perdre une sélection en tournant la
   * page, ce qui est la surprise la plus coûteuse de cet écran.
   */
  const pageKeys = selection ? rows.map((row) => selection.keyOf(row.original)) : [];
  const allPageSelected =
    pageKeys.length > 0 && pageKeys.every((key) => selection?.selected.includes(key));

  const togglePage = (checked: boolean) => {
    if (!selection) return;

    selection.onChange(
      checked
        ? [...new Set([...selection.selected, ...pageKeys])]
        : selection.selected.filter((key) => !pageKeys.includes(key)),
    );
  };

  const toggleRow = (key: string, checked: boolean) => {
    if (!selection) return;

    selection.onChange(
      checked
        ? [...new Set([...selection.selected, key])]
        : selection.selected.filter((current) => current !== key),
    );
  };

  return (
    <div className="dense:gap-3 flex flex-col gap-4">
      {kpis}
      {toolbar}

      {/* En mode compact, la table est déjà posée dans une Card : pas de
          seconde bordure ni de second arrondi. */}
      <div className={cn('overflow-hidden', !compact && 'bg-card rounded-lg border')}>
        {/*
          `relative` : les libellés `sr-only` des actions (position absolue)
          prenaient pour repère un ancêtre HORS de la zone défilante, et
          élargissaient la page de 100 à 300 px sur Colis (chantier
          « Affichage », point 5). Repérés ici, ils sont rognés avec le reste.
        */}
        <div className="relative overflow-x-auto">
          <table className="w-full caption-bottom border-collapse text-sm">
            <thead className="bg-table-header text-table-header-foreground">
              {table.getHeaderGroups().map((headerGroup) => (
                <tr key={headerGroup.id}>
                  {selection ? (
                    <th scope="col" className={cn('w-10', compact ? 'px-3 py-2' : CELL_PADDING)}>
                      <Checkbox
                        checked={allPageSelected}
                        onCheckedChange={(checked) => togglePage(checked === true)}
                        aria-label={t('selectPage')}
                      />
                    </th>
                  ) : null}
                  {headerGroup.headers.map((header) => {
                    const meta = header.column.columnDef.meta;
                    const sorted = header.column.getIsSorted();
                    const canSort = header.column.getCanSort();

                    return (
                      <th
                        key={header.id}
                        scope="col"
                        colSpan={header.colSpan}
                        aria-sort={
                          sorted === 'asc'
                            ? 'ascending'
                            : sorted === 'desc'
                              ? 'descending'
                              : undefined
                        }
                        className={cn(
                          'text-xs font-semibold tracking-wide whitespace-nowrap uppercase',
                          compact ? 'px-3 py-2' : CELL_PADDING,
                          cellClasses(meta),
                        )}
                      >
                        {header.isPlaceholder ? null : canSort ? (
                          <button
                            type="button"
                            onClick={header.column.getToggleSortingHandler()}
                            title={sorted === 'asc' ? t('sortDesc') : t('sortAsc')}
                            className={cn(
                              'inline-flex items-center gap-1.5 rounded outline-none',
                              'focus-visible:ring-primary-foreground/60 hover:opacity-80 focus-visible:ring-2',
                              meta?.align === 'end' && 'flex-row-reverse',
                            )}
                          >
                            <table.FlexRender header={header} />
                            {sorted === 'asc' ? (
                              <ArrowUp className="size-3.5" />
                            ) : sorted === 'desc' ? (
                              <ArrowDown className="size-3.5" />
                            ) : (
                              <ChevronsUpDown className="size-3.5 opacity-50" />
                            )}
                          </button>
                        ) : (
                          <table.FlexRender header={header} />
                        )}
                      </th>
                    );
                  })}
                </tr>
              ))}
            </thead>

            <tbody>
              {rows.length === 0 ? (
                <tr>
                  <td
                    colSpan={table.getAllLeafColumns().length + (selection ? 1 : 0)}
                    className="px-4 py-16"
                  >
                    <div className="flex flex-col items-center gap-2 text-center">
                      <Inbox className="text-muted-foreground/60 size-8" />
                      <p className="text-sm font-medium">{emptyTitle ?? t('noResults')}</p>
                      <p className="text-muted-foreground text-xs">
                        {emptyHint ?? t('noResultsHint')}
                      </p>
                    </div>
                  </td>
                </tr>
              ) : (
                sections.flatMap((section) => {
                  const open = section.key === null || !groups || groups.isOpen(section.key);
                  const header =
                    section.key !== null && groups ? (
                      <tr
                        key={`group-${section.key}`}
                        className="bg-muted/40 border-table-border cursor-pointer border-t-2"
                        // Toute la ligne plie / déplie, sauf un clic sur un de ses
                        // gestes, ou venu d'un dialogue ouvert depuis elle (portail
                        // React : l'évènement remonte l'arbre React, pas le DOM).
                        onClick={(event) => {
                          const target = event.target as HTMLElement;
                          if (!event.currentTarget.contains(target)) return;
                          if (target.closest('button, a, input, textarea, [role="menuitem"]'))
                            return;
                          groups.onToggle(section.key!);
                        }}
                      >
                        <td colSpan={columnCount} className={cn('align-middle', CELL_PADDING)}>
                          <div className="flex items-start gap-2">
                            <button
                              type="button"
                              onClick={() => groups.onToggle(section.key!)}
                              aria-expanded={open}
                              aria-label={groups.toggleLabel(section.key, open)}
                              className="hover:bg-muted mt-0.5 shrink-0 rounded p-0.5"
                            >
                              <ChevronRight
                                className={cn(
                                  'size-4 transition-transform',
                                  open ? 'rotate-90' : 'flip-rtl',
                                )}
                              />
                            </button>
                            <div className="min-w-0 flex-1">
                              {groups.header(
                                section.key,
                                section.rows.map((row) => row.original),
                                open,
                              )}
                            </div>
                          </div>
                        </td>
                      </tr>
                    ) : null;
                  return [
                    header,
                    ...(open ? section.rows : []).map((row) => (
                      <tr
                        key={row.id}
                        title={getRowTitle?.(row.original)}
                        onClick={onRowClick ? () => onRowClick(row.original) : undefined}
                        className={cn(
                          'group/row hover:bg-table-row-hover/60 border-table-border border-t transition-colors',
                          onRowClick && 'cursor-pointer',
                        )}
                      >
                        {selection ? (
                          /*
                        `stopPropagation` : la ligne entière est cliquable sur
                        cet écran, et cocher une case ne doit pas ouvrir la
                        fiche du colis qu'on voulait justement sélectionner.
                      */
                          <td
                            className={cn('align-middle', compact ? 'px-3 py-2' : CELL_PADDING)}
                            onClick={(event) => event.stopPropagation()}
                          >
                            <Checkbox
                              checked={selection.selected.includes(selection.keyOf(row.original))}
                              onCheckedChange={(checked) =>
                                toggleRow(selection.keyOf(row.original), checked === true)
                              }
                              aria-label={t('selectRow')}
                            />
                          </td>
                        ) : null}
                        {row.getAllCells().map((cell) => (
                          <td
                            key={cell.id}
                            className={cn(
                              'align-middle',
                              compact ? 'px-3 py-2' : CELL_PADDING,
                              cellClasses(cell.column.columnDef.meta),
                            )}
                          >
                            <table.FlexRender cell={cell} />
                          </td>
                        ))}
                      </tr>
                    )),
                  ];
                })
              )}
            </tbody>

            {footer && rows.length > 0 ? (
              <tfoot className="bg-muted/50 border-table-border border-t-2 font-semibold">
                <tr>
                  {selection ? <td className={compact ? 'px-3 py-2' : CELL_PADDING} /> : null}
                  {table.getAllLeafColumns().map((column) => (
                    <td
                      key={column.id}
                      className={cn(
                        'align-middle tabular-nums',
                        compact ? 'px-3 py-2' : CELL_PADDING,
                        cellClasses(column.columnDef.meta),
                      )}
                    >
                      {footer[column.id] ?? null}
                    </td>
                  ))}
                </tr>
              </tfoot>
            ) : null}
          </table>
        </div>

        {compact ? null : <DataTablePagination table={table} />}
      </div>

      {/*
        LA BARRE DE SÉLECTION, sous la table et non flottante au-dessus : une
        barre en surimpression masque la dernière ligne, c'est-à-dire celle
        qu'on vient de cocher.
      */}
      {selection && selection.selected.length > 0 ? (
        <div className="bg-card flex flex-wrap items-center gap-3 rounded-lg border p-3">
          <span className="text-sm font-medium tabular-nums">
            {selection.total === undefined
              ? t('selectedCount', { count: selection.selected.length })
              : t('selectedOfTotal', {
                  count: selection.count ?? selection.selected.length,
                  total: String(selection.total),
                })}
          </span>
          <button
            type="button"
            onClick={() => selection.onChange([])}
            className="text-muted-foreground hover:text-foreground text-xs underline-offset-2 hover:underline"
          >
            {t('clearSelection')}
          </button>
          <span className="flex-1" />
          {selection.actions}
        </div>
      ) : null}
    </div>
  );
}
