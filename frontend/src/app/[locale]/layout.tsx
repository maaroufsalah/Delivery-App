import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { hasLocale } from 'next-intl';
import { NextIntlClientProvider } from 'next-intl';
import { setRequestLocale } from 'next-intl/server';
import { EnvironmentBadge } from '@/components/layout/environment-badge';
import { NavigationTracker } from '@/components/navigation-tracker';
import { QueryProvider } from '@/components/query-provider';
import { CompanySettingsProvider } from '@/components/theme/company-settings-provider';
import { CompanyStyle } from '@/components/theme/company-style';
import { ThemeProvider } from '@/components/theme/theme-provider';
import { Toaster } from '@/components/ui/sonner';
import { TooltipProvider } from '@/components/ui/tooltip';
import { messagesFor, ROOT_NAMESPACES } from '@/i18n/messages';
import { localeDirection, routing } from '@/i18n/routing';
import { getCompanySettings } from '@/lib/api/settings';
import './../globals.css';

/**
 * Rendu DYNAMIQUE, et c'est une conséquence directe de C8 (D-048).
 *
 * Ce layout lit la configuration de la société pour en dériver les variables
 * CSS de marque et le titre de l'onglet. Tant qu'une installation servait une
 * seule société (D-013, caduque), pré-rendre les trois locales était correct :
 * la marque ne changeait jamais.
 *
 * Depuis que plusieurs tenants partagent la même production, la marque dépend
 * de l'HÔTE. Pré-rendre reviendrait à figer dans le HTML le nom, le logo et les
 * couleurs du premier tenant rencontré au build, et à les servir à tout le
 * monde — Wimo verrait la marque de SpeedyTrans. Ce n'est pas une fuite de
 * donnée, c'est pire pour le client : c'est afficher le nom d'un concurrent.
 *
 * `generateStaticParams` a donc disparu : elle et `force-dynamic` se
 * contrediraient, et la garder donnerait l'illusion d'un pré-rendu qui n'a
 * plus lieu.
 */
export const dynamic = 'force-dynamic';

export async function generateMetadata(): Promise<Metadata> {
  const settings = await getCompanySettings();

  return {
    title: { default: settings.name, template: `%s · ${settings.name}` },
    // SEC-01 : l'icône est fabriquée à l'upload par l'API (fichier assaini), plus de rendu à la volée.
    // Sans icône (aucun logo téléversé depuis), le logo tel quel.
    icons: { icon: settings.icon ?? settings.logo },
  };
}

export default async function RootLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;

  if (!hasLocale(routing.locales, locale)) {
    notFound();
  }

  setRequestLocale(locale);

  // La marque vient de la société résolue par l'HÔTE : l'appel porte
  // `x-forwarded-host`, et l'API en déduit le tenant (C8 bloc 5, D-049).
  const settings = await getCompanySettings();

  return (
    <html lang={locale} dir={localeDirection(locale)} suppressHydrationWarning>
      <head>
        <CompanyStyle
          primaryColor={settings.primaryColor}
          secondaryColor={settings.secondaryColor}
          theme={settings.theme}
        />
      </head>
      <body>
        {/*
          Le fournisseur RACINE ne porte que `error` — C7 bloc 4.

          Sans prop `messages`, next-intl sérialise le catalogue ENTIER dans le
          HTML : la page d'accueil embarquait ainsi le vocabulaire de la
          facturation, des réclamations et de l'entrepôt. Chaque groupe de
          routes pose désormais le sien, et celui-ci ne couvre que la frontière
          d'erreur de `[locale]`, seul composant client rendu à ce niveau.
        */}
        <NextIntlClientProvider messages={await messagesFor(ROOT_NAMESPACES)}>
          {/*
            Clair par défaut, et le thème du système ne s'invite plus — C9 bloc 1.

            `defaultTheme="system"` faisait basculer en sombre un poste dont
            l'OS est en sombre, sans que personne l'ait demandé pour CETTE
            application. Un back-office de livraison s'ouvre le plus souvent en
            agence, sur un écran partagé, à côté de documents imprimés : le
            clair est le défaut attendu, et le sombre reste à un clic.

            `enableSystem={false}` retire la valeur `system` du jeu : sans lui,
            next-themes continuerait de l'accepter et un ancien `localStorage`
            la ramènerait.
          */}
          <ThemeProvider
            attribute="class"
            defaultTheme="light"
            enableSystem={false}
            disableTransitionOnChange
          >
            <CompanySettingsProvider initialSettings={settings}>
              <QueryProvider>
                {/*
                  LE TÉMOIN DE NAVIGATION — point 4 du chantier « Bon de retour
                  livreur ». Il ne rend rien ; il compte NOS pas, pour que le
                  bouton « Retour » d'une fiche sache s'il peut reculer vers la
                  liste qu'on vient de filtrer ou s'il doit s'y rendre en
                  repli. Monté ICI parce qu'il doit voir les pas faits avant que
                  le bouton n'existe.
                */}
                <NavigationTracker />
                <TooltipProvider delayDuration={200}>{children}</TooltipProvider>
                <Toaster position={localeDirection(locale) === 'rtl' ? 'top-left' : 'top-right'} />
                {/* D-199 : « RECETTE » / « UAT » sur toutes les pages, connexion comprise. */}
                <EnvironmentBadge label={settings.environmentLabel ?? null} />
              </QueryProvider>
            </CompanySettingsProvider>
          </ThemeProvider>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}
