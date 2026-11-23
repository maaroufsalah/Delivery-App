import { hasLocale, NextIntlClientProvider } from 'next-intl';
import { notFound } from 'next/navigation';
import { LocaleSwitcher } from '@/components/layout/locale-switcher';
import { ThemeToggle } from '@/components/layout/theme-toggle';
import { CourierShell } from '@/features/courier/courier-shell';
import { routing } from '@/i18n/routing';
import { COURIER_NAMESPACES, messagesFor } from '@/i18n/messages';
import { requireSession } from '@/lib/session';
import { getCompanySettings } from '@/lib/api/settings';

/**
 * L'espace LIVREUR — C11 bloc 7 (D-068), la coque refaite par L1 bloc 1.
 *
 * **Une coquille à part, et pas un back-office rétréci.** Un livreur travaille
 * debout, sur un téléphone, souvent en 3G et souvent dehors. Le back-office a
 * une barre latérale de quarante entrées dont il n'en utilise aucune.
 *
 * **Ce que L1 change** : trois onglets deviennent neuf destinations, la barre
 * basse ne sert plus qu'en dessous de 1024 px — au-dessus, une barre latérale,
 * parce qu'un livreur qui vient chercher un relevé au bureau est sur un écran
 * large — et l'en-tête porte enfin la marque, l'agence et le nom.
 *
 * **Le catalogue est réduit** aux espaces qui le concernent
 * (`COURIER_NAMESPACES`) : pas de tarifs, pas d'entrepôt, pas de référentiel.
 *
 * **Le rôle est vérifié ici pour l'ERGONOMIE, pas pour la sécurité.** La portée
 * serveur borne déjà chaque lecture au livreur de la session — un livreur ne
 * voit que ses tournées, ses colis, ses remises, et l'API le garantit sans rien
 * savoir de cet écran (D-082).
 */
export default async function CourierLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  if (!hasLocale(routing.locales, locale)) notFound();

  // Session absente = session finie : `requireSession()` lève un `401`, et la
  // frontière d'erreur fait le saut. Le renvoi vers `/login` vit dans le proxy
  // et nulle part ailleurs (C17).
  const user = await requireSession();

  /*
   * `courierId` plutôt que le rôle : c'est le RATTACHEMENT qui rend l'espace
   * utile, et c'est lui que la portée serveur utilise. Un compte portant le
   * rôle sans fiche livreur n'aurait que des listes vides.
   */
  if (!user.courierId) notFound();

  const [messages, company] = await Promise.all([
    messagesFor(COURIER_NAMESPACES),
    /*
     * La marque. `getCompanySettings()` porte déjà ses propres replis — elle
     * sert le layout racine sur TOUTES les routes — donc l'espace s'ouvre même
     * si l'appel échoue : le livreur perdrait un logo, pas sa tournée.
     */
    getCompanySettings(),
  ]);

  return (
    <NextIntlClientProvider messages={messages}>
      <CourierShell
        user={{
          name: `${user.firstName} ${user.lastName}`,
          agency: null,
          company: company.name,
          logo: company.logo || null,
          logoBackground: company.logoBackground ?? null,
          permissions: user.permissions,
        }}
      >
        <header className="bg-background sticky top-0 z-10 flex h-16 shrink-0 items-center gap-3 border-b px-4 lg:px-6">
          <span className="min-w-0 flex-1">
            <span className="block truncate text-sm font-semibold">
              {user.firstName} {user.lastName}
            </span>
            <span className="text-muted-foreground block truncate text-xs">{company.name}</span>
          </span>

          {/* Même ordre que le back-office et l'espace vendeur (recette 4, point 3). */}
          <LocaleSwitcher />
          <ThemeToggle />
        </header>

        {/*
          `pb-20` sous 1024 px : la barre basse est fixée et flotte au-dessus du
          contenu. Sans cette marge, la dernière ligne de chaque écran serait
          définitivement illisible — et c'est toujours la plus récente.
        */}
        <main className="flex-1 px-4 pt-4 pb-20 lg:px-6 lg:pb-8">{children}</main>
      </CourierShell>
    </NextIntlClientProvider>
  );
}
