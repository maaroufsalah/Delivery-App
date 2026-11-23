import {
  AlertTriangle,
  Banknote,
  Boxes,
  FileText,
  MessageSquare,
  Package,
  PackageCheck,
  ReceiptText,
  Truck,
} from 'lucide-react';
import { getFormatter, getTranslations } from 'next-intl/server';
import {
  CHEQUE_PENDING_FILTER,
  getParcelStatus,
  MERCHANT_PARCEL_BUCKET_KEYS,
} from '@delivery/shared';
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { Card } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';
import { getCheques } from '@/lib/api/finance';
import { getMerchantSummary } from '@/lib/api/merchant';

/**
 * LE TABLEAU DE BORD DU VENDEUR — V2 vendeur bloc 2, spécification §8.
 *
 * **Pourquoi il ne réutilise pas celui du back-office.** `/dashboard` répond à
 * la question d'un exploitant : combien de colis sont partis, quel est le taux
 * de réussite, combien de cash reste à remettre par les livreurs. Un vendeur se
 * pose une autre question — où en sont MES colis, qui me doit quoi, qu'est-ce
 * qui attend une réponse — et les données internes qu'affiche l'autre écran lui
 * sont d'ailleurs interdites (D-060).
 *
 * **Le même endpoint que l'application mobile**, `GET /merchant/me/summary`.
 * Deux calculs séparés auraient fini par se contredire sur l'écran qui fait
 * entrer l'argent ; ici, le téléphone et le navigateur disent le même chiffre,
 * par construction.
 *
 * **Les seaux viennent du paquet partagé.** Le serveur remplit
 * `MERCHANT_PARCEL_BUCKETS`, l'écran le lit : un statut ajouté demain tombera
 * du bon côté des deux côtés à la fois.
 */
export async function MerchantDashboard() {
  const [summary, pendingCheques, t, tStatus, tBucket, format] = await Promise.all([
    getMerchantSummary(),
    /*
     * « MES CHÈQUES » — lot S (D-231). Le compteur sort de la LISTE elle-même
     * (`status=en-cours`, une ligne demandée, le total lu) : la tuile et
     * l'écran qu'elle ouvre comptent avec la même portée, par construction.
     * Best-effort : une panne de ce compteur ne doit pas éteindre l'accueil.
     */
    getCheques({ status: CHEQUE_PENDING_FILTER, pageSize: 1 })
      .then((page) => page.total)
      .catch(() => null),
    getTranslations('merchantDashboard'),
    getTranslations('status'),
    getTranslations('merchantBucket'),
    getFormatter(),
  ]);

  /* Les trois alertes ne s'affichent que lorsqu'elles ont quelque chose à
     dire : un « 0 facture impayée » occupe la place sans rien apprendre. */
  const alerts = [
    summary.unpaidInvoices > 0
      ? {
          key: 'invoices',
          icon: FileText,
          href: '/invoices',
          label: t('unpaidInvoices', { count: String(summary.unpaidInvoices) }),
          hint: format.number(summary.unpaidAmount / 100, 'currency'),
        }
      : null,
    summary.openClaims > 0
      ? {
          key: 'claims',
          icon: MessageSquare,
          href: '/claims',
          label: t('openClaims', { count: String(summary.openClaims) }),
          hint: null,
        }
      : null,
    summary.lowStockLines > 0
      ? {
          key: 'stock',
          icon: Boxes,
          href: '/stock',
          label: t('lowStockLines', { count: String(summary.lowStockLines) }),
          hint: null,
        }
      : null,
  ].filter((alert) => alert !== null);

  return (
    <div className="flex flex-col gap-6">
      <KpiGrid className={pendingCheques === null ? undefined : 'sm:grid-cols-3 lg:grid-cols-5'}>
        <KpiCard
          label={t('kpi.total')}
          value={format.number(summary.parcelsTotal, 'integer')}
          hint={t('kpi.totalHint')}
          icon={Package}
        />
        <KpiCard
          label={t('kpi.delivered')}
          value={format.number(summary.parcels.delivered, 'integer')}
          hint={t('kpi.deliveredHint')}
          icon={PackageCheck}
          tone="var(--chart-4)"
        />
        <KpiCard
          label={t('kpi.moving')}
          value={format.number(
            summary.parcels.inTransit + summary.parcels.outForDelivery,
            'integer',
          )}
          hint={t('kpi.movingHint')}
          icon={Truck}
          tone="var(--chart-3)"
        />
        <KpiCard
          label={t('kpi.codToReceive')}
          value={format.number(summary.codToReceive / 100, 'currency')}
          hint={summary.billingType === 'professional' ? t('kpi.codHintPro') : t('kpi.codHint')}
          icon={Banknote}
          tone="var(--chart-2)"
        />
        {pendingCheques === null ? null : (
          <KpiCard
            label={t('kpi.cheques')}
            value={format.number(pendingCheques, 'integer')}
            hint={t('kpi.chequesHint')}
            icon={ReceiptText}
            href={`/cheques?status=${CHEQUE_PENDING_FILTER}`}
          />
        )}
      </KpiGrid>

      {/* --- Les huit seaux de la spécification --- */}
      <Card className="gap-4 p-4">
        <h2 className="text-sm font-semibold tracking-tight">{t('buckets')}</h2>

        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-7">
          {MERCHANT_PARCEL_BUCKET_KEYS.map((bucket) => (
            <div key={bucket} className="bg-muted/40 rounded-lg px-3 py-2">
              <p className="text-xl font-semibold tabular-nums">
                {format.number(summary.parcels[bucket], 'integer')}
              </p>
              <p className="text-muted-foreground truncate text-xs">{tBucket(bucket)}</p>
            </div>
          ))}
        </div>
      </Card>

      <div className="grid gap-6 lg:grid-cols-3">
        {/* --- Dernières activités — spécification §2 --- */}
        <Card className="gap-3 p-4 lg:col-span-2">
          <h2 className="text-sm font-semibold tracking-tight">{t('activity')}</h2>

          {summary.activity.length === 0 ? (
            <p className="text-muted-foreground text-sm">{t('noActivity')}</p>
          ) : (
            <ul className="flex flex-col">
              {summary.activity.map((event, index) => {
                const definition = getParcelStatus(event.statusId);

                return (
                  <li
                    key={`${event.parcelCode ?? 'x'}-${event.at}-${index}`}
                    className="flex min-h-11 items-center gap-3 border-b py-2 last:border-b-0"
                  >
                    <span
                      className="size-2 shrink-0 rounded-full"
                      style={{ backgroundColor: definition?.color ?? 'var(--muted-foreground)' }}
                      aria-hidden
                    />
                    <span className="min-w-0 flex-1 truncate text-sm">
                      {definition ? tStatus(definition.key) : ''}
                    </span>
                    {event.parcelCode ? (
                      <Link
                        href={`/parcels/${encodeURIComponent(event.parcelCode)}`}
                        className="text-muted-foreground hover:text-foreground font-mono text-xs"
                      >
                        {event.parcelCode}
                      </Link>
                    ) : null}
                    <time className="text-muted-foreground shrink-0 text-xs" dateTime={event.at}>
                      {format.dateTime(new Date(event.at), 'stamp')}
                    </time>
                  </li>
                );
              })}
            </ul>
          )}
        </Card>

        {/* --- Ce qui appelle un geste --- */}
        <Card className="gap-3 p-4">
          <h2 className="text-sm font-semibold tracking-tight">{t('attention')}</h2>

          {alerts.length === 0 ? (
            <p className="text-muted-foreground text-sm">{t('nothingToDo')}</p>
          ) : (
            <ul className="flex flex-col gap-1">
              {alerts.map((alert) => (
                <li key={alert.key}>
                  <Link
                    href={alert.href}
                    className="hover:bg-muted flex min-h-11 items-center gap-3 rounded-lg px-2 text-sm"
                  >
                    <alert.icon className="text-muted-foreground size-4 shrink-0" aria-hidden />
                    <span className="min-w-0 flex-1 truncate">{alert.label}</span>
                    {alert.hint ? (
                      <span className="text-muted-foreground shrink-0 tabular-nums">
                        {alert.hint}
                      </span>
                    ) : null}
                  </Link>
                </li>
              ))}
            </ul>
          )}

          {alerts.length === 0 ? null : (
            <p className="text-muted-foreground flex items-center gap-1.5 text-xs">
              <AlertTriangle className="size-3.5" aria-hidden />
              {t('attentionHint')}
            </p>
          )}
        </Card>
      </div>
    </div>
  );
}
