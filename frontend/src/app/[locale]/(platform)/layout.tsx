import { hasLocale, NextIntlClientProvider } from 'next-intl';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { ShieldCheck } from 'lucide-react';
import { LocaleSwitcher } from '@/components/layout/locale-switcher';
import { ThemeToggle } from '@/components/layout/theme-toggle';
import { Link } from '@/i18n/navigation';
import { routing } from '@/i18n/routing';
import { messagesFor, PLATFORM_NAMESPACES } from '@/i18n/messages';
import { requireSession } from '@/lib/session';

/**
 * Coquille de la console plateforme — C8 bloc 4, D-050.
 *
 * **Elle ne ressemble volontairement à aucun back-office.** Pas de marque, pas
 * de couleur de tenant, pas de sidebar : un fond neutre et un bandeau sombre.
 * C'est une précaution de MÉTIER autant que de goût — un administrateur qui
 * passe de la console à l'espace d'un client, puis d'un client à l'autre, doit
 * savoir au premier coup d'œil où il se trouve avant de cliquer. Une console
 * peinte aux couleurs de Wimo ferait croire qu'on est chez Wimo.
 *
 * **Le rôle est vérifié ici pour l'ERGONOMIE, pas pour la sécurité.** L'API
 * refuse `platform.*` à tout autre que `PLATFORM_ADMIN`, et c'est elle qui
 * tient la porte. Ce garde-ci évite seulement d'afficher une page vouée à un
 * 403 sur chaque appel.
 */
export default async function PlatformLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  if (!hasLocale(routing.locales, locale)) notFound();

  // Voir `(app)/layout.tsx` : le renvoi vers `/login` appartient au proxy (C17).
  const user = await requireSession();

  /*
   * 404 et non 403 : la console ne se signale pas à qui n'y a pas droit. Un
   * `SUPER_ADMIN` de tenant qui tente l'URL apprendrait sinon qu'il existe une
   * administration au-dessus de la sienne, et où elle se trouve.
   */
  if (!user.roles.includes('PLATFORM_ADMIN')) notFound();

  const [messages, t] = await Promise.all([
    messagesFor(PLATFORM_NAMESPACES),
    getTranslations('platform'),
  ]);

  return (
    <NextIntlClientProvider messages={messages}>
      <div className="bg-muted/30 flex min-h-dvh flex-col">
        <header className="flex h-14 shrink-0 items-center gap-3 bg-neutral-900 px-4 text-neutral-100 sm:px-6">
          <ShieldCheck className="size-5 shrink-0" aria-hidden />
          {/* Sous `md`, la place va à la navigation : le bouclier dit déjà « console ». */}
          <div className="hidden min-w-0 md:block">
            <p className="truncate text-sm font-semibold">{t('title')}</p>
          </div>

          {/* Espaces clients, journal technique, erreurs — recette 4, point 5 (D-134). */}
          <nav className="flex min-w-0 items-center gap-3 overflow-x-auto text-sm whitespace-nowrap text-neutral-300 md:ms-2">
            <Link href="/platform" className="hover:text-white">
              {t('nav.tenants')}
            </Link>
            <Link href="/platform/journal" className="hover:text-white">
              {t('nav.journal')}
            </Link>
            <Link href="/platform/errors" className="hover:text-white">
              {t('nav.errors')}
            </Link>
          </nav>

          <div className="ms-auto flex items-center gap-1">
            <span className="me-2 hidden truncate text-xs text-neutral-400 sm:inline">
              {user.email}
            </span>
            <LocaleSwitcher />
            <ThemeToggle />
          </div>
        </header>

        <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-8 sm:px-6">{children}</main>
      </div>
    </NextIntlClientProvider>
  );
}
