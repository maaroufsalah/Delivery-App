import { Mail, MapPin, Package, Phone, Search } from 'lucide-react';
import { getTranslations } from 'next-intl/server';
import { LocaleSwitcher } from '@/components/layout/locale-switcher';
import { ThemeToggle } from '@/components/layout/theme-toggle';
import { Button } from '@/components/ui/button';
import { Link } from '@/i18n/navigation';
import { getCompanySettings } from '@/lib/api/settings';
import { CompanyLogo } from '@/components/layout/company-logo';

/**
 * En-tête et pied de page du site public.
 *
 * Ni `AppShell` ni sidebar : le visiteur n'est pas un agent. La marque et les
 * coordonnées sont lues dans la configuration société au rendu — et depuis C8
 * (D-048) cette configuration est celle du TENANT résolu par l'hôte : le même
 * code sert deux sociétés, chacune avec son nom, son logo et son téléphone.
 */
export async function PublicHeader() {
  const [settings, t] = await Promise.all([getCompanySettings(), getTranslations('public.nav')]);

  return (
    <header className="bg-background/90 supports-[backdrop-filter]:bg-background/70 sticky top-0 z-30 border-b backdrop-blur">
      <div className="mx-auto flex h-16 w-full max-w-6xl items-center gap-3 px-4 sm:px-6">
        <Link
          href="/"
          className="focus-visible:ring-ring flex min-w-0 items-center gap-2.5 rounded-md outline-none focus-visible:ring-2"
        >
          <CompanyLogo src={settings.logo} background={settings.logoBackground} />
          <span className="truncate text-sm font-semibold tracking-tight">{settings.name}</span>
        </Link>

        <nav className="ms-auto flex items-center gap-1">
          <Button asChild variant="ghost" size="sm" className="gap-1.5">
            <Link href="/track">
              <Search className="size-4" aria-hidden />
              <span className="hidden sm:inline">{t('track')}</span>
            </Link>
          </Button>
          <LocaleSwitcher />
          <ThemeToggle />
          <Button asChild size="sm" className="ms-1">
            <Link href="/login">{t('login')}</Link>
          </Button>
        </nav>
      </div>
    </header>
  );
}

export async function PublicFooter() {
  const [settings, t, legal] = await Promise.all([
    getCompanySettings(),
    getTranslations('public.footer'),
    getTranslations('legal'),
  ]);

  const sections = [
    { key: 'services' as const, links: ['delivery', 'pickup', 'cod', 'warehouse'] as const },
    { key: 'company' as const, links: ['about', 'courier', 'contact'] as const },
  ];

  /** Adresse et ville sur une ligne, sans virgule orpheline si l'une manque. */
  const address = [settings.address, settings.city]
    .map((part) => part?.trim())
    .filter(Boolean)
    .join(', ');

  return (
    <footer className="bg-muted/40 border-t">
      <div className="mx-auto w-full max-w-6xl px-4 py-10 sm:px-6">
        <div className="grid gap-8 sm:grid-cols-2 lg:grid-cols-4">
          <div className="lg:col-span-2">
            <div className="flex items-center gap-2.5">
              <CompanyLogo src={settings.logo} background={settings.logoBackground} />
              <span className="text-sm font-semibold tracking-tight">{settings.name}</span>
            </div>
            <p className="text-muted-foreground mt-3 max-w-sm text-sm">{t('tagline')}</p>

            {/*
              Les coordonnées réelles de la société — C8 bloc 3.

              Chaque ligne ne s'affiche que si elle est renseignée : un espace
              en essai n'a pas encore rempli sa fiche, et un pied de page avec
              « Téléphone : » suivi de rien fait plus de mal qu'un pied court.

              Téléphone et e-mail sont des LIENS : sur mobile, c'est là que le
              visiteur appuie pour appeler, et c'est la première chose qu'on
              cherche sur le site d'un transporteur.
            */}
            <ul className="text-muted-foreground mt-4 flex flex-col gap-1.5 text-sm">
              {address ? (
                <li className="flex items-start gap-2">
                  <MapPin className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                  <span>{address}</span>
                </li>
              ) : null}

              {settings.phone ? (
                <li className="flex items-center gap-2">
                  <Phone className="size-3.5 shrink-0" aria-hidden />
                  <a href={`tel:${settings.phone.replace(/\s+/g, '')}`} className="hover:underline">
                    <span dir="ltr">{settings.phone}</span>
                  </a>
                </li>
              ) : null}

              {settings.email ? (
                <li className="flex items-center gap-2">
                  <Mail className="size-3.5 shrink-0" aria-hidden />
                  <a href={`mailto:${settings.email}`} className="hover:underline">
                    <span dir="ltr">{settings.email}</span>
                  </a>
                </li>
              ) : null}
            </ul>
          </div>

          {sections.map((section) => (
            <div key={section.key}>
              <h2 className="text-xs font-semibold tracking-wider uppercase">
                {t(`sections.${section.key}`)}
              </h2>
              <ul className="mt-3 flex flex-col gap-2">
                {section.links.map((link) => (
                  <li key={link}>
                    <span className="text-muted-foreground text-sm">{t(`links.${link}`)}</span>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>

        <div className="text-muted-foreground mt-8 flex flex-wrap items-center justify-between gap-2 border-t pt-6 text-xs">
          {/* La raison sociale prime dans une mention de droits : c'est elle
              qui a valeur légale. Le nom d'usage supplée. */}
          <p>{t('rights', { company: settings.legalName ?? settings.name, year: '2026' })}</p>
          {/* Les mêmes deux pages que sur le domaine nu : elles décrivent la
              plateforme, qui est la même pour tous les transporteurs. */}
          <nav className="flex flex-wrap items-center gap-x-4 gap-y-1">
            <Link href="/privacy" className="hover:underline">
              {legal('privacy.title')}
            </Link>
            <Link href="/terms" className="hover:underline">
              {legal('terms.title')}
            </Link>
          </nav>
          <p className="flex items-center gap-1.5">
            <Package className="size-3.5" aria-hidden />
            {settings.country} · {settings.currency}
          </p>
        </div>
      </div>
    </footer>
  );
}
