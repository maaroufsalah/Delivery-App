'use client';

import { searchOptions } from '@delivery/shared';
import { Check, ChevronsUpDown, type LucideIcon } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { type ComponentProps, useEffect, useMemo, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Command,
  CommandEmpty,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';

/** Une entrée du menu. `value` est exactement ce que le `<Select>` émettait. */
export interface SearchableOption {
  value: string;
  label: string;
  /** Texte secondaire à droite (le nom arabe d'une ville, un code…). */
  hint?: string | null;
  /** Pastille colorée (statuts, familles de statut). */
  color?: string;
  /** D'autres mots qui trouvent l'entrée (nom arabe, code, téléphone…). */
  keywords?: ReadonlyArray<string | null | undefined>;
  disabled?: boolean;
}

/** Préfixe des valeurs cmdk de l'option vide : elle ne peut pas heurter une vraie valeur. */
const EMPTY_PREFIX = '__empty:';

/**
 * LE MENU DÉROULANT AVEC RECHERCHE — lot J, J2.
 *
 * UN seul composant pour tous les menus qui méritent une recherche : filtres
 * de liste (Expéditeur, Livreur, Agence, Statut…), listes de clients, de
 * livreurs, de produits, de statuts autorisés. `CitySelect` en est la version
 * « villes ». À l'ouverture, un champ de recherche en tête filtre à chaque
 * frappe, sans casse ni accents (`searchOptions`, la règle partagée) ; flèches,
 * Entrée et Échap au clavier (cmdk + Radix) ; aucune liste n'est coupée.
 *
 * **Il ne change rien au contrat** : la valeur émise est la `value` de
 * l'option, exactement ce que `<Select>` émettait ; une option « vide »
 * (« Tous », « Aucune agence ») garde sa sentinelle par `emptyOption`.
 *
 * **Il vit sous `<FormControl>`** : `id`, `aria-invalid`, `aria-describedby`
 * posés par react-hook-form arrivent en props et vont sur le bouton.
 *
 * Le choix courant reste coché ET visible : à l'ouverture, il est la ligne
 * active et la liste défile jusqu'à lui.
 */
export function SearchableSelect({
  options,
  value,
  onValueChange,
  placeholder,
  emptyOption,
  searchPlaceholder,
  emptyText,
  icon: Icon,
  disabled,
  className,
  ...trigger
}: {
  options: readonly SearchableOption[];
  /** La `value` d'une option, celle de `emptyOption`, ou `''`. */
  value: string;
  onValueChange: (value: string) => void;
  placeholder?: string;
  /** Une première ligne hors liste : « Tous », « Aucune agence »… */
  emptyOption?: { value: string; label: string };
  searchPlaceholder?: string;
  emptyText?: string;
  /** Icône avant la valeur (filtres du tableau de bord : agence, livreur, période…). */
  icon?: LucideIcon;
  disabled?: boolean;
  className?: string;
} & Omit<ComponentProps<'button'>, 'value' | 'onChange' | 'children'>) {
  const t = useTranslations('common');
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [active, setActive] = useState('');
  const listRef = useRef<HTMLDivElement>(null);

  const selected = useMemo(
    () => options.find((option) => option.value === value),
    [options, value],
  );
  const matches = useMemo(() => searchOptions(options, query), [options, query]);
  const showEmpty =
    emptyOption !== undefined && searchOptions([{ label: emptyOption.label }], query).length > 0;

  const label = selected
    ? selected.label
    : emptyOption && value === emptyOption.value
      ? emptyOption.label
      : placeholder;

  // Le choix courant défile dans la vue à l'ouverture — après le rendu de la liste.
  useEffect(() => {
    if (!open) return;
    const frame = requestAnimationFrame(() => {
      listRef.current?.querySelector('[data-current="true"]')?.scrollIntoView({ block: 'nearest' });
    });
    return () => cancelAnimationFrame(frame);
  }, [open]);

  const choose = (next: string) => {
    setOpen(false);
    setQuery('');
    if (next !== value) onValueChange(next);
  };

  return (
    <Popover
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        // La ligne active est le choix courant ; la saisie ne survit pas à la
        // fermeture : rouvrir montre la liste entière.
        if (next)
          setActive(emptyOption && value === emptyOption.value ? EMPTY_PREFIX + value : value);
        else setQuery('');
      }}
    >
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          disabled={disabled}
          className={cn(
            'w-full justify-between gap-2 font-normal',
            !selected && 'text-muted-foreground',
            className,
          )}
          {...trigger}
        >
          <span className="flex min-w-0 items-center gap-2">
            {Icon ? <Icon className="text-muted-foreground size-4 shrink-0" aria-hidden /> : null}
            {selected?.color ? (
              <span
                className="size-2 shrink-0 rounded-full"
                style={{ backgroundColor: selected.color }}
                aria-hidden
              />
            ) : null}
            <span className="truncate">{label}</span>
          </span>
          <ChevronsUpDown className="text-muted-foreground size-4 shrink-0" aria-hidden />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-(--radix-popover-trigger-width) min-w-64 p-0" align="start">
        {/* `shouldFilter={false}` : c'est `searchOptions` qui filtre, pas cmdk,
            pour que « ain » trouve Aïn Sebaâ et que l'arabe soit cherché. */}
        <Command shouldFilter={false} value={active} onValueChange={setActive}>
          <CommandInput
            placeholder={searchPlaceholder ?? t('menuSearch')}
            value={query}
            onValueChange={setQuery}
            autoFocus
          />
          <CommandList ref={listRef} className="max-h-72">
            <CommandEmpty>{emptyText ?? t('menuEmpty')}</CommandEmpty>
            {emptyOption && showEmpty ? (
              <CommandItem
                value={EMPTY_PREFIX + emptyOption.value}
                data-current={value === emptyOption.value}
                onSelect={() => choose(emptyOption.value)}
              >
                <Check
                  className={cn(
                    'size-4',
                    value === emptyOption.value ? 'opacity-100' : 'opacity-0',
                  )}
                />
                <span className="truncate">{emptyOption.label}</span>
              </CommandItem>
            ) : null}
            {matches.map((option) => (
              <CommandItem
                key={option.value}
                value={option.value}
                disabled={option.disabled}
                data-current={option.value === value}
                onSelect={() => choose(option.value)}
              >
                <Check
                  className={cn('size-4', option.value === value ? 'opacity-100' : 'opacity-0')}
                />
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
            ))}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
