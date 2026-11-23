import { routeTargetsSender } from '@delivery/shared';
import { ChevronRight, PackageSearch, Truck } from 'lucide-react';
import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { Card, CardContent } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';
import { getCourierRoutes } from '@/lib/api/courier';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courier.nav');
  return { title: t('circuits') };
}

/**
 * LES CIRCUITS — L2 bloc 9, maquette 5, le miroir de l'écran mobile.
 *
 * **L'écran qui manquait des deux côtés.** `/me/tour` montrait la PREMIÈRE
 * tournée active ; depuis C12, un livreur qui ramasse le matin et livre
 * l'après-midi en a deux le même jour (D-085), et la seconde n'était
 * atteignable par aucun chemin. Elle devient `/me/circuits/[code]`, et cette
 * page les liste.
 *
 * **Les brouillons apparaissent aussi.** Sur le téléphone ils se composent au
 * scan ; sur le web on ne scanne pas, mais un livreur qui a chargé sa moto le
 * matin doit pouvoir vérifier au bureau ce qu'il porte, et démarrer son circuit
 * si le réseau lui a manqué dehors.
 */
export default async function CourierCircuitsPage() {
  const [t, tNav, tTour] = await Promise.all([
    getTranslations('courier.circuits'),
    getTranslations('courier.nav'),
    getTranslations('courier.tour'),
  ]);

  const [active, drafts] = await Promise.all([
    getCourierRoutes('active').catch(() => []),
    getCourierRoutes('draft').catch(() => []),
  ]);

  /*
   * LIVRAISON SEULEMENT — Recette 5, point 1, comme sur le téléphone. Les
   * circuits de ramassage sont sous « Ramassages » (`/me/pickups`).
   */
  const routes = [...active, ...drafts].filter((route) => !routeTargetsSender(route.kind));

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold tracking-tight">{tNav('circuits')}</h1>

      {routes.length === 0 ? (
        <Card>
          <CardContent className="text-muted-foreground flex flex-col items-center gap-3 p-10 text-center text-sm">
            <Truck className="size-10 opacity-40" aria-hidden />
            {tTour('empty')}
          </CardContent>
        </Card>
      ) : (
        <ul className="grid gap-3 lg:grid-cols-2">
          {routes.map((route) => {
            const toSender = routeTargetsSender(route.kind);
            const done = route.parcelCount - route.remainingCount;

            return (
              <li key={route.id}>
                <Link
                  href={`/me/circuits/${route.code}`}
                  className="focus-visible:ring-ring block rounded-xl outline-none focus-visible:ring-2"
                >
                  <Card className="hover:bg-muted/50 transition-colors">
                    <CardContent className="flex flex-col gap-3 p-4">
                      <div className="flex items-center gap-3">
                        <span
                          className={
                            toSender
                              ? 'bg-success/15 text-success flex size-11 shrink-0 items-center justify-center rounded-xl'
                              : 'bg-primary/10 text-primary flex size-11 shrink-0 items-center justify-center rounded-xl'
                          }
                        >
                          {toSender ? (
                            <PackageSearch className="size-5" aria-hidden />
                          ) : (
                            <Truck className="size-5" aria-hidden />
                          )}
                        </span>

                        <span className="min-w-0 flex-1">
                          <span className="block text-sm font-semibold">
                            {t(toSender ? 'pickup' : 'delivery')}
                          </span>
                          <span className="text-muted-foreground block text-sm">
                            {t(toSender ? 'points' : 'deliveries', {
                              count: String(route.parcelCount),
                            })}
                            {route.status === 'draft' ? ` · ${t('draft')}` : ''}
                          </span>
                        </span>

                        <ChevronRight
                          className="text-muted-foreground size-5 rtl:rotate-180"
                          aria-hidden
                        />
                      </div>

                      {/* La progression, seulement quand le circuit est parti :
                          un brouillon n'a rien à montrer d'autre que ce qu'on
                          vient d'y poser. */}
                      {route.status === 'active' ? (
                        <span className="flex flex-col gap-1.5">
                          <span className="bg-muted block h-2 w-full overflow-hidden rounded-full">
                            <span
                              className="bg-primary block h-full rounded-full"
                              style={{
                                width: `${
                                  route.parcelCount > 0
                                    ? Math.round((done / route.parcelCount) * 100)
                                    : 0
                                }%`,
                              }}
                            />
                          </span>
                          <span className="text-muted-foreground text-xs">
                            {tTour('progress', {
                              done: String(done),
                              total: String(route.parcelCount),
                            })}
                          </span>
                        </span>
                      ) : null}
                    </CardContent>
                  </Card>
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
