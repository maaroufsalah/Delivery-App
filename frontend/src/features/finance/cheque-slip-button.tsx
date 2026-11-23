'use client';

import { FileText } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useCallback, useTransition } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { useRouter } from '@/i18n/navigation';
import { downloadChequeGroupSlipAction, downloadChequeSlipAction } from '@/lib/actions/finance';
import { downloadBase64 } from '@/lib/download';

/**
 * LE BON DE REMISE D'UN CHÈQUE — D-265.
 *
 * Le téléchargement écrit une ligne dans l'historique du chèque : la fiche se
 * rafraîchit pour la montrer. Aucun statut ne change.
 */
export function useChequeSlipDownload(onDone?: () => void) {
  const t = useTranslations('finance.cheques');
  const [pending, startTransition] = useTransition();

  // Stable : la colonne d'actions du tableau en dépend (`useMemo`).
  const download = useCallback(
    (id: string, parcelCode: string) => {
      startTransition(async () => {
        const result = await downloadChequeSlipAction(id);

        if (result.ok) {
          downloadBase64(result.data, `BRC-${parcelCode}.pdf`, 'application/pdf');
          toast.success(t('slipDownloaded'));
          onDone?.();
        } else toast.error(result.error);
      });
    },
    [onDone, t],
  );

  return { download, pending };
}

/**
 * LE BON DE REMISE GROUPÉ — chantier « Bon de remise groupé par vendeur ».
 *
 * Un bon par vendeur de la sélection, tous dans un PDF ; le nom du fichier
 * porte le numéro tiré par le serveur. Chaque chèque reçoit sa ligne
 * d'historique ; aucun statut ne change.
 */
export function useChequeGroupSlipDownload(onDone?: () => void) {
  const t = useTranslations('finance.cheques');
  const [pending, startTransition] = useTransition();

  const download = useCallback(
    (ids: string[]) => {
      startTransition(async () => {
        const result = await downloadChequeGroupSlipAction(ids);

        if (result.ok) {
          downloadBase64(result.data.file, result.data.filename, 'application/pdf');
          toast.success(t('groupSlipDownloaded', { count: String(ids.length) }));
          onDone?.();
        } else toast.error(result.error);
      });
    },
    [onDone, t],
  );

  return { download, pending };
}

/** Le bouton de la fiche — la ligne du tableau passe par son menu d'actions. */
export function ChequeSlipButton({ id, parcelCode }: { id: string; parcelCode: string }) {
  const t = useTranslations('finance.cheques');
  const router = useRouter();
  const { download, pending } = useChequeSlipDownload(() => router.refresh());

  return (
    <Button variant="outline" disabled={pending} onClick={() => download(id, parcelCode)}>
      <FileText className="size-4" />
      {t('slip')}
    </Button>
  );
}
