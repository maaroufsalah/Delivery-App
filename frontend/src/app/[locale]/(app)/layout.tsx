import { belongsElsewhere, landingPathFor } from '@delivery/shared';
import { hasLocale, type Locale, NextIntlClientProvider } from 'next-intl';
import { notFound } from 'next/navigation';
import { AppShell } from '@/components/layout/app-shell';
import { LocaleSwitcher } from '@/components/layout/locale-switcher';
import { ThemeToggle } from '@/components/layout/theme-toggle';
import { MerchantShell } from '@/features/merchant/merchant-shell';
import { TenantBanner } from '@/components/layout/tenant-banner';
import { ForcedPasswordChange } from '@/features/accounts/forced-password-change';
import { redirect } from '@/i18n/navigation';
import { routing } from '@/i18n/routing';
import { APP_EXCLUDED_NAMESPACES, messagesWithout } from '@/i18n/messages';
import { requireSession } from '@/lib/session';
import { getClaimCounters } from '@/lib/api/claims';
import { getPendingCounts } from '@/lib/api/directory';
import type { NavBadges } from '@/lib/navigation-config';
import { readSidebarPreferences } from '@/lib/sidebar-preferences.server';
import { getCompanySettings } from '@/lib/api/settings';
import { getMerchantCompany } from '@/lib/api/merchant';

/**
 * Garde du back-office.
 *
 * Le renouvellement du jeton a déjà eu lieu dans `proxy.ts`, **et le jeton neuf
 * est posé sur la requête que ce layout est en train de rendre** (C17) : les
 * appels ci-dessous partent donc avec, et non avec l'ancien.
 *
 * Si la session est encore absente ici, elle est bel et bien terminée —
 * `requireSession()` lève un `401` que la frontière d'erreur traduit en saut
 * vers la connexion. **Ce layout ne redirige plus lui-même** : son `307`
 * écrasait la réponse du proxy, `Set-Cookie` compris.
 */
export default async function AppLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  if (!hasLocale(routing.locales, locale)) notFound();

  const user = await requireSession();

  /*
   * L'ATTERRISSAGE PAR RÔLE, ICI ET PAS DANS LA PAGE — L1 bloc 0.
   *
   * Le constat de production : un livreur qui se connecte voyait la sidebar
   * admin s'afficher, puis disparaître, remplacée par une redirection 307.
   * Ce n'était pas un défaut de logique — la redirection était juste, elle
   * était au mauvais ÉTAGE.
   *
   * Next rend un layout AVANT la page qu'il enveloppe, et pousse son HTML au
   * navigateur sans attendre. `redirect()` posé dans `dashboard/page.tsx`
   * s'exécutait donc après que la coque complète — barre latérale, quarante
   * entrées de menu, badges — avait déjà été envoyée et peinte. Le livreur
   * voyait un back-office qui n'est pas le sien, une demi-seconde, à chaque
   * connexion.
   *
   * Remonté ici, le test précède tout rendu : un compte qui n'appartient pas au
   * back-office ne reçoit AUCUN octet de sa coque. Et le garde couvre
   * désormais toutes les routes du groupe, pas la seule `/dashboard` — un
   * livreur qui ouvrait `/parcels` par un lien la voyait entièrement.
   *
   * La règle elle-même reste partagée (`landingPathFor`), et testée avec les
   * quatre rôles.
   */
  if (belongsElsewhere(user)) {
    redirect({ href: landingPathFor(user), locale: locale as Locale });
    return null;
  }

  // Compte invité : l'API ne laisse ouverte que la route de changement de mot
  // de passe. Le back-office n'est donc pas rendu du tout — un menu complet sur
  // des écrans qui répondent tous 403 serait une fausse promesse.
  /*
   * Le back-office reçoit tout le catalogue SAUF la landing (C7 bloc 4) : un
   * agent n'a que faire du vocabulaire commercial du site public, et c'est
   * autant de moins dans le HTML de chaque écran.
   */
  const messages = await messagesWithout(APP_EXCLUDED_NAMESPACES);

  if (user.mustChangePassword) {
    return (
      <NextIntlClientProvider messages={messages}>
        <ForcedPasswordChange />
      </NextIntlClientProvider>
    );
  }

  /*
   * Préférences de barre latérale lues ICI, au serveur — C10 bloc 4.
   *
   * C'est ce qui évite le flash : le HTML envoyé porte déjà la bonne section
   * ouverte et la bonne largeur. Les lire côté client, dans un effet, ferait
   * peindre la barre deux fois.
   */
  /*
   * LA COQUE DE L'E-COMMERÇANT — L1 bloc 7.
   *
   * Décidée ICI, au serveur, avant tout rendu : un vendeur ne reçoit aucun
   * octet de la barre latérale du back-office. C'est la seconde moitié de la
   * demande du bloc 0 — la première était de ne plus le montrer au livreur.
   *
   * **Les ÉCRANS ne changent pas.** `/parcels`, `/parcels/new`, `/invoices`,
   * `/claims` sont déjà bornés à ses colis par la portée serveur, testés, et
   * en production. Ce qui n'allait pas était la barre autour d'eux : quarante
   * entrées dont il en utilise neuf, avec des sections qu'il ouvre pour
   * découvrir qu'elles sont interdites. En dupliquer des copies sous `/shop`
   * aurait fait deux formulaires de création de colis à tenir d'accord — et le
   * second aurait divergé sur l'écran qui fait entrer l'argent.
   *
   * `clientId` plutôt que le rôle : c'est le RATTACHEMENT qui décide, comme
   * partout ailleurs (D-072). Un compte STAFF rattaché à une fiche client est
   * un vendeur, quelle que soit l'étiquette qu'on lui a posée.
   */
  if (user.clientId) {
    /*
     * Lot V, V4 (D-234) : le régime de SA fiche décide de « Mes versements ».
     * Une lecture qui échoue ne retire rien : l'entrée reste, comme avant.
     */
    const [company, merchant] = await Promise.all([
      getCompanySettings(),
      getMerchantCompany().catch(() => null),
    ]);

    return (
      <NextIntlClientProvider messages={messages}>
        <MerchantShell
          user={{
            name: `${user.firstName} ${user.lastName}`,
            company: company.name,
            logo: company.logo || null,
            logoBackground: company.logoBackground ?? null,
            permissions: user.permissions,
            billingType: merchant?.billingType ?? null,
          }}
        >
          <header className="bg-background sticky top-0 z-10 flex h-16 shrink-0 items-center gap-3 border-b px-4 lg:px-6">
            <span className="min-w-0 flex-1">
              <span className="block truncate text-sm font-semibold">
                {user.firstName} {user.lastName}
              </span>
              <span className="text-muted-foreground block truncate text-xs">{company.name}</span>
            </span>

            {/*
              LANGUE ET THÈME, EN HAUT À DROITE — recette SpeedyTrans 4, point 3.
              Les mêmes composants et le même ordre que l'en-tête du back-office
              (`AppShell`) : la même mémorisation, le RTL arabe du layout racine.
            */}
            <LocaleSwitcher />
            <ThemeToggle />
          </header>

          {/* `pb-20` sous 1024 px : la barre basse flotte au-dessus du contenu. */}
          <main className="flex-1 px-4 pt-4 pb-20 lg:px-6 lg:pb-8">{children}</main>
        </MerchantShell>
      </NextIntlClientProvider>
    );
  }

  const sidebar = await readSidebarPreferences();

  return (
    <NextIntlClientProvider messages={messages}>
      <AppShell
        user={user}
        badges={await navBadges(user.permissions)}
        banner={<TenantBanner tenant={user.tenant} impersonated={user.impersonated} />}
        initialSection={sidebar.section}
        initialCollapsed={sidebar.collapsed}
      >
        {children}
      </AppShell>
    </NextIntlClientProvider>
  );
}

/**
 * Compteurs de menu.
 *
 * Une pastille sur « Réclamations » dit qu'il y a quelque chose à traiter —
 * c'est la seule raison d'en afficher une. L'appel est BEST-EFFORT : une API
 * qui ne répond pas sur ce point ne doit pas empêcher le back-office de
 * s'afficher, elle doit juste priver le menu de son chiffre.
 */
async function navBadges(granted: readonly string[]): Promise<NavBadges> {
  /*
   * Deux sources, deux permissions, et chacune tombe seule — C13 bloc 2.
   *
   * `Promise.all` ferait perdre les réclamations quand le répertoire répond
   * mal, et inversement. `allSettled` ne le permet pas : chaque compteur est
   * indépendant, et un menu qui perd un chiffre vaut mieux qu'un menu qui les
   * perd tous.
   */
  const [claims, pending] = await Promise.all([
    granted.includes('claims.read') ? getClaimCounters().catch(() => null) : Promise.resolve(null),
    granted.includes('directory.read')
      ? getPendingCounts().catch(() => null)
      : Promise.resolve(null),
  ]);

  return {
    ...(claims ? { claims: claims.open } : {}),
    /*
     * Zéro n'est PAS affiché : une pastille dit qu'il y a quelque chose à
     * faire, et « 0 » dit le contraire tout en occupant la place. Le menu
     * traite `undefined` comme « pas de chiffre ».
     */
    ...(pending?.clients ? { clients: pending.clients } : {}),
    ...(pending?.couriers ? { couriers: pending.couriers } : {}),
  };
}
