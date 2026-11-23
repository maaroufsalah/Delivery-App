import { CLAIM_STATUSES, type ClaimStatus } from '@delivery/shared';
import { MessageSquare, Plus } from 'lucide-react';
import type { Metadata } from 'next';
import { getFormatter, getTranslations } from 'next-intl/server';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { ClaimStatusBadge } from '@/features/claims/status-badge';
import { Link } from '@/i18n/navigation';
import { getClaims } from '@/lib/api/claims';
import { cn } from '@/lib/utils';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courier.nav');
  return { title: t('claims') };
}

/**
 * LES RÉCLAMATIONS DU LIVREUR — le complément de L2.
 *
 * **L'écran qui manquait, et qui se voyait.** `courier-shell.tsx` déclare
 * l'entrée « Réclamations » sous `needs: 'claims.read'` depuis L1 ; la page,
 * elle, n'a jamais été écrite. Tant que la clé n'était pas accordée, l'entrée
 * n'apparaissait pas et personne ne s'en apercevait — et c'est exactement pour
 * ça que la clôture V1 avait retiré la clé après l'avoir posée. Le déploiement
 * de L2 l'a accordée pour de bon : l'entrée est devenue un `404` visible par
 * tous les livreurs des deux tenants.
 *
 * **L'API était prête** : `claimWhere()` joint le colis et exige
 * `parcel.courierId` (B-19), donc `GET /claims` rend déjà une liste bornée aux
 * réclamations qui portent sur SES colis. Une réclamation sans colis —
 * « règlement non reçu », une affaire entre le vendeur et la société — lui
 * reste invisible, et c'est voulu.
 *
 * **Le filtre passe par l'URL**, comme partout dans `/me` : une page qu'on met
 * en favori, qu'on partage, et qui revient au bouton retour.
 */
export default async function CourierClaimsPage({
  searchParams,
}: {
  searchParams: Promise<{ status?: string }>;
}) {
  const { status: raw } = await searchParams;
  const status = CLAIM_STATUSES.includes(raw as ClaimStatus) ? (raw as ClaimStatus) : undefined;

  const [t, tClaims, tStatus, format, page] = await Promise.all([
    getTranslations('courier.claims'),
    getTranslations('claims'),
    getTranslations('claims.status'),
    getFormatter(),
    getClaims({ pageSize: 25, ...(status ? { status } : {}) }).catch(() => null),
  ]);

  const rows = page?.items ?? [];

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold tracking-tight">{tClaims('title')}</h1>

        <Link
          href="/me/claims/new"
          className="bg-primary text-primary-foreground focus-visible:ring-ring inline-flex min-h-10 items-center gap-2 rounded-lg px-4 text-sm font-medium outline-none focus-visible:ring-2"
        >
          <Plus className="size-4" aria-hidden />
          {tClaims('new')}
        </Link>
      </header>

      <nav className="flex flex-wrap gap-2" aria-label={tClaims('title')}>
        <FilterLink href="/me/claims" label={t('all')} active={!status} />
        {CLAIM_STATUSES.map((value) => (
          <FilterLink
            key={value}
            href={`/me/claims?status=${value}`}
            label={tStatus(value)}
            active={status === value}
          />
        ))}
      </nav>

      {rows.length === 0 ? (
        <Card>
          <CardContent className="text-muted-foreground flex flex-col items-center gap-3 p-10 text-center text-sm">
            <MessageSquare className="size-10 opacity-40" aria-hidden />
            <span className="font-medium">{tClaims('empty')}</span>
            {t('emptyHint')}
          </CardContent>
        </Card>
      ) : (
        <ul className="flex flex-col gap-2">
          {rows.map((claim) => (
            <li key={claim.id}>
              <Card>
                <CardContent className="flex flex-col gap-1.5 p-4">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <Link
                      href={`/me/claims/${claim.code}`}
                      className="font-mono text-sm font-semibold"
                    >
                      {claim.code}
                    </Link>

                    <span className="flex items-center gap-2">
                      {/* Le retard AVANT le statut : c'est lui qui appelle un
                          geste, et « non traitée depuis six jours » ne se lit
                          pas dans « non traitée ». */}
                      {claim.isLate ? <Badge variant="destructive">{t('late')}</Badge> : null}
                      <ClaimStatusBadge status={claim.status} />
                    </span>
                  </div>

                  <span className="text-sm font-medium">{claim.subject}</span>

                  <span className="text-muted-foreground text-xs">
                    {claim.typeLabel}
                    {claim.parcelCode ? ` · ${claim.parcelCode}` : ''} ·{' '}
                    {format.dateTime(new Date(claim.createdAt), 'stamp')}
                  </span>
                </CardContent>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/** Un filtre : un LIEN, donc une page qu'on met en favori et qu'on partage. */
function FilterLink({ href, label, active }: { href: string; label: string; active: boolean }) {
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
