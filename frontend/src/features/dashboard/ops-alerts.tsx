'use client';

import type { DashboardAlerts, DashboardOverviewAlerts } from '@delivery/shared';
import {
  AlertTriangle,
  Banknote,
  MapPinX,
  PackageX,
  PhoneCall,
  Route as RouteIcon,
  ShieldCheck,
  TrendingDown,
  Undo2,
} from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ParcelStatusBadge } from '@/features/parcels/status-badge';
import { Link } from '@/i18n/navigation';

type Severity = 'critical' | 'warning';

/** Une famille rendue : son titre, son nombre, ses lignes cliquables. */
interface Family {
  key: string;
  severity: Severity;
  icon: typeof AlertTriangle;
  title: string;
  count: number;
  /** Le lien « tout voir » de la famille. */
  href?: string;
  items: Array<{ key: string; href: string; primary: string; secondary?: string; badge: React.ReactNode }>;
}

/**
 * ALERTES OPÉRATIONNELLES — chantier « Dashboard de pilotage », spec § 10.
 *
 * Huit familles : les sept du prompt, plus « colis enlisés en relance » qui
 * existait déjà et qu'on ne supprime pas. Chaque ligne est un LIEN vers la
 * donnée concernée — la fiche du livreur, le colis, le circuit, le RF-, la
 * liste des colis d'une ville. Les deux familles d'argent n'arrivent qu'avec
 * `finance.read` : absentes du JSON, absentes de l'écran.
 *
 * Rouge = critique, orange = à surveiller — les deux teintes viennent des
 * tokens (`--destructive`, `--chart-5`), jamais d'une palette locale.
 */
/**
 * Les liens de détail, en DONNÉES : des chaînes et une requête d'URL, jamais
 * une fonction — une fonction ne traverse pas la frontière serveur → client
 * (correctif du 05/10). Les liens par élément se construisent ici.
 */
export interface OpsAlertLinks {
  /** Requête de la liste des colis (période, filtres), sans la ville ni le statut. */
  parcelsQuery: string;
  /** `statusIds` des échecs, pour « colis d'une ville en échec ». */
  failedStatusIds: string;
  openRoutes: string;
  stuckParcels: string;
  codNotRemitted: string;
  cashVariances: string;
}

export function OpsAlerts({
  alerts,
  legacy,
  links,
}: {
  alerts: DashboardAlerts;
  /** Les deux familles historiques de `GET /dashboard`. */
  legacy: DashboardOverviewAlerts;
  links: OpsAlertLinks;
}) {
  const hrefs = {
    courier: (id: string) => `/couriers/${id}`,
    parcel: (code: string) => `/parcels/${code}`,
    route: (code: string) => `/routes/${code}`,
    remittance: (code: string) => `/remittances/${encodeURIComponent(code)}`,
    cityParcels: (cityId: string) => {
      const next = new URLSearchParams(links.parcelsQuery);
      next.set('destinationCityId', cityId);
      next.set('statusIds', links.failedStatusIds);
      return `/parcels?${next.toString()}`;
    },
    openRoutes: links.openRoutes,
    stuckParcels: links.stuckParcels,
    codNotRemitted: links.codNotRemitted,
    cashVariances: links.cashVariances,
  };
  const t = useTranslations('dashboard.opsAlerts');
  const tAlerts = useTranslations('dashboard.alerts');
  const tKpi = useTranslations('dashboard.kpi');
  const format = useFormatter();
  const pct = (value: number) => format.number(value, 'percent');
  const money = (cents: number) => format.number(cents / 100, 'currency');
  const th = alerts.thresholds;

  const families: Family[] = [
    {
      key: 'courierReturnRate',
      severity: 'critical',
      icon: Undo2,
      title: t('courierReturnRate.title', { rate: pct(th.courierReturnRate) }),
      count: alerts.courierReturnRate.count,
      items: alerts.courierReturnRate.items.map((item) => ({
        key: item.courierId,
        href: hrefs.courier(item.courierId),
        primary: item.courierName,
        secondary: t('courierReturnRate.detail', { returned: String(item.returned), delivered: String(item.delivered) }),
        badge: pct(item.rate),
      })),
    },
    ...(alerts.codNotRemitted
      ? [
          {
            key: 'codNotRemitted',
            severity: 'warning' as Severity,
            icon: Banknote,
            title: t('codNotRemitted.title', { hours: String(th.codLateHours) }),
            count: alerts.codNotRemitted.count,
            href: hrefs.codNotRemitted,
            items: alerts.codNotRemitted.items.map((item) => ({
              key: item.courierId,
              href: hrefs.courier(item.courierId),
              primary: item.courierName,
              secondary: t('codNotRemitted.detail', {
                count: String(item.count),
                hours: format.number(item.oldestHours),
              }),
              badge: money(item.amount),
            })),
          },
        ]
      : []),
    {
      key: 'staleRoutes',
      severity: 'critical',
      icon: RouteIcon,
      title: t('staleRoutes.title'),
      count: alerts.staleRoutes.count,
      href: hrefs.openRoutes,
      items: alerts.staleRoutes.items.map((item) => ({
        key: item.code,
        href: hrefs.route(item.code),
        primary: item.code,
        secondary: `${item.courierName} · ${item.cityName}`,
        badge: tAlerts('routes.hours', { count: String(item.hoursOpen) }),
      })),
    },
    {
      key: 'stuckParcels',
      severity: 'warning',
      icon: PackageX,
      title: t('stuckParcels.title', { days: String(th.stuckParcelDays) }),
      count: alerts.stuckParcels.count,
      href: hrefs.stuckParcels,
      items: alerts.stuckParcels.items.map((item) => ({
        key: item.code,
        href: hrefs.parcel(item.code),
        primary: item.code,
        secondary: item.recipientName,
        badge: (
          <span className="flex items-center gap-1.5">
            <ParcelStatusBadge statusId={item.statusId} />
            <span className="tabular-nums">{t('stuckParcels.days', { count: String(item.days) })}</span>
          </span>
        ),
      })),
    },
    ...(alerts.cashVariances
      ? [
          {
            key: 'cashVariances',
            severity: 'critical' as Severity,
            icon: AlertTriangle,
            title: t('cashVariances.title'),
            count: alerts.cashVariances.count,
            href: hrefs.cashVariances,
            items: alerts.cashVariances.items.map((item) => ({
              key: item.code,
              href: hrefs.remittance(item.code),
              primary: item.code,
              secondary: `${item.courierName} · ${item.cityName}`,
              badge: money(item.variance),
            })),
          },
        ]
      : []),
    {
      key: 'performanceDrops',
      severity: 'warning',
      icon: TrendingDown,
      title: t('performanceDrops.title', { pts: format.number(th.performanceDropPts * 100) }),
      count: alerts.performanceDrops.count,
      items: alerts.performanceDrops.items.map((item) => ({
        key: item.courierId,
        href: hrefs.courier(item.courierId),
        primary: item.courierName,
        secondary: t('performanceDrops.detail', {
          previous: pct(item.previousRate),
          current: pct(item.currentRate),
        }),
        badge: `−${format.number((item.previousRate - item.currentRate) * 100, { maximumFractionDigits: 0 })} ${tKpi('trendPoints')}`,
      })),
    },
    {
      key: 'cityFailureRate',
      severity: 'critical',
      icon: MapPinX,
      title: t('cityFailureRate.title', { rate: pct(th.cityFailureRate) }),
      count: alerts.cityFailureRate.count,
      items: alerts.cityFailureRate.items.map((item) => ({
        key: item.cityId,
        href: hrefs.cityParcels(item.cityId),
        primary: item.cityName,
        secondary: t('cityFailureRate.detail', { failed: String(item.failed), total: String(item.total) }),
        badge: pct(item.rate),
      })),
    },
    {
      key: 'stuckInRetry',
      severity: 'warning',
      icon: PhoneCall,
      title: tAlerts('retry.title'),
      count: legacy.stuckInRetry.length,
      items: legacy.stuckInRetry.slice(0, 5).map((item) => ({
        key: item.code,
        href: hrefs.parcel(item.code),
        primary: item.code,
        secondary: item.recipientName,
        badge: (
          <span className="flex items-center gap-1.5">
            <ParcelStatusBadge statusId={item.statusId} />
            <span className="tabular-nums">{tAlerts('retry.attempts', { count: item.attempts })}</span>
          </span>
        ),
      })),
    },
  ];

  const active = families.filter((family) => family.count > 0);
  const critical = active.filter((family) => family.severity === 'critical').length;

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-2">
        <div>
          <CardTitle className="flex items-center gap-2">
            <AlertTriangle className="text-primary size-4" aria-hidden />
            {t('title')}
          </CardTitle>
          <CardDescription>{t('description')}</CardDescription>
        </div>
        {active.length > 0 ? (
          <Badge
            variant="secondary"
            className="rounded-full tabular-nums"
            style={critical > 0 ? { color: 'var(--destructive)' } : undefined}
          >
            {t('families', { count: active.length })}
          </Badge>
        ) : null}
      </CardHeader>
      <CardContent className="flex flex-col gap-5">
        {active.length === 0 ? (
          <p className="text-muted-foreground flex items-center gap-2 text-sm">
            <ShieldCheck className="size-4" aria-hidden />
            {tAlerts('empty')}
          </p>
        ) : null}

        {active.map((family) => {
          const tone = family.severity === 'critical' ? 'var(--destructive)' : 'var(--chart-5)';
          return (
            <section key={family.key} aria-label={family.title}>
              <h3 className="mb-2 flex items-center gap-2 text-sm font-medium">
                <family.icon className="size-4 shrink-0" style={{ color: tone }} aria-hidden />
                <span className="min-w-0 flex-1 truncate">{family.title}</span>
                <Badge
                  variant="secondary"
                  className="rounded-full tabular-nums"
                  style={{ color: tone, backgroundColor: `color-mix(in oklab, ${tone} 12%, transparent)` }}
                >
                  {format.number(family.count)}
                </Badge>
              </h3>
              <ul className="flex flex-col gap-2">
                {family.items.map((item) => (
                  <li key={item.key}>
                    <Link
                      href={item.href}
                      className="hover:bg-accent/60 focus-visible:ring-ring flex flex-wrap items-center gap-2 rounded-md border p-2.5 text-sm outline-none focus-visible:ring-2"
                    >
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium">{item.primary}</span>
                        {item.secondary ? (
                          <span className="text-muted-foreground block truncate text-xs">
                            {item.secondary}
                          </span>
                        ) : null}
                      </span>
                      <span className="shrink-0 text-xs font-medium tabular-nums">{item.badge}</span>
                    </Link>
                  </li>
                ))}
              </ul>
              {family.href && family.count > family.items.length ? (
                <Link
                  href={family.href}
                  className="text-primary mt-1.5 inline-block text-xs font-medium hover:underline"
                >
                  {t('seeAll', { count: format.number(family.count) })}
                </Link>
              ) : null}
            </section>
          );
        })}
      </CardContent>
    </Card>
  );
}

