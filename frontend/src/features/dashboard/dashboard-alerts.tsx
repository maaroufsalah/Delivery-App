'use client';

import { PhoneCall, Route as RouteIcon, ShieldCheck } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ParcelStatusBadge } from '@/features/parcels/status-badge';
import { Link } from '@/i18n/navigation';
import type { DashboardAlerts as Alerts } from '@/lib/api/dashboard';

/**
 * Bloc d'alertes.
 *
 * Deux signaux, choisis parce qu'ils coûtent de l'argent sans être visibles
 * dans une liste triée par date :
 *  - un colis qui enchaîne les relances téléphoniques consomme du temps livreur
 *    et du temps service client sans jamais aboutir ;
 *  - un circuit ouvert depuis plus de 48 h signale des colis chez un livreur
 *    dont personne ne suit plus l'état (DOMAIN.md §1.13).
 */
export function DashboardAlerts({ alerts }: { alerts: Alerts }) {
  const t = useTranslations('dashboard.alerts');
  const empty = alerts.stuckInRetry.length === 0 && alerts.staleRoutes.length === 0;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('title')}</CardTitle>
        <CardDescription>{t('description')}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-5">
        {empty ? (
          <p className="text-muted-foreground flex items-center gap-2 text-sm">
            <ShieldCheck className="size-4" aria-hidden />
            {t('empty')}
          </p>
        ) : null}

        {alerts.stuckInRetry.length > 0 ? (
          <section>
            <h3 className="mb-2 flex items-center gap-2 text-sm font-medium">
              <PhoneCall className="text-destructive size-4" aria-hidden />
              {t('retry.title')}
              <Badge variant="secondary" className="rounded-full tabular-nums">
                {alerts.stuckInRetry.length}
              </Badge>
            </h3>
            <ul className="flex flex-col gap-2">
              {alerts.stuckInRetry.slice(0, 5).map((item) => (
                <li key={item.code}>
                  <Link
                    href={`/parcels/${item.code}`}
                    className="hover:bg-accent/60 focus-visible:ring-ring flex flex-wrap items-center gap-2 rounded-md border p-2.5 text-sm outline-none focus-visible:ring-2"
                  >
                    <span className="font-mono text-xs font-medium">{item.code}</span>
                    <span className="text-muted-foreground min-w-0 flex-1 truncate text-xs">
                      {item.recipientName}
                    </span>
                    <ParcelStatusBadge statusId={item.statusId} />
                    <Badge variant="outline" className="rounded-full tabular-nums">
                      {t('retry.attempts', { count: item.attempts })}
                    </Badge>
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        ) : null}

        {alerts.staleRoutes.length > 0 ? (
          <section>
            <h3 className="mb-2 flex items-center gap-2 text-sm font-medium">
              <RouteIcon className="text-destructive size-4" aria-hidden />
              {t('routes.title')}
              <Badge variant="secondary" className="rounded-full tabular-nums">
                {alerts.staleRoutes.length}
              </Badge>
            </h3>
            <ul className="flex flex-col gap-2">
              {alerts.staleRoutes.slice(0, 5).map((item) => (
                <li key={item.code}>
                  <Link
                    href={`/routes/${item.code}`}
                    className="hover:bg-accent/60 focus-visible:ring-ring flex flex-wrap items-center gap-2 rounded-md border p-2.5 text-sm outline-none focus-visible:ring-2"
                  >
                    <span className="font-mono text-xs font-medium">{item.code}</span>
                    <span className="text-muted-foreground min-w-0 flex-1 truncate text-xs">
                      {item.courierName} · {item.cityName}
                    </span>
                    <Badge variant="outline" className="rounded-full tabular-nums">
                      {t('routes.hours', { count: String(item.hoursOpen) })}
                    </Badge>
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        ) : null}
      </CardContent>
    </Card>
  );
}
