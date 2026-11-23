import type {
  AdminFieldStatusResult,
  CreateParcelPayload,
  EditParcelFeesPayload,
  Paginated,
  ParcelImportListItem,
  ParcelImportMode,
  ParcelImportPreview,
  ParcelImportResult,
  ParcelImportRowInput,
  Parcel,
  ParcelBlRequestPayload,
  ParcelDetail,
  ParcelEvent,
  ParcelFilters,
  ParcelListItem,
  ParcelNote,
  ParcelStats,
  Quote,
  ChangeDestinationPayload,
  ExpectedPaymentChangePayload,
  LabelFormat,
  LabelJob,
  SellerParcelUpdatePayload,
  CancelDeliveryPayload,
  CancelDeliveryResult,
  StatusCorrectionPayload,
  StatusCorrectionPreview,
  StatusCorrectionResult,
  ParcelAdminDeletePayload,
  ParcelClientChangePayload,
  ParcelClientChangePreview,
  ParcelClientChangePreviewPayload,
  ParcelClientChangeResult,
} from '@delivery/shared';
import { apiFetch, apiFetchBase64 } from '@/lib/http';

/**
 * Colis — `GET /parcels`, `GET /parcels/:code`, `POST /parcels`,
 * `POST /parcels/:code/events`, `POST /parcels/:code/notes`.
 *
 * Les signatures sont inchangées depuis la phase mockée : c'était le contrat,
 * il a tenu. Aucun composant n'a été touché par la bascule.
 *
 * Le filtrage par agence, client ou livreur n'apparaît PAS dans ces appels :
 * il est appliqué côté serveur depuis la session. Un filtre de portée envoyé
 * par le client ne serait pas un filtre, ce serait une suggestion.
 */

export interface ParcelListQuery extends ParcelFilters {
  page?: number;
  pageSize?: number;
  /** Candidats à un BR- ou un BD- : pas encore pris en charge, sur aucun bon actif (D-132). */
  noteCandidate?: boolean;
  /**
   * Les plus récents en premier (date de création) — chantier « Tri par
   * défaut ». Demandé par les ÉCRANS ; l'export, qui ne passe pas par ici,
   * garde son ordre.
   */
  recent?: boolean;
}

function toQuery(query: ParcelListQuery) {
  return {
    page: query.page,
    pageSize: query.pageSize,
    search: query.search,
    statusId: query.statusId,
    statusGroup: query.statusGroup,
    statusIds: query.statusIds,
    clientId: query.clientId,
    courierId: query.courierId,
    destinationCityId: query.destinationCityId,
    billingState: query.billingState,
    financialStatus: query.financialStatus,
    /* Le filtre financier du vendeur — lot O, D-227. */
    merchantFinancialStatus: query.merchantFinancialStatus,
    /* Le filtre « Type » — chantier « Étiquette orange », point 2. */
    kind: query.kind,
    /* La période — chantier « Retours recette 26/09 », point 1. */
    period: query.period,
    from: query.from,
    to: query.to,
    noteCandidate: query.noteCandidate ? 'true' : undefined,
    recent: query.recent ? 'true' : undefined,
    // D-257 : les colis supprimés (administrateur).
    deleted: query.deleted,
  };
}

export async function getParcels(query: ParcelListQuery = {}): Promise<Paginated<ParcelListItem>> {
  return apiFetch<Paginated<ParcelListItem>>('/parcels', { query: toQuery(query) });
}

export async function getParcel(
  code: string,
  /** D-257 : la fiche d'un colis supprimé — l'administrateur seul, l'API le décide. */
  options: { includeDeleted?: boolean } = {},
): Promise<ParcelDetail> {
  return apiFetch<ParcelDetail>(`/parcels/${encodeURIComponent(code)}`, {
    query: options.includeDeleted ? { deleted: '1' } : undefined,
  });
}

export async function getParcelStats(filters: ParcelFilters = {}): Promise<ParcelStats> {
  return apiFetch<ParcelStats>('/parcels/stats', { query: toQuery(filters) });
}

/* -------------------------------------------------------------------------- */
/* Étiquettes en arrière-plan — lot D, D3 (D-190)                             */
/* -------------------------------------------------------------------------- */

/** Ce que la liste demande : ses cochés, ou toute la liste filtrée (D-176). */
export type LabelJobSelection =
  | { codes: string[] }
  | { all: true; exclude?: string[]; filters: Record<string, string | number | undefined> };

/** `POST /parcels/label-jobs` — au-delà de 500 étiquettes, un travail. */
export async function createLabelJob(
  selection: LabelJobSelection,
  format: LabelFormat,
): Promise<LabelJob> {
  return apiFetch<LabelJob>('/parcels/label-jobs', {
    method: 'POST',
    body: { ...selection, format },
  });
}

/** `GET /parcels/label-jobs` — les miens, des dernières 24 h. */
export async function getLabelJobs(): Promise<LabelJob[]> {
  return apiFetch<LabelJob[]>('/parcels/label-jobs');
}

/** `GET /parcels/label-jobs/:id` — où en est la préparation. */
export async function getLabelJob(id: string): Promise<LabelJob> {
  return apiFetch<LabelJob>(`/parcels/label-jobs/${encodeURIComponent(id)}`);
}

/* -------------------------------------------------------------------------- */
/* Tarification — DOMAIN.md §3.1                                              */
/* -------------------------------------------------------------------------- */

export interface QuoteParcelQuery {
  originCityId: string;
  destinationCityId: string;
  deliveryTarget: 'district' | 'agency' | 'relay-point';
  clientId?: string;
}

export async function quoteParcel(query: QuoteParcelQuery): Promise<Quote> {
  return apiFetch<Quote>('/parcels/quote', { method: 'POST', body: query });
}

/**
 * Code du prochain colis, en aperçu à la saisie.
 *
 * L'API ne consomme PAS de rang ici : la séquence n'est réservée qu'à la
 * création, par une instruction atomique. Deux agents qui ouvrent le formulaire
 * en même temps voient donc le même code, et c'est assumé — seule la création
 * tranche (DOMAIN.md §3.2, §4 dette n°6).
 */
export async function previewParcelCode(
  originCityId: string,
  destinationCityId: string,
): Promise<string> {
  const { code } = await apiFetch<{ code: string }>('/parcels/next-code', {
    query: { originCityId, destinationCityId },
  });
  return code;
}

/* -------------------------------------------------------------------------- */
/* Écritures                                                                   */
/* -------------------------------------------------------------------------- */

export async function createParcel(input: CreateParcelPayload): Promise<Parcel> {
  return apiFetch<Parcel>('/parcels', { method: 'POST', body: input });
}

export interface ChangeParcelStatusInput {
  statusId: number;
  comment?: string;
  /** Date de RDV, saisie avec un statut de report (14, 38). */
  scheduledDate?: string;
  /** Contestation d'un statut terminal : bypass encadré, commentaire obligatoire. */
  dispute?: boolean;
  /** D-246 : le livreur qui reçoit le colis, exigé par l'API pour « En cours de livraison ». */
  courierId?: string;
  /** La nouvelle destination, quand ce livreur ne couvre pas la ville du colis. */
  destinationCityId?: string;
}

export async function changeParcelStatus(
  code: string,
  input: ChangeParcelStatusInput,
): Promise<ParcelEvent> {
  return apiFetch<ParcelEvent>(`/parcels/${encodeURIComponent(code)}/events`, {
    method: 'POST',
    body: input,
  });
}

/** Le geste de l'administrateur : la cible, le commentaire obligatoire, la date de report. */
export interface FieldStatusChange {
  statusId: number;
  comment: string;
  scheduledDate?: string;
}

/**
 * `POST /parcels/field-status` — « Statut libre pour l'admin » (D-245).
 *
 * D'un statut de terrain à un autre, à l'unité ou en masse : les colis cochés,
 * ou toute la liste filtrée, dans la même forme que les étiquettes (D-176).
 * Chaque colis est jugé SEUL : la réponse dit ce qui a changé et ce qui a été
 * refusé, avec le motif. Tous les verrous sont au serveur.
 */
export async function changeFieldStatus(
  change: FieldStatusChange,
  selection: LabelJobSelection,
): Promise<AdminFieldStatusResult> {
  return apiFetch<AdminFieldStatusResult>('/parcels/field-status', {
    method: 'POST',
    body: { ...change, ...selection },
  });
}

/**
 * Modifier les frais d'un colis — C11 bloc 4, D-063.
 *
 * `PATCH` : c'est une modification partielle d'un colis existant. Elle rend le
 * DÉTAIL complet, historique compris, pour que la fiche se redessine sans
 * relecture.
 */
export async function editParcelFees(
  code: string,
  body: EditParcelFeesPayload,
): Promise<ParcelDetail> {
  return apiFetch<ParcelDetail>(`/parcels/${encodeURIComponent(code)}/fees`, {
    method: 'PATCH',
    body,
  });
}

export async function addParcelNote(code: string, body: string): Promise<ParcelNote> {
  return apiFetch<ParcelNote>(`/parcels/${encodeURIComponent(code)}/notes`, {
    method: 'POST',
    body: { body },
  });
}

/* -------------------------------------------------------------------------- */
/* Import Excel — C7 bloc 1                                                   */
/* -------------------------------------------------------------------------- */

export interface ParcelImportSource {
  /** Classeur en base64 — D-022 : le fichier voyage dans un corps JSON. */
  content?: string;
  /** Lignes corrigées à l'écran. Exclusif de `content`. */
  rows?: ParcelImportRowInput[];
  filename?: string;
  clientId?: string;
}

/** Aperçu — l'API n'écrit rien, et rend les erreurs ligne par ligne. */
export async function previewParcelImport(
  source: ParcelImportSource,
): Promise<ParcelImportPreview> {
  return apiFetch<ParcelImportPreview>('/parcels/import/preview', {
    method: 'POST',
    body: source,
  });
}

/**
 * « Télécharger les anomalies » — lot J, J4. Le classeur revient en base64 :
 * même source et même validateur que l'aperçu, rien n'est écrit.
 */
export async function parcelImportAnomalies(source: ParcelImportSource): Promise<string> {
  return apiFetchBase64('/parcels/import/anomalies.xlsx', { method: 'POST', body: source });
}

export async function runParcelImport(
  source: ParcelImportSource & { mode: ParcelImportMode },
): Promise<ParcelImportResult> {
  return apiFetch<ParcelImportResult>('/parcels/import', { method: 'POST', body: source });
}

export async function getParcelImports(
  query: { page?: number; pageSize?: number; clientId?: string } = {},
): Promise<Paginated<ParcelImportListItem>> {
  return apiFetch<Paginated<ParcelImportListItem>>('/parcels/imports', { query });
}

/**
 * « BL À RÉCUPÉRER ? » sur un colis existant — chantier « Retours, BL Clients
 * Pro », point 5.
 *
 * Une route à elle seule, comme les frais : il n'existe pas de `PATCH` général
 * du colis, et en ouvrir un pour un booléen aurait mis la description, le
 * montant et l'adresse à portée du même geste.
 */
/** `PATCH /parcels/:code` — le vendeur corrige son colis avant ramassage (recette 3, point 3). */
export async function updateSellerParcel(
  code: string,
  body: SellerParcelUpdatePayload,
): Promise<ParcelDetail> {
  return apiFetch<ParcelDetail>(`/parcels/${encodeURIComponent(code)}`, { method: 'PATCH', body });
}

/** `DELETE /parcels/:code` — le vendeur supprime son colis avant ramassage (point 3). */
export async function deleteSellerParcel(code: string): Promise<{ code: string }> {
  return apiFetch<{ code: string }>(`/parcels/${encodeURIComponent(code)}`, { method: 'DELETE' });
}

/** `PATCH /parcels/:code/expected-payment` — le comptoir bascule Espèces ↔ Chèque (point 3). */
export async function changeParcelExpectedPayment(
  code: string,
  body: ExpectedPaymentChangePayload,
): Promise<ParcelDetail> {
  return apiFetch<ParcelDetail>(`/parcels/${encodeURIComponent(code)}/expected-payment`, {
    method: 'PATCH',
    body,
  });
}

export async function setParcelBlRequested(
  code: string,
  body: ParcelBlRequestPayload,
): Promise<ParcelDetail> {
  return apiFetch<ParcelDetail>(`/parcels/${encodeURIComponent(code)}/bl`, {
    method: 'PATCH',
    body,
  });
}

/** Changer la destination — chantier « Finance », point 7. */
export async function changeDestination(
  code: string,
  body: ChangeDestinationPayload,
): Promise<ParcelDetail> {
  return apiFetch<ParcelDetail>(`/parcels/${encodeURIComponent(code)}/destination`, {
    method: 'PATCH',
    body,
  });
}

/**
 * LA CORRECTION LIBRE DU STATUT — D-250. L'aperçu (lecture seule : le plan
 * lisible, les livreurs et circuits proposés, l'empreinte), puis l'écriture.
 */
export async function previewStatusCorrection(
  code: string,
  body: StatusCorrectionPayload,
): Promise<StatusCorrectionPreview> {
  return apiFetch<StatusCorrectionPreview>(
    `/parcels/${encodeURIComponent(code)}/correction/preview`,
    { method: 'POST', body },
  );
}

/**
 * D-256 — l'administrateur supprime un colis pas encore pris en charge (motif),
 * et change le client d'un colis : aperçu, puis écriture.
 */
export async function adminDeleteParcel(
  code: string,
  body: ParcelAdminDeletePayload,
): Promise<{ code: string }> {
  return apiFetch<{ code: string }>(`/parcels/${encodeURIComponent(code)}/admin-delete`, {
    method: 'POST',
    body,
  });
}

export async function previewClientChange(
  code: string,
  body: ParcelClientChangePreviewPayload,
): Promise<ParcelClientChangePreview> {
  return apiFetch<ParcelClientChangePreview>(
    `/parcels/${encodeURIComponent(code)}/client/preview`,
    { method: 'POST', body },
  );
}

export async function changeParcelClient(
  code: string,
  body: ParcelClientChangePayload,
): Promise<ParcelClientChangeResult> {
  return apiFetch<ParcelClientChangeResult>(`/parcels/${encodeURIComponent(code)}/client`, {
    method: 'POST',
    body,
  });
}

export async function applyStatusCorrection(
  code: string,
  body: StatusCorrectionPayload,
): Promise<StatusCorrectionResult> {
  return apiFetch<StatusCorrectionResult>(`/parcels/${encodeURIComponent(code)}/correction`, {
    method: 'POST',
    body,
  });
}

/**
 * « Annuler la livraison » — lot I, I2 (D-210). Administrateur seul, motif
 * obligatoire ; le serveur défait l'argent de la livraison ou refuse.
 */
export async function cancelDelivery(
  code: string,
  body: CancelDeliveryPayload,
): Promise<CancelDeliveryResult> {
  return apiFetch<CancelDeliveryResult>(`/parcels/${encodeURIComponent(code)}/cancel-delivery`, {
    method: 'POST',
    body,
  });
}
