import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { ChangePasswordForm } from '@/features/accounts/change-password-form';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('accounts.changePassword');
  return { title: t('title') };
}

export default async function PasswordSettingsPage() {
  const t = await getTranslations('accounts.changePassword');

  return (
    <section className="flex flex-col gap-4">
      <div>
        <h2 className="text-lg font-semibold tracking-tight">{t('title')}</h2>
        <p className="text-muted-foreground mt-1 text-sm">{t('subtitle')}</p>
      </div>
      <ChangePasswordForm />
    </section>
  );
}
