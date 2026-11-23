'use client';

import { findParcelStatus } from '@delivery/shared';
import { useTranslations } from 'next-intl';
import { CellBadge } from '@/components/data-table/cells';
import { statusIcon } from '@/lib/status-icons';

/**
 * Badge de statut colis. Couleur et icône viennent du référentiel partagé,
 * le libellé du fichier de messages — jamais de couleur en dur dans une page.
 */
export function ParcelStatusBadge({ statusId }: { statusId: number }) {
  const t = useTranslations('status');
  const status = findParcelStatus(statusId);

  if (!status) {
    return <CellBadge label={`#${statusId}`} />;
  }

  return <CellBadge label={t(status.key)} color={status.color} icon={statusIcon(status.icon)} />;
}
