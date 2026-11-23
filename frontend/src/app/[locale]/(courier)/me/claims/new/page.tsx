import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { BackButton } from '@/components/back-button';
import { ClaimForm } from '@/features/claims/claim-form';
import { getClaimTypes } from '@/lib/api/claims';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('claims.form');
  return { title: t('title') };
}

/**
 * OUVRIR UNE RÉCLAMATION, CÔTÉ LIVREUR — le complément de L2.
 *
 * **Il ne choisit pas l'e-commerçant, et l'écran ne le lui demande pas**
 * (`hideClient`). Il réclame À PROPOS D'UN COLIS — « arrivé abîmé », « adresse
 * fausse », « destinataire introuvable depuis trois jours » — et
 * l'e-commerçant concerné est celui du colis. Le lui faire choisir dans une
 * liste reviendrait à lui montrer l'annuaire des clients de la société, ce que
 * D-082 interdit ; le serveur le déduit du colis, et cherche ce colis avec SON
 * `courierId`.
 *
 * **`?parcel=` préremplit le code.** La fiche d'un colis ouvre ce formulaire
 * avec son code déjà posé : recopier quinze caractères à la main, debout, est
 * le genre de friction qui fait qu'on ne réclame pas.
 *
 * **Aucune liste de clients n'est chargée** — l'appel n'est pas fait, il n'est
 * pas filtré. Une donnée qu'on ne demande pas ne fuit pas.
 */
export default async function NewCourierClaimPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const preset = Array.isArray(params.parcel) ? params.parcel[0] : params.parcel;

  const [types, t] = await Promise.all([getClaimTypes(), getTranslations('claims.form')]);

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <BackButton fallbackHref="/me/claims" />

      <header className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold tracking-tight">{t('title')}</h1>
        <p className="text-muted-foreground text-sm">{t('subtitle')}</p>
      </header>

      <ClaimForm
        types={types}
        clients={[]}
        hideClient
        basePath="/me/claims"
        presetParcelCode={preset}
      />
    </div>
  );
}
