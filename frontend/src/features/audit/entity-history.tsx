'use client';

import type { JournalEntry } from '@delivery/shared';
import { History, Loader2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useEffect, useState, useTransition } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { entityJournalAction } from '@/lib/actions/audit';
import { useJournalText } from './use-journal-text';

/**
 * L'onglet « Historique » d'une fiche — C11 bloc 5 (D-064), en langage métier
 * depuis la recette SpeedyTrans 4, point 4 (D-133).
 *
 * **Chargé à l'ouverture de la fiche, pas avec elle.** Une fiche de colis fait
 * déjà quatre requêtes ; en ajouter une cinquième au chemin critique pour un
 * panneau qu'on ne regarde pas toujours ralentirait tout le monde pour
 * quelques-uns. Le panneau se remplit après, et il le dit pendant.
 *
 * Même lecture que Paramètres > Journal — « Changement de statut : Attente de
 * ramassage → Ramassé », par qui, quand — sans la liste des champs bruts qu'il
 * affichait (`deletedAt`, `courierCommission`…).
 */
export function EntityHistory({ entity, entityId }: { entity: string; entityId: string }) {
  const t = useTranslations('settings.journal');
  const { tx, action } = useJournalText();
  const format = useFormatter();

  const [entries, setEntries] = useState<JournalEntry[] | null>(null);
  const [pending, startTransition] = useTransition();

  useEffect(() => {
    startTransition(async () => {
      const result = await entityJournalAction(entity, entityId);
      // Un échec laisse la liste vide plutôt qu'une erreur en rouge : le
      // journal est un complément, son absence ne doit pas alarmer sur une
      // fiche qui, elle, s'est chargée.
      setEntries(result.ok ? result.data.items : []);
    });
  }, [entity, entityId]);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <History className="size-4" aria-hidden />
          {t('entityTitle')}
        </CardTitle>
      </CardHeader>

      <CardContent>
        {entries === null || pending ? (
          <p className="text-muted-foreground flex items-center gap-2 py-4 text-sm">
            <Loader2 className="size-4 animate-spin" aria-hidden />
            {t('loading')}
          </p>
        ) : entries.length === 0 ? (
          <p className="text-muted-foreground py-4 text-sm">{t('entityEmpty')}</p>
        ) : (
          <ul className="divide-border divide-y text-sm">
            {entries.map((item) => (
              <li key={item.id} className="flex flex-col gap-0.5 py-2.5">
                <span className="font-medium">{action(item)}</span>
                <span className="text-muted-foreground text-xs">
                  {item.user ? `${item.user.name} (${tx(`role.${item.user.role}`)})` : tx('system')}{' '}
                  · {format.dateTime(new Date(item.at), 'stamp')}
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
