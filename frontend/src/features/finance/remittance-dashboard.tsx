'use client';

import type { RemittanceOpenRow, RemittanceSummary } from '@delivery/shared';
import {
  AlertTriangle,
  Banknote,
  CheckCircle2,
  HandCoins,
  Receipt,
  ReceiptText,
  Wallet,
} from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo } from 'react';
import { CellMuted, CellNumber, CellStack } from '@/components/data-table/cells';
import { DataTable } from '@/components/data-table/data-table';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { PeriodFilter } from '@/features/dashboard/period-filter';
import { useRouter } from '@/i18n/navigation';

const helper = createDataTableColumnHelper<RemittanceOpenRow>();

/**
 * LE TABLEAU DE BORD DES RETOURS DE FONDS — chantier « Retour de fonds
 * unique », point 6.
 *
 * **Il ne calcule rien.** Les cinq chiffres arrivent sommés du serveur, sur
 * toute la période : les recalculer ici n'aurait vu que la page affichée.
 *
 * **Deux temps, et l'écran le dit.** Les cartes racontent la PÉRIODE — ce qui
 * s'est encaissé, ce qui est rentré, ce qui a manqué. Le tableau en dessous
 * raconte MAINTENANT — qui porte encore de l'argent, depuis quand, et combien.
 * Le second ignore la période volontairement : un livreur qui garde la recette
 * d'août doit apparaître en septembre, sinon l'écran cache exactement ce qu'on
 * l'ouvre pour voir.
 *
 * **Le livreur ne se choisit pas ici** : la barre de filtres de la liste, en
 * dessous, pilote déjà `courierId` dans l'URL, et le serveur borne les deux
 * avec. Un second sélecteur aurait été un second endroit où se tromper.
 */
export function RemittanceDashboard({ summary }: { summary: RemittanceSummary }) {
  const t = useTranslations('finance.remittances.summary');
  const format = useFormatter();
  const router = useRouter();

  const money = (cents: number) => format.number(cents / 100, 'currency');

  /*
   * DEUX BLOCS, UNE LECTURE — D-243. Le serveur rend tous les bons non
   * réceptionnés ; `courierClosedAt` dit lesquels le livreur a fermés. L'écran
   * ne déduit rien d'autre : il range les lignes dans le bloc que ce champ
   * désigne. « À réceptionner » se lit de la plus ancienne clôture à la plus
   * récente, parce que c'est l'ordre dans lequel l'argent attend.
   */
  const toReceive = useMemo(
    () => summary.open.filter((row) => row.courierClosedAt !== null),
    [summary.open],
  );
  const stillOpen = useMemo(
    () => summary.open.filter((row) => row.courierClosedAt === null),
    [summary.open],
  );

  const stampOf = (iso: string) => format.dateTime(new Date(iso), 'stamp');

  const buildColumns = (closed: boolean): DataTableColumns<RemittanceOpenRow> =>
    helper.columns([
      helper.accessor('courierName', {
        header: t('columns.courier'),
        cell: (info) => <CellStack label={info.getValue()} sub={info.row.original.agencyName} />,
      }),
      helper.accessor('code', {
        header: t('columns.code'),
        meta: { width: 'w-36', hideBelow: 'sm' },
        cell: (info) => <CellStack mono label={info.getValue()} />,
      }),
      helper.accessor('parcelCount', {
        header: t('columns.parcels'),
        sortFn: 'basic',
        meta: { align: 'end', hideBelow: 'md' },
        cell: (info) => <CellNumber value={format.number(info.getValue())} />,
      }),
      /* Une caisse fermée se date de sa CLÔTURE (c'est depuis elle que
         l'argent attend), une caisse ouverte de son ouverture. */
      closed
        ? helper.accessor((row) => row.courierClosedAt ?? '', {
            id: 'courierClosedAt',
            header: t('columns.closedAt'),
            sortFn: 'basic',
            meta: { hideBelow: 'sm' },
            cell: (info) => {
              const value = info.getValue();
              return (
                <CellStack
                  label={value ? stampOf(value) : '-'}
                  sub={t('openedOn', { date: stampOf(info.row.original.openedAt) })}
                />
              );
            },
          })
        : helper.accessor('openedAt', {
            header: t('columns.openedAt'),
            sortFn: 'basic',
            meta: { hideBelow: 'lg' },
            cell: (info) => <CellStack label={stampOf(info.getValue())} />,
          }),
      helper.accessor('chequeAmount', {
        header: t('columns.cheques'),
        sortFn: 'basic',
        meta: { align: 'end', hideBelow: 'lg' },
        cell: (info) => {
          const amount = info.getValue();
          if (amount === 0) return <CellMuted>—</CellMuted>;

          return (
            <CellStack
              label={money(amount)}
              sub={t('chequeCount', { count: String(info.row.original.chequeCount) })}
            />
          );
        },
      }),

      helper.accessor('toHandOver', {
        header: t('columns.toHandOver'),
        sortFn: 'basic',
        meta: { align: 'end' },
        cell: (info) => <CellNumber value={money(info.getValue())} />,
      }),
    ]);

  // `money` et `buildColumns` se reconstruisent à chaque rendu ; c'est `format`
  // qui porte la locale.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const openColumns = useMemo(() => buildColumns(false), [t, format]);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const closedColumns = useMemo(() => buildColumns(true), [t, format]);

  const { totals } = summary;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-base font-semibold tracking-tight">{t('title')}</h2>
          <p className="text-muted-foreground text-sm">{t('hint')}</p>
        </div>
        {/* Les bornes viennent RÉSOLUES du serveur : l'écran affiche la fenêtre
            qui a été sommée, et non celle qu'il aurait recalculée. */}
        <PeriodFilter period={summary.period} from={summary.from} to={summary.to} />
      </div>

      <KpiGrid>
        <KpiCard
          label={t('collected')}
          value={money(totals.collected)}
          hint={t('cashOf', { cash: money(totals.cash) })}
          icon={Wallet}
          tone="var(--chart-1)"
        />
        <KpiCard
          label={t('count')}
          value={format.number(totals.count)}
          hint={t('bons', {
            open: format.number(totals.openCount),
            confirmed: format.number(totals.confirmedCount),
          })}
          icon={Receipt}
          tone="var(--chart-4)"
        />
        <KpiCard
          label={t('cheque')}
          value={money(totals.cheque)}
          hint={t('chequeHint')}
          icon={ReceiptText}
          tone="var(--chart-5)"
        />
        <KpiCard
          label={t('toHandOver')}
          value={money(totals.toHandOver)}
          hint={t('toHandOverHint')}
          icon={HandCoins}
          tone="var(--chart-3)"
        />
        <KpiCard
          label={t('handed')}
          value={money(totals.handed)}
          hint={t('handedHint')}
          icon={Banknote}
          tone="var(--chart-2)"
        />
        <KpiCard
          label={t('variance')}
          value={money(totals.variance)}
          hint={totals.variance === 0 ? t('noVariance') : t('varianceHint')}
          icon={totals.variance === 0 ? CheckCircle2 : AlertTriangle}
          tone={totals.variance === 0 ? 'var(--chart-2)' : 'var(--destructive)'}
        />
      </KpiGrid>

      {/*
        À RÉCEPTIONNER — D-243. Les caisses que les livreurs ont fermées :
        l'argent est prêt, il attend l'agence. Posé AVANT les caisses encore
        ouvertes, parce que c'est ici qu'il y a un geste à faire.
      */}
      <Card className="gap-0 overflow-hidden p-0">
        <CardHeader className="px-4 py-4">
          <CardTitle>{t('toReceive.title')}</CardTitle>
          <CardDescription>{t('toReceive.hint')}</CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          <DataTable
            compact
            data={toReceive}
            columns={closedColumns}
            initialSorting={[{ id: 'courierClosedAt', desc: false }]}
            emptyTitle={t('toReceive.empty')}
            emptyHint={t('toReceive.emptyHint')}
            onRowClick={(row) => router.push(`/remittances/${encodeURIComponent(row.code)}`)}
          />
        </CardContent>
      </Card>

      <Card className="gap-0 overflow-hidden p-0">
        <CardHeader className="px-4 py-4">
          <CardTitle>{t('open.title')}</CardTitle>
          {/* Dit à l'écran, et pas seulement dans le code : ce bloc ne suit pas
              la période choisie au-dessus. */}
          <CardDescription>{t('open.hint')}</CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          <DataTable
            compact
            data={stillOpen}
            columns={openColumns}
            initialSorting={[{ id: 'toHandOver', desc: true }]}
            emptyTitle={t('open.empty')}
            emptyHint={t('open.emptyHint')}
            onRowClick={(row) => router.push(`/remittances/${encodeURIComponent(row.code)}`)}
          />
        </CardContent>
      </Card>
    </div>
  );
}
