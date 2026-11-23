import { PLATFORM_NAME } from '@delivery/shared';
import { getTranslations } from 'next-intl/server';
import { LocaleSwitcher } from '@/components/layout/locale-switcher';
import { ThemeToggle } from '@/components/layout/theme-toggle';
import { Button } from '@/components/ui/button';
import { Link } from '@/i18n/navigation';

/**
 * En-tête et pied du domaine NU — C9 bloc 5.
 *
 * La landing de la plateforme n'avait aucune coquille : elle flottait, sans
 * marque visible ni moyen de revenir en haut. C'était cohérent quand la page
 * était une note d'information ; ça ne l'est plus pour une vitrine
 * commerciale, où le premier repère du visiteur est le logo.
 *
 * **Ce n'est pas `PublicHeader`.** Celui-là lit la configuration du TENANT
 * résolu par l'hôte et propose « suivre un colis » et « se connecter » : trois
 * choses qui n'existent pas ici. Le domaine nu n'a ni tenant, ni colis à
 * suivre, ni compte à ouvrir — s'y connecter n'aurait de sens que pour un
 * administrateur de plateforme, et cette porte-là ne s'annonce pas sur la
 * page d'accueil commerciale.
 *
 * Le nom et le logo sont ceux de `PLATFORM_BRAND`, jamais ceux d'un client.
 */

const CONTACT_EMAIL = 'contact@nocido.com';

export async function PlatformHeader() {
  const t = await getTranslations('platform.landing');

  return (
    <header className="bg-background/90 supports-[backdrop-filter]:bg-background/70 sticky top-0 z-30 border-b backdrop-blur">
      <div className="mx-auto flex h-16 w-full max-w-6xl items-center gap-3 px-4 sm:px-6">
        <a
          href="#"
          className="focus-visible:ring-ring flex min-w-0 items-center gap-2.5 rounded-md outline-none focus-visible:ring-2"
        >
          {/* eslint-disable-next-line @next/next/no-img-element -- SVG statique, pas d'optimisation à gagner */}
          <img
            src="/brands/nocido.svg"
            alt=""
            width={32}
            height={32}
            className="size-8 shrink-0 object-contain"
          />
          <span className="truncate text-sm font-semibold tracking-tight">{PLATFORM_NAME}</span>
        </a>

        <nav className="ms-auto flex items-center gap-1">
          <LocaleSwitcher />
          <ThemeToggle />
          <Button asChild size="sm" className="ms-1">
            <a href="#essai">{t('cta')}</a>
          </Button>
        </nav>
      </div>
    </header>
  );
}

export async function PlatformFooter() {
  const t = await getTranslations('legal');

  return (
    <footer className="border-t">
      <div className="text-muted-foreground mx-auto flex w-full max-w-6xl flex-col gap-3 px-4 py-10 text-sm sm:flex-row sm:items-center sm:justify-between sm:px-6">
        <span>
          © {new Date().getFullYear()} {PLATFORM_NAME}
        </span>

        {/*
          Les deux pages légales, liées depuis le domaine NU — M1 bloc 9.
          C'est l'adresse que les fiches des deux magasins pointent, et un
          relecteur d'Apple ou de Google doit pouvoir y arriver depuis
          l'accueil, sans compte et sans chercher.
        */}
        <nav className="flex flex-wrap items-center gap-x-4 gap-y-1">
          <Link href="/privacy" className="hover:text-foreground transition-colors">
            {t('privacy.title')}
          </Link>
          <Link href="/terms" className="hover:text-foreground transition-colors">
            {t('terms.title')}
          </Link>
          <a href={`mailto:${CONTACT_EMAIL}`} className="hover:text-foreground transition-colors">
            {CONTACT_EMAIL}
          </a>
        </nav>
      </div>
    </footer>
  );
}
