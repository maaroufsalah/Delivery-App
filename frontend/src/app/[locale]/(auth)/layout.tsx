import { NextIntlClientProvider } from 'next-intl';
import { AUTH_NAMESPACES, messagesFor } from '@/i18n/messages';

/**
 * Connexion : un formulaire et rien d'autre.
 *
 * Le groupe n'avait pas de layout ; il en reçoit un pour la seule raison de
 * porter son propre fournisseur de traductions (C7 bloc 4). Un écran de
 * connexion n'a besoin ni du menu, ni des statuts de colis, ni de la
 * facturation.
 */
export default async function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <NextIntlClientProvider messages={await messagesFor(AUTH_NAMESPACES)}>
      {children}
    </NextIntlClientProvider>
  );
}
