'use client';

import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useState } from 'react';
import { CellMuted, CellNumber, CellStack } from '@/components/data-table/cells';
import { DataTable } from '@/components/data-table/data-table';
import { DataTableToolbar } from '@/components/data-table/data-table-toolbar';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { Badge } from '@/components/ui/badge';
import type { PublicRate } from '@/lib/api/public';
import { cn } from '@/lib/utils';

const helper = createDataTableColumnHelper<PublicRate>();

/**
 * Grille tarifaire publique — la MÊME DataTable que le back-office.
 *
 * Un visiteur cherche deux choses : « vous livrez chez moi ? » et « combien ? ».
 * D'où la recherche par ville et les chips de plage de prix ; le reste de la
 * mécanique (tri, masquage responsive, état vide) vient du composant commun.
 *
 * Les plages sont exprimées en centimes : `AmountInCents` partout, aucun
 * flottant (DOMAIN.md §4 dette n°2).
 */
const PRICE_RANGES = [
  { key: 'low', max: 2500 },
  { key: 'mid', min: 2500, max: 3500 },
  { key: 'high', min: 3500 },
] as const;

type RangeKey = (typeof PRICE_RANGES)[number]['key'];

export function PublicRatesTable({ rates }: { rates: PublicRate[] }) {
  const t = useTranslations('public.rates');
  const format = useFormatter();

  const [search, setSearch] = useState('');
  const [range, setRange] = useState<RangeKey | null>(null);

  const filtered = useMemo(() => {
    const bounds = PRICE_RANGES.find((item) => item.key === range);
    if (!bounds) return rates;
    return rates.filter((rate) => {
      const min = 'min' in bounds ? bounds.min : 0;
      const max = 'max' in bounds ? bounds.max : Number.POSITIVE_INFINITY;
      return rate.price >= min && rate.price < max;
    });
  }, [rates, range]);

  const columns: DataTableColumns<PublicRate> = useMemo(
    () =>
      helper.columns([
        helper.accessor('cityName', {
          header: t('columns.city'),
          cell: (info) => <CellStack label={info.getValue()} sub={info.row.original.zoneName} />,
        }),
        helper.accessor('zoneName', {
          header: t('columns.zone'),
          meta: { hideBelow: 'md' },
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),
        helper.accessor('deliveryDays', {
          header: t('columns.delay'),
          sortFn: 'basic',
          meta: { align: 'center' },
          cell: (info) => {
            const days = info.getValue() as number;
            return (
              <Badge variant="secondary" className="rounded-full font-normal whitespace-nowrap">
                {days === 0 ? t('sameDay') : t('days', { count: days })}
              </Badge>
            );
          },
        }),
        helper.accessor('price', {
          header: t('columns.price'),
          sortFn: 'basic',
          meta: { align: 'end' },
          cell: (info) => (
            <CellNumber value={format.number((info.getValue() as number) / 100, 'currency')} />
          ),
        }),
      ]),
    [t, format],
  );

  return (
    <DataTable
      data={filtered}
      columns={columns}
      globalFilter={search}
      initialSorting={[{ id: 'price', desc: false }]}
      pageSize={25}
      emptyTitle={t('empty')}
      emptyHint={t('emptyHint')}
      toolbar={
        <DataTableToolbar
          search={search}
          onSearchChange={setSearch}
          searchPlaceholder={t('searchPlaceholder')}
          onReset={
            search || range
              ? () => {
                  setSearch('');
                  setRange(null);
                }
              : undefined
          }
        >
          <div className="col-span-2 flex flex-wrap gap-2 md:col-auto">
            {PRICE_RANGES.map((item) => {
              const active = range === item.key;
              return (
                <button
                  key={item.key}
                  type="button"
                  aria-pressed={active}
                  onClick={() => setRange(active ? null : item.key)}
                  className={cn(
                    'rounded-full border px-3 py-1.5 text-xs font-medium transition-colors',
                    'focus-visible:ring-ring outline-none focus-visible:ring-2',
                    active
                      ? 'border-primary bg-primary text-primary-foreground'
                      : 'hover:bg-muted text-muted-foreground',
                  )}
                >
                  {t(`ranges.${item.key}`)}
                </button>
              );
            })}
          </div>
        </DataTableToolbar>
      }
    />
  );
}
