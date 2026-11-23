'use client';

import { type City, searchCities, type Zone } from '@delivery/shared';
import { Search, X } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useMemo, useState } from 'react';
import { CellMuted, CellStack } from '@/components/data-table/cells';
import { DataTable } from '@/components/data-table/data-table';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { SearchableSelect } from '@/components/searchable-select';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';

const ALL_ZONES = '__all__';
/** Au-delà, les puces se replient : une grille « tout coché » compte 325 villes. */
const CHIPS_FOLDED = 30;

const helper = createDataTableColumnHelper<City>();

/**
 * COCHER DES VILLES DANS UN TABLEAU — lot F (F1, D-206), mis en commun au lot K
 * (D-218).
 *
 * Né pour les villes d'un livreur, repris tel quel pour les villes visées par
 * une remise client : un seul tableau de cases dans l'application.
 *
 * - Recherche sans casse ni accents (`searchCities` : nom, arabe, code).
 * - Filtre Zone, par le menu avec recherche (J2, D-216).
 * - « Tout cocher (N) » / « Tout décocher (N) » portent sur le RÉSULTAT FILTRÉ :
 *   cocher les vingt villes d'une zone, c'est choisir la zone puis un clic.
 * - **Une ville cochée reste visible**, cochée, à sa place.
 * - Lot N2 (D-225, option A) : les cochées en PUCES au-dessus du tableau,
 *   toutes, quel que soit le filtre (× pour décocher, repliées au-delà de 30),
 *   et la case « Cochées seulement ». Un changement de filtre ne perd rien : la
 *   sélection est à l'appelant ; les puces le rendent visible.
 *
 * La sélection appartient à l'appelant (`selected`, `onChange`) : la grille du
 * livreur y attache ses montants, la remise ne garde que les identifiants.
 * `extraColumns` s'ajoutent après Ville et Zone.
 */
export function CityChecklist({
  cities,
  zones,
  selected,
  onChange,
  checkHeader,
  extraColumns,
}: {
  cities: readonly City[];
  zones: readonly Zone[];
  selected: ReadonlySet<string>;
  /** Coche (`true`) ou décoche les villes données — une, ou tout le résultat filtré. */
  onChange: (cityIds: string[], checked: boolean) => void;
  /** En-tête de la colonne des cases : « Servie », « Visée »… */
  checkHeader: string;
  extraColumns?: DataTableColumns<City>;
}) {
  const t = useTranslations('cityChecklist');
  const [search, setSearch] = useState('');
  const [zone, setZone] = useState(ALL_ZONES);
  const [onlyChecked, setOnlyChecked] = useState(false);
  const [allChips, setAllChips] = useState(false);

  const zoneName = useMemo(() => new Map(zones.map((entry) => [entry.id, entry.name])), [zones]);

  const filtered = useMemo(
    () =>
      searchCities(cities, search).filter(
        (city) =>
          (zone === ALL_ZONES || city.zoneId === zone) && (!onlyChecked || selected.has(city.id)),
      ),
    [cities, search, zone, onlyChecked, selected],
  );

  // Les cochées, TOUTES, quel que soit le filtre : c'est ce que le tableau ne montrait pas.
  const checked = useMemo(
    () =>
      cities
        .filter((city) => selected.has(city.id))
        .sort((a, b) => a.name.localeCompare(b.name, 'fr')),
    [cities, selected],
  );
  const chips = allChips ? checked : checked.slice(0, CHIPS_FOLDED);

  const checkedInView = filtered.filter((city) => selected.has(city.id)).length;

  const columns: DataTableColumns<City> = useMemo(
    () => [
      ...helper.columns([
        helper.display({
          id: 'checked',
          header: checkHeader,
          meta: { width: 'w-16' },
          cell: (info) => {
            const city = info.row.original;
            return (
              <Checkbox
                checked={selected.has(city.id)}
                aria-label={city.name}
                onCheckedChange={(checked) => onChange([city.id], checked === true)}
              />
            );
          },
        }),
        helper.accessor('name', {
          header: t('city'),
          cell: (info) => <CellStack label={info.getValue()} sub={info.row.original.code} />,
        }),
        helper.accessor('zoneId', {
          header: t('zone'),
          meta: { hideBelow: 'md' },
          cell: (info) => <CellMuted>{zoneName.get(info.getValue()) ?? '-'}</CellMuted>,
        }),
      ]),
      ...(extraColumns ?? []),
    ],
    [checkHeader, extraColumns, onChange, selected, t, zoneName],
  );

  return (
    <div className="flex flex-col gap-3">
      {/* Lot N2 (D-225, option A) : la sélection, visible au-dessus du tableau, décochable. */}
      {checked.length > 0 ? (
        <div className="bg-muted/40 flex flex-col gap-2 rounded-lg border p-3">
          <p className="text-sm font-medium">{t('checked', { count: String(checked.length) })}</p>
          <ul
            className="flex flex-wrap gap-1.5"
            aria-label={t('checked', { count: String(checked.length) })}
          >
            {chips.map((city) => (
              <li key={city.id}>
                <button
                  type="button"
                  onClick={() => onChange([city.id], false)}
                  aria-label={t('uncheck', { city: city.name })}
                  className="bg-primary/10 text-foreground hover:bg-primary/20 focus-visible:ring-ring inline-flex items-center gap-1 rounded-full border py-0.5 ps-2.5 pe-1.5 text-xs focus-visible:ring-2 focus-visible:outline-none"
                >
                  {city.name}
                  <X className="size-3.5" aria-hidden />
                </button>
              </li>
            ))}
            {checked.length > CHIPS_FOLDED ? (
              <li>
                <Button
                  type="button"
                  size="sm"
                  variant="ghost"
                  className="h-6 px-2 text-xs"
                  onClick={() => setAllChips((open) => !open)}
                >
                  {allChips
                    ? t('chipsLess')
                    : t('chipsMore', { count: String(checked.length - CHIPS_FOLDED) })}
                </Button>
              </li>
            ) : null}
          </ul>
        </div>
      ) : null}

      <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
        <div className="relative flex-1">
          <Search className="text-muted-foreground pointer-events-none absolute start-2.5 top-2.5 size-4" />
          <Input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t('search')}
            aria-label={t('search')}
            className="ps-8"
          />
        </div>
        <SearchableSelect
          options={zones.map((entry) => ({ value: entry.id, label: entry.name }))}
          value={zone}
          onValueChange={setZone}
          placeholder={t('zone')}
          emptyOption={{ value: ALL_ZONES, label: t('allZones') }}
          className="sm:w-56"
          aria-label={t('zone')}
        />
      </div>

      {/* `type="button"` : la liste vit aussi dans un formulaire (remise), où un bouton soumettrait. */}
      <div className="flex flex-wrap items-center gap-2">
        <label className="flex items-center gap-2 text-sm">
          <Checkbox
            checked={onlyChecked}
            onCheckedChange={(next) => setOnlyChecked(next === true)}
          />
          {t('onlyChecked')}
        </label>
        <Button
          type="button"
          size="sm"
          variant="outline"
          disabled={checkedInView === filtered.length}
          onClick={() =>
            onChange(
              filtered.map((city) => city.id),
              true,
            )
          }
        >
          {t('checkAll', { count: String(filtered.length) })}
        </Button>
        <Button
          type="button"
          size="sm"
          variant="ghost"
          disabled={checkedInView === 0}
          onClick={() =>
            onChange(
              filtered.map((city) => city.id),
              false,
            )
          }
        >
          {t('uncheckAll', { count: String(checkedInView) })}
        </Button>
      </div>

      <DataTable data={filtered} columns={columns} pageSize={25} emptyTitle={t('empty')} />
    </div>
  );
}
