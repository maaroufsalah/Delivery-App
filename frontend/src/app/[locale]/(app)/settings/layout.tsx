import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { SettingsTabs } from '@/features/settings/settings-tabs';
import { getCurrentUser } from '@/lib/api/auth';

export default async function SettingsLayout({ children }: { children: React.ReactNode }) {
  const [t, user] = await Promise.all([getTranslations('settings'), getCurrentUser()]);

  return (
    <>
      <PageHeader title={t('title')} subtitle={t('subtitle')} />
      <SettingsTabs permissions={user?.permissions ?? []} />
      {children}
    </>
  );
}
