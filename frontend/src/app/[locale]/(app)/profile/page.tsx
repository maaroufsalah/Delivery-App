import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { ChangePasswordForm } from '@/features/accounts/change-password-form';
import { ProfileForm } from '@/features/accounts/profile-form';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { requireSession } from '@/lib/session';
import { getCities } from '@/lib/api/directory';
import { getMerchantCompany } from '@/lib/api/merchant';
import { MerchantCompanyForm } from '@/features/merchant/merchant-company-form';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('accounts.profile');
  return { title: t('title') };
}

/**
 * « Mon profil » — C13 bloc 4, pour TOUS les rôles.
 *
 * Hors de `Paramètres`, volontairement : cette section est filtrée par
 * `settings.read`, et un livreur ou un e-commerçant n'y serait jamais allé
 * chercher son propre nom. L'écran se trouve sous le nom, dans le menu du bas,
 * là où l'on cherche ce qui nous concerne.
 *
 * Le changement de mot de passe vit ICI plutôt que sur son écran séparé : on
 * ouvre son profil pour la même raison dans les deux cas.
 */
export default async function ProfilePage() {
  // Sans session, il n'y a pas de profil à montrer. Le layout lève déjà, mais
  // cette page peut être atteinte pendant l'expiration d'un cookie — et c'est
  // un `401` qu'elle lève, jamais un renvoi (C17).
  const user = await requireSession();

  const t = await getTranslations('accounts.profile');
  const tPassword = await getTranslations('accounts.changePassword');

  /*
   * « MA SOCIÉTÉ » — V2 vendeur bloc 9, pour le VENDEUR seul.
   *
   * Un agent d'agence n'a pas de fiche client à modifier, et le back-office
   * lui offre déjà l'annuaire. Charger ces deux appels pour tout le monde
   * ferait payer à chaque ouverture de profil une section que personne d'autre
   * ne voit.
   */
  const merchant = user.clientId ? await Promise.all([getMerchantCompany(), getCities()]) : null;

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />

      <div className="flex flex-col gap-4">
        {merchant ? <MerchantCompanyForm company={merchant[0]} cities={merchant[1]} /> : null}

        <ProfileForm
          profile={{
            firstName: user.firstName,
            lastName: user.lastName,
            phone: user.phone ?? null,
            email: user.email,
          }}
        />

        <Card>
          <CardHeader>
            <CardTitle className="text-base">{tPassword('title')}</CardTitle>
            <CardDescription>{tPassword('subtitle')}</CardDescription>
          </CardHeader>
          <CardContent>
            <ChangePasswordForm />
          </CardContent>
        </Card>
      </div>
    </>
  );
}
