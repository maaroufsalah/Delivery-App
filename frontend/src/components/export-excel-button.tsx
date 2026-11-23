'use client';

import { Download, Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import type { ReferentialResult } from '@/lib/actions/action-result';
import { downloadBase64 } from '@/lib/download';

/**
 * « EXPORTER EXCEL » SUR UNE FICHE — nuit du 11/11/2026, chantier « Export
 * Excel des circuits ».
 *
 * Les LISTES ont leur bouton dans la barre d'outils (`DataTableToolbar`), qui
 * recopie l'URL. Une fiche n'a ni barre ni filtres : elle exporte SON document.
 * Ce bouton fait la même chose que celui des listes — même téléchargement, même
 * message « N lignes exportées » — avec une action que la fiche lui prête.
 *
 * Le classeur transite en base64 par une Server Action (D-018) : le cookie de
 * session ne suivrait pas un lien direct vers l'API.
 */
export function ExportExcelButton({
  label,
  filename,
  onExport,
  disabled,
}: {
  label: string;
  /** Sans extension : `.xlsx` est ajouté. */
  filename: string;
  onExport: () => Promise<ReferentialResult<{ file: string; rows: number | null }>>;
  disabled?: boolean;
}) {
  const t = useTranslations('table');
  const [pending, setPending] = useState(false);

  const run = async () => {
    setPending(true);
    const result = await onExport();
    setPending(false);

    if (!result.ok) {
      toast.error(result.error === 'invalid' ? t('exportFailed') : result.error);
      return;
    }

    downloadBase64(
      result.data.file,
      `${filename}.xlsx`,
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    );
    if (result.data.rows !== null) toast.success(t('exported', { count: result.data.rows }));
  };

  return (
    <Button
      type="button"
      variant="outline"
      size="sm"
      onClick={run}
      disabled={pending || disabled}
      className="gap-1.5"
    >
      {pending ? (
        <Loader2 className="size-4 animate-spin" />
      ) : (
        <Download className="size-4" aria-hidden />
      )}
      {label}
    </Button>
  );
}
