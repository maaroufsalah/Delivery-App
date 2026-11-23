'use client';

import type { JournalChange, JournalEntry, JournalObject, JournalValue } from '@delivery/shared';
import { useFormatter, useTranslations } from 'next-intl';
import { useCallback, useMemo } from 'react';

/**
 * LE JOURNAL EN CLAIR, dans la langue de l'écran — recette SpeedyTrans 4,
 * point 4 (D-133).
 *
 * L'API rend des clés et des valeurs typées ; ce crochet les dit : un montant
 * en MAD, une date au format local, un statut par son libellé, une valeur
 * d'énumération traduite. `journal-labels.spec.ts` (API) garantit que chaque clé
 * émise a sa traduction dans les trois langues.
 */
export function useJournalText() {
  const t = useTranslations('settings.journal.business');
  const tStatus = useTranslations('status');
  const format = useFormatter();

  // Les clés viennent de l'API : le typage de `fr.json` ne peut pas les connaître.
  const tx = useCallback((key: string) => (t as unknown as (key: string) => string)(key), [t]);

  const value = useCallback(
    (entry: JournalValue): string => {
      if (entry === null) return '—';
      switch (entry.kind) {
        case 'money':
          return format.number(entry.cents / 100, 'currency');
        case 'date':
          return format.dateTime(new Date(entry.iso), 'short');
        case 'number':
          return format.number(entry.value);
        case 'bool':
          return entry.value ? t('yes') : t('no');
        case 'parcel-status':
          return tStatus(entry.key as Parameters<typeof tStatus>[0]);
        case 'enum':
          return tx(`enum.${entry.value}`);
        case 'text':
          return entry.text;
      }
    },
    [format, t, tStatus, tx],
  );

  const field = useCallback((change: JournalChange) => tx(`field.${change.field}`), [tx]);

  const object = useCallback(
    (entry: JournalObject, count = 1) => {
      const type = tx(`object.${entry.type}`);
      const reference = entry.reference ? ` ${entry.reference}` : '';
      const more = count > 1 ? ` ${t('more', { count: String(count - 1) })}` : '';
      return `${type}${reference}${more}`;
    },
    [t, tx],
  );

  /** « Changement de statut : Attente de ramassage → Ramassé ». */
  const action = useCallback(
    (entry: Pick<JournalEntry, 'action' | 'headline'>) => {
      const label = tx(`action.${entry.action}`);
      const headline = entry.headline;
      if (!headline) return label;
      if (entry.action === 'amount') {
        return `${field(headline)} : ${value(headline.before)} → ${value(headline.after)}`;
      }
      return `${label} : ${value(headline.before)} → ${value(headline.after)}`;
    },
    [field, tx, value],
  );

  /** Le seul changement qui nomme la ligne : « Attente → Ramassé », « Montant : 500 → 450 ». */
  const change = useCallback(
    (entry: Pick<JournalEntry, 'action' | 'headline'>) => {
      const headline = entry.headline;
      if (!headline) return null;
      const arrow = `${value(headline.before)} → ${value(headline.after)}`;
      return entry.action === 'amount' ? `${field(headline)} : ${arrow}` : arrow;
    },
    [field, value],
  );

  return useMemo(
    () => ({ t, tx, value, field, object, action, change }),
    [t, tx, value, field, object, action, change],
  );
}
