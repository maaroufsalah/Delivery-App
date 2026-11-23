'use client';

import { FINANCIAL_STATUS_COLORS, type FinanceOverview } from '@delivery/shared';
import {
  AlertTriangle,
  Banknote,
  CheckCircle2,
  CreditCard,
  FileText,
  HandCoins,
  Package,
  Percent,
  Receipt,
  Scale,
  Truck,
  Wallet,
} from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';

/**
 * LE TABLEAU DE BORD FINANCIER — chantier « Finance », point 10, D-112.
 *
 * **Cet écran ne calcule RIEN.** Pas une addition, pas un pourcentage, pas un
 * total de colonne : chaque chiffre affiché est arrivé tel quel du serveur, où
 * il a été sommé sur l'ENSEMBLE de la période. Un total recalculé au rendu se
 * calcule sur ce que la page a reçu, et ment dès qu'il y a une seconde page.
 *
 * **Les deux axes du colis restent séparés** (D-105) : la carte « livrés » lit
 * l'axe logistique, la liste en dessous lit l'axe financier. Elles ne
 * s'additionnent pas, et rien ici ne les additionne.
 *
 * **Le solde des livreurs ne suit pas la période**, et le dit à l'écran : une
 * dette est un état, pas un flux.
 */
export function FinanceOverviewScreen({ data }: { data: FinanceOverview }) {
  const t = useTranslations('finance.overview');
  const tFinancial = useTranslations('financialStatus');
  const format = useFormatter();

  const money = (cents: number) => format.number(cents / 100, 'currency');
  const count = (value: number) => format.number(value);

  return (
    <div className="flex flex-col gap-6">
      {/* --- Bloc 1 : les bons de retour de fonds -------------------------- */}
      <section className="flex flex-col gap-3">
        <SectionTitle title={t('cash.title')} hint={t('cash.hint')} />

        <KpiGrid>
          <KpiCard
            label={t('cash.collected')}
            value={money(data.cash.collected)}
            hint={t('cash.sessions', {
              open: count(data.cash.openCount),
              validated: count(data.cash.validatedCount),
            })}
            icon={Wallet}
            tone="var(--chart-1)"
          />

          <KpiCard
            label={t('cash.expected')}
            value={money(data.cash.expected)}
            hint={t('cash.commissions', { amount: money(data.cash.commissions) })}
            icon={HandCoins}
            tone="var(--chart-3)"
          />

          <KpiCard
            label={t('cash.handed')}
            value={money(data.cash.handed)}
            hint={t('cash.split', {
              cash: money(data.cash.cash),
              cheque: money(data.cash.cheque),
            })}
            icon={Banknote}
            tone="var(--chart-2)"
          />

          <KpiCard
            label={t('cash.variance')}
            value={money(data.cash.variance)}
            hint={data.cash.variance === 0 ? t('cash.noVariance') : t('cash.varianceHint')}
            icon={data.cash.variance === 0 ? CheckCircle2 : AlertTriangle}
            tone={data.cash.variance === 0 ? 'var(--chart-2)' : 'var(--destructive)'}
          />
        </KpiGrid>
      </section>

      {/* --- Bloc 2 : le grand livre des livreurs -------------------------- */}
      <Card>
        <CardHeader>
          <CardTitle>{t('balances.title')}</CardTitle>
          {/* La période ne s'y applique pas, et l'écran le dit plutôt que de
              laisser croire à un chiffre du mois. */}
          <CardDescription>{t('balances.hint')}</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <p className="text-2xl font-semibold tracking-tight tabular-nums">
            {money(data.balances.total)}
          </p>

          {data.balances.rows.length === 0 ? (
            <p className="text-muted-foreground py-2 text-sm">{t('balances.empty')}</p>
          ) : (
            <ul className="divide-border divide-y">
              {data.balances.rows.map((row) => (
                <li key={row.courierId} className="flex items-center gap-3 py-2.5">
                  <Link
                    href={`/remittances?courierId=${row.courierId}`}
                    className="min-w-0 flex-1 text-sm hover:underline"
                  >
                    {row.courierName}
                  </Link>
                  <span
                    className={
                      row.balance < 0
                        ? 'text-destructive shrink-0 text-sm font-medium tabular-nums'
                        : 'shrink-0 text-sm font-medium tabular-nums'
                    }
                  >
                    {money(row.balance)}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      {/* --- Bloc 3 : les chèques ------------------------------------------ */}
      <section className="flex flex-col gap-3">
        <SectionTitle title={t('cheques.title')} hint={t('cheques.hint')} />

        <KpiGrid>
          <KpiCard
            label={t('cheques.received')}
            value={money(data.cheques.received.amount)}
            hint={t('cheques.count', { count: count(data.cheques.received.count) })}
            icon={CreditCard}
            tone="var(--chart-1)"
          />
          <KpiCard
            label={t('cheques.pending')}
            value={money(data.cheques.pending.amount)}
            hint={t('cheques.count', { count: count(data.cheques.pending.count) })}
            icon={Scale}
            tone="var(--chart-4)"
          />
          <KpiCard
            label={t('cheques.cashed')}
            value={money(data.cheques.cashed.amount)}
            hint={t('cheques.count', { count: count(data.cheques.cashed.count) })}
            icon={CheckCircle2}
            tone="var(--chart-2)"
          />
          <KpiCard
            label={t('cheques.rejected')}
            value={money(data.cheques.rejected.amount)}
            hint={t('cheques.count', { count: count(data.cheques.rejected.count) })}
            icon={AlertTriangle}
            tone={
              data.cheques.rejected.count > 0 ? 'var(--destructive)' : 'var(--muted-foreground)'
            }
          />
        </KpiGrid>
      </section>

      {/* --- Blocs 4 et 5 : les deux axes du colis -------------------------- */}
      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>{t('parcels.title')}</CardTitle>
            <CardDescription>{t('parcels.hint')}</CardDescription>
          </CardHeader>
          <CardContent>
            {data.parcelsByFinancialStatus.length === 0 ? (
              <p className="text-muted-foreground py-2 text-sm">{t('parcels.empty')}</p>
            ) : (
              <ul className="divide-border divide-y">
                {data.parcelsByFinancialStatus.map((row) => (
                  <li key={row.status} className="flex items-center gap-3 py-2.5">
                    <span
                      className="size-2.5 shrink-0 rounded-full"
                      style={{ backgroundColor: FINANCIAL_STATUS_COLORS[row.status] }}
                      aria-hidden
                    />
                    <Link
                      href={`/parcels?financialStatus=${row.status}`}
                      className="min-w-0 flex-1 text-sm hover:underline"
                    >
                      {tFinancial(row.status)}
                    </Link>
                    <span className="text-muted-foreground shrink-0 text-xs tabular-nums">
                      {count(row.count)}
                    </span>
                    <span className="shrink-0 text-sm font-medium tabular-nums">
                      {money(row.amount)}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <div className="flex flex-col gap-4">
          {/* L'axe LOGISTIQUE, dans sa propre carte : les deux ne se mélangent
              ni dans le code, ni dans les libellés, ni à l'écran. */}
          <KpiCard
            label={t('parcels.delivered')}
            value={count(data.deliveredCount)}
            hint={t('parcels.deliveredHint')}
            icon={Truck}
            tone="var(--chart-2)"
          />

          <KpiGrid>
            <KpiCard
              label={t('invoices.open')}
              value={money(data.clientInvoices.open.amount)}
              hint={t('invoices.count', { count: count(data.clientInvoices.open.count) })}
              icon={FileText}
              tone="var(--chart-3)"
            />
            <KpiCard
              label={t('invoices.paid')}
              value={money(data.clientInvoices.paid.amount)}
              hint={t('invoices.count', { count: count(data.clientInvoices.paid.count) })}
              icon={Receipt}
              tone="var(--chart-2)"
            />
            <KpiCard
              label={t('statements.open')}
              value={money(data.courierStatements.open.amount)}
              hint={t('statements.count', { count: count(data.courierStatements.open.count) })}
              icon={Package}
              tone="var(--chart-4)"
            />
            <KpiCard
              label={t('statements.validated')}
              value={money(data.courierStatements.validated.amount)}
              hint={t('statements.count', {
                count: count(data.courierStatements.validated.count),
              })}
              icon={Percent}
              tone="var(--chart-1)"
            />
          </KpiGrid>
        </div>
      </div>
    </div>
  );
}

function SectionTitle({ title, hint }: { title: string; hint: string }) {
  return (
    <div>
      <h2 className="text-base font-semibold tracking-tight">{title}</h2>
      <p className="text-muted-foreground text-sm">{hint}</p>
    </div>
  );
}
