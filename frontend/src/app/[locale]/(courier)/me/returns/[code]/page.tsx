import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { BackButton } from '@/components/back-button';
import { HandoverDetailScreen } from '@/features/returns/handover-detail';
import { getHandover } from '@/lib/api/returns';
import { ApiError } from '@/lib/http';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ code: string }>;
}): Promise<Metadata> {
  const { code } = await params;
  return { title: decodeURIComponent(code) };
}

/**
 * LA FICHE D'UN BON DE RETOUR, CÔTÉ LIVREUR — chantier « Bon de retour
 * livreur », point 1 : « détail, PDF, prise en charge, remise ».
 *
 * **Elle manquait, et la liste menait dans le vide.** Le chantier précédent
 * faisait pointer les bons du livreur vers `/returns/handovers/:code`, qui vit
 * dans la coque du BACK-OFFICE : un livreur qui cliquait était renvoyé chez lui
 * par le garde de rôle, sans rien comprendre. Le bon existait, son écran non.
 *
 * **Le même composant que l'admin**, et c'est ce qui garantit les libellés
 * identiques (point 7 du chantier précédent). Ce qui diffère est décidé par le
 * SERVEUR : `confirmable`, `closable` et `requestable` sont faux pour un
 * livreur, donc les trois boutons d'agence ne se rendent pas. L'écran ne
 * connaît pas la règle — il affiche ce qu'on l'autorise à proposer.
 *
 * La portée fait le reste : `GET /return-handovers/:code` est borné à
 * `courierId`, donc le code d'un collègue rend 404 (D-082).
 */
export default async function CourierHandoverPage({
  params,
}: {
  params: Promise<{ code: string }>;
}) {
  const { code } = await params;
  const t = await getTranslations('courier.returns');

  const handover = await getHandover(decodeURIComponent(code)).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center gap-3">
        {/*
          La flèche RECULE — point 4. Le repli garde l'onglet des bons : une
          notification ouvre cette fiche sans rien derrière elle, et retomber
          sur l'onglet « Colis » n'aurait aucun rapport avec ce qu'on lisait.
        */}
        <BackButton fallbackHref="/me/returns?tab=handovers" label={t('handovers')} iconOnly />
        <h1 className="font-mono text-lg font-semibold">{handover.code}</h1>
      </div>

      <HandoverDetailScreen handover={handover} />
    </div>
  );
}
