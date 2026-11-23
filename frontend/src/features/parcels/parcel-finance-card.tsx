'use client';

import {
  CHEQUE_STATUS_COLORS,
  findParcelStatus,
  type ParcelFinanceView,
  type ParcelRouteLink,
} from '@delivery/shared';
import { Banknote, CheckCircle2, Coins, CreditCard, FileText, Route, Wallet } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { Link } from '@/i18n/navigation';

/**
 * TOUT L'ARGENT DU COLIS, SUR LA FICHE — chantier « Finance », point 11, D-113.
 *
 * **Une carte, pas un onglet.** L'agent qui ouvre une fiche pour trancher une
 * contestation veut voir l'encaissement, la caisse et la facture dans le même
 * mouvement ; un onglet aurait demandé de retenir un chiffre le temps d'un
 * clic.
 *
 * **Aucun statut logistique ici.** Ni badge de statut, ni libellé d'état du
 * carton : l'axe logistique a sa carte, son badge et son vocabulaire, et les
 * mélanger est exactement la dette que D-105 ferme. La seule exception est
 * l'issue posée sur une COURSE, dans la carte des circuits — et elle y est
 * nommée comme telle.
 *
 * **La carte n'existe pas pour un e-commerçant** : l'API ne lui sérialise pas
 * `finance` (D-060), et le composant rend alors `null`.
 */
export function ParcelFinanceCard({ finance }: { finance: ParcelFinanceView | undefined }) {
  const t = useTranslations('parcels.detail.finance');
  const tCheque = useTranslations('finance.cheques.status');
  const format = useFormatter();

  if (!finance) return null;

  const money = (cents: number) => format.number(cents / 100, 'currency');

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Wallet className="size-4" aria-hidden />
          {t('title')}
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {/*
          ENCAISSÉ, et à côté ce qu'il FALLAIT encaisser. Le second sans le
          premier ne dit pas si l'affaire est close ; le premier sans le second
          ne dit pas s'il manque quelque chose.
        */}
        <div className="bg-accent text-accent-foreground flex items-baseline justify-between gap-3 rounded-lg px-4 py-3">
          <span className="text-sm font-medium">{t('collected')}</span>
          <span className="text-xl font-semibold tabular-nums">
            {finance.collected === null ? t('notCollected') : money(finance.collected)}
          </span>
        </div>

        <dl className="grid gap-2 text-sm">
          <Row label={t('toCollect')} value={money(finance.toCollect)} icon={Coins} />

          {finance.cash !== null ? (
            <Row label={t('cash')} value={money(finance.cash)} icon={Banknote} />
          ) : null}

          {finance.cheque !== null && finance.cheque > 0 ? (
            <Row label={t('cheque')} value={money(finance.cheque)} icon={CreditCard} />
          ) : null}

          {finance.commission !== null ? (
            <Row
              label={t('commission')}
              value={money(finance.commission)}
              sub={
                finance.commissionSource
                  ? t(`commissionSource.${finance.commissionSource}`)
                  : undefined
              }
            />
          ) : null}
        </dl>

        {finance.chequeNumber ? (
          <>
            <Separator />
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="text-muted-foreground text-xs">{t('chequeNumber')}</span>
              <span className="flex items-center gap-2">
                <span className="font-mono text-sm">{finance.chequeNumber}</span>
                {finance.chequeStatus ? (
                  <Dot color={CHEQUE_STATUS_COLORS[finance.chequeStatus]}>
                    {tCheque(finance.chequeStatus)}
                  </Dot>
                ) : null}
              </span>
            </div>
          </>
        ) : null}

        {finance.remittanceCode ? (
          <>
            <Separator />
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="text-muted-foreground text-xs">{t('remittance')}</span>
              {/*
                Le BON, par son NUMÉRO — D-115. C'était un jour de caisse ; un
                numéro se retrouve, s'imprime et se montre au livreur.
              */}
              <Link
                href={`/remittances/${encodeURIComponent(finance.remittanceCode)}`}
                className="font-mono text-sm underline underline-offset-2"
              >
                {finance.remittanceCode}
              </Link>
            </div>
          </>
        ) : null}

        {finance.invoiceCode || finance.payoutCode ? <Separator /> : null}

        {/*
          LA FACTURE DU VENDEUR. Le code est un lien : la fiche colis est le
          point d'où l'on remonte au dossier, pas une impasse où l'on relève un
          numéro pour aller le chercher dans une liste.
        */}
        {finance.invoiceCode ? (
          <div className="flex flex-wrap items-center justify-between gap-2">
            <span className="text-muted-foreground flex items-center gap-2 text-xs">
              <FileText className="size-3.5" aria-hidden />
              {t('invoice')}
            </span>
            <span className="flex items-center gap-2">
              <Link
                href={`/invoices/${encodeURIComponent(finance.invoiceCode)}`}
                className="font-mono text-sm underline underline-offset-2"
              >
                {finance.invoiceCode}
              </Link>
              {finance.invoicePaidAt ? (
                <Badge variant="secondary" className="gap-1 rounded-full font-normal">
                  <CheckCircle2 className="size-3" aria-hidden />
                  {format.dateTime(new Date(finance.invoicePaidAt), 'short')}
                </Badge>
              ) : null}
            </span>
          </div>
        ) : null}

        {finance.payoutCode ? (
          <div className="flex flex-wrap items-center justify-between gap-2">
            <span className="text-muted-foreground text-xs">{t('payout')}</span>
            <Link
              href={`/cod-payouts/${encodeURIComponent(finance.payoutCode)}`}
              className="font-mono text-sm underline underline-offset-2"
            >
              {finance.payoutCode}
            </Link>
          </div>
        ) : null}

        {finance.reimbursedAmount !== null ? (
          <div className="flex items-baseline justify-between gap-3 rounded-lg border px-4 py-2.5">
            <span className="text-sm font-medium">{t('reimbursed')}</span>
            <span className="text-sm font-semibold tabular-nums">
              {money(finance.reimbursedAmount)}
            </span>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

/**
 * LES CIRCUITS QUI ONT PORTÉ CE COLIS — point 11.
 *
 * Au pluriel : une tournée dont il est ressorti, une seconde présentation le
 * lendemain, un ramassage avant tout cela. N'afficher que le dernier aurait
 * effacé la moitié de l'histoire.
 *
 * C'est le seul endroit de la vue financière où un statut LOGISTIQUE apparaît,
 * et il y est nommé pour ce qu'il est : l'issue posée par le livreur sur CETTE
 * course, pas l'état du colis.
 */
export function ParcelRoutesCard({ routes }: { routes: ParcelRouteLink[] }) {
  const t = useTranslations('parcels.detail.routes');
  const tStatus = useTranslations('status');
  const tKind = useTranslations('routeKind');
  const format = useFormatter();

  if (routes.length === 0) return null;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Route className="size-4" aria-hidden />
          {t('title')}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <ul className="divide-border divide-y">
          {routes.map((route) => {
            const outcome =
              route.finalStatusId === null ? null : findParcelStatus(route.finalStatusId);

            return (
              <li key={route.routeId} className="flex flex-wrap items-center gap-2 py-2.5">
                <Link
                  href={`/routes/${encodeURIComponent(route.code)}`}
                  className="font-mono text-sm underline underline-offset-2"
                >
                  {route.code}
                </Link>
                <Badge variant="outline" className="rounded-full font-normal">
                  {tKind(route.kind)}
                </Badge>
                <span className="text-muted-foreground min-w-0 flex-1 truncate text-xs">
                  {route.courierName} · {route.cityName}
                </span>
                {outcome ? (
                  // « Issue de la course », pas « statut du colis ».
                  <Badge variant="secondary" className="rounded-full font-normal">
                    {tStatus(outcome.key)}
                  </Badge>
                ) : null}
                <span className="text-muted-foreground shrink-0 text-xs tabular-nums">
                  {route.confirmedAt
                    ? format.dateTime(new Date(route.confirmedAt), 'stamp')
                    : t('pending')}
                </span>
              </li>
            );
          })}
        </ul>
      </CardContent>
    </Card>
  );
}

function Row({
  label,
  value,
  sub,
  icon: Icon,
}: {
  label: string;
  value: string;
  sub?: string;
  icon?: React.ComponentType<{ className?: string }>;
}) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <dt className="text-muted-foreground flex items-center gap-2">
        {Icon ? <Icon className="size-3.5" aria-hidden /> : null}
        {label}
        {sub ? <span className="text-xs">({sub})</span> : null}
      </dt>
      <dd className="tabular-nums">{value}</dd>
    </div>
  );
}

function Dot({ color, children }: { color: string; children: React.ReactNode }) {
  return (
    <span
      className="inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs"
      style={{
        backgroundColor: `color-mix(in oklab, ${color} 16%, transparent)`,
        color,
      }}
    >
      {children}
    </span>
  );
}
