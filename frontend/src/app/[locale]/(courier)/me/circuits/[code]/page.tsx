import { findParcelStatus, routeTargetsSender } from '@delivery/shared';
import { Truck } from 'lucide-react';
import type { Metadata } from 'next';
import { getFormatter, getTranslations } from 'next-intl/server';
import { BackButton } from '@/components/back-button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { CourierNotePickup } from '@/features/courier/note-pickup';
import { Link } from '@/i18n/navigation';
import { getCourierRoutes } from '@/lib/api/courier';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courier.nav');
  return { title: t('circuits') };
}

/**
 * LE DÉTAIL D'UN CIRCUIT — C11 bloc 7 (D-068), restylé par L1 bloc 4, et
 * désigné par son CODE depuis L2 bloc 9 (maquette 6).
 *
 * **Ce que le code change.** L'écran montrait la première tournée active,
 * quelle qu'elle soit ; depuis C12 un livreur en a deux le même jour (D-085) et
 * la seconde n'était atteignable nulle part. `/me/circuits` les liste, et
 * celui-ci en montre UN.
 *
 * **Ce qui ne change pas** : la progression visible, les arrêts dans l'ordre du
 * sac avec leur rang (D-073), et l'accès direct à la fiche — donc au
 * changement de statut — depuis chaque ligne.
 *
 * **La source est `/courier/me/routes`**, comme sur le mobile : elle rend les
 * colis AVEC la tournée. `GET /routes` aurait demandé un appel par circuit
 * pour la même chose, et les deux supports auraient pu diverger.
 *
 * **Brouillons compris.** Sur le web on ne scanne pas, mais un livreur doit
 * pouvoir vérifier au bureau ce qu'il a chargé, et démarrer son circuit si le
 * réseau lui a manqué dehors.
 */
export default async function CourierCircuitPage({
  params,
}: {
  params: Promise<{ code: string }>;
}) {
  const { code } = await params;

  const [t, tNav, tNotes, tStatus, format, active, drafts] = await Promise.all([
    getTranslations('courier.tour'),
    getTranslations('courier.nav'),
    getTranslations('courier.notes'),
    getTranslations('status'),
    getFormatter(),
    getCourierRoutes('active').catch(() => []),
    getCourierRoutes('draft').catch(() => []),
  ]);

  /*
   * Le circuit demandé, et rien d'autre. La portée serveur ne rend que les
   * siens : un code inconnu n'est donc pas « celui d'un autre livreur », c'est
   * un code qui n'existe pas pour lui.
   */
  const routes = [...active, ...drafts].filter((route) => route.code === decodeURIComponent(code));

  const money = (cents: number) =>
    format.number(cents / 100, { style: 'currency', currency: 'MAD' });

  if (routes.length === 0) {
    return (
      <div className="flex flex-col gap-4">
        {/* Un retour — point 4. Cette page n'en avait aucun. */}
        <BackButton fallbackHref="/me/circuits" />
        <h1 className="text-xl font-semibold tracking-tight">{tNav('circuits')}</h1>
        <Card>
          <CardContent className="text-muted-foreground flex flex-col items-center gap-3 p-10 text-center text-sm">
            <Truck className="size-10 opacity-40" aria-hidden />
            {t('empty')}
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <BackButton fallbackHref="/me/circuits" />
      <h1 className="text-xl font-semibold tracking-tight">{tNav('circuits')}</h1>

      {routes.map((route) => {
        const done = route.parcelCount - route.remainingCount;
        const toSender = routeTargetsSender(route.kind);

        return (
          <section key={route.id} className="flex flex-col gap-3">
            <header className="flex flex-col gap-2">
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <h2 className="text-base font-semibold">
                  {t('progress', { done: String(done), total: String(route.parcelCount) })}
                </h2>
                <span className="text-muted-foreground font-mono text-xs">{route.code}</span>
              </div>
              {/*
                « 10 livrés · 2 à retourner (1 bon de retour en attente de
                réception) » — Recette 7, point 8 (D-155) : le circuit se clôt
                tout seul quand les retours sont reçus par l'agence.
              */}
              <div className="text-muted-foreground text-xs tabular-nums">
                {route.kind === 'delivery'
                  ? `${t('autoProgress', { delivered: String(route.deliveredCount), toReturn: String(route.toReturnCount) })}${
                      route.pendingHandoverCount > 0
                        ? ` (${t('autoPending', { count: route.pendingHandoverCount })})`
                        : ''
                    }`
                  : null}
              </div>

              {/* La barre : deux div, pas un SVG. Une progression est un
                  rectangle dans un rectangle, et la largeur se dit en %. */}
              <div className="bg-muted h-2 w-full overflow-hidden rounded-full">
                <div
                  className="bg-primary h-full rounded-full"
                  style={{
                    width: `${route.parcelCount > 0 ? Math.round((done / route.parcelCount) * 100) : 0}%`,
                  }}
                />
              </div>

              {toSender ? null : (
                <p className="text-muted-foreground text-sm">
                  {t('toCollect')} · {money(route.codToCollect)}
                </p>
              )}
            </header>

            {/*
              LES BONS DE LA TOURNÉE — retouches Anass, point 5.

              Un vendeur qui prépare quarante colis fait UN bon : le livreur
              valide une fois, pas quarante. La section n'existe que sur un
              ramassage qui en porte — une livraison n'en a pas.
            */}
            {route.notes.length > 0 ? (
              <div className="flex flex-col gap-2">
                <p className="text-sm font-medium">{tNotes('title')}</p>
                {route.notes.map((note) => {
                  const belongs = new Set(note.parcelIds);

                  return (
                    <CourierNotePickup
                      key={note.id}
                      routeCode={route.code}
                      note={note}
                      parcels={route.parcels.filter((parcel) => belongs.has(parcel.id))}
                    />
                  );
                })}
              </div>
            ) : null}

            <ul className="flex flex-col gap-2">
              {route.parcels.map((parcel) => {
                const definition = findParcelStatus(parcel.statusId);

                return (
                  <li key={parcel.id}>
                    <Card className={parcel.confirmed ? 'opacity-60' : undefined}>
                      <CardContent className="flex items-center gap-3 p-4">
                        {/* LE RANG DU SAC : les colis sont chargés dans cet
                            ordre (D-073), et c'est celui-là qu'on cherche. */}
                        <span className="bg-muted flex size-8 shrink-0 items-center justify-center rounded-full text-xs font-bold">
                          {parcel.sequence}
                        </span>

                        <span className="min-w-0 flex-1">
                          <Link
                            href={`/me/parcels/${parcel.code}`}
                            className="block truncate text-sm font-medium"
                          >
                            {toSender ? parcel.senderName : parcel.recipientName}
                          </Link>
                          <span className="text-muted-foreground block truncate text-xs">
                            {toSender ? parcel.pickupAddress : parcel.deliveryAddress}
                            {` · ${parcel.destinationCityName}`}
                          </span>
                        </span>

                        {toSender ? null : (
                          <span className="hidden text-sm font-semibold tabular-nums sm:block">
                            {money(parcel.codToCollect)}
                          </span>
                        )}

                        {definition ? (
                          <Badge
                            variant="outline"
                            style={{ borderColor: definition.color, color: definition.color }}
                          >
                            {tStatus(definition.key)}
                          </Badge>
                        ) : null}
                      </CardContent>
                    </Card>
                  </li>
                );
              })}
            </ul>
          </section>
        );
      })}
    </div>
  );
}
