'use client';

import type { FinanceDashboard } from '@delivery/shared';
import { Banknote, Clock, FileText, TrendingUp, Wallet } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';

/**
 * Le point du jour de la facturation.
 *
 * Quatre chiffres et une liste, et tous mènent quelque part : le cash à
 * remettre ouvre les retours de fonds du livreur, les impayés ouvrent les
 * factures non soldées. Un tableau de bord qui ne fait que montrer laisse
 * l'opérateur chercher lui-même où agir.
 *
 * Le COD n'est pas du chiffre d'affaires — c'est de l'argent qui traverse la
 * société. Seuls les frais lui appartiennent, et c'est le seul des quatre
 * chiffres qui mesure ce qu'elle gagne.
 *
 * **Le sélecteur d'agence a quitté cet écran** au point 10 : il vit maintenant
 * dans la barre de filtres de la page, avec la période et le livreur. Deux
 * sélecteurs d'agence sur une même page auraient pu en afficher deux valeurs.
 */
export function FinanceDashboardScreen({ data }: { data: FinanceDashboard }) {
  const t = useTranslations('finance.dashboard');
  const format = useFormatter();

  const money = (cents: number) => format.number(cents / 100, 'currency');

  return (
    <div className="flex flex-col gap-4">
      <KpiGrid>
        <KpiCard
          label={t('cashToRemit')}
          value={money(data.cashToRemitTotal)}
          hint={t('couriersInvolved', { count: String(data.cashToRemit.length) })}
          icon={Wallet}
          tone="var(--chart-3)"
        />

        <KpiCard
          label={t('remittedNotInvoiced')}
          value={money(data.remittedNotInvoiced.amount)}
          hint={t('parcels', { count: String(data.remittedNotInvoiced.parcelCount) })}
          icon={Banknote}
          tone="var(--chart-2)"
        />

        <KpiCard
          label={t('unpaid')}
          value={money(data.unpaidInvoices.amount)}
          hint={
            data.unpaidInvoices.oldestAgeDays === null
              ? t('noUnpaid')
              : t('oldest', {
                  count: String(data.unpaidInvoices.count),
                  days: String(data.unpaidInvoices.oldestAgeDays),
                })
          }
          icon={FileText}
          tone={data.unpaidInvoices.overdueCount > 0 ? 'var(--destructive)' : 'var(--chart-1)'}
        />

        <KpiCard
          label={t('feeRevenue')}
          value={money(data.feeRevenueThisMonth)}
          hint={t('feeRevenueHint')}
          icon={TrendingUp}
          tone="var(--chart-4)"
        />
      </KpiGrid>

      {data.unpaidInvoices.overdueCount > 0 ? (
        <p className="text-destructive text-sm">
          <Clock className="me-1.5 inline size-4 align-[-3px]" aria-hidden />
          {t('overdue', { count: String(data.unpaidInvoices.overdueCount) })}{' '}
          <Link href="/invoices?status=issued" className="underline underline-offset-2">
            {t('seeInvoices')}
          </Link>
        </p>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>{t('byCourier')}</CardTitle>
          <CardDescription>{t('byCourierHint')}</CardDescription>
        </CardHeader>
        <CardContent>
          {data.cashToRemit.length === 0 ? (
            <p className="text-muted-foreground py-6 text-center text-sm">{t('noCash')}</p>
          ) : (
            <ul className="divide-border divide-y">
              {data.cashToRemit.map((row) => (
                <li key={row.courierId} className="flex items-center gap-3 py-2.5">
                  <Link
                    href={`/remittances?courierId=${row.courierId}`}
                    className="min-w-0 flex-1 text-sm hover:underline"
                  >
                    {row.courierName}
                  </Link>
                  <span className="text-muted-foreground shrink-0 text-xs">
                    {t('parcels', { count: String(row.parcelCount) })}
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
    </div>
  );
}
