import { AlertTriangle, Clock, Eye, Lock } from 'lucide-react';
import { getTranslations } from 'next-intl/server';
import type { TenantBanner as TenantState } from '@/lib/api/auth';

/**
 * Bandeau d'état de l'espace — C8 bloc 4, D-050.
 *
 * Trois situations, trois tons :
 *
 *  - **Essai en cours** — une ligne discrète qui rappelle l'échéance. Elle ne
 *    s'affiche qu'à partir du dernier tiers du mois : un compte à rebours
 *    présent dès le premier jour devient un meuble, et on ne le lit plus au
 *    moment où il compte.
 *  - **Essai terminé** — le ton monte, parce que l'écriture est fermée et que
 *    l'utilisateur va s'en rendre compte à sa prochaine action. Autant qu'il
 *    l'apprenne du bandeau plutôt que d'un message d'erreur.
 *  - **Espace suspendu** — l'API refuse déjà tout ; le bandeau dit qui appeler.
 *
 * Composant SERVEUR, sans état : le profil est lu par le layout, et ces
 * quelques mots n'ont aucune raison de traverser le pont client.
 */
export async function TenantBanner({
  tenant,
  impersonated = false,
}: {
  tenant: TenantState | null;
  /** Session ouverte depuis la console — C8, D-051. */
  impersonated?: boolean;
}) {
  const t = await getTranslations('tenant');

  /*
   * L'emprunt d'identité passe AVANT tout le reste : c'est l'information la
   * plus importante à l'écran. Quelqu'un qui agit dans l'espace d'un client
   * doit le savoir à chaque instant — une écriture faite ici est faite chez le
   * client, sous le nom de son administrateur.
   */
  if (impersonated) {
    return (
      <Banner tone="danger" icon={<Eye className="size-4 shrink-0" aria-hidden />}>
        <strong className="font-semibold">{t('impersonating.title')}</strong>{' '}
        {t('impersonating.body')}
      </Banner>
    );
  }

  // Administrateur de plateforme, ou société active : rien d'autre à dire.
  if (!tenant || tenant.status === 'ACTIVE') return null;

  if (tenant.status === 'SUSPENDED') {
    return (
      <Banner tone="danger" icon={<Lock className="size-4 shrink-0" aria-hidden />}>
        <strong className="font-semibold">{t('suspended.title')}</strong> {t('suspended.body')}
      </Banner>
    );
  }

  if (tenant.expired) {
    return (
      <Banner tone="danger" icon={<AlertTriangle className="size-4 shrink-0" aria-hidden />}>
        <strong className="font-semibold">{t('trial.expiredTitle')}</strong>{' '}
        {t('trial.expiredBody')}
      </Banner>
    );
  }

  const left = tenant.trialDaysLeft;

  /*
   * Un essai sans échéance ne s'annonce pas, et un essai qui a encore de la
   * marge non plus. Le seuil est volontairement une constante et non un
   * réglage : c'est une règle de la PLATEFORME, pas du client — D-019 vise ce
   * qui varie d'un client à l'autre, ce qui n'est pas le cas ici.
   */
  if (left === null || left > TRIAL_NOTICE_DAYS) return null;

  return (
    <Banner tone="warning" icon={<Clock className="size-4 shrink-0" aria-hidden />}>
      {t('trial.remaining', { count: left })}
    </Banner>
  );
}

/** À partir de combien de jours restants le bandeau apparaît. */
const TRIAL_NOTICE_DAYS = 10;

/**
 * L'enveloppe commune.
 *
 * Les couleurs sont fixes et NON dérivées de la marque : un avertissement qui
 * prendrait la couleur primaire du client serait illisible chez celui dont la
 * marque est déjà rouge, et inaudible chez les autres (D-033).
 */
function Banner({
  tone,
  icon,
  children,
}: {
  tone: 'warning' | 'danger';
  icon: React.ReactNode;
  children: React.ReactNode;
}) {
  const palette =
    tone === 'danger'
      ? 'border-red-500/30 bg-red-500/10 text-red-900 dark:text-red-200'
      : 'border-amber-500/30 bg-amber-500/10 text-amber-900 dark:text-amber-200';

  return (
    <div
      // `status` et non `alert` : l'information est importante mais pas
      // urgente, et `alert` interromprait la lecture d'un lecteur d'écran à
      // chaque navigation.
      role="status"
      className={`flex items-start gap-2 border-b px-4 py-2.5 text-sm sm:px-6 lg:px-8 ${palette}`}
    >
      {icon}
      <p className="text-balance">{children}</p>
    </div>
  );
}
