'use client';

import {
  PARCEL_IMPORT_FIELDS,
  type ParcelImportField,
  type ParcelImportRow,
} from '@delivery/shared';
import { useFormatter, useTranslations } from 'next-intl';
import { useEffect, useMemo, useRef, useState } from 'react';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { cn } from '@/lib/utils';

const helper = createDataTableColumnHelper<ParcelImportRow>();

/**
 * Colonnes de la relecture d'import — C7 bloc 1.
 *
 * L'écran ne déclare AUCUN `<table>` : c'est la `DataTable` de l'application
 * qui rend la grille (CLAUDE.md §2), avec des cellules éditables. Une troisième
 * structure tabulaire — après la table et la matrice tarifaire — n'aurait
 * apporté qu'un `<input>` dans un `<td>`.
 *
 * Chaque cellule porte SON erreur : le message est sous le champ, pas dans une
 * liste à part. C'est la différence entre « ligne 412 : ville inconnue » et une
 * case rouge qu'on corrige sans quitter des yeux ce qu'on tape.
 */

/** Point de rupture sous lequel une colonne secondaire disparaît. */
const HIDE_BELOW: Partial<Record<ParcelImportField, 'sm' | 'md' | 'lg' | 'xl'>> = {
  externalCode: 'lg',
  description: 'xl',
  openOnDelivery: 'xl',
  fragile: 'xl',
  notes: 'xl',
  quantity: 'lg',
  address: 'md',
  district: 'md',
};

const WIDTH: Partial<Record<ParcelImportField, string>> = {
  address: 'w-56',
  description: 'w-48',
  notes: 'w-40',
  quantity: 'w-20',
  openOnDelivery: 'w-20',
  fragile: 'w-20',
};

export function useImportColumns({
  onEdit,
  disabled,
}: {
  onEdit: (row: number, field: ParcelImportField, value: string) => void;
  disabled: boolean;
}): DataTableColumns<ParcelImportRow> {
  const t = useTranslations('parcels.import.columns');
  const format = useFormatter();
  // Le dernier `onEdit`, lu au moment de la saisie : il ne fait pas partie des
  // dépendances des colonnes, qui ne se recréent donc pas à chaque frappe.
  const onEditRef = useRef(onEdit);
  useEffect(() => {
    onEditRef.current = onEdit;
  });

  return useMemo(
    () =>
      helper.columns([
        helper.accessor('row', {
          header: t('row'),
          meta: { width: 'w-14', align: 'end' },
          cell: (info) => {
            // Lot J, J4 : une anomalie de LIGNE (ligne vide au milieu du
            // fichier) n'a pas de cellule à elle : elle se dit sous le numéro.
            const rowErrors = info.row.original.errors.filter((item) => !item.column);
            return (
              <div className="flex flex-col items-end gap-0.5">
                <span className="text-muted-foreground font-mono text-xs tabular-nums">
                  {info.getValue()}
                </span>
                {rowErrors.map((item) => (
                  <span
                    key={item.code}
                    className="text-destructive min-w-36 text-start text-[11px] leading-tight"
                  >
                    {item.message}
                  </span>
                ))}
              </div>
            );
          },
        }),

        ...PARCEL_IMPORT_FIELDS.map((field) =>
          helper.accessor((row: ParcelImportRow) => row.values[field], {
            id: field,
            header: t(field),
            meta: { hideBelow: HIDE_BELOW[field], width: WIDTH[field] },
            cell: (info) => {
              const entry = info.row.original;
              const error = entry.errors.find((item) => item.column === field);
              // Lot G (D-208) : un avertissement n'écarte pas la ligne, il la signale.
              const warning = error
                ? undefined
                : entry.warnings?.find((item) => item.column === field);

              return (
                <div className="flex flex-col gap-0.5">
                  <EditableCell
                    value={entry.values[field]}
                    disabled={disabled}
                    invalid={error !== undefined}
                    label={`${t(field)}, ${t('row')} ${entry.row}`}
                    cellKey={`${entry.row}:${field}`}
                    onCommit={(value) => onEditRef.current(entry.row, field, value)}
                  />
                  {error ? (
                    <span className="text-destructive text-[11px] leading-tight">
                      {error.message}
                    </span>
                  ) : null}
                  {warning ? (
                    <span className="text-muted-foreground text-[11px] leading-tight">
                      {warning.message}
                    </span>
                  ) : null}
                </div>
              );
            },
          }),
        ),

        helper.accessor('fee', {
          header: t('fee'),
          meta: { align: 'end', width: 'w-24', hideBelow: 'lg' },
          cell: (info) => {
            const fee = info.getValue();
            return (
              <span className="text-muted-foreground text-sm tabular-nums">
                {fee === null
                  ? '-'
                  : format.number(fee / 100, { style: 'currency', currency: 'MAD' })}
              </span>
            );
          },
        }),
      ]),
    [t, format, disabled],
  );
}

/**
 * UNE CELLULE QU'ON TAPE LIBREMENT — recette 6, point 5.
 *
 * Sur /parcels/import, corriger une adresse « bloquait la saisie » : chaque
 * frappe réécrivait toute la relecture, les colonnes étaient recréées (`onEdit`
 * changeait à chaque rendu), et la DataTable remontait l'`<input>` — curseur
 * perdu, caractères mangés. Ici, la frappe ne touche que la cellule ; la
 * valeur part dans la relecture à la SORTIE du champ (ou sur Entrée), et
 * « Vérifier à nouveau » revalide les lignes corrigées.
 *
 * Une valeur venue d'ailleurs (la relecture renvoyée par le serveur) remplace
 * le brouillon — pendant le rendu, pas dans un effet.
 *
 * Lot J, J3 : Entrée valide SANS quitter le champ — le focus reste sur la
 * cellule corrigée ; `data-import-cell` permet à l'écran de l'y ramener après
 * « Vérifier à nouveau » (le champ est désactivé pendant la vérification).
 */
function EditableCell({
  value,
  disabled,
  invalid,
  label,
  cellKey,
  onCommit,
}: {
  value: string;
  disabled: boolean;
  invalid: boolean;
  label: string;
  /** `<ligne du fichier>:<champ>` — l'adresse de la cellule pour y revenir. */
  cellKey: string;
  onCommit: (value: string) => void;
}) {
  const [draft, setDraft] = useState(value);
  const [seen, setSeen] = useState(value);
  if (seen !== value) {
    setSeen(value);
    setDraft(value);
  }

  return (
    <input
      value={draft}
      disabled={disabled}
      onChange={(event) => setDraft(event.target.value)}
      onBlur={() => {
        if (draft !== value) onCommit(draft);
      }}
      onKeyDown={(event) => {
        if (event.key === 'Enter' && draft !== value) onCommit(draft);
      }}
      data-import-cell={cellKey}
      aria-invalid={invalid ? true : undefined}
      aria-label={label}
      className={cn(
        'w-full min-w-24 rounded-md border bg-transparent px-2 py-1 text-sm outline-none',
        'focus-visible:border-primary focus-visible:ring-primary/30 focus-visible:ring-2',
        invalid
          ? 'border-destructive/60 bg-destructive/5'
          : 'hover:border-input border-transparent',
      )}
    />
  );
}
