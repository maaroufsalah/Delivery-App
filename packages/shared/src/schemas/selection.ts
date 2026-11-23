import { z } from 'zod';

/**
 * CHOISIR DANS UNE LISTE DE CANDIDATS, SANS PLAFOND CACHÉ — chantier « Plafond
 * de 200 » (2026-10-03).
 *
 * BR-2610-0002 avait 358 candidats ; la modale en chargeait 200 d'un coup,
 * cochait « 0 / 200 » et rendait les autres invisibles. Désormais, partout où
 * l'on choisit des colis (ou des bons) dans une liste :
 *
 *  - la liste se lit PAR PAGES (`CANDIDATE_PAGE_SIZE`), avec son TOTAL réel et
 *    une recherche faite au serveur ;
 *  - la sélection part SOIT en liste d'identifiants, SOIT en critère —
 *    « tous ceux de cette recherche, sauf ceux-ci » (`all`, `search`,
 *    `exclude`) —, que le serveur résout lui-même dans la transaction
 *    d'écriture : « Tout cocher » coche aussi ce qui n'est pas encore chargé.
 *
 * `SELECTION_MAX` n'est pas un plafond silencieux : au-delà, l'API REFUSE
 * (422 `selection-too-large`) et l'écran le dit.
 */
export const CANDIDATE_PAGE_SIZE = 100;
export const SELECTION_MAX = 5_000;

/**
 * LES ÉTIQUETTES EN ARRIÈRE-PLAN — lot D, D3 (D-190).
 *
 * Environ 40 ms par étiquette : au-delà de quelques centaines, la requête
 * dépasse le délai du proxy (60 s vers 1 500 étiquettes), et le Black Friday
 * en demande 5 000. Au-delà du seuil, la demande devient un TRAVAIL : l'écran
 * dit « Préparation de X étiquettes… », puis « Télécharger ». En dessous, le
 * PDF part tout de suite, comme avant.
 */
export const LABELS_ASYNC_THRESHOLD = 500;

/** Le PDF préparé reste téléchargeable 24 h, par son seul demandeur. */
export const LABEL_JOB_TTL_HOURS = 24;

/** Une page de candidats : le total réel, et combien sont sélectionnables. */
export interface CandidatePage<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  /** Ceux qu'on peut cocher (les grisés exclus) — la base de « Tout cocher ». */
  selectableTotal: number;
}

const ids = z.array(z.string().min(1)).max(SELECTION_MAX, 'too-many');

/** Les champs « par critère », à étendre dans chaque schéma d'ajout. */
export const selectionByCriteria = {
  /** Vrai : tous les candidats de la recherche, chargés ou non. */
  all: z.boolean().optional(),
  search: z.string().trim().max(120).optional(),
  /** Décochés à la main après « Tout cocher ». */
  exclude: ids.optional(),
};

/** Une liste d'identifiants facultative quand `all` est posé. */
export const optionalIds = ids.optional();

/** Il faut l'un ou l'autre : des identifiants, ou `all`. */
export function hasSelection(value: { all?: boolean; ids?: readonly string[] }): boolean {
  return value.all === true || (value.ids?.length ?? 0) > 0;
}

/** Les mêmes champs, côté écran. */
export interface SelectionPayload {
  ids?: string[];
  all?: boolean;
  search?: string;
  exclude?: string[];
}
