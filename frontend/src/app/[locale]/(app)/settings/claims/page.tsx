import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { ClaimTypesForm } from '@/features/settings/claim-types-form';
import { getClaimTypes } from '@/lib/api/claims';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('settings.claims');
  return { title: t('title') };
}

/**
 * Paramètres > Réclamations — le référentiel des types (D-019).
 *
 * Le DÉLAI de traitement vit dans Paramètres > Métier, avec les autres réglages
 * d'installation : c'est une politique de service, pas un élément de
 * référentiel, et il n'a pas sa place au milieu d'une liste de motifs.
 */
export default async function ClaimSettingsPage() {
  const [types, t] = await Promise.all([getClaimTypes(), getTranslations('settings.claims')]);

  return (
    <section className="flex flex-col gap-4">
      <div>
        <h2 className="text-lg font-semibold tracking-tight">{t('title')}</h2>
        <p className="text-muted-foreground mt-1 text-sm">{t('subtitle')}</p>
      </div>
      <ClaimTypesForm types={types} />
    </section>
  );
}
