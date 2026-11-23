'use client';

import { type FieldChange, isParcelTraceField, parcelTraceValue } from '@delivery/shared';
import { History } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useJournalText } from '@/features/audit/use-journal-text';

/**
 * L'HISTORIQUE DES CORRECTIONS d'un colis — D-257.
 *
 * Le journal des explications (`field_changes`) lu en phrases : « Client
 * changé : papy joe → rania, frais 20,00 → 25,00, motif : …, par …, le
 * 14/11/2026 22:21:19 », « Colis supprimé … », « Sorti du bon BR-… ». Les
 * lignes d'un même geste (même instant, même auteur, même motif) font une
 * phrase. Lecture seule : aucune ligne ne se modifie ni ne se supprime.
 */

/** Les lignes d'un même geste. */
interface Gesture {
  key: string;
  at: string;
  authorName: string;
  reason: string | null;
  rows: FieldChange[];
}

function gesturesOf(changes: readonly FieldChange[]): Gesture[] {
  const byKey = new Map<string, Gesture>();
  for (const change of changes) {
    // Le départ d'un bon porte un motif préfixé : il fait sa propre phrase.
    const key =
      change.field === 'deliveryNote'
        ? `note|${change.id}`
        : `${change.at}|${change.authorName}|${change.reason ?? ''}`;
    const gesture = byKey.get(key) ?? {
      key,
      at: change.at,
      authorName: change.authorName,
      reason: change.reason,
      rows: [],
    };
    gesture.rows.push(change);
    byKey.set(key, gesture);
  }
  return [...byKey.values()].sort((a, b) => b.at.localeCompare(a.at));
}

export function ParcelTraceCard({ changes }: { changes: readonly FieldChange[] }) {
  const t = useTranslations('parcels.trace');
  const format = useFormatter();
  const journal = useJournalText();
  const gestures = gesturesOf(changes);
  if (gestures.length === 0) return null;

  /*
   * Historique lisible (16/11/2026) : la sorte de la valeur se lit sur son
   * CHAMP (`PARCEL_TRACE_FIELDS`), et elle se dit comme au journal : montant en
   * MAD, date, Oui / Non, statut et paiement par leur nom, fiche par son nom.
   */
  const value = (row: FieldChange, side: 'old' | 'new') => {
    const label = side === 'old' ? row.oldLabel : row.newLabel;
    const raw = side === 'old' ? row.oldValue : row.newValue;
    return journal.value(parcelTraceValue(row.field, raw, label));
  };
  // Aucun nom technique : un champ inconnu (jamais attendu, le spec l'interdit) se dit « Autre champ ».
  const fieldName = (field: string) => (isParcelTraceField(field) ? t(`fields.${field}`) : t('fields.other'));

  const sentence = (gesture: Gesture): string => {
    const by = { by: gesture.authorName, at: format.dateTime(new Date(gesture.at), 'stamp') };
    const reason = gesture.reason ?? '-';
    const client = gesture.rows.find((row) => row.field === 'clientId');
    const fee = gesture.rows.find((row) => row.field === 'deliveryFee');
    const deleted = gesture.rows.find((row) => row.field === 'deletedAt');
    const note = gesture.rows.find((row) => row.field === 'deliveryNote');

    if (client) {
      return fee
        ? t('clientChangeFee', {
            from: value(client, 'old'),
            to: value(client, 'new'),
            feeFrom: value(fee, 'old'),
            feeTo: value(fee, 'new'),
            reason,
            ...by,
          })
        : t('clientChange', { from: value(client, 'old'), to: value(client, 'new'), reason, ...by });
    }
    if (deleted) return t('deleted', { reason, ...by });
    if (note) return t('leftNote', { code: note.oldValue ?? '', reason, ...by });
    return t('other', {
      fields: gesture.rows
        .map((row) => `${fieldName(row.field)} : ${value(row, 'old')} → ${value(row, 'new')}`)
        .join(', '),
      reason,
      ...by,
    });
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <History className="size-4" aria-hidden />
          {t('title')}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <ul className="divide-border divide-y text-sm">
          {gestures.map((gesture) => (
            <li key={gesture.key} className="py-2.5">
              {sentence(gesture)}
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
