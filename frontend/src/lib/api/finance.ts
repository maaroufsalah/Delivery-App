import type {
  Bank,
  BankAccount,
  BillableParcelLine,
  BulkInvoiceCandidate,
  BulkInvoiceResult,
  ChequeDetail,
  ChequeBulkResult,
  ChequePage,
  ChequeStatus,
  CodPayoutBulkCandidate,
  CodPayoutBulkInput,
  CodPayoutBulkResult,
  CodPayoutCandidate,
  CourierBalance,
  CodPayoutCreatePayload,
  CodPayoutDetail,
  CodPayoutListItem,
  CodPayoutParcelsPayload,
  CodPayoutPayPayload,
  CourierStatementDetail,
  CourierStatementListItem,
  CourierStatementPayload,
  ChequePhotoPayload,
  StatementPayPayload,
  FinanceDashboard,
  FinanceOverview,
  InvoiceCreatePayload,
  InvoiceDetail,
  InvoiceIssuePayload,
  InvoiceClientKind,
  InvoiceDaySummary,
  InvoiceListItem,
  InvoiceMonthlyPayload,
  InvoiceAdjustmentPayload,
  InvoiceParcelsPayload,
  Paginated,
  PaymentListItem,
  PaymentPayload,
  RemittanceCandidate,
  RemittanceCountPayload,
  RemittanceCreatePayload,
  RemittanceDetail,
  RemittanceListItem,
  RemittanceSummary,
  RemittanceParcelsPayload,
  TransferOrderCreateInput,
  TransferOrderDetail,
  TransferOrderListItem,
} from '@delivery/shared';
import { apiFetch, apiFetchBase64 } from '@/lib/http';

/**
 * Facturation — factures (`FA-`), règlements et retours de fonds (`RF-`).
 *
 * Une fonction = un endpoint, comme partout ailleurs. Les trois listes sont
 * paginées par le serveur ; les deux piles d'entrée — colis facturables, colis
 * dont le cash est à remonter — ne le sont pas : ce sont des plans de travail,
 * pas des archives, et on les compose d'un seul écran.
 */

type Query = Record<string, string | number | undefined>;

function toSearch(query: object): Record<string, string | number> {
  const search: Record<string, string | number> = {};

  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue;
    search[key] = value as string | number;
  }

  return search;
}

/** Le point du jour — `GET /finance/dashboard`. */
export async function getFinanceDashboard(agencyId?: string): Promise<FinanceDashboard> {
  return apiFetch<FinanceDashboard>('/finance/dashboard', { query: toSearch({ agencyId }) });
}

/** Le tableau de bord financier — `GET /finance/overview`, point 10. */
export async function getFinanceOverview(query: {
  period?: string;
  from?: string;
  to?: string;
  courierId?: string;
  agencyId?: string;
}): Promise<FinanceOverview> {
  return apiFetch<FinanceOverview>('/finance/overview', { query: toSearch(query) });
}

/* --- Factures ------------------------------------------------------------- */

export async function getInvoicePage(
  query: Query & { status?: string; clientId?: string; agencyId?: string; unpaidOnly?: string },
): Promise<Paginated<InvoiceListItem>> {
  return apiFetch<Paginated<InvoiceListItem>>('/invoices', { query: toSearch(query) });
}

/** Les filtres de la liste des factures, que ses en-têtes de jour rejouent (D-254). */
type InvoiceFilters = Query & {
  status?: string;
  clientId?: string;
  agencyId?: string;
  unpaidOnly?: string;
};

/**
 * `GET /invoices/days` ou `/invoices-pro/days` — les en-têtes des jours de la
 * page affichée (D-254) : nombre, colis, total, règlements par méthode.
 */
export async function getInvoiceDays(
  kind: InvoiceClientKind,
  query: InvoiceFilters & { days: string },
): Promise<InvoiceDaySummary[]> {
  return apiFetch<InvoiceDaySummary[]>(kind === 'pro' ? '/invoices-pro/days' : '/invoices/days', {
    query: toSearch(query),
  });
}

/**
 * `GET /invoices/day-pdf` ou `/invoices-pro/day-pdf` — les factures d'un jour
 * (ou les brouillons, `day = draft`) en un PDF, une page chacune (D-254).
 */
export async function downloadInvoiceDayPdf(
  kind: InvoiceClientKind,
  query: InvoiceFilters & { day: string },
): Promise<string> {
  return apiFetchBase64(kind === 'pro' ? '/invoices-pro/day-pdf' : '/invoices/day-pdf', {
    query: toSearch(query),
  });
}

/* --- « Factures clients pro » — recette SpeedyTrans 3, point 4 ------------- */

/** `GET /invoices-pro` — les factures des Clients Pro (`isPro`). */
export async function getProInvoicePage(
  query: Query & { status?: string; clientId?: string; agencyId?: string; unpaidOnly?: string },
): Promise<Paginated<InvoiceListItem>> {
  return apiFetch<Paginated<InvoiceListItem>>('/invoices-pro', { query: toSearch(query) });
}

/** `POST /invoices-pro` — facture unitaire d'un Client Pro (422 pour un autre). */
export async function createProInvoice(body: InvoiceCreatePayload): Promise<InvoiceDetail> {
  return apiFetch<InvoiceDetail>('/invoices-pro', { method: 'POST', body });
}

/** `POST /invoices-pro/monthly` — facture du mois d'un Client Pro. */
export async function createProMonthlyInvoice(body: InvoiceMonthlyPayload): Promise<InvoiceDetail> {
  return apiFetch<InvoiceDetail>('/invoices-pro/monthly', { method: 'POST', body });
}

/** `GET /invoices-pro/bulk-candidates` — Clients Pro ayant des colis à facturer. */
export async function getProBulkInvoiceCandidates(): Promise<BulkInvoiceCandidate[]> {
  return apiFetch<BulkInvoiceCandidate[]>('/invoices-pro/bulk-candidates');
}

/** `POST /invoices-pro/bulk` — une facture par Client Pro coché. */
export async function createProBulkInvoices(body: {
  clientIds: string[];
  agencyId?: string;
  note?: string;
}): Promise<BulkInvoiceResult> {
  return apiFetch<BulkInvoiceResult>('/invoices-pro/bulk', { method: 'POST', body });
}

export async function getInvoice(code: string): Promise<InvoiceDetail> {
  return apiFetch<InvoiceDetail>(`/invoices/${encodeURIComponent(code)}`);
}

/** Les colis facturables d'UN e-commerçant : la pile d'entrée de la composition. */
export async function getBillableParcels(query: {
  clientId: string;
  agencyId?: string;
  from?: string;
  to?: string;
  search?: string;
}): Promise<BillableParcelLine[]> {
  return apiFetch<BillableParcelLine[]>('/invoices/billable', { query: toSearch(query) });
}

export async function createInvoice(body: InvoiceCreatePayload): Promise<InvoiceDetail> {
  return apiFetch<InvoiceDetail>('/invoices', { method: 'POST', body });
}

/**
 * « Générer la facture du mois » — C11 bloc 3, D-061.
 *
 * Elle rend un BROUILLON déjà composé : le service a ramassé lui-même les colis
 * facturables de la période. L'émission reste un second geste (D-062).
 */
export async function createMonthlyInvoice(body: InvoiceMonthlyPayload): Promise<InvoiceDetail> {
  return apiFetch<InvoiceDetail>('/invoices/monthly', { method: 'POST', body });
}

export async function addInvoiceParcels(
  code: string,
  body: InvoiceParcelsPayload,
): Promise<InvoiceDetail> {
  return apiFetch<InvoiceDetail>(`/invoices/${encodeURIComponent(code)}/parcels`, {
    method: 'POST',
    body,
  });
}

export async function removeInvoiceParcel(code: string, parcelId: string): Promise<InvoiceDetail> {
  return apiFetch<InvoiceDetail>(`/invoices/${encodeURIComponent(code)}/parcels/${parcelId}`, {
    method: 'DELETE',
  });
}

/** `POST /invoices/:code/adjustments` — une ligne hors colis (D-181). */
export async function addInvoiceAdjustment(
  code: string,
  body: InvoiceAdjustmentPayload,
): Promise<InvoiceDetail> {
  return apiFetch<InvoiceDetail>(`/invoices/${encodeURIComponent(code)}/adjustments`, {
    method: 'POST',
    body,
  });
}

/** `DELETE /invoices/:code/adjustments/:id` — tant que la facture est un brouillon. */
export async function removeInvoiceAdjustment(code: string, id: string): Promise<InvoiceDetail> {
  return apiFetch<InvoiceDetail>(
    `/invoices/${encodeURIComponent(code)}/adjustments/${encodeURIComponent(id)}`,
    { method: 'DELETE' },
  );
}

export async function issueInvoice(
  code: string,
  body: InvoiceIssuePayload,
): Promise<InvoiceDetail> {
  return apiFetch<InvoiceDetail>(`/invoices/${encodeURIComponent(code)}/issue`, {
    method: 'POST',
    body,
  });
}

export async function deleteInvoice(code: string): Promise<{ code: string }> {
  return apiFetch<{ code: string }>(`/invoices/${encodeURIComponent(code)}`, { method: 'DELETE' });
}

/**
 * La facture en PDF, encodée en base64.
 *
 * L'endpoint rend du binaire : il ne passe donc pas par `apiFetch`, qui parse du
 * JSON. Le base64 traverse une Server Action jusqu'au navigateur, qui en fait un
 * Blob — même chemin que le bon de retour.
 */
export async function downloadInvoicePdf(code: string): Promise<string> {
  return apiFetchBase64(`/invoices/${encodeURIComponent(code)}/pdf`);
}

/**
 * `GET /invoices/:code/export` — la facture en Excel : une feuille « Colis »,
 * une feuille « Récapitulatif » (11/11/2026). Clients et clients pro, par le
 * code. Le nombre de lignes voyage en en-tête (`x-export-rows`).
 */
export async function exportInvoice(code: string): Promise<{ file: string; rows: number | null }> {
  let rows: number | null = null;
  const file = await apiFetchBase64(`/invoices/${encodeURIComponent(code)}/export`, {}, (headers) => {
    const value = Number(headers.get('x-export-rows'));
    rows = Number.isFinite(value) && headers.has('x-export-rows') ? value : null;
  });
  return { file, rows };
}

/* --- Règlements ----------------------------------------------------------- */

export async function getPaymentPage(
  query: Query & { method?: string; clientId?: string; agencyId?: string; invoiceId?: string },
): Promise<Paginated<PaymentListItem>> {
  return apiFetch<Paginated<PaymentListItem>>('/payments', { query: toSearch(query) });
}

export async function createPayment(body: PaymentPayload): Promise<PaymentListItem> {
  return apiFetch<PaymentListItem>('/payments', { method: 'POST', body });
}

export async function deletePayment(id: string): Promise<{ id: string }> {
  return apiFetch<{ id: string }>(`/payments/${encodeURIComponent(id)}`, { method: 'DELETE' });
}

/* --- Retours de fonds ------------------------------------------------------ */

export async function getRemittancePage(
  query: Query & {
    status?: string;
    /** Le statut AFFICHÉ (D-243) : `open`, `closed`, `received`, `validated`. */
    stage?: string;
    courierId?: string;
    cityId?: string;
    agencyId?: string;
  },
): Promise<Paginated<RemittanceListItem>> {
  return apiFetch<Paginated<RemittanceListItem>>('/remittances', { query: toSearch(query) });
}

/**
 * Le tableau de bord posé en tête des retours de fonds — point 6.
 *
 * Les cartes suivent la période, la liste des bons ouverts non : le serveur
 * l'a tranché, l'écran ne fait que l'afficher.
 */
export async function getRemittanceSummary(query: {
  period?: string;
  from?: string;
  to?: string;
  courierId?: string;
  agencyId?: string;
}): Promise<RemittanceSummary> {
  return apiFetch<RemittanceSummary>('/remittances/summary', { query: toSearch(query) });
}

export async function getRemittance(code: string): Promise<RemittanceDetail> {
  return apiFetch<RemittanceDetail>(`/remittances/${encodeURIComponent(code)}`);
}

/** Les colis livrés par CE livreur dont le cash n'est pas encore remonté. */
export async function getRemittanceCandidates(query: {
  courierId: string;
  cityId?: string;
  from?: string;
  to?: string;
  search?: string;
}): Promise<RemittanceCandidate[]> {
  return apiFetch<RemittanceCandidate[]>('/remittances/candidates', { query: toSearch(query) });
}

export async function createRemittance(body: RemittanceCreatePayload): Promise<RemittanceDetail> {
  return apiFetch<RemittanceDetail>('/remittances', { method: 'POST', body });
}

export async function addRemittanceParcels(
  code: string,
  body: RemittanceParcelsPayload,
): Promise<RemittanceDetail> {
  return apiFetch<RemittanceDetail>(`/remittances/${encodeURIComponent(code)}/parcels`, {
    method: 'POST',
    body,
  });
}

export async function removeRemittanceParcel(
  code: string,
  parcelId: string,
): Promise<RemittanceDetail> {
  return apiFetch<RemittanceDetail>(
    `/remittances/${encodeURIComponent(code)}/parcels/${parcelId}`,
    {
      method: 'DELETE',
    },
  );
}

/** Le comptage — C11 bloc 2. Le seul montant saisi du module. */
export async function countRemittance(
  code: string,
  body: RemittanceCountPayload,
): Promise<RemittanceDetail> {
  return apiFetch<RemittanceDetail>(`/remittances/${encodeURIComponent(code)}/count`, {
    method: 'PATCH',
    body,
  });
}

/** Le reçu du livreur — détail colis par colis, commission comprise (C11 bloc 2). */
export async function downloadRemittancePdf(code: string): Promise<string> {
  return apiFetchBase64(`/remittances/${encodeURIComponent(code)}/pdf`);
}

export async function confirmRemittance(code: string): Promise<RemittanceDetail> {
  return apiFetch<RemittanceDetail>(`/remittances/${encodeURIComponent(code)}/confirm`, {
    method: 'POST',
  });
}

/**
 * `POST /remittances/:code/reopen` — l'agence rouvre une caisse FERMÉE par le
 * livreur (D-243). Refus 409 `courier-has-newer-remittance` quand il en a déjà
 * ouvert une autre : l'agence corrige alors les lignes de la caisse fermée.
 */
export async function reopenRemittance(code: string): Promise<RemittanceDetail> {
  return apiFetch<RemittanceDetail>(`/remittances/${encodeURIComponent(code)}/reopen`, {
    method: 'POST',
  });
}

export async function deleteRemittance(code: string): Promise<{ code: string }> {
  return apiFetch<{ code: string }>(`/remittances/${encodeURIComponent(code)}`, {
    method: 'DELETE',
  });
}

/* --- Bons de versement (client PRO) — C11 bloc 3 ---------------------------- */

export async function getCodPayoutPage(
  query: Query & { status?: string; clientId?: string; agencyId?: string },
): Promise<Paginated<CodPayoutListItem>> {
  return apiFetch<Paginated<CodPayoutListItem>>('/cod-payouts', { query: toSearch(query) });
}

export async function getCodPayout(code: string): Promise<CodPayoutDetail> {
  return apiFetch<CodPayoutDetail>(`/cod-payouts/${encodeURIComponent(code)}`);
}

/** Colis livrés ET remboursés par le livreur, dont le COD n'est pas encore versé. */
export async function getCodPayoutCandidates(query: {
  clientId: string;
  from?: string;
  to?: string;
  search?: string;
}): Promise<CodPayoutCandidate[]> {
  return apiFetch<CodPayoutCandidate[]>('/cod-payouts/candidates', { query: toSearch(query) });
}

/** Lot U (D-233) — le récapitulatif « En masse » : un client et une agence par ligne. */
export async function getCodPayoutBulkCandidates(query: {
  from?: string;
  to?: string;
  agencyId?: string;
}): Promise<CodPayoutBulkCandidate[]> {
  return apiFetch<CodPayoutBulkCandidate[]>('/cod-payouts/bulk-candidates', {
    query: toSearch(query),
  });
}

/** Lot U (D-233) — un BV en brouillon par client et par agence. */
export async function createCodPayoutsBulk(body: CodPayoutBulkInput): Promise<CodPayoutBulkResult> {
  return apiFetch<CodPayoutBulkResult>('/cod-payouts/bulk', { method: 'POST', body });
}

export async function createCodPayout(body: CodPayoutCreatePayload): Promise<CodPayoutDetail> {
  return apiFetch<CodPayoutDetail>('/cod-payouts', { method: 'POST', body });
}

export async function addCodPayoutParcels(
  code: string,
  body: CodPayoutParcelsPayload,
): Promise<CodPayoutDetail> {
  return apiFetch<CodPayoutDetail>(`/cod-payouts/${encodeURIComponent(code)}/parcels`, {
    method: 'POST',
    body,
  });
}

export async function removeCodPayoutParcel(
  code: string,
  parcelId: string,
): Promise<CodPayoutDetail> {
  return apiFetch<CodPayoutDetail>(`/cod-payouts/${encodeURIComponent(code)}/parcels/${parcelId}`, {
    method: 'DELETE',
  });
}

/** Le geste qui fige : montants gelés, colis marqués versés. */
export async function payCodPayout(
  code: string,
  body: CodPayoutPayPayload,
): Promise<CodPayoutDetail> {
  return apiFetch<CodPayoutDetail>(`/cod-payouts/${encodeURIComponent(code)}/pay`, {
    method: 'POST',
    body,
  });
}

export async function downloadCodPayoutPdf(code: string): Promise<string> {
  return apiFetchBase64(`/cod-payouts/${encodeURIComponent(code)}/pdf`);
}

export async function deleteCodPayout(code: string): Promise<{ code: string }> {
  return apiFetch<{ code: string }>(`/cod-payouts/${encodeURIComponent(code)}`, {
    method: 'DELETE',
  });
}

/* --- Relevés de rémunération livreur ---------------------------------------- */

export async function getCourierStatementPage(
  query: Query & { status?: string; courierId?: string; agencyId?: string },
): Promise<Paginated<CourierStatementListItem>> {
  return apiFetch<Paginated<CourierStatementListItem>>('/courier-statements', {
    query: toSearch(query),
  });
}

export async function getCourierStatement(code: string): Promise<CourierStatementDetail> {
  return apiFetch<CourierStatementDetail>(`/courier-statements/${encodeURIComponent(code)}`);
}

export async function createCourierStatement(
  body: CourierStatementPayload,
): Promise<CourierStatementDetail> {
  return apiFetch<CourierStatementDetail>('/courier-statements', { method: 'POST', body });
}

export async function recomputeCourierStatement(code: string): Promise<CourierStatementDetail> {
  return apiFetch<CourierStatementDetail>(
    `/courier-statements/${encodeURIComponent(code)}/recompute`,
    { method: 'POST' },
  );
}

export async function validateCourierStatement(code: string): Promise<CourierStatementDetail> {
  return apiFetch<CourierStatementDetail>(
    `/courier-statements/${encodeURIComponent(code)}/validate`,
    { method: 'POST' },
  );
}

/**
 * Règle le relevé — C11 bloc 7, D-067.
 *
 * Le corps porte le MODE et, s'il y en a un, le chèque. Aucun montant : c'est
 * le relevé qui dit ce qui est dû (D-065).
 */
export async function payCourierStatement(
  code: string,
  body: StatementPayPayload,
): Promise<CourierStatementDetail> {
  return apiFetch<CourierStatementDetail>(`/courier-statements/${encodeURIComponent(code)}/pay`, {
    method: 'POST',
    body,
  });
}

/**
 * La photo du chèque, en base64 — M1, ce qui manquait à C11 bloc 7.
 *
 * L'endpoint rend du binaire : il ne passe donc pas par `apiFetch`, qui parse
 * du JSON. Le base64 traverse une Server Action jusqu'au navigateur, qui en
 * fait un Blob — même chemin que les PDF.
 */
export async function downloadChequePhoto(code: string): Promise<string> {
  return apiFetchBase64(`/courier-statements/${encodeURIComponent(code)}/cheque-photo`);
}

/** La photo du chèque, prise par le LIVREUR depuis son téléphone (D-068). */
export async function attachChequePhoto(
  code: string,
  body: ChequePhotoPayload,
): Promise<CourierStatementDetail> {
  return apiFetch<CourierStatementDetail>(
    `/courier-statements/${encodeURIComponent(code)}/cheque-photo`,
    { method: 'POST', body },
  );
}

export async function deleteCourierStatement(code: string): Promise<{ code: string }> {
  return apiFetch<{ code: string }>(`/courier-statements/${encodeURIComponent(code)}`, {
    method: 'DELETE',
  });
}

/** Le relevé en PDF, encodé en base64 — même chemin que la facture. */
export async function downloadCourierStatementPdf(code: string): Promise<string> {
  return apiFetchBase64(`/courier-statements/${encodeURIComponent(code)}/pdf`);
}

/* --- Référentiel bancaire --------------------------------------------------- */

export async function getBanks(): Promise<Bank[]> {
  return apiFetch<Bank[]>('/banks');
}

export async function getBankAccounts(): Promise<BankAccount[]> {
  return apiFetch<BankAccount[]>('/bank-accounts');
}

/** Le solde d'un livreur et son grand livre — point 4. */
export async function getCourierBalance(courierId: string): Promise<CourierBalance> {
  return apiFetch<CourierBalance>(`/couriers/${encodeURIComponent(courierId)}/balance`);
}

export async function settleCourierBalance(
  courierId: string,
  body: { amount: number; method: 'cash' | 'transfer' | 'check'; note?: string },
): Promise<CourierBalance> {
  return apiFetch<CourierBalance>(
    `/couriers/${encodeURIComponent(courierId)}/balance/settlements`,
    { method: 'POST', body },
  );
}

/* --- Factures clients en masse — chantier « Finance », point 5 -------------- */

export async function getBulkInvoiceCandidates(): Promise<BulkInvoiceCandidate[]> {
  return apiFetch<BulkInvoiceCandidate[]>('/invoices/bulk-candidates');
}

export async function createBulkInvoices(body: {
  clientIds: string[];
  agencyId?: string;
  note?: string;
}): Promise<BulkInvoiceResult> {
  return apiFetch<BulkInvoiceResult>('/invoices/bulk', { method: 'POST', body });
}

/* --- Ordre de virement groupé — chantier « Caisse, OV, Petite caisse » ------ */

export async function createTransferOrder(
  body: TransferOrderCreateInput,
): Promise<TransferOrderDetail> {
  return apiFetch<TransferOrderDetail>('/transfer-orders', { method: 'POST', body });
}

/** Les ordres déjà générés, le plus récent d'abord — consultation seule. */
export async function getTransferOrderPage(
  query: Query,
): Promise<Paginated<TransferOrderListItem>> {
  return apiFetch<Paginated<TransferOrderListItem>>('/transfer-orders', {
    query: toSearch(query),
  });
}

export async function downloadTransferOrderPdf(code: string): Promise<string> {
  return apiFetchBase64(`/transfer-orders/${encodeURIComponent(code)}/pdf`);
}

export async function downloadTransferOrderXlsx(code: string): Promise<string> {
  return apiFetchBase64(`/transfer-orders/${encodeURIComponent(code)}/xlsx`);
}

/* --- Chèques reçus des destinataires — chantier « Finance », point 9 -------- */

export interface ChequeListQuery {
  page?: number;
  pageSize?: number;
  status?: string;
  courierId?: string;
  bank?: string;
  from?: string;
  to?: string;
  search?: string;
  /** Le vendeur du colis — chantier « Chèques », point 2. */
  clientId?: string;
}

/** `status: CHEQUE_PENDING_FILTER` : les quatre marches avant la réception (lot S). */
export async function getCheques(query: ChequeListQuery = {}): Promise<ChequePage> {
  return apiFetch<ChequePage>('/cheques', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      status: query.status,
      courierId: query.courierId,
      /*
       * `clientId` était DANS le type et pas dans la requête — trouvé à l'audit
       * du chantier « Correctifs avant démo », point 6. La page le lisait dans
       * l'URL, `ChequeListQuery` le déclarait, le service API le savait filtrer :
       * seule cette ligne manquait, et le filtre « vendeur » du chantier
       * « Chèques » point 2 n'a donc jamais rien filtré.
       */
      clientId: query.clientId,
      bank: query.bank,
      from: query.from,
      to: query.to,
      search: query.search,
    },
  });
}

/**
 * LE DÉTAIL D'UN CHÈQUE, avec son JOURNAL — chantier « Correctifs avant
 * démo », point 6.
 *
 * La route existait côté API depuis le chantier « Finance » point 9 et n'était
 * appelée par aucun écran : la liste ne montre pas l'historique daté que la
 * mission demande, et un vendeur qui réclame son argent a besoin de lire QUAND
 * son chèque a changé de main.
 */
export async function getCheque(id: string): Promise<ChequeDetail> {
  return apiFetch<ChequeDetail>(`/cheques/${encodeURIComponent(id)}`);
}

/**
 * Le bon de remise du chèque en PDF (base64) — D-265. Un POST : l'API inscrit
 * chaque téléchargement dans l'historique du chèque.
 */
export async function downloadChequeSlip(id: string): Promise<string> {
  return apiFetchBase64(`/cheques/${encodeURIComponent(id)}/slip`, { method: 'POST' });
}

/**
 * `POST /cheques/slip` — le bon de remise GROUPÉ : un bon par vendeur de la
 * sélection, dans un seul PDF, inscrit dans l'historique de chaque chèque.
 */
export async function downloadChequeGroupSlip(
  ids: string[],
): Promise<{ file: string; filename: string }> {
  // Le numéro BRC-AAMM-NNNN est tiré par le serveur : le nom du fichier aussi.
  let filename = 'BRC.pdf';
  const file = await apiFetchBase64('/cheques/slip', { method: 'POST', body: { ids } }, (headers) => {
    const match = /filename="([^"]+)"/.exec(headers.get('content-disposition') ?? '');
    if (match?.[1]) filename = match[1];
  });

  return { file, filename };
}

/** Le même geste sur une PILE de chèques — chantier « Chèques », point 2. */
export async function bulkAdvanceCheques(body: {
  ids: string[];
  status: ChequeStatus;
  comment?: string;
}): Promise<ChequeBulkResult> {
  return apiFetch<ChequeBulkResult>('/cheques/bulk-advance', { method: 'POST', body });
}

/**
 * Les trois gestes d'un chèque. Aucune création : il naît à l'encaissement —
 * un chèque saisi deux fois est un chèque encaissé deux fois.
 */
export async function advanceCheque(
  id: string,
  step: 'advance' | 'reject' | 'cancel',
  body: { comment?: string },
): Promise<ChequeDetail> {
  return apiFetch<ChequeDetail>(`/cheques/${encodeURIComponent(id)}/${step}`, {
    method: 'POST',
    body,
  });
}
