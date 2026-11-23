'use client';

import { ClipboardCheck, Download, Loader2, Search, X } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { type CityChoice, CitySelect } from '@/components/city-select';
import { joinMulti, MultiSelect, splitMulti } from '@/components/multi-select';
import { type SearchableOption, SearchableSelect } from '@/components/searchable-select';
import { exportListAction } from '@/lib/actions/documents';
import { downloadBase64 } from '@/lib/download';
import { cn } from '@/lib/utils';

/**
 * Carte de filtres placée au-dessus de la table : recherche, listes déroulantes,
 * puis chips des filtres actifs. Même composition sur toutes les pages.
 */

export interface FilterOption {
  value: string;
  label: string;
  /** Pastille colorée (statuts, familles de statut). */
  color?: string;
}

export interface FilterSelect {
  id: string;
  label: string;
  value: string;
  options: FilterOption[];
  onChange: (value: string) => void;
  /**
   * Les VILLES derrière ce filtre — chantier « Retours recette 26/09 »,
   * point 2. Renseignées, la barre rend le `CitySelect` partagé (recherche
   * sans casse ni accents, nom arabe compris) à la place de la liste
   * déroulante ; `options` reste, c'est lui qui nourrit les pastilles actives.
   */
  cities?: readonly CityChoice[];
  /**
   * CHOIX MULTIPLE — D-255. Vrai : l'utilisateur coche plusieurs valeurs ;
   * `value` et `onChange` portent alors `"a,b"` (une seule : `"a"`, comme
   * avant ; aucune : `ALL_VALUE`). À ne poser que sur un filtre dont l'API
   * accepte la liste. Une pastille par valeur cochée.
   */
  multiple?: boolean;
}

/** Valeur sentinelle : Radix Select interdit une SelectItem de valeur vide. */
export const ALL_VALUE = '__all__';

/** Les villes en options, comme `CitySelect` les présente (nom arabe, code). */
function cityOptions(cities: readonly CityChoice[]): SearchableOption[] {
  return cities.map((city) => ({
    value: city.id,
    label: city.name,
    hint: city.nameAr,
    keywords: [city.nameAr, city.code],
  }));
}

/**
 * Filtre MIS EN AVANT — C13 bloc 2.
 *
 * Un bouton à bascule posé à côté de la recherche, et non une entrée de plus
 * dans une liste déroulante. La différence est celle entre « on peut filtrer
 * sur les demandes à valider » et « il y a trois demandes à valider » : le
 * second se voit sans ouvrir quoi que ce soit, et c'est tout l'intérêt.
 *
 * Il ne s'applique PAS tout seul au chargement. Une liste qui s'ouvre
 * amputée — même pour de bonnes raisons — fait chercher des fiches qui sont
 * pourtant là, et la première réaction est de croire à une perte de données.
 */
export interface ToolbarHighlight {
  label: string;
  /** Affiché en pastille. Zéro rend le bouton discret plutôt que de crier. */
  count?: number;
  active: boolean;
  onToggle: (next: boolean) => void;
}

export function DataTableToolbar({
  search,
  onSearchChange,
  searchPlaceholder,
  selects = [],
  highlight,
  onReset,
  exportResource,
  exportQuery,
  children,
}: {
  search: string;
  onSearchChange: (value: string) => void;
  searchPlaceholder?: string;
  selects?: FilterSelect[];
  /** Filtre mis en avant, à gauche des listes déroulantes (C13 bloc 2). */
  highlight?: ToolbarHighlight;
  onReset?: () => void;
  /**
   * Chemin de la ressource côté API (`parcels`, `cities`…). Renseigné, la barre
   * affiche « Exporter ». C'est le SEUL branchement qu'un écran a à faire : ni
   * colonnes, ni requête, ni téléchargement à réécrire.
   */
  exportResource?: string;
  /**
   * CE QUE L'URL NE PORTE PAS — chantier « Retours recette 26/09 », point 1.
   *
   * L'export rejoue l'URL ; un filtre résolu côté serveur (la période, lue en
   * cookie ou par défaut) n'y est pas forcément. L'écran le passe ici, et
   * l'URL garde le dernier mot : ce qui est affiché est ce qui s'exporte.
   */
  exportQuery?: Record<string, string>;
  children?: React.ReactNode;
}) {
  const t = useTranslations('table');
  const tCommon = useTranslations('common');

  /**
   * UN FILTRE SANS OPTION NE S'AFFICHE PAS — V2 vendeur bloc 1.
   *
   * La coque vendeur enveloppe les écrans du back-office (D-090), et ces
   * écrans portent un filtre « Client » que la portée serveur réduit à une
   * seule ligne — la sienne — voire à aucune. Le laisser rendait une liste
   * déroulante qui ne propose rien, sur quatre écrans à la fois.
   *
   * La règle est générale et n'est pas propre au vendeur : un filtre dont le
   * serveur ne rend aucune valeur n'a rien à faire dans une barre d'outils,
   * quel que soit le rôle qui la regarde.
   */
  const usable = selects.filter((select) => select.options.length > 0);

  // Une pastille par valeur : un filtre multiple en porte autant que de cases cochées (D-255).
  const activeChips = usable
    .filter((select) => select.value !== ALL_VALUE)
    .flatMap((select) => {
      const values = select.multiple ? splitMulti(select.value, ALL_VALUE) : [select.value];
      return values.map((value) => ({
        select,
        option: select.options.find((option) => option.value === value),
        clear: () =>
          select.onChange(
            select.multiple
              ? joinMulti(
                  values.filter((other) => other !== value),
                  ALL_VALUE,
                )
              : ALL_VALUE,
          ),
      }));
    })
    .filter(
      (chip): chip is { select: FilterSelect; option: FilterOption; clear: () => void } =>
        Boolean(chip.option),
    );

  const hasActive = activeChips.length > 0 || search.length > 0;

  return (
    /*
      LA RECHERCHE N'EST PLUS ÉCRASÉE — chantier « Affichage web et
      étiquette », point 5.

      La recherche et les filtres partageaient une ligne qui ne passait pas à
      la ligne (les filtres étaient dans un bloc à part) : sur un laptop, six
      filtres prenaient la place et le champ tombait à 50 px. Désormais la
      recherche et les filtres forment UNE rangée qui va à la ligne ; la
      recherche n'y descend jamais sous 280 px, et ce qui ne tient pas passe
      sur la deuxième ligne.
    */
    <Card className="dense:p-3 gap-3 p-4">
      <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center">
        <div className="relative w-full sm:w-auto sm:max-w-md sm:min-w-[280px] sm:flex-[1_1_280px]">
          <Search className="text-muted-foreground pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2" />
          <Input
            value={search}
            onChange={(event) => onSearchChange(event.target.value)}
            placeholder={searchPlaceholder ?? tCommon('searchPlaceholder')}
            aria-label={tCommon('search')}
            className="ps-9"
          />
        </div>

        {/* `sm:contents` : au-delà du mobile, les filtres sont des voisins
            directs de la recherche dans la même rangée qui va à la ligne. */}
        <div className="grid grid-cols-2 gap-2 sm:contents">
          {highlight ? (
            <Button
              type="button"
              size="sm"
              variant={highlight.active ? 'default' : 'outline'}
              aria-pressed={highlight.active}
              onClick={() => highlight.onToggle(!highlight.active)}
              className="col-span-2 h-9 justify-center gap-1.5 sm:col-span-1"
            >
              <ClipboardCheck className="size-4" aria-hidden />
              {highlight.label}
              {highlight.count ? (
                <Badge
                  variant={highlight.active ? 'secondary' : 'default'}
                  className="h-5 min-w-5 justify-center px-1 tabular-nums"
                >
                  {highlight.count}
                </Badge>
              ) : null}
            </Button>
          ) : null}

          {/* Lot J (J2) : chaque filtre est un menu AVEC RECHERCHE — le même
              composant partout ; la valeur émise (`ALL_VALUE`, un id) ne
              change pas, donc ni l'URL, ni la liste, ni l'export (D4). */}
          {usable.map((select) =>
            select.multiple ? (
              <MultiSelect
                key={select.id}
                options={select.cities ? cityOptions(select.cities) : select.options}
                value={select.value}
                onValueChange={select.onChange}
                allLabel={select.label}
                allValue={ALL_VALUE}
                searchPlaceholder={select.cities ? tCommon('citySearch') : undefined}
                emptyText={select.cities ? tCommon('cityEmpty') : undefined}
                aria-label={select.label}
                className="dense:min-w-40 w-full sm:w-auto sm:min-w-44"
              />
            ) : select.cities ? (
              <CitySelect
                key={select.id}
                cities={select.cities}
                value={select.value}
                onValueChange={select.onChange}
                emptyOption={{ value: ALL_VALUE, label: select.label }}
                aria-label={select.label}
                className="dense:min-w-40 w-full sm:w-auto sm:min-w-44"
              />
            ) : (
              <SearchableSelect
                key={select.id}
                options={select.options}
                value={select.value}
                onValueChange={select.onChange}
                emptyOption={{ value: ALL_VALUE, label: select.label }}
                aria-label={select.label}
                className="dense:min-w-40 w-full sm:w-auto sm:min-w-44"
              />
            ),
          )}

          {exportResource ? (
            <ExportButton resource={exportResource} search={search} extra={exportQuery} />
          ) : null}

          {children}
        </div>
      </div>

      {hasActive ? (
        <div className="flex flex-wrap items-center gap-2 border-t pt-3">
          <span className="text-muted-foreground text-xs">{t('activeFilters')}</span>

          {search ? <Chip label={`“${search}”`} onClear={() => onSearchChange('')} /> : null}

          {activeChips.map(({ select, option, clear }) => (
            <Chip
              key={`${select.id}:${option.value}`}
              label={option.label}
              color={option.color}
              onClear={clear}
            />
          ))}

          {onReset ? (
            <Button variant="ghost" size="sm" className="h-7 px-2 text-xs" onClick={onReset}>
              {t('clearFilters')}
            </Button>
          ) : null}
        </div>
      ) : null}
    </Card>
  );
}

/**
 * « Exporter » — le même bouton partout.
 *
 * Il n'a rien à savoir de l'écran : les filtres et le tri vivent déjà dans
 * l'URL (`useServerTable` les y écrit), et la recherche libre est portée par la
 * barre elle-même. L'export part donc avec exactement l'état affiché.
 *
 * Le classeur revient en base64 par une Server Action, comme les PDF : le
 * cookie de session est `httpOnly` sur l'origine du web, un lien direct vers
 * l'API partirait sans lui (D-018).
 */
function ExportButton({
  resource,
  search,
  extra,
}: {
  resource: string;
  search: string;
  extra?: Record<string, string>;
}) {
  const t = useTranslations('table');
  const searchParams = useSearchParams();
  const [pending, setPending] = useState(false);

  const run = async () => {
    const query: Record<string, string> = { ...extra };
    for (const [key, value] of searchParams.entries()) {
      // `page` n'a pas de sens pour un export : il les parcourt toutes.
      if (key !== 'page') query[key] = value;
    }
    if (search) query.search = search;

    setPending(true);
    const result = await exportListAction(resource, query);
    setPending(false);

    if (!result.ok) {
      toast.error(result.error === 'invalid' ? t('exportFailed') : result.error);
      return;
    }

    downloadBase64(
      result.data.file,
      `${resource}.xlsx`,
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    );
    // Le message de fin dit combien — Recette 5, point 8.
    if (result.data.rows !== null) toast.success(t('exported', { count: result.data.rows }));
  };

  return (
    <Button
      type="button"
      variant="outline"
      size="sm"
      onClick={run}
      disabled={pending}
      className="gap-1.5"
    >
      {pending ? (
        <Loader2 className="size-4 animate-spin" />
      ) : (
        <Download className="size-4" aria-hidden />
      )}
      <span className="hidden sm:inline">{t('export')}</span>
    </Button>
  );
}

function Chip({ label, color, onClear }: { label: string; color?: string; onClear: () => void }) {
  const tCommon = useTranslations('common');

  return (
    <Badge variant="secondary" className={cn('gap-1.5 rounded-full ps-2.5 pe-1 font-normal')}>
      {color ? <span className="size-2 rounded-full" style={{ backgroundColor: color }} /> : null}
      <span className="max-w-40 truncate">{label}</span>
      <button
        type="button"
        onClick={onClear}
        aria-label={`${tCommon('reset')}, ${label}`}
        className="hover:bg-foreground/10 rounded-full p-0.5"
      >
        <X className="size-3" />
      </button>
    </Badge>
  );
}
