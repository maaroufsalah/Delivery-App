'use client';

import type { RouteNoteCandidate } from '@delivery/shared';
import { useFormatter, useTranslations } from 'next-intl';
import { useCallback } from 'react';
import { CandidatePicker, type CandidateQuery } from '@/features/candidates/candidate-picker';
import { addRouteNotesAction, routeNoteCandidatePageAction } from '@/lib/actions/routes';

/**
 * AJOUTER DES BONS À UN CIRCUIT DE RAMASSAGE — retouches Anass, point 5.
 *
 * Les colis de chaque bon entrent avec lui ; c'est le serveur qui les y met.
 * Chantier « Plafond de 200 » : la liste vient par pages, au vrai total, et
 * « Tout cocher » prend tous les bons de la recherche (la borne cachée de 200
 * bons à la lecture et de 100 à l'ajout a disparu).
 */
export function RouteNoteComposer({ code, onDone }: { code: string; onDone: () => void }) {
  const t = useTranslations('routes.notes');
  const tPick = useTranslations('candidates');
  const format = useFormatter();

  const load = useCallback(
    (query: CandidateQuery) => routeNoteCandidatePageAction(code, query),
    [code],
  );

  return (
    <CandidatePicker<RouteNoteCandidate>
      title={t('composeTitle')}
      description={t('composeHint')}
      searchPlaceholder={t('searchPlaceholder')}
      load={load}
      submit={(selection) => addRouteNotesAction(code, selection)}
      submitLabel={(count) => tPick('addNotes', { count })}
      successMessage={(count) => tPick('addedNotes', { count })}
      onDone={onDone}
      renderItem={(note) => (
        <div className="flex items-start gap-3">
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-mono text-sm font-medium">{note.code}</span>
            </div>
            <p className="truncate text-sm">{note.clientName}</p>
            <p className="text-muted-foreground truncate text-xs">
              {note.pickupAddress || note.cityName}
            </p>
          </div>
          <span className="shrink-0 text-sm font-medium tabular-nums">
            {t('parcelCount', { count: format.number(note.parcelCount, 'integer') })}
          </span>
        </div>
      )}
    />
  );
}
