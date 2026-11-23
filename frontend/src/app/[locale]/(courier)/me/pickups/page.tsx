import { pickupNoteState, routeTargetsSender, type PickupNoteState } from '@delivery/shared';
import { ChevronRight, PackageSearch, Phone, Warehouse } from 'lucide-react';
import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { Card, CardContent } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';
import { getCourierNotes, getCourierRoutes } from '@/lib/api/courier';
import { cn } from '@/lib/utils';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courier.pickups');
  return { title: t('title') };
}

/** Les trois onglets de la maquette 7, dans son ordre. */
const TABS: PickupNoteState[] = ['pending', 'in-progress', 'done'];

/**
 * LA GESTION DES RAMASSAGES — L2 bloc 9, maquette 7, le miroir de l'écran
 * mobile.
 *
 * **Trois onglets pour deux états, et la règle est PARTAGÉE.** Le bon est « à
 * valider » ou « validé » (DOMAIN.md §1.12) ; Anass demande En attente, En
 * cours, Terminés. Le troisième est un état du TRAVAIL, pas du bon, et
 * `pickupNoteState()` le déduit des colis — la même fonction que le téléphone,
 * donc le même bon dans le même onglet des deux côtés.
 *
 * **L'onglet passe par l'URL** : `?tab=done` est une page qui se met en favori
 * et qui revient au bouton retour. Un état client aurait demandé un composant
 * client pour une liste rendue au serveur.
 *
 * **L'écran se tait tant que la route n'est pas déployée** :
 * `GET /courier/me/notes` est l'un des trois endpoints du chantier, et un 404
 * affiche un état vide qui le dit — jamais une erreur technique.
 */
export default async function CourierPickupsPage({
  searchParams,
}: {
  searchParams: Promise<{ tab?: string }>;
}) {
  const { tab: raw } = await searchParams;
  const tab: PickupNoteState = TABS.includes(raw as PickupNoteState)
    ? (raw as PickupNoteState)
    : 'pending';

  const [t, tParcels, tCircuits, notes, active, drafts] = await Promise.all([
    getTranslations('courier.pickups'),
    getTranslations('courier.parcels'),
    getTranslations('courier.circuits'),
    getCourierNotes('pickup').catch(() => null),
    getCourierRoutes('active').catch(() => []),
    getCourierRoutes('draft').catch(() => []),
  ]);

  /*
   * LES CIRCUITS DE RAMASSAGE — Recette 5, point 1. Ils étaient sous
   * « Circuits », mêlés aux livraisons ; ils vivent ici, au-dessus des bons.
   */
  const circuits = [...active, ...drafts].filter((route) => routeTargetsSender(route.kind));

  const rows = notes ?? [];
  const shown = rows.filter((note) => pickupNoteState(note) === tab);

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold tracking-tight">{t('title')}</h1>

      {circuits.length > 0 ? (
        <ul className="grid gap-2 lg:grid-cols-2">
          {circuits.map((route) => (
            <li key={route.id}>
              <Link
                href={`/me/circuits/${route.code}`}
                className="focus-visible:ring-ring block rounded-xl outline-none focus-visible:ring-2"
              >
                <Card className="hover:bg-muted/50 transition-colors">
                  <CardContent className="flex items-center gap-3 p-4">
                    <span className="bg-success/15 text-success flex size-11 shrink-0 items-center justify-center rounded-xl">
                      <PackageSearch className="size-5" aria-hidden />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block text-sm font-semibold">
                        {tCircuits('pickup')} · <span className="font-mono">{route.code}</span>
                      </span>
                      <span className="text-muted-foreground block text-sm">
                        {tCircuits('points', { count: String(route.parcelCount) })}
                        {route.status === 'draft' ? ` · ${tCircuits('draft')}` : ''}
                      </span>
                    </span>
                    <ChevronRight
                      className="text-muted-foreground size-5 rtl:rotate-180"
                      aria-hidden
                    />
                  </CardContent>
                </Card>
              </Link>
            </li>
          ))}
        </ul>
      ) : null}

      <nav className="flex flex-wrap gap-2" aria-label={t('title')}>
        {TABS.map((key) => (
          <Link
            key={key}
            href={key === 'pending' ? '/me/pickups' : `/me/pickups?tab=${key}`}
            aria-current={tab === key ? 'page' : undefined}
            className={cn(
              'focus-visible:ring-ring flex min-h-10 items-center gap-1.5 rounded-full border px-4 text-sm transition-colors outline-none focus-visible:ring-2',
              tab === key
                ? 'border-primary bg-primary text-primary-foreground font-medium'
                : 'hover:bg-muted text-muted-foreground',
            )}
          >
            {t(key)}
            <span className="tabular-nums opacity-70">
              {rows.filter((note) => pickupNoteState(note) === key).length}
            </span>
          </Link>
        ))}
      </nav>

      {shown.length === 0 ? (
        <Card>
          <CardContent className="text-muted-foreground flex flex-col items-center gap-3 p-10 text-center text-sm">
            <Warehouse className="size-10 opacity-40" aria-hidden />
            <span className="font-medium">{t(notes ? 'empty' : 'unavailable')}</span>
            {t(notes ? 'emptyHint' : 'unavailableHint')}
          </CardContent>
        </Card>
      ) : (
        <ul className="flex flex-col gap-2">
          {shown.map((note) => (
            <li key={note.id}>
              <Card>
                <CardContent className="flex flex-col gap-1.5 p-4">
                  <div className="flex items-center justify-between gap-3">
                    <span className="font-mono text-sm font-semibold">{note.code}</span>
                    <span className="text-muted-foreground text-xs">
                      {t('parcels', {
                        done: String(note.pickedCount),
                        total: String(note.parcelCount + note.removedCount),
                      })}
                    </span>
                  </div>

                  <span className="text-muted-foreground text-sm">
                    {tParcels('client')} : {note.clientName}
                  </span>
                  <span className="text-muted-foreground text-sm">
                    {tParcels('addressLabel')} : {note.address}, {note.cityName}
                  </span>

                  {/* Le geste du terrain : appeler le client. Sur le web il
                      ouvre le logiciel de téléphonie du poste, ou rien — et
                      c'est déjà mieux que de recopier un numéro à la main. */}
                  <a
                    href={`tel:${note.clientPhone}`}
                    className="text-primary focus-visible:ring-ring mt-1 inline-flex min-h-10 w-fit items-center gap-2 text-sm outline-none focus-visible:ring-2"
                  >
                    <Phone className="size-4" aria-hidden />
                    {note.clientPhone}
                  </a>
                </CardContent>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
