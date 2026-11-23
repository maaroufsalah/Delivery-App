import { getTranslations } from 'next-intl/server';

/**
 * LE LIBELLÉ D'ENVIRONNEMENT — chantier UAT, D-199.
 *
 * La recette et l'UAT servent les mêmes images, la même marque, les mêmes
 * écrans : rien ne distingue deux onglets ouverts côte à côte, sauf ce
 * libellé. Il vient de l'API (`environmentLabel` de `/public/company`, lu par
 * le layout racine) et vaut `null` en production, où rien ne s'affiche.
 *
 * DISCRET par construction : une pastille de pied de page, centrée,
 * `pointer-events-none` — elle ne recouvre jamais un geste, pas même la barre
 * d'onglets du mobile au-dessus de laquelle elle se pose. Couleurs neutres du
 * thème : ce n'est ni une marque ni un statut.
 */
export async function EnvironmentBadge({ label }: { label: string | null }) {
  if (!label) return null;

  const t = await getTranslations('common');

  return (
    <div
      className="pointer-events-none fixed inset-x-0 bottom-0 z-50 flex justify-center pb-[max(env(safe-area-inset-bottom),2px)]"
      role="note"
      aria-label={t('environment', { label })}
    >
      <span
        data-environment={label}
        className="bg-foreground/75 text-background rounded-t-md px-2 py-0.5 text-[10px] leading-none font-semibold tracking-widest"
      >
        {label}
      </span>
    </div>
  );
}
