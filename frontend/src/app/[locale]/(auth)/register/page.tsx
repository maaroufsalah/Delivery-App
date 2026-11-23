import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { RegisterScreen } from '@/features/auth/register-screen';
import { Link } from '@/i18n/navigation';
import { getPublicCities } from '@/lib/api/public';
import { getCompanySettings } from '@/lib/api/settings';
import { CompanyLogo } from '@/components/layout/company-logo';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('auth.register');
  return { title: t('title') };
}

/**
 * Inscription publique — C13 bloc 1.
 *
 * La société n'apparaît nulle part : c'est celle de l'HÔTE. Cette page servie
 * sur `wimo.…` inscrit chez Wimo, la même sur `speedy.…` inscrit chez
 * SpeedyTrans, et le formulaire n'a pas à le savoir.
 */
export default async function RegisterPage() {
  const [settings, cities] = await Promise.all([getCompanySettings(), getPublicCities()]);

  return (
    <div className="flex min-h-dvh flex-col items-center justify-center px-4 py-12">
      <Link
        href="/"
        className="focus-visible:ring-ring mb-6 flex items-center gap-2.5 rounded-md outline-none focus-visible:ring-2"
      >
        <CompanyLogo src={settings.logo} background={settings.logoBackground} size="md" />
        <span className="text-base font-semibold tracking-tight">{settings.name}</span>
      </Link>

      <RegisterScreen cities={cities} />
    </div>
  );
}
