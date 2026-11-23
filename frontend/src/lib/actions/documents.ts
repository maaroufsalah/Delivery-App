'use server';

import type { LabelFormat, LabelJob } from '@delivery/shared';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import { createLabelJob, getLabelJob, getLabelJobs } from '@/lib/api/parcels';
import { apiFetchBase64 } from '@/lib/http';

/**
 * Les documents imprimables : étiquettes, feuille de route, bons.
 *
 * Ils passent par une Server Action et non par un lien direct vers l'API : la
 * session vit dans un cookie `httpOnly` posé sur l'origine du web, et un
 * `<a href>` vers l'API partirait sans elle. Le PDF revient donc en base64 et
 * le navigateur en fait un `Blob` — c'est déjà la voie des pièces jointes des
 * fiches clients.
 */

export async function parcelLabelAction(
  code: string,
  format: LabelFormat,
): Promise<ReferentialResult<string>> {
  try {
    return {
      ok: true,
      data: await apiFetchBase64(`/parcels/${encodeURIComponent(code)}/label`, {
        query: { format },
      }),
    };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * LES E-TICKETS D'UNE SÉLECTION — V2 vendeur bloc 3, spécification §4.
 *
 * Un seul PDF pour toute la sélection, et non une archive : le vendeur
 * imprime, il ne décompresse pas quarante fichiers un par un.
 *
 * **La borne est côté SERVEUR** (deux cents codes) ; celle-ci ne fait que
 * transmettre. Refaire la vérification ici ne protégerait rien — une Server
 * Action est une frontière réseau, et ce qui lui arrive n'a pas forcément
 * traversé l'écran.
 */
export async function parcelLabelsAction(
  selection: ParcelLabelSelection,
  format: LabelFormat,
): Promise<ReferentialResult<string>> {
  try {
    /*
     * D-176 (lot B, B5) : en CORPS, par `POST` — cinq mille codes ne tiennent
     * pas dans une URL. Les cochés OU toute la liste filtrée ; le serveur rend
     * l'ordre de la liste et refuse au-delà de 5 000 (422 nommé).
     */
    return {
      ok: true,
      data: await apiFetchBase64('/parcels/labels', {
        method: 'POST',
        body: { ...selection, format },
      }),
    };
  } catch (error) {
    return toFailure(error);
  }
}

/** Ce que la liste des colis demande : ses cochés, ou toute la liste filtrée. */
export type ParcelLabelSelection =
  | { codes: string[] }
  | { all: true; exclude?: string[]; filters: Record<string, string | number | undefined> };

/*
 * LES ÉTIQUETTES EN ARRIÈRE-PLAN — lot D, D3 (D-190). Au-delà de 500, la
 * demande crée un travail ; l'écran demande où il en est, puis télécharge le
 * PDF, toujours par le serveur (D-018) : le cookie de session ne quitte pas le
 * web, et l'API ne rend le fichier qu'à son demandeur.
 */
export async function createLabelJobAction(
  selection: ParcelLabelSelection,
  format: LabelFormat,
): Promise<ReferentialResult<LabelJob>> {
  try {
    return { ok: true, data: await createLabelJob(selection, format) };
  } catch (error) {
    return toFailure(error, 'parcels.label-jobs');
  }
}

export async function labelJobsAction(): Promise<ReferentialResult<LabelJob[]>> {
  try {
    return { ok: true, data: await getLabelJobs() };
  } catch (error) {
    return toFailure(error, 'parcels.label-jobs');
  }
}

export async function labelJobAction(id: string): Promise<ReferentialResult<LabelJob>> {
  try {
    return { ok: true, data: await getLabelJob(id) };
  } catch (error) {
    return toFailure(error, 'parcels.label-jobs');
  }
}

export async function labelJobFileAction(id: string): Promise<ReferentialResult<string>> {
  try {
    return {
      ok: true,
      data: await apiFetchBase64(`/parcels/label-jobs/${encodeURIComponent(id)}/file`),
    };
  } catch (error) {
    return toFailure(error, 'parcels.label-jobs');
  }
}

export async function routeLabelsAction(
  code: string,
  format: LabelFormat,
): Promise<ReferentialResult<string>> {
  try {
    return {
      ok: true,
      data: await apiFetchBase64(`/routes/${encodeURIComponent(code)}/labels`, {
        query: { format },
      }),
    };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * L'EXPORT EXCEL D'UN CIRCUIT — une ligne par colis (nuit du 11/11/2026,
 * chantier « Export Excel des circuits »). Le chemin est composé ICI, sur le
 * serveur, à partir du seul code du circuit : rien de ce que le navigateur
 * envoie ne choisit la ressource appelée. La portée est celle de l'API.
 */
export async function routeExportAction(
  code: string,
): Promise<ReferentialResult<{ file: string; rows: number | null }>> {
  try {
    let rows: number | null = null;
    const file = await apiFetchBase64(
      `/routes/${encodeURIComponent(code)}/export`,
      {},
      (headers) => {
        const value = Number(headers.get('x-export-rows'));
        rows = Number.isFinite(value) && headers.has('x-export-rows') ? value : null;
      },
    );
    return { ok: true, data: { file, rows } };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * L'EXPORT EXCEL DE LA FICHE D'UN BON — une ligne par colis (chantier « Export
 * Excel partout », 12/11/2026). Une action par document, sur le modèle de
 * `routeExportAction` : le chemin est composé ICI, sur le serveur, à partir du
 * seul code du bon. La portée et le 404 sont ceux de la fiche, côté API.
 */
type DocumentExport = ReferentialResult<{ file: string; rows: number | null }>;

/*
 * Le chemin reste écrit EN CLAIR dans chaque action, à l'appel d'`apiFetchBase64` :
 * `ui-api-calls.spec.ts` lit chaque appel du web et vérifie qu'il tombe sur une
 * route réelle. Ce qui est partagé ici n'est que la lecture du nombre de lignes.
 */
async function documentExport(
  download: (onHeaders: (headers: Headers) => void) => Promise<string>,
): Promise<DocumentExport> {
  try {
    let rows: number | null = null;
    const file = await download((headers) => {
      const value = Number(headers.get('x-export-rows'));
      rows = Number.isFinite(value) && headers.has('x-export-rows') ? value : null;
    });
    return { ok: true, data: { file, rows } };
  } catch (error) {
    return toFailure(error);
  }
}

/** Bon de ramassage (BR-) ou de dépôt (BD-). */
export async function deliveryNoteExportAction(code: string): Promise<DocumentExport> {
  return documentExport((onHeaders) =>
    apiFetchBase64(`/delivery-notes/${encodeURIComponent(code)}/export`, {}, onHeaders),
  );
}

/** Bon de retour livreur (RL-). */
export async function returnHandoverExportAction(code: string): Promise<DocumentExport> {
  return documentExport((onHeaders) =>
    apiFetchBase64(`/return-handovers/${encodeURIComponent(code)}/export`, {}, onHeaders),
  );
}

/** Bon de retour vendeur (BRT-). */
export async function returnSlipExportAction(code: string): Promise<DocumentExport> {
  return documentExport((onHeaders) =>
    apiFetchBase64(`/return-slips/${encodeURIComponent(code)}/export`, {}, onHeaders),
  );
}

/** Caisse (RF-). */
export async function remittanceExportAction(code: string): Promise<DocumentExport> {
  return documentExport((onHeaders) =>
    apiFetchBase64(`/remittances/${encodeURIComponent(code)}/export`, {}, onHeaders),
  );
}

/** Bon de versement (BV-). */
export async function codPayoutExportAction(code: string): Promise<DocumentExport> {
  return documentExport((onHeaders) =>
    apiFetchBase64(`/cod-payouts/${encodeURIComponent(code)}/export`, {}, onHeaders),
  );
}

/** La feuille de route papier — le filet de sécurité quand le téléphone lâche. */
export async function routeSheetAction(code: string): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await apiFetchBase64(`/routes/${encodeURIComponent(code)}/sheet`) };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deliveryNotePdfAction(code: string): Promise<ReferentialResult<string>> {
  try {
    return {
      ok: true,
      data: await apiFetchBase64(`/delivery-notes/${encodeURIComponent(code)}/pdf`),
    };
  } catch (error) {
    return toFailure(error);
  }
}

export async function returnSlipPdfAction(code: string): Promise<ReferentialResult<string>> {
  try {
    return {
      ok: true,
      data: await apiFetchBase64(`/return-slips/${encodeURIComponent(code)}/pdf`),
    };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Export Excel d'une liste — UNE action pour toutes les ressources.
 *
 * `resource` est le chemin du contrôleur (`parcels`, `cities`, `orders`…) et
 * `query` la chaîne de paramètres de l'écran, filtres et tri compris. L'API
 * rejoue sa propre liste avec ces paramètres : l'export ne peut donc pas
 * diverger de ce qui est affiché.
 *
 * La ressource est validée contre une liste blanche. Sans elle, ce paramètre
 * viendrait du navigateur et composerait librement le chemin appelé sur l'API.
 */
const EXPORTABLE = [
  'parcels',
  'routes',
  'delivery-notes',
  'clients',
  'couriers',
  'agencies',
  'users',
  'cities',
  'districts',
  'zones',
  'delivery-types',
  'return-handovers',
  'return-slips',
  // Lot E, E1.2 (D-201) : la liste des BL, « Bons de livraison ».
  'bl/delivery-slips',
  'products',
  'stock',
  'storage-requests',
  'orders',
  'invoices',
  // « Factures clients pro » — recette 3, point 4.
  'invoices-pro',
  'payments',
  'remittances',
  // Chantier « Export Excel partout » (12/11/2026) : la liste des chèques.
  'cheques',
  // C11 bloc 3 — les bons de versement (D-061).
  'cod-payouts',
  // Lot M (D-222) — le stock des livreurs : la liste, et le détail d'un livreur.
  'courier-stock',
  'courier-stock/parcels',
  // La petite caisse — chantier « Caisse, OV, Petite caisse », point 3.
  'petty-cash',
  'courier-statements',
  /*
   * Chantier « Affichage web et étiquette », point 4 : les quatre écrans qui
   * restaient sans export. « Mes retours » du vendeur et la pile de l'agence,
   * les bons BL, et la petite caisse du livreur (ses caisses passent par
   * `remittances`, déjà là).
   */
  'returns/pending',
  'bl/notes',
  'courier/me/petty-cash',
  'claims',
  // C11 bloc 5 — le journal des modifications (D-064).
  'audit',
  // Recette SpeedyTrans 4, point 4 — le journal LISIBLE (D-133).
  'audit/journal',
] as const;

export type ExportableResource = (typeof EXPORTABLE)[number];

export async function exportListAction(
  resource: string,
  query: Record<string, string>,
): Promise<ReferentialResult<{ file: string; rows: number | null }>> {
  if (!(EXPORTABLE as readonly string[]).includes(resource)) {
    return { ok: false, error: 'invalid' };
  }

  try {
    // Le nombre de lignes voyage en en-tête (`x-export-rows`, Recette 5, point 8).
    let rows: number | null = null;
    const file = await apiFetchBase64(`/${resource}/export`, { query }, (headers) => {
      const value = Number(headers.get('x-export-rows'));
      rows = Number.isFinite(value) && headers.has('x-export-rows') ? value : null;
    });
    return { ok: true, data: { file, rows } };
  } catch (error) {
    return toFailure(error);
  }
}
