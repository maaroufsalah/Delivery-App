'use client';

import type { ParcelListItem, SelectionPayload } from '@delivery/shared';
import { useFormatter, useTranslations } from 'next-intl';
import { useCallback } from 'react';
import { CandidatePicker, type CandidateQuery } from '@/features/candidates/candidate-picker';
import { ParcelStatusBadge } from '@/features/parcels/status-badge';
import type { ReferentialResult } from '@/lib/actions/action-result';
import { noteCandidatePageAction } from '@/lib/actions/routes';

/**
 * « AJOUTER DES COLIS » À UN BON BR- / BD- — et la demande du vendeur.
 *
 * Chantier « Plafond de 200 » : la liste n'est plus chargée d'un bloc par la
 * page (une page de 200, BR-2610-0002 en avait 358) ; elle vient du serveur
 * par pages, au vrai total, avec la recherche (`CandidatePicker`). « Tout
 * cocher » envoie un critère que l'API résout dans la transaction d'ajout.
 *
 * Les candidats sont ceux de `GET /parcels?noteCandidate=true` (D-132) : pas
 * encore pris en charge, sur aucun bon actif.
 */
export function NoteComposer({
  clientId,
  onDone,
  onSubmit,
  title,
  hint,
  submitLabel,
  successMessage,
}: {
  /** Le vendeur du bon ; `null` pour le vendeur connecté (sa portée le borne). */
  clientId: string | null;
  onDone: () => void;
  /** L'écriture réelle : ids cochés, ou « tous les candidats de la recherche ». */
  onSubmit: (selection: SelectionPayload) => Promise<ReferentialResult<unknown>>;
  title?: string;
  hint?: string;
  submitLabel?: (count: number) => string;
  successMessage?: (count: number) => string;
}) {
  const t = useTranslations('notes.compose');
  const tPick = useTranslations('candidates');
  const format = useFormatter();

  const load = useCallback(
    (query: CandidateQuery) => noteCandidatePageAction(clientId, query),
    [clientId],
  );

  return (
    <CandidatePicker<ParcelListItem>
      title={title ?? t('title')}
      description={hint ?? t('hint')}
      load={load}
      submit={onSubmit}
      submitLabel={submitLabel ?? ((count) => tPick('addParcels', { count }))}
      successMessage={successMessage ?? ((count) => tPick('addedParcels', { count }))}
      onDone={onDone}
      renderItem={(parcel) => (
        <div className="flex items-start gap-3">
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-mono text-sm font-medium">{parcel.code}</span>
              <ParcelStatusBadge statusId={parcel.statusId} />
            </div>
            <p className="text-muted-foreground mt-0.5 truncate text-xs">
              {parcel.recipientName} · {parcel.recipientPhone} · {parcel.destinationCityName}
            </p>
          </div>
          <span className="shrink-0 text-sm font-medium tabular-nums">
            {format.number(parcel.codAmount / 100, 'currency')}
          </span>
        </div>
      )}
    />
  );
}
