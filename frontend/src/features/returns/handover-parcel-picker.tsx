'use client';

import { findParcelStatus, type ReturnParcelLine } from '@delivery/shared';
import { Search } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo } from 'react';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';

/**
 * LA LISTE DES COLIS À RAPPORTER — chantier « Retours, BL Clients Pro »,
 * point 2, étape 2.
 *
 * « Liste des colis éligibles chez ce livreur (code, vendeur, destinataire,
 * statut, date de livraison/tentative, montant, frais, livreur), recherche par
 * code, filtres, multi-sélection. »
 *
 * **Un seul composant pour deux écrans**, et c'est le point : l'assistant de
 * création et l'ajout à un bon existant montrent la MÊME liste. Deux copies
 * auraient fini par ne plus proposer les mêmes colis, et l'agent n'aurait eu
 * aucun moyen de savoir laquelle avait raison.
 *
 * **La recherche est locale, la liste est servie filtrée.** Le serveur borne
 * déjà les candidats à ce qui est rapportable ; ce champ-ci retrouve une ligne
 * dans ce qu'on a sous les yeux, ce qui est le geste du comptoir. Il prend le
 * TÉLÉPHONE, comme celui des bons rendus au vendeur.
 *
 * **Les huit colonnes tiennent à 375 px** parce qu'elles ne sont pas des
 * colonnes : chaque colis est une carte à trois lignes, et le montant comme les
 * frais vivent à droite, en chiffres tabulaires.
 */
export function HandoverParcelPicker({
  candidates,
  selected,
  onSelectedChange,
  search,
  onSearchChange,
}: {
  candidates: ReturnParcelLine[];
  selected: string[];
  onSelectedChange: (next: string[]) => void;
  search: string;
  onSearchChange: (next: string) => void;
}) {
  const t = useTranslations('returns.handovers');
  const tSlips = useTranslations('returns.slips');
  const tStatus = useTranslations('status');
  const format = useFormatter();

  const visible = useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle) return candidates;

    return candidates.filter(
      (parcel) =>
        parcel.code.toLowerCase().includes(needle) ||
        parcel.recipientName.toLowerCase().includes(needle) ||
        parcel.recipientPhone.toLowerCase().includes(needle) ||
        parcel.clientName.toLowerCase().includes(needle),
    );
  }, [candidates, search]);

  const allVisibleSelected =
    visible.length > 0 && visible.every((parcel) => selected.includes(parcel.id));

  const toggle = (id: string, checked: boolean) => {
    onSelectedChange(
      checked ? [...new Set([...selected, id])] : selected.filter((entry) => entry !== id),
    );
  };

  return (
    <div className="flex flex-col gap-3">
      <div className="relative">
        <Search
          className="text-muted-foreground pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2"
          aria-hidden
        />
        <Input
          value={search}
          onChange={(event) => onSearchChange(event.target.value)}
          placeholder={tSlips('searchCandidates')}
          className="ps-9"
        />
      </div>

      {candidates.length === 0 ? (
        <div className="text-muted-foreground rounded-lg border border-dashed p-6 text-center text-sm">
          <p className="font-medium">{t('wizardEmpty')}</p>
          <p className="mt-1">{t('wizardEmptyHint')}</p>
        </div>
      ) : (
        <>
          <label className="bg-muted/50 flex items-center gap-3 rounded-lg px-3 py-2 text-sm">
            <Checkbox
              checked={allVisibleSelected}
              onCheckedChange={(value) => {
                const ids = visible.map((parcel) => parcel.id);
                onSelectedChange(
                  value === true
                    ? [...new Set([...selected, ...ids])]
                    : selected.filter((entry) => !ids.includes(entry)),
                );
              }}
              aria-label={tSlips('selectAll')}
            />
            <span className="flex-1">{tSlips('selectAll')}</span>
            <span className="text-muted-foreground text-xs tabular-nums">
              {tSlips('counter', {
                selected: String(selected.length),
                total: String(visible.length),
              })}
            </span>
          </label>

          <ul className="flex flex-col gap-2">
            {visible.map((parcel) => {
              const definition = findParcelStatus(parcel.statusId);

              return (
                <li key={parcel.id}>
                  <label className="hover:bg-muted/40 flex cursor-pointer items-start gap-3 rounded-lg border p-3">
                    <Checkbox
                      checked={selected.includes(parcel.id)}
                      onCheckedChange={(value) => toggle(parcel.id, value === true)}
                      className="mt-0.5"
                      aria-label={parcel.code}
                    />

                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-mono text-sm font-medium">{parcel.code}</span>
                        {definition ? (
                          <span
                            className="rounded-full px-2 py-0.5 text-[11px]"
                            style={{
                              backgroundColor: `${definition.color}1a`,
                              color: definition.color,
                            }}
                          >
                            {tStatus(definition.key)}
                          </span>
                        ) : null}
                      </div>

                      <p className="text-muted-foreground mt-0.5 truncate text-xs">
                        {parcel.recipientName} · {parcel.recipientPhone}
                      </p>

                      {/*
                        Le vendeur, le livreur et la date de la dernière
                        tentative : les trois choses qu'on vérifie avant de
                        cocher, et qui manquaient à l'écran d'avant.
                      */}
                      <p className="text-muted-foreground truncate text-xs">
                        {parcel.clientName}
                        {parcel.courierName ? ` · ${parcel.courierName}` : ''} ·{' '}
                        {format.dateTime(new Date(parcel.statusAt), 'stamp')}
                      </p>
                    </div>

                    <div className="flex shrink-0 flex-col items-end">
                      <span className="text-sm font-medium tabular-nums">
                        {format.number(parcel.codAmount / 100, 'currency')}
                      </span>
                      <span className="text-muted-foreground text-xs tabular-nums">
                        {format.number(parcel.deliveryFee / 100, 'currency')}
                      </span>
                    </div>
                  </label>
                </li>
              );
            })}
          </ul>
        </>
      )}
    </div>
  );
}
