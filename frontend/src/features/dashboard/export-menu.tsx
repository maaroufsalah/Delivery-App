'use client';

import type { DashboardExportFormat, DashboardExportSection } from '@delivery/shared';
import { Download, FileSpreadsheet, FileText, Loader2, Printer } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { exportDashboardAction } from '@/lib/actions/dashboard';
import { downloadBase64 } from '@/lib/download';

/**
 * EXPORTER UNE SECTION — chantier « Dashboard de pilotage », spec § 16.
 *
 * Un menu à deux entrées, Excel et CSV, posé dans l'en-tête de chaque
 * classement. `query` est l'état EXACT de la section (période, filtres, tri) :
 * l'export rejoue la requête de l'écran, sans pagination. `try / finally` et
 * un garde contre le double clic (audit UX-01 / UX-02) : un export lent ne
 * bloque pas le bouton pour toujours.
 */
export function ExportMenu({
  section,
  query,
  filename,
}: {
  section: DashboardExportSection;
  /** Période, filtres, tri — ce que l'écran a demandé. */
  query: Record<string, string>;
  /** Base du nom de fichier : « livreurs », « villes »… */
  filename: string;
}) {
  const t = useTranslations('dashboard.export');
  const tTable = useTranslations('table');
  const [busy, setBusy] = useState(false);

  const run = async (format: DashboardExportFormat) => {
    if (busy) return;
    setBusy(true);
    try {
      const result = await exportDashboardAction(section, format, query);
      if (!result.ok) {
        toast.error(result.error === 'invalid' ? tTable('exportFailed') : result.error);
        return;
      }
      const stamp = new Date().toISOString().slice(0, 10);
      downloadBase64(result.data.file, `${filename}-${stamp}.${format}`, result.data.mimeType);
      toast.success(
        result.data.rows === null ? t('done') : tTable('exported', { count: result.data.rows }),
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="sm" className="gap-1.5 print:hidden" disabled={busy}>
          {busy ? (
            <Loader2 className="size-4 animate-spin" aria-hidden />
          ) : (
            <Download className="size-4" aria-hidden />
          )}
          <span className="hidden sm:inline">{tTable('export')}</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem onSelect={() => void run('xlsx')}>
          <FileSpreadsheet className="size-4" aria-hidden />
          {t('excel')}
        </DropdownMenuItem>
        <DropdownMenuItem onSelect={() => void run('csv')}>
          <FileText className="size-4" aria-hidden />
          {t('csv')}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/**
 * IMPRIMER LE TABLEAU DE BORD — spec § 16.
 *
 * `window.print()` et une feuille `@media print` : la barre latérale, les
 * filtres et les boutons disparaissent, les cartes passent sur une colonne. Pas
 * de PDF serveur — un tableau de bord s'imprime tel qu'on le voit.
 */
export function PrintDashboardButton() {
  const t = useTranslations('dashboard.export');
  return (
    <Button
      variant="outline"
      size="sm"
      className="gap-1.5 print:hidden"
      onClick={() => window.print()}
    >
      <Printer className="size-4" aria-hidden />
      <span className="hidden sm:inline">{t('print')}</span>
    </Button>
  );
}
