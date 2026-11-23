import {
  Banknote,
  ChevronRight,
  Clock,
  FileText,
  History,
  Percent,
  Scale,
  Wallet,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import type { Metadata } from 'next';
import { getFormatter, getTranslations } from 'next-intl/server';
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { Card, CardContent } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';
import { getCourierOwnBalance, getCourierPerformance } from '@/lib/api/courier';
import { getCourierStatementPage, getRemittancePage } from '@/lib/api/finance';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courier.nav');
  return { title: t('billing') };
}

/**
 * LA FACTURATION — L2 bloc 9, maquette 10, le miroir de l'écran mobile.
 *
 * **Aucun endpoint neuf.** `GET /courier-statements` et `GET /remittances`
 * tournent depuis M1 et la portée serveur les borne au livreur. Ce qui manquait
 * n'était pas la donnée, c'était l'écran qui la rassemble : le livreur avait
 * deux listes et aucun endroit d'où lire son mois d'un coup d'œil.
 *
 * **Les totaux se somment sur les RELEVÉS**, pas sur les remises : un relevé
 * est la pièce qui fait foi entre l'agence et le livreur (C11, D-065), et les
 * remises n'en portent qu'une partie — le cash rendu, pas les commissions.
 *
 * **Trois des quatre tuiles d'Anass, et une quatrième qui change** (D-L2-9). Il
 * demande « Frais de livraison » ; ce n'est pas l'argent du livreur, c'est ce
 * que la société facture à l'e-commerçant, et D-072 tient que la donnée
 * commerciale ne voyage pas jusqu'à lui. La tuile devient « Net à payer ».
 */
export default async function CourierBillingPage() {
  const [t, tHome, tNav, tBalance, format] = await Promise.all([
    getTranslations('courier.billing'),
    getTranslations('courier.home'),
    getTranslations('courier.nav'),
    getTranslations('courier.balance'),
    getFormatter(),
  ]);

  const [statements, remittances, performance, balance] = await Promise.all([
    getCourierStatementPage({ pageSize: 25 }).catch(() => null),
    getRemittancePage({ pageSize: 25 }).catch(() => null),
    getCourierPerformance('month').catch(() => null),
    /*
     * SON SOLDE — chantier « Finance », point 4. « Visible dans l'app
     * livreur », demande la mission, et elle a raison : une dette qu'on ne voit
     * pas est une dette qu'on conteste le jour où elle est retenue sur une
     * paie.
     *
     * Best-effort comme ses voisins : une lecture qui échoue fait disparaître
     * la tuile, elle ne casse pas l'écran.
     */
    getCourierOwnBalance().catch(() => null),
  ]);

  const rows = statements?.items ?? [];

  const collected = rows.reduce((sum, row) => sum + row.codCollected, 0);
  const commissions = rows.reduce((sum, row) => sum + row.commissionsTotal, 0);
  const due = rows.filter((row) => row.paidAt === null).reduce((sum, row) => sum + row.totalDue, 0);

  const paidCount = rows.filter((row) => row.paidAt !== null).length;
  const pendingCount = rows.length - paidCount;

  const money = (cents: number) =>
    format.number(cents / 100, { style: 'currency', currency: 'MAD' });

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-xl font-semibold tracking-tight">{tNav('billing')}</h1>

      <KpiGrid>
        <KpiCard label={t('collected')} value={money(collected)} icon={Banknote} />
        <KpiCard label={t('commissions')} value={money(commissions)} icon={Percent} />
        <KpiCard label={t('due')} value={money(due)} icon={Wallet} />
        {/* « À remettre » vient de l'endpoint de performance, non déployé : la
            tuile disparaît plutôt que d'afficher un zéro qui voudrait dire
            « vous ne devez rien ». */}
        {performance ? (
          <KpiCard
            label={tHome('toRemit')}
            value={money(performance.toRemitInCents)}
            icon={Clock}
          />
        ) : null}
        {/*
          LE SOLDE, et seulement s'il n'est pas nul — point 4. Une tuile à
          « 0,00 » sur la moitié des livreurs aurait dilué celle qui compte :
          celle du livreur qui doit deux cents dirhams et ne le savait pas.
        */}
        {balance && balance.balance !== 0 ? (
          <KpiCard
            label={tBalance('title')}
            value={money(balance.balance)}
            hint={tBalance('hint')}
            icon={Scale}
          />
        ) : null}
      </KpiGrid>

      <section className="flex flex-col gap-3">
        <h2 className="text-base font-semibold">{t('details')}</h2>

        <Card>
          <CardContent className="flex flex-col p-1">
            <DetailRow
              icon={Banknote}
              label={t('paymentsReceived')}
              value={String(paidCount)}
              href="/me/statements?filter=paid"
            />
            <DetailRow
              icon={Clock}
              label={t('paymentsPending')}
              value={String(pendingCount)}
              href="/me/statements?filter=pending"
            />
            <DetailRow
              icon={FileText}
              label={t('statements')}
              value={String(rows.length)}
              href="/me/statements"
            />
            <DetailRow
              icon={History}
              label={t('history')}
              value={String(remittances?.items.length ?? 0)}
              href="/me/remittances"
            />
          </CardContent>
        </Card>
      </section>
    </div>
  );
}

/** Une ligne « Voir détails » : le libellé, son compte, un chevron. */
function DetailRow({
  icon: Icon,
  label,
  value,
  href,
}: {
  icon: LucideIcon;
  label: string;
  value: string;
  href: string;
}) {
  return (
    <Link
      href={href}
      className="hover:bg-muted focus-visible:ring-ring flex min-h-13 items-center gap-3 rounded-lg px-3 text-sm transition-colors outline-none focus-visible:ring-2"
    >
      <Icon className="text-muted-foreground size-4 shrink-0" aria-hidden />
      <span className="min-w-0 flex-1 truncate">{label}</span>
      <span className="text-muted-foreground font-semibold tabular-nums">{value}</span>
      <ChevronRight className="text-muted-foreground size-4 rtl:rotate-180" aria-hidden />
    </Link>
  );
}
