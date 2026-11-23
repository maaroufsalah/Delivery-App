import type { DashboardRoutes } from '@delivery/shared';
import { ArrowRight, Route as RouteIcon } from 'lucide-react';
import { getFormatter, getTranslations } from 'next-intl/server';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';

/**
 * PERFORMANCE DES CIRCUITS — chantier « Dashboard de pilotage », spec § 8.
 *
 * Composant serveur : huit chiffres, trois états (ouverts, > 24 h, > 48 h) et
 * cinq mesures de période. Les retards sont CLIQUABLES vers la liste des
 * circuits ouverts ; les deux seuils sont ceux du prompt — 24 h alerte, 48 h
 * critique.
 */
export async function RoutesBlock({
  routes,
  links,
}: {
  routes: DashboardRoutes;
  links: { open: string; closed: string };
}) {
  const [t, format] = await Promise.all([getTranslations('dashboard.routes'), getFormatter()]);

  const num = (value: number | null, digits = 1) =>
    value === null ? '—' : format.number(value, { maximumFractionDigits: digits });
  const hours = (value: number | null) => (value === null ? '—' : t('hours', { count: num(value) }));

  const cells: Array<{ key: string; label: string; value: string; href?: string; tone?: string }> = [
    { key: 'open', label: t('open'), value: format.number(routes.open), href: links.open },
    { key: 'closed', label: t('closed'), value: format.number(routes.closed), href: links.closed },
    {
      key: 'late24',
      label: t('late24'),
      value: format.number(routes.late24),
      href: links.open,
      tone: routes.late24 > 0 ? 'var(--chart-5)' : undefined,
    },
    {
      key: 'late48',
      label: t('late48'),
      value: format.number(routes.late48),
      href: links.open,
      tone: routes.late48 > 0 ? 'var(--destructive)' : undefined,
    },
    { key: 'avgParcels', label: t('avgParcels'), value: num(routes.avgParcelsPerRoute) },
    { key: 'avgDelivered', label: t('avgDelivered'), value: num(routes.avgDeliveredPerRoute) },
    { key: 'avgReturns', label: t('avgReturns'), value: num(routes.avgReturnsPerRoute) },
    {
      key: 'avgCod',
      label: t('avgCod'),
      value:
        routes.avgCodPerRoute === null
          ? '—'
          : format.number(routes.avgCodPerRoute / 100, 'currency'),
    },
    { key: 'avgDuration', label: t('avgDuration'), value: hours(routes.avgDurationHours) },
  ];

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-2">
        <div>
          <CardTitle className="flex items-center gap-2">
            <RouteIcon className="text-primary size-4" aria-hidden />
            {t('title')}
          </CardTitle>
          <CardDescription>{t('description')}</CardDescription>
        </div>
        <Button asChild variant="ghost" size="sm" className="gap-1.5 print:hidden">
          <Link href={links.open}>
            {t('seeAll')}
            <ArrowRight className="flip-rtl size-4" aria-hidden />
          </Link>
        </Button>
      </CardHeader>
      <CardContent>
        <dl className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {cells.map((cell) => {
            const body = (
              <>
                <dt className="text-muted-foreground truncate text-xs">{cell.label}</dt>
                <dd
                  className="mt-0.5 text-lg font-semibold tabular-nums"
                  style={cell.tone ? { color: cell.tone } : undefined}
                >
                  {cell.value}
                </dd>
              </>
            );
            return cell.href ? (
              <Link
                key={cell.key}
                href={cell.href}
                className="hover:bg-accent/60 focus-visible:ring-ring rounded-md border p-3 outline-none focus-visible:ring-2"
              >
                {body}
              </Link>
            ) : (
              <div key={cell.key} className="rounded-md border p-3">
                {body}
              </div>
            );
          })}
        </dl>
      </CardContent>
    </Card>
  );
}
