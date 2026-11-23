import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { BusinessForm } from '@/features/settings/business-form';
import { getBusinessSettings } from '@/lib/api/accounts';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('settings.business');
  return { title: t('title') };
}

export default async function BusinessSettingsPage() {
  const [settings, t] = await Promise.all([
    getBusinessSettings(),
    getTranslations('settings.business'),
  ]);

  return (
    <section className="flex flex-col gap-4">
      <div>
        <h2 className="text-lg font-semibold tracking-tight">{t('title')}</h2>
        <p className="text-muted-foreground mt-1 text-sm">{t('subtitle')}</p>
      </div>
      <BusinessForm settings={settings} />
    </section>
  );
}
