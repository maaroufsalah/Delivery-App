import { TriangleAlert } from 'lucide-react';
import { getTranslations } from 'next-intl/server';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ChangePasswordForm } from '@/features/accounts/change-password-form';

/**
 * Écran imposé à un compte encore sous mot de passe temporaire.
 *
 * Il remplace tout le back-office plutôt que de s'y ajouter : l'API ferme
 * toutes les routes sauf celle-ci (`@AllowPasswordChange`), donc un menu
 * accessible n'ouvrirait que des écrans en 403. Mieux vaut une porte fermée
 * qu'une porte qui claque.
 */
export async function ForcedPasswordChange() {
  const t = await getTranslations('accounts.changePassword');

  return (
    <div className="flex min-h-dvh items-center justify-center px-4 py-12">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>{t('title')}</CardTitle>
          <CardDescription className="flex items-start gap-2">
            <TriangleAlert className="mt-0.5 size-4 shrink-0 text-amber-500" />
            {t('forced')}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <ChangePasswordForm />
        </CardContent>
      </Card>
    </div>
  );
}
