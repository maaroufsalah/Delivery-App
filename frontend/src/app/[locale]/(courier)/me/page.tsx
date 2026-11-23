import {
  courierFigures,
  findParcelStatus,
  recentActivity,
  STATUS_GROUP_COLORS,
} from '@delivery/shared';
import {
  Banknote,
  CheckCircle2,
  ChevronRight,
  Coins,
  Package,
  PackageSearch,
  RotateCcw,
  Route,
  Wallet,
  XCircle,
} from 'lucide-react';
import type { Metadata } from 'next';
import { getFormatter, getTranslations } from 'next-intl/server';
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { CodeLookup } from '@/features/courier/code-lookup';
import { Link } from '@/i18n/navigation';
import { getAnnouncementFeed } from '@/lib/api/announcements';
import { ProgressRing } from '@/features/courier/progress-ring';
import { getCourierMe, getCourierPerformance, getCourierRoutes } from '@/lib/api/courier';
import { getCourierPettyCash } from '@/lib/api/petty-cash';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courier.nav');
  return { title: t('home') };
}

/**
 * L'ACCUEIL DU LIVREUR SUR LE WEB — L1 bloc 2.
 *
 * **Le miroir de l'accueil mobile**, aux mêmes chiffres et dans le même ordre :
 * ce qui reste à faire se lit avant de choisir quoi faire. Les chiffres sortent
 * de `courierFigures()` — la MÊME fonction que le téléphone, partagée depuis
 * `packages/shared` — donc les deux supports ne peuvent pas afficher deux
 * comptes différents du même jour.
 *
 * **HUIT chiffres depuis L2 bloc 9**, comme la maquette 2 : trois compteurs de
 * colis, deux d'incidents, deux montants, puis le taux de livraison. Les deux
 * derniers viennent de `GET /courier/me/performance`, que la production n'a pas
 * encore : `courierFigures()` rend alors `null`, et une tuile `null`
 * DISPARAÎT — `null` n'est pas zéro, et zéro dirait « vous n'avez rien à
 * remettre », ce qui est faux tant qu'on n'en sait rien.
 *
 * **Ce que le web n'a pas, et ce qu'il met à la place.** Pas de scanner : un
 * navigateur d'agence n'a pas de caméra utile, et une permission caméra
 * demandée sur un poste fixe ne mène nulle part. La tuile devient « Saisir un
 * code » — un champ, une touche Entrée, la fiche colis.
 *
 * **Rendu au SERVEUR, sans squelette.** Le mobile en a besoin : il peint
 * d'abord, charge ensuite. Ici la page ne s'affiche qu'une fois les données
 * là ; un squelette ne se verrait jamais, et l'ajouter reviendrait à rendre la
 * page deux fois pour rien. Le chargement est celui du navigateur, et il est
 * déjà visible.
 */
export default async function CourierHomePage() {
  /*
   * La petite caisse (D-168) : sans la clé `pettycash.courier` l'API répond
   * 403, et la tuile disparaît — même règle que les actualités. Lancée avant le
   * `Promise.all`, elle part en parallèle sans en alourdir l'inférence.
   */
  const pettyCashRequest = getCourierPettyCash().catch(() => null);
  const [t, tNav, tStatus, format, me, routes, performance, news] = await Promise.all([
    getTranslations('courier.home'),
    getTranslations('courier.nav'),
    getTranslations('status'),
    getFormatter(),
    getCourierMe().catch(() => null),
    getCourierRoutes('active').catch(() => []),
    /*
     * La performance est l'endpoint neuf du chantier : un 404 ne vide pas
     * l'accueil, il retire deux tuiles et l'anneau. Même règle que les
     * actualités, et même raison.
     */
    getCourierPerformance('day').catch(() => undefined),
    /*
     * Les actualités sont un CONFORT : une API muette sur ce point ne doit pas
     * vider l'accueil, elle doit juste faire disparaître la section — c'est la
     * règle du bandeau du tableau de bord (C7 bloc 3), et celle du mobile.
     */
    getAnnouncementFeed(2).catch(() => []),
  ]);
  const pettyCash = await pettyCashRequest;

  const figures = courierFigures(performance, routes);
  const recent = recentActivity(routes);

  const money = (cents: number) =>
    format.number(cents / 100, { style: 'currency', currency: 'MAD' });

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold tracking-tight">
          {t('greeting', { name: me?.firstName ?? '' })}
        </h1>
        <p className="text-muted-foreground text-sm">
          {me?.agencyName ? `${t('role')} · ${me.agencyName} · ${me.cityName}` : t('role')}
        </p>
      </header>

      {/*
        LES HUIT CHIFFRES, dans l'ordre de la maquette : les colis, puis ce qui
        a mal tourné, puis l'argent. L'ordre n'est pas décoratif — l'inverse
        ferait commencer la journée par une note.

        Les COULEURS sont celles du référentiel (`STATUS_GROUP_COLORS`) et pas
        celles de la maquette, qui se contredit d'ailleurs : « à ramasser » y
        est bleu sur l'écran 2 et orange sur l'écran 14. Ce sont des familles de
        statuts, et le mobile les peint déjà ainsi (D-L2-1).
      */}
      <KpiGrid>
        {figures.toPickup === null ? null : (
          <KpiCard
            label={t('toPickup')}
            value={String(figures.toPickup)}
            icon={PackageSearch}
            tone={STATUS_GROUP_COLORS.initial}
          />
        )}
        <KpiCard
          label={t('toDeliver')}
          value={String(figures.toDeliver)}
          icon={Package}
          tone={STATUS_GROUP_COLORS.transit}
        />
        <KpiCard
          label={t('delivered')}
          value={String(figures.delivered)}
          icon={CheckCircle2}
          tone={STATUS_GROUP_COLORS.terminal}
        />
        <KpiCard
          label={t('returns')}
          value={String(figures.returning)}
          icon={RotateCcw}
          tone={STATUS_GROUP_COLORS.return}
        />
        {figures.failed === null ? null : (
          <KpiCard
            label={t('failed')}
            value={String(figures.failed)}
            icon={XCircle}
            tone={STATUS_GROUP_COLORS.failure}
          />
        )}
        <KpiCard
          label={t('collectedAmount')}
          value={money(figures.collectedInCents)}
          icon={Banknote}
        />
        {figures.toRemitInCents === null ? null : (
          <KpiCard label={t('toRemit')} value={money(figures.toRemitInCents)} icon={Wallet} />
        )}
      </KpiGrid>

      {/* --- La performance : l'anneau, et le détail à un clic ------------- */}
      {figures.deliveryRate ? (
        <section className="flex flex-col gap-3">
          <h2 className="text-base font-semibold">{t('performance')}</h2>

          <Link
            href="/me/stats"
            className="bg-card hover:bg-muted focus-visible:ring-ring flex items-center gap-4 rounded-xl border p-4 transition-colors outline-none focus-visible:ring-2"
          >
            <ProgressRing percent={figures.deliveryRate.percent} label={t('deliveryRate')} />

            <span className="min-w-0 flex-1">
              <span className="block text-sm font-medium">{t('deliveryRate')}</span>
              <span className="text-muted-foreground block text-sm">
                {t('rateCount', {
                  delivered: String(figures.deliveryRate.delivered),
                  total: String(figures.deliveryRate.total),
                })}
              </span>
            </span>

            <ChevronRight className="text-muted-foreground size-5 rtl:rotate-180" aria-hidden />
          </Link>
        </section>
      ) : null}

      {/* --- Actions rapides : le scanner remplacé par une saisie --------- */}
      <section className="flex flex-col gap-3">
        <h2 className="text-base font-semibold">{t('actions')}</h2>

        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <CodeLookup
            label={t('lookup')}
            placeholder={t('lookupPlaceholder')}
            submit={t('lookupSubmit')}
          />

          <QuickTile href="/me/circuits" icon={Route} label={tNav('circuits')} />
          <QuickTile href="/me/parcels" icon={PackageSearch} label={tNav('parcels')} />
          <QuickTile href="/me/billing" icon={Banknote} label={tNav('billing')} />
          {pettyCash ? (
            <QuickTile
              href="/me/petty-cash"
              icon={Coins}
              label={tNav('pettyCash')}
              badge={pettyCash.drafts}
            />
          ) : null}
        </div>
      </section>

      {/* --- Ce qui vient de se passer ------------------------------------ */}
      <section className="flex flex-col gap-3">
        <h2 className="text-base font-semibold">{t('recent')}</h2>

        {recent.length === 0 ? (
          <Card>
            <CardContent className="text-muted-foreground p-8 text-center text-sm">
              {t('noActivity')}
            </CardContent>
          </Card>
        ) : (
          <ul className="flex flex-col gap-2">
            {recent.map((parcel) => {
              const definition = findParcelStatus(parcel.statusId);

              return (
                <li key={parcel.id}>
                  <Card>
                    <CardContent className="flex items-center gap-3 p-4">
                      <span className="min-w-0 flex-1">
                        <Link
                          href={`/me/parcels/${parcel.code}`}
                          className="block truncate text-sm font-medium"
                        >
                          {parcel.recipientName}
                        </Link>
                        <span className="text-muted-foreground block truncate font-mono text-xs">
                          {parcel.code} · {parcel.destinationCityName}
                        </span>
                      </span>

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
        )}
      </section>

      {/* --- Actualités : la section DISPARAÎT si la route ne répond pas --- */}
      {news.length > 0 ? (
        <section className="flex flex-col gap-3">
          <h2 className="text-base font-semibold">{tNav('news')}</h2>

          <ul className="flex flex-col gap-2">
            {news.map((item) => (
              <li key={item.id}>
                <Card>
                  <CardContent className="flex flex-col gap-1 p-4">
                    <span className="text-sm font-medium">{item.title}</span>
                    {item.intro ? (
                      <span className="text-muted-foreground text-sm">{item.intro}</span>
                    ) : null}
                  </CardContent>
                </Card>
              </li>
            ))}
          </ul>
        </section>
      ) : null}
    </div>
  );
}

/** Une tuile d'action : une destination, une icône, une cible large. */
function QuickTile({
  href,
  icon: Icon,
  label,
  badge,
}: {
  href: string;
  icon: typeof Route;
  label: string;
  /** Un compteur en attente (D-168 : ses brouillons de petite caisse). */
  badge?: number;
}) {
  return (
    <Link
      href={href}
      className="bg-card hover:bg-muted focus-visible:ring-ring flex min-h-20 items-center gap-3 rounded-xl border p-4 text-sm font-medium transition-colors outline-none focus-visible:ring-2"
    >
      <span className="bg-primary/10 text-primary flex size-10 shrink-0 items-center justify-center rounded-lg">
        <Icon className="size-5" aria-hidden />
      </span>
      {label}
      {badge ? (
        <Badge className="ms-auto rounded-full tabular-nums" aria-label={String(badge)}>
          {badge}
        </Badge>
      ) : null}
    </Link>
  );
}
