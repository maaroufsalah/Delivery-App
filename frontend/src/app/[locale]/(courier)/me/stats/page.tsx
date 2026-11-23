import { STATUS_GROUP_COLORS } from '@delivery/shared';
import {
  Banknote,
  CheckCircle2,
  Package,
  PackageSearch,
  RotateCcw,
  Wallet,
  XCircle,
} from 'lucide-react';
import type { Metadata } from 'next';
import { getFormatter, getTranslations } from 'next-intl/server';
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { Card, CardContent } from '@/components/ui/card';
import { ProgressRing } from '@/features/courier/progress-ring';
import { Link } from '@/i18n/navigation';
import { getCourierPerformance } from '@/lib/api/courier';
import { cn } from '@/lib/utils';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courier.nav');
  return { title: t('stats') };
}

/**
 * MES STATISTIQUES — L2 bloc 9, le miroir de l'écran mobile.
 *
 * C'est aussi ce que le profil (maquette 12) appelle « Mes statistiques », et
 * la destination du chevron de performance de l'accueil.
 *
 * **La période passe par l'URL**, pas par un état client : `?period=month` est
 * une page à part entière, qui se met en favori, se partage et revient au
 * bouton retour. Un `useState` aurait demandé un composant client et un second
 * aller-retour pour la même chose.
 *
 * **Sans l'endpoint, l'écran le DIT.** Il n'invente pas des chiffres depuis la
 * tournée : l'accueil le fait déjà, et un écran de statistiques qui ne
 * compterait que la journée en cours mentirait sur son titre.
 */
export default async function CourierStatsPage({
  searchParams,
}: {
  searchParams: Promise<{ period?: string }>;
}) {
  const { period: raw } = await searchParams;
  const period = raw === 'month' ? 'month' : 'day';

  const [t, tNav, tStats, format, performance] = await Promise.all([
    getTranslations('courier.home'),
    getTranslations('courier.nav'),
    getTranslations('courier.stats'),
    getFormatter(),
    getCourierPerformance(period).catch(() => null),
  ]);

  const money = (cents: number) =>
    format.number(cents / 100, { style: 'currency', currency: 'MAD' });

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold tracking-tight">{tNav('stats')}</h1>

        <nav className="flex gap-2" aria-label={tNav('stats')}>
          <PeriodLink href="/me/stats" label={tStats('day')} active={period === 'day'} />
          <PeriodLink
            href="/me/stats?period=month"
            label={tStats('month')}
            active={period === 'month'}
          />
        </nav>
      </header>

      {!performance ? (
        <Card>
          <CardContent className="text-muted-foreground flex flex-col gap-2 p-10 text-center text-sm">
            <span className="font-medium">{tStats('unavailable')}</span>
            {tStats('unavailableHint')}
          </CardContent>
        </Card>
      ) : (
        <>
          {/* --- Le taux, en grand : c'est pour lui qu'on vient ----------- */}
          <Card>
            <CardContent className="flex flex-col items-center gap-3 p-8">
              <ProgressRing
                percent={performance.deliveryRate.percent}
                size={132}
                thickness={13}
                label={t('deliveryRate')}
              />
              <span className="text-center">
                <span className="block text-base font-semibold">{t('deliveryRate')}</span>
                <span className="text-muted-foreground block text-sm">
                  {t('rateCount', {
                    delivered: String(performance.deliveryRate.delivered),
                    total: String(performance.deliveryRate.total),
                  })}
                </span>
              </span>
            </CardContent>
          </Card>

          <section className="flex flex-col gap-3">
            <h2 className="text-base font-semibold">{tStats('counts')}</h2>
            <KpiGrid>
              <KpiCard
                label={t('toPickup')}
                value={String(performance.toPickup)}
                icon={PackageSearch}
                tone={STATUS_GROUP_COLORS.initial}
              />
              <KpiCard
                label={t('toDeliver')}
                value={String(performance.toDeliver)}
                icon={Package}
                tone={STATUS_GROUP_COLORS.transit}
              />
              <KpiCard
                label={t('delivered')}
                value={String(performance.delivered)}
                icon={CheckCircle2}
                tone={STATUS_GROUP_COLORS.terminal}
              />
              <KpiCard
                label={t('returns')}
                value={String(performance.returning)}
                icon={RotateCcw}
                tone={STATUS_GROUP_COLORS.return}
              />
              <KpiCard
                label={t('failed')}
                value={String(performance.failed)}
                icon={XCircle}
                tone={STATUS_GROUP_COLORS.failure}
              />
            </KpiGrid>
          </section>

          <section className="flex flex-col gap-3">
            <h2 className="text-base font-semibold">{tStats('amounts')}</h2>
            <KpiGrid>
              <KpiCard
                label={t('collectedAmount')}
                value={money(performance.collectedInCents)}
                icon={Banknote}
              />
              <KpiCard
                label={t('toRemit')}
                value={money(performance.toRemitInCents)}
                icon={Wallet}
              />
            </KpiGrid>
          </section>
        </>
      )}
    </div>
  );
}

/** Une période : un LIEN, donc une page qu'on met en favori et qu'on partage. */
function PeriodLink({ href, label, active }: { href: string; label: string; active: boolean }) {
  return (
    <Link
      href={href}
      aria-current={active ? 'page' : undefined}
      className={cn(
        'focus-visible:ring-ring flex min-h-10 items-center rounded-full border px-4 text-sm transition-colors outline-none focus-visible:ring-2',
        active
          ? 'border-primary bg-primary text-primary-foreground font-medium'
          : 'hover:bg-muted text-muted-foreground',
      )}
    >
      {label}
    </Link>
  );
}
