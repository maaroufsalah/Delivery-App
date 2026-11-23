import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { CourierPettyCash } from '@/features/courier/courier-petty-cash';
import { getCourierPettyCash } from '@/lib/api/petty-cash';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courier.nav');
  return { title: t('pettyCash') };
}

/**
 * LA PETITE CAISSE DU LIVREUR SUR LE WEB — D-168, le miroir de l'écran mobile.
 *
 * Ses dépenses seules (la portée serveur ne lui rend rien d'autre), sans solde
 * ni total de caisse. Lecture au serveur, écritures par Server Action (D-018).
 */
export default async function CourierPettyCashPage() {
  const [t, page] = await Promise.all([
    getTranslations('courier.pettyCash'),
    getCourierPettyCash(),
  ]);

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold tracking-tight">{t('title')}</h1>
        <p className="text-muted-foreground text-sm">{t('subtitle')}</p>
      </header>

      <CourierPettyCash page={page} />
    </div>
  );
}
