import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { ThemeForm } from '@/features/settings/theme-form';
import { getCompanySettings, getThemeWorkbench } from '@/lib/api/settings';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('settings.theme');
  return { title: t('title') };
}

export default async function ThemeSettingsPage() {
  const [settings, workbench] = await Promise.all([
    getCompanySettings(),
    getThemeWorkbench().catch(() => ({ palettes: [], standard: null, previous: null })),
  ]);

  return (
    <ThemeForm settings={settings} standard={workbench.standard} previous={workbench.previous} />
  );
}
