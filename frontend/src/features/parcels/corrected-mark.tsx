'use client';

import { useFormatter, useTranslations } from 'next-intl';
import { Badge } from '@/components/ui/badge';

/**
 * « CORRIGÉ DEPUIS » — D-250. Sur la ligne d'un document clôturé (circuit,
 * bon de retour réceptionné) dont le colis a été corrigé par l'administrateur
 * après coup : la ligne dit ce qui s'est passé alors, la marque dit qu'elle
 * n'est plus le dernier mot. Rien si la ligne n'est pas marquée.
 */
export function CorrectedMark({ at }: { at: string | null | undefined }) {
  const t = useTranslations('parcels.correction');
  const format = useFormatter();
  if (!at) return null;
  return (
    <Badge variant="outline" className="text-muted-foreground text-[11px] font-normal">
      {t('mark', { date: format.dateTime(new Date(at), { dateStyle: 'short' }) })}
    </Badge>
  );
}
