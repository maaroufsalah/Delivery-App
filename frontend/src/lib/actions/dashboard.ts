'use server';

import {
  DASHBOARD_EXPORT_FORMATS,
  DASHBOARD_EXPORT_SECTIONS,
  type DashboardExportFormat,
  type DashboardExportSection,
} from '@delivery/shared';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import { apiFetchBase64 } from '@/lib/http';

/**
 * L'EXPORT D'UNE SECTION DU TABLEAU DE BORD — chantier « Dashboard de
 * pilotage », spec § 16.
 *
 * Même chemin que `exportListAction` : le fichier revient en base64 par une
 * Server Action, parce que le cookie de session est `httpOnly` sur l'origine
 * du web et qu'un lien direct vers l'API partirait sans lui (D-018). `query`
 * porte les filtres de l'écran tels qu'ils sont — ce qui est affiché est ce qui
 * s'exporte.
 */
export async function exportDashboardAction(
  section: DashboardExportSection,
  format: DashboardExportFormat,
  query: Record<string, string>,
): Promise<ReferentialResult<{ file: string; rows: number | null; mimeType: string }>> {
  if (
    !(DASHBOARD_EXPORT_SECTIONS as readonly string[]).includes(section) ||
    !(DASHBOARD_EXPORT_FORMATS as readonly string[]).includes(format)
  ) {
    return { ok: false, error: 'invalid' };
  }

  try {
    let rows: number | null = null;
    const file = await apiFetchBase64(
      '/dashboard/export',
      { query: { ...query, section, format } },
      (headers) => {
        const value = Number(headers.get('x-export-rows'));
        rows = Number.isFinite(value) && headers.has('x-export-rows') ? value : null;
      },
    );
    return {
      ok: true,
      data: {
        file,
        rows,
        mimeType:
          format === 'csv'
            ? 'text/csv;charset=utf-8'
            : 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      },
    };
  } catch (error) {
    return toFailure(error);
  }
}
