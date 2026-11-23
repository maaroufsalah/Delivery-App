'use client';

import { searchOptions } from '@delivery/shared';
import { Check, ChevronsUpDown } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Command,
  CommandEmpty,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import type { SearchableOption } from '@/components/searchable-select';
import { cn } from '@/lib/utils';

/** Séparateur des valeurs d'un filtre multiple, dans l'URL comme à l'API (D-255). */
export const MULTI_SEPARATOR = ',';

/** `"a,b"` → `['a', 'b']` ; la sentinelle « Tous » ou rien → `[]`. */
export function splitMulti(value: string | undefined, allValue: string): string[] {
  if (!value || value === allValue) return [];
  return [...new Set(value.split(MULTI_SEPARATOR).filter(Boolean))];
}

/** `['a', 'b']` → `"a,b"` ; `[]` → la sentinelle « Tous ». */
export function joinMulti(values: readonly string[], allValue: string): string {
  return values.length === 0 ? allValue : values.join(MULTI_SEPARATOR);
}

/**
 * LE MENU À CHOIX MULTIPLE DES FILTRES DE LISTE — D-255.
 *
 * Le frère de `SearchableSelect` : même déclencheur, même recherche sans casse
 * ni accents, même pastille de couleur. Chaque ligne se COCHE au lieu de
 * fermer le menu ; « Tous » décoche tout. Le choix part à la FERMETURE du menu
 * (clic dehors, Échap) : cocher trois statuts fait une navigation, pas trois.
 *
 * Le contrat reste une chaîne : `"a,b"` pour plusieurs valeurs, la valeur seule
 * pour une, la sentinelle `allValue` pour aucune. Une URL à une seule valeur
 * se lit donc exactement comme avant.
 */
export function MultiSelect({
  options,
  value,
  onValueChange,
  allLabel,
  allValue,
  searchPlaceholder,
  emptyText,
  className,
  'aria-label': ariaLabel,
}: {
  options: readonly SearchableOption[];
  value: string;
  onValueChange: (value: string) => void;
  /** Le libellé du filtre, affiché quand rien n'est coché (« Statut »). */
  allLabel: string;
  allValue: string;
  searchPlaceholder?: string;
  emptyText?: string;
  className?: string;
  'aria-label'?: string;
}) {
  const t = useTranslations('common');
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [draft, setDraft] = useState<string[]>([]);

  const committed = useMemo(() => splitMulti(value, allValue), [value, allValue]);
  const current = open ? draft : committed;
  const matches = useMemo(() => searchOptions(options, query), [options, query]);
  const chosen = options.filter((option) => current.includes(option.value));

  const label =
    chosen.length === 0
      ? allLabel
      : chosen.length === 1
        ? chosen[0]!.label
        : `${allLabel} (${chosen.length})`;

  const toggle = (next: string) =>
    setDraft((values) =>
      values.includes(next) ? values.filter((item) => item !== next) : [...values, next],
    );

  const commit = () => {
    // L'ordre des options, pas celui des clics : une même sélection, une même URL.
    const ordered = options.map((option) => option.value).filter((item) => draft.includes(item));
    const next = joinMulti(ordered, allValue);
    if (next !== joinMulti(committed, allValue)) onValueChange(next);
  };

  return (
    <Popover
      open={open}
      onOpenChange={(next) => {
        if (next) setDraft(committed);
        else {
          commit();
          setQuery('');
        }
        setOpen(next);
      }}
    >
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          aria-label={ariaLabel}
          className={cn(
            'w-full justify-between gap-2 font-normal',
            chosen.length === 0 && 'text-muted-foreground',
            className,
          )}
        >
          <span className="flex min-w-0 items-center gap-2">
            {chosen.length === 1 && chosen[0]!.color ? (
              <span
                className="size-2 shrink-0 rounded-full"
                style={{ backgroundColor: chosen[0]!.color }}
                aria-hidden
              />
            ) : null}
            <span className="truncate">{label}</span>
          </span>
          <ChevronsUpDown className="text-muted-foreground size-4 shrink-0" aria-hidden />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-(--radix-popover-trigger-width) min-w-64 p-0" align="start">
        <Command shouldFilter={false}>
          <CommandInput
            placeholder={searchPlaceholder ?? t('menuSearch')}
            value={query}
            onValueChange={setQuery}
            autoFocus
          />
          <CommandList className="max-h-72">
            <CommandEmpty>{emptyText ?? t('menuEmpty')}</CommandEmpty>
            {query === '' ? (
              <CommandItem value={`__all:${allValue}`} onSelect={() => setDraft([])}>
                <Check className={cn('size-4', current.length === 0 ? 'opacity-100' : 'opacity-0')} />
                <span className="truncate">{allLabel}</span>
              </CommandItem>
            ) : null}
            {matches.map((option) => {
              const checked = current.includes(option.value);
              return (
                <CommandItem
                  key={option.value}
                  value={option.value}
                  disabled={option.disabled}
                  onSelect={() => toggle(option.value)}
                  data-checked={checked}
                >
                  <span
                    className={cn(
                      'border-primary flex size-4 shrink-0 items-center justify-center rounded-sm border',
                      checked ? 'bg-primary text-primary-foreground' : 'opacity-60',
                    )}
                    aria-hidden
                  >
                    {checked ? <Check className="size-3" /> : null}
                  </span>
                  {option.color ? (
                    <span
                      className="size-2 shrink-0 rounded-full"
                      style={{ backgroundColor: option.color }}
                      aria-hidden
                    />
                  ) : null}
                  <span className="truncate">{option.label}</span>
                  {option.hint ? (
                    <span className="text-muted-foreground ms-auto truncate text-xs" dir="auto">
                      {option.hint}
                    </span>
                  ) : null}
                </CommandItem>
              );
            })}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
