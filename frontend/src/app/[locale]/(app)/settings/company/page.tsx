import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { CompanyForm } from '@/features/settings/company-form';
import { getCompanySettings, getThemeWorkbench } from '@/lib/api/settings';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('settings.company');
  return { title: t('title') };
}

export default async function CompanySettingsPage() {
  const [settings, workbench] = await Promise.all([
    getCompanySettings(),
    /*
     * Les palettes viennent de la BASE (B-10) : en ajouter une pour un client
     * ne doit pas demander un redéploiement. L'appel est best-effort — une API
     * muette sur ce point doit laisser l'écran utilisable avec ses sélecteurs,
     * pas le remplacer par une erreur.
     */
    getThemeWorkbench().catch(() => ({ palettes: [], standard: null, previous: null })),
  ]);

  return (
    <CompanyForm
      settings={settings}
      palettes={workbench.palettes}
      standard={workbench.standard}
      previous={workbench.previous}
    />
  );
}
