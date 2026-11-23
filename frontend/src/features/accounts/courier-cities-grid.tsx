'use client';

import type { City, CourierDetail, Zone } from '@delivery/shared';
import { Loader2, TriangleAlert, Wallet } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useCallback, useMemo, useState } from 'react';
import { toast } from 'sonner';
import { CityChecklist } from '@/components/city-checklist';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { setCourierCitiesAction } from '@/lib/actions/accounts';
import { useUnsavedChanges } from '@/lib/use-unsaved-changes';

/** Ligne de grille en unité de SAISIE : des dirhams, pas des centimes. */
interface RateRow {
  pickupFee: number;
  deliveryFee: number;
}

const helper = createDataTableColumnHelper<City>();

/** La grille en une chaîne comparable : villes triées et leurs deux montants. */
function gridKey(rows: ReadonlyMap<string, RateRow>): string {
  return [...rows]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([cityId, row]) => `${cityId}:${row.pickupFee}:${row.deliveryFee}`)
    .join('|');
}

/**
 * LES VILLES DU LIVREUR ET SA GRILLE DE PAIE — lot F, F1 (D-206).
 *
 * Un tableau de TOUTES les villes de la société : la case dit « ville servie
 * par ce livreur » (ses circuits proposent les colis de toutes ses villes), les
 * deux montants à côté disent ce qui lui est dû par course. C'est la même
 * table qu'avant (`CourierCity`), remplacée en bloc par `setCities` : cocher
 * une ville crée sa ligne de grille, à 0 par défaut — ce que paie aujourd'hui
 * une ville absente (`courierCommissionFor`), donc rien ne change pour la paie.
 *
 * Les cases, la recherche, le filtre Zone et « Tout cocher » vivent dans
 * `CityChecklist`, partagé avec les villes d'une remise client (lot K) ; cette
 * carte n'ajoute que les deux montants et l'enregistrement — et, lot N2 (D-225),
 * l'avertissement « Modifications non enregistrées » en quittant la fiche.
 */
export function CourierCitiesGrid({
  courier,
  cities,
  zones,
}: {
  courier: CourierDetail;
  cities: City[];
  zones: Zone[];
}) {
  const t = useTranslations('accounts.couriers');
  const tGrid = useTranslations('accounts.couriers.citiesGrid');
  const tList = useTranslations('cityChecklist');
  const tCommon = useTranslations('common');

  const [saving, setSaving] = useState(false);
  const [rows, setRows] = useState<Map<string, RateRow>>(
    () =>
      new Map(
        courier.cities.map((entry) => [
          entry.cityId,
          { pickupFee: entry.pickupFee / 100, deliveryFee: entry.deliveryFee / 100 },
        ]),
      ),
  );
  // Ce qui est en base : la référence de « modifié » (lot N2, D-225), reposée à chaque enregistrement.
  const [saved, setSaved] = useState(() => gridKey(rows));

  const selected = useMemo(() => new Set(rows.keys()), [rows]);
  const dirty = gridKey(rows) !== saved;
  useUnsavedChanges(dirty, tGrid('unsavedConfirm'));

  const toggle = useCallback((cityIds: string[], checked: boolean) => {
    setRows((current) => {
      const next = new Map(current);
      for (const cityId of cityIds) {
        if (checked) next.set(cityId, current.get(cityId) ?? { pickupFee: 0, deliveryFee: 0 });
        else next.delete(cityId);
      }
      return next;
    });
  }, []);

  const setRate = (cityId: string, field: keyof RateRow, value: number) => {
    setRows((current) => {
      const row = current.get(cityId);
      if (!row) return current;
      const next = new Map(current);
      next.set(cityId, { ...row, [field]: value });
      return next;
    });
  };

  const save = async () => {
    setSaving(true);
    const result = await setCourierCitiesAction(courier.id, {
      cities: [...rows].map(([cityId, row]) => ({ cityId, ...row })),
    });
    setSaving(false);

    if (result.ok) {
      setSaved(gridKey(rows));
      toast.success(t('gridSaved'));
    } else toast.error(result.error);
  };

  const rateColumns: DataTableColumns<City> = useMemo(
    () =>
      helper.columns([
        helper.display({
          id: 'pickupFee',
          header: t('fields.pickupFee'),
          cell: (info) => {
            const city = info.row.original;
            const row = rows.get(city.id);
            return (
              <Input
                type="number"
                min={0}
                step="0.01"
                className="w-24"
                aria-label={`${t('fields.pickupFee')} — ${city.name}`}
                disabled={!row}
                value={row?.pickupFee ?? ''}
                onChange={(event) => setRate(city.id, 'pickupFee', event.target.valueAsNumber || 0)}
              />
            );
          },
        }),
        helper.display({
          id: 'deliveryFee',
          header: t('fields.deliveryFee'),
          cell: (info) => {
            const city = info.row.original;
            const row = rows.get(city.id);
            return (
              <Input
                type="number"
                min={0}
                step="0.01"
                className="w-24"
                aria-label={`${t('fields.deliveryFee')} — ${city.name}`}
                disabled={!row}
                value={row?.deliveryFee ?? ''}
                onChange={(event) =>
                  setRate(city.id, 'deliveryFee', event.target.valueAsNumber || 0)
                }
              />
            );
          },
        }),
      ]),
    [rows, t],
  );

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Wallet className="size-4" />
          {t('grid')}
          <span className="text-muted-foreground ms-auto text-sm font-normal">
            {tList('count', { count: rows.size })}
          </span>
        </CardTitle>
        <CardDescription>{tGrid('hint')}</CardDescription>
      </CardHeader>

      <CardContent className="flex flex-col gap-3">
        {/* Un livreur sans ville ne devrait pas exister — D-206 ; rien n'est bloqué ailleurs. */}
        {courier.cities.length === 0 ? (
          <div
            role="alert"
            className="border-warning/40 bg-warning/10 flex items-start gap-2 rounded-lg border p-3 text-sm"
          >
            <TriangleAlert className="text-warning mt-0.5 size-4 shrink-0" aria-hidden />
            <span>{tGrid('noCity')}</span>
          </div>
        ) : null}

        <CityChecklist
          cities={cities}
          zones={zones}
          selected={selected}
          onChange={toggle}
          checkHeader={tGrid('served')}
          extraColumns={rateColumns}
        />

        <div className="flex flex-wrap items-center justify-end gap-3">
          {dirty ? (
            <span role="status" className="text-warning text-sm font-medium">
              {tGrid('unsaved')}
            </span>
          ) : null}
          <Button onClick={save} disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : null}
            {tCommon('save')}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
