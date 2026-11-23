'use client';

import type { ReturnHandoverDetail, ReturnParcelLine } from '@delivery/shared';
import { useTranslations } from 'next-intl';
import { useCallback, useRef } from 'react';
import { CandidatePicker, type CandidateQuery } from '@/features/candidates/candidate-picker';
import { addHandoverParcelsAction, handoverCandidatePageAction } from '@/lib/actions/returns';

/**
 * AJOUTER DES COLIS À UN BON EXISTANT — point 2, la multi-sélection.
 *
 * La douchette reste, et elle reste le geste du quai. Celui-ci sert l'autre
 * moment : l'agent devant son écran qui compose la demande du lendemain, et qui
 * n'a aucun carton sous la main à scanner.
 *
 * Chantier « Plafond de 200 » : la pile du livreur (tronquée à 300 lignes)
 * vient par pages, au vrai total, recherche au serveur ; « Tout cocher » prend
 * toute la recherche, chargée ou non.
 */
export function HandoverComposer({
  handover,
  onDone,
}: {
  handover: ReturnHandoverDetail;
  onDone: (next: ReturnHandoverDetail | null) => void;
}) {
  const t = useTranslations('returns.handovers');
  const tPick = useTranslations('candidates');
  // Une réf, pas un état : `onDone` part dans le même tour que l'ajout.
  const added = useRef<ReturnHandoverDetail | null>(null);

  const load = useCallback(
    (query: CandidateQuery) => handoverCandidatePageAction(handover.courierId, query),
    [handover.courierId],
  );

  return (
    <CandidatePicker<ReturnParcelLine>
      title={t('addParcels')}
      description={t('wizardParcelsHint')}
      load={load}
      submit={async (selection) => {
        const result = await addHandoverParcelsAction(handover.code, {
          ...(selection.all
            ? { all: true, search: selection.search, exclude: selection.exclude }
            : { parcelIds: selection.ids ?? [] }),
          note: '',
        });
        if (result.ok) added.current = result.data;
        return result;
      }}
      submitLabel={(count) => tPick('addParcels', { count })}
      successMessage={(count) => t('addParcelsDone', { count })}
      onDone={() => onDone(added.current)}
      renderItem={(parcel) => (
        <div className="min-w-0">
          <span className="block font-mono text-sm font-medium">{parcel.code}</span>
          <span className="text-muted-foreground block truncate text-xs">
            {parcel.recipientName} · {parcel.clientName}
          </span>
        </div>
      )}
    />
  );
}
