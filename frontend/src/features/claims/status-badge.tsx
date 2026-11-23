import type { ClaimStatus } from '@delivery/shared';
import { CheckCircle2, CircleDot, Clock, XCircle } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { CellBadge } from '@/components/data-table/cells';

/**
 * Pastille de statut d'une réclamation.
 *
 * Les couleurs viennent des tokens de graphique de la marque, jamais d'une
 * palette Tailwind en dur (CLAUDE.md §3) : un client dont la charte est verte
 * ne doit pas voir du bleu Tailwind apparaître dans ses écrans.
 */
const TONE: Record<ClaimStatus, string> = {
  open: 'var(--chart-2)',
  'in-progress': 'var(--chart-1)',
  resolved: 'var(--chart-4)',
  rejected: 'var(--muted-foreground)',
};

const ICON = {
  open: CircleDot,
  'in-progress': Clock,
  resolved: CheckCircle2,
  rejected: XCircle,
} as const;

export function ClaimStatusBadge({ status }: { status: ClaimStatus }) {
  const t = useTranslations('claims.status');

  return <CellBadge label={t(status)} color={TONE[status]} icon={ICON[status]} />;
}
