import { NextIntlClientProvider } from 'next-intl';
import { headers } from 'next/headers';
import { PlatformFooter, PlatformHeader } from '@/features/platform/platform-shell';
import { PublicFooter, PublicHeader } from '@/features/public/public-shell';
import { messagesFor, PUBLIC_NAMESPACES, PLATFORM_NAMESPACES } from '@/i18n/messages';
import { isPlatformHost } from '@/lib/platform-domain';

/**
 * Coquille du site public.
 *
 * Ni sidebar, ni barre inférieure : le visiteur n'est pas un agent.
 *
 * **Deux coquilles, et non une** — C8 bloc 5. Sur `wimo.delivery.nocido.com`,
 * l'en-tête et le pied portent la marque de Wimo, lue par l'hôte. Sur le
 * domaine NU, il n'y a pas de tenant : la coquille est celle de la PLATEFORME,
 * qui porte la marque Nocido et ne propose ni suivi de colis ni connexion,
 * puisqu'il n'y a ici ni colis ni espace client (C9 bloc 5).
 *
 * Le fournisseur de traductions ne porte QUE ce que son groupe utilise
 * (C7 bloc 4), ce qui vaut aussi pour ces deux cas : la landing de la
 * plateforme n'a que faire du vocabulaire d'un transporteur.
 */
export default async function PublicLayout({ children }: { children: React.ReactNode }) {
  const host = (await headers()).get('x-forwarded-host') ?? (await headers()).get('host');

  if (isPlatformHost(host)) {
    return (
      <NextIntlClientProvider messages={await messagesFor(PLATFORM_NAMESPACES)}>
        <div className="flex min-h-dvh flex-col">
          <PlatformHeader />
          <main className="flex-1">{children}</main>
          <PlatformFooter />
        </div>
      </NextIntlClientProvider>
    );
  }

  return (
    <NextIntlClientProvider messages={await messagesFor(PUBLIC_NAMESPACES)}>
      <div className="flex min-h-dvh flex-col">
        <PublicHeader />
        <main className="flex-1">{children}</main>
        <PublicFooter />
      </div>
    </NextIntlClientProvider>
  );
}
