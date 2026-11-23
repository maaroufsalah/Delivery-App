'use client';

import {
  findParcelStatus,
  RETURN_REASONS,
  type ReturnParcelLine,
  type ReturnReason,
  type ReturnSlipDetail,
} from '@delivery/shared';
import { useFormatter, useTranslations } from 'next-intl';
import { useCallback, useRef, useState } from 'react';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { CandidatePicker, type CandidateQuery } from '@/features/candidates/candidate-picker';
import { addSlipParcelsAction, slipCandidatePageAction } from '@/lib/actions/returns';

/**
 * COMPOSER UN BON DE RETOUR — retouches Anass, point 7.
 *
 * « Multi-sélection, recherche par code / destinataire / téléphone. » Un bon
 * de retour porte la marchandise d'une semaine d'échecs : on coche la pile,
 * sous un seul motif, et on l'ajoute d'un geste.
 *
 * Chantier « Plafond de 200 » : la liste (tronquée à 300 lignes, chargées par
 * la page) vient désormais du serveur par pages, au vrai total, la recherche
 * aussi ; « Tout cocher » prend toute la recherche, chargée ou non.
 */
export function SlipComposer({
  code,
  onDone,
}: {
  code: string;
  onDone: (slip: ReturnSlipDetail | null) => void;
}) {
  const t = useTranslations('returns.slips');
  const tReason = useTranslations('returns.slips.reasons');
  const tStatus = useTranslations('status');
  const tPick = useTranslations('candidates');
  const format = useFormatter();

  const [reason, setReason] = useState<ReturnReason>('refused');
  // Une réf, pas un état : `onDone` part dans le même tour que l'ajout.
  const added = useRef<ReturnSlipDetail | null>(null);

  const load = useCallback((query: CandidateQuery) => slipCandidatePageAction(code, query), [code]);

  return (
    <CandidatePicker<ReturnParcelLine>
      title={t('add')}
      description={t('addHint')}
      searchPlaceholder={t('searchCandidates')}
      load={load}
      toolbar={
        <Select value={reason} onValueChange={(value) => setReason(value as ReturnReason)}>
          <SelectTrigger className="sm:w-52" aria-label={t('fields.reason')}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {RETURN_REASONS.map((entry) => (
              <SelectItem key={entry} value={entry}>
                {tReason(entry)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      }
      submit={async (selection) => {
        const result = await addSlipParcelsAction(code, {
          ...(selection.all
            ? { all: true, search: selection.search, exclude: selection.exclude }
            : { parcelIds: selection.ids ?? [] }),
          reason,
        });
        if (result.ok) added.current = result.data;
        return result;
      }}
      submitLabel={(count) => tPick('addParcels', { count })}
      successMessage={(count) => t('addedCount', { count })}
      onDone={() => onDone(added.current)}
      renderItem={(parcel) => {
        const definition = findParcelStatus(parcel.statusId);
        return (
          <div className="flex items-start gap-3">
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-mono text-sm font-medium">{parcel.code}</span>
                {definition ? (
                  <span
                    className="rounded-full px-2 py-0.5 text-[11px]"
                    style={{ backgroundColor: `${definition.color}1a`, color: definition.color }}
                  >
                    {tStatus(definition.key)}
                  </span>
                ) : null}
              </div>
              <p className="text-muted-foreground mt-0.5 truncate text-xs">
                {parcel.recipientName} · {parcel.recipientPhone}
              </p>
            </div>
            <span className="shrink-0 text-sm font-medium tabular-nums">
              {format.number(parcel.codAmount / 100, 'currency')}
            </span>
          </div>
        );
      }}
    />
  );
}
