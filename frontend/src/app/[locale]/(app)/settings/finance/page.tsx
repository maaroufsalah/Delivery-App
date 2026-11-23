import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { FinanceForm } from '@/features/settings/finance-form';
import { getBankAccounts } from '@/lib/api/finance';
import { getFinanceSettings } from '@/lib/api/settings';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('settings.finance');
  return { title: t('title') };
}

/**
 * Paramètres > Finance — le regroupement de tout ce qui touche à l'argent.
 *
 * Les comptes bancaires sont chargés ici parce que l'un d'eux se choisit comme
 * compte par défaut des virements : sans la liste, le réglage ne serait qu'un
 * identifiant à recopier à la main.
 */
export default async function FinanceSettingsPage() {
  const [settings, accounts, t] = await Promise.all([
    getFinanceSettings(),
    getBankAccounts(),
    getTranslations('settings.finance'),
  ]);

  return (
    <section className="flex flex-col gap-4">
      <div>
        <h2 className="text-lg font-semibold tracking-tight">{t('title')}</h2>
        <p className="text-muted-foreground mt-1 text-sm">{t('subtitle')}</p>
      </div>
      <FinanceForm settings={settings} accounts={accounts} />
    </section>
  );
}
