'use server';

import {
  type BulkInvoiceCandidate,
  type BulkInvoiceResult,
  type InvoiceClientKind,
  type TransferOrderCreateInput,
  type TransferOrderDetail,
  type ChequeBulkResult,
  type ChequeDetail,
  type ChequeStatus,
  type CourierBalance,
  type CodPayoutCreatePayload,
  codPayoutCreateSchema,
  type CodPayoutBulkCandidate,
  type CodPayoutBulkResult,
  codPayoutBulkSchema,
  type CodPayoutDetail,
  type CodPayoutParcelsPayload,
  codPayoutParcelsSchema,
  type CodPayoutPayPayload,
  codPayoutPaySchema,
  type CourierStatementDetail,
  type CourierStatementPayload,
  courierStatementSchema,
  type ChequePhotoPayload,
  chequePhotoSchema,
  type StatementPayPayload,
  statementPaySchema,
  type InvoiceCreatePayload,
  invoiceCreateSchema,
  type InvoiceDetail,
  type InvoiceIssuePayload,
  invoiceIssueSchema,
  type InvoiceMonthlyPayload,
  invoiceMonthlySchema,
  type InvoiceParcelsPayload,
  invoiceAdjustmentSchema,
  invoiceParcelsSchema,
  type PaymentListItem,
  type PaymentPayload,
  paymentSchema,
  type RemittanceCandidate,
  type RemittanceCountPayload,
  remittanceCountSchema,
  type RemittanceCreatePayload,
  remittanceCreateSchema,
  type RemittanceDetail,
  type RemittanceParcelsPayload,
  remittanceParcelsSchema,
} from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import {
  getRemittanceCandidates,
  addCodPayoutParcels,
  addInvoiceParcels,
  createCodPayout,
  createCodPayoutsBulk,
  getCodPayoutBulkCandidates,
  deleteCodPayout,
  downloadCodPayoutPdf,
  payCodPayout,
  removeCodPayoutParcel,
  createCourierStatement,
  deleteCourierStatement,
  downloadCourierStatementPdf,
  getCourierStatement,
  downloadInvoicePdf,
  downloadInvoiceDayPdf,
  exportInvoice,
  downloadRemittancePdf,
  attachChequePhoto,
  downloadChequePhoto,
  downloadChequeSlip,
  downloadChequeGroupSlip,
  payCourierStatement,
  recomputeCourierStatement,
  validateCourierStatement,
  addRemittanceParcels,
  confirmRemittance,
  countRemittance,
  createInvoice,
  createMonthlyInvoice,
  createProBulkInvoices,
  createProInvoice,
  createProMonthlyInvoice,
  getProBulkInvoiceCandidates,
  createPayment,
  createRemittance,
  deleteInvoice,
  deletePayment,
  deleteRemittance,
  issueInvoice,
  removeInvoiceParcel,
  addInvoiceAdjustment,
  removeInvoiceAdjustment,
  advanceCheque,
  bulkAdvanceCheques,
  createBulkInvoices,
  getBulkInvoiceCandidates,
  createTransferOrder,
  downloadTransferOrderPdf,
  downloadTransferOrderXlsx,
  removeRemittanceParcel,
  reopenRemittance,
  settleCourierBalance,
} from '@/lib/api/finance';

/**
 * Écritures du menu FACTURATION.
 *
 * Même forme qu'ailleurs : la Server Action revalide la saisie, appelle
 * l'endpoint, et laisse remonter le motif d'un 409. Le motif compte
 * particulièrement ici — « ce colis est déjà sur une facture émise » se corrige
 * en le retirant du lot, « refusé » ne dit pas quoi faire.
 *
 * Une émission, un règlement et une confirmation de recette changent DEUX
 * écrans : le document et sa liste. Les deux sont revalidés, sinon la liste
 * continue d'afficher un brouillon qui n'existe plus.
 */

function revalidateFinance(route: string): void {
  revalidatePath(`/[locale]/(app)/${route}`, 'page');
}

/* --- Factures ------------------------------------------------------------- */

/**
 * Le TYPE de la page d'où part le geste — recette SpeedyTrans 3, point 4.
 * « normal » : `/invoices` ; « pro » : `/invoices-pro`. L'API refuse (422) un
 * client de l'autre type ; le paramètre ne choisit que la porte d'entrée.
 */
function invoicesRoute(kind: InvoiceClientKind): string {
  return kind === 'pro' ? 'invoices-pro' : 'invoices';
}

export async function createInvoiceAction(
  input: unknown,
  kind: InvoiceClientKind = 'normal',
): Promise<ReferentialResult<InvoiceDetail>> {
  const parsed = invoiceCreateSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const payload = parsed.data as InvoiceCreatePayload;
    const invoice = kind === 'pro' ? await createProInvoice(payload) : await createInvoice(payload);
    revalidateFinance(invoicesRoute(kind));
    return { ok: true, data: invoice };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * « Générer la facture du mois » — C11 bloc 3, D-061.
 *
 * Un seul appel pour un brouillon déjà composé. Le 409 remonte tel quel : « pas
 * un client professionnel » et « aucun colis facturable sur cette période » sont
 * deux refus que l'écran doit savoir distinguer.
 */
export async function createMonthlyInvoiceAction(
  input: unknown,
  kind: InvoiceClientKind = 'normal',
): Promise<ReferentialResult<InvoiceDetail>> {
  const parsed = invoiceMonthlySchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const payload = parsed.data as InvoiceMonthlyPayload;
    const invoice =
      kind === 'pro' ? await createProMonthlyInvoice(payload) : await createMonthlyInvoice(payload);
    revalidateFinance(invoicesRoute(kind));
    return { ok: true, data: invoice };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Ajout de colis par LOT.
 *
 * La facture entière revient : l'écran de composition se redessine sans
 * relecture, ce qui compte quand on vient d'ajouter cent lignes.
 */
export async function addInvoiceParcelsAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<InvoiceDetail>> {
  const parsed = invoiceParcelsSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const invoice = await addInvoiceParcels(code, parsed.data as InvoiceParcelsPayload);
    revalidateFinance('invoices/[code]');
    return { ok: true, data: invoice };
  } catch (error) {
    return toFailure(error);
  }
}

export async function removeInvoiceParcelAction(
  code: string,
  parcelId: string,
): Promise<ReferentialResult<InvoiceDetail>> {
  try {
    const invoice = await removeInvoiceParcel(code, parcelId);
    revalidateFinance('invoices/[code]');
    return { ok: true, data: invoice };
  } catch (error) {
    return toFailure(error);
  }
}

/** Une ligne hors colis sur un brouillon — lot Argent 2, A2-1 (D-181). */
export async function addInvoiceAdjustmentAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<InvoiceDetail>> {
  const parsed = invoiceAdjustmentSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const invoice = await addInvoiceAdjustment(code, parsed.data);
    revalidateFinance('invoices/[code]');
    revalidateFinance('invoices');
    return { ok: true, data: invoice };
  } catch (error) {
    return toFailure(error);
  }
}

export async function removeInvoiceAdjustmentAction(
  code: string,
  id: string,
): Promise<ReferentialResult<InvoiceDetail>> {
  try {
    const invoice = await removeInvoiceAdjustment(code, id);
    revalidateFinance('invoices/[code]');
    revalidateFinance('invoices');
    return { ok: true, data: invoice };
  } catch (error) {
    return toFailure(error);
  }
}

export async function issueInvoiceAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<InvoiceDetail>> {
  const parsed = invoiceIssueSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const invoice = await issueInvoice(code, parsed.data as InvoiceIssuePayload);
    // L'émission marque les colis facturés : la pile des facturables change
    // aussi, et c'est elle qu'on regarde pour composer la facture suivante.
    revalidateFinance('invoices/[code]');
    revalidateFinance('invoices');
    return { ok: true, data: invoice };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteInvoiceAction(code: string): Promise<ReferentialResult> {
  try {
    await deleteInvoice(code);
    revalidateFinance('invoices');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/** Le PDF revient en base64 ; le fichier est fabriqué par le navigateur. */
export async function downloadInvoicePdfAction(code: string): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await downloadInvoicePdf(code) };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * « Imprimer » sur l'en-tête d'un jour de la liste des factures (D-254) : les
 * factures du jour, aux filtres de la liste, en un PDF base64.
 */
export async function downloadInvoiceDayPdfAction(
  kind: InvoiceClientKind,
  query: Record<string, string> & { day: string },
): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await downloadInvoiceDayPdf(kind, query) };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * L'export Excel d'une facture (11/11/2026) : le classeur revient en base64,
 * comme le PDF. La portée est celle de l'API — un vendeur n'exporte que les
 * siennes.
 */
export async function exportInvoiceAction(
  code: string,
): Promise<ReferentialResult<{ file: string; rows: number | null }>> {
  try {
    return { ok: true, data: await exportInvoice(code) };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Règlements ----------------------------------------------------------- */

export async function createPaymentAction(
  input: unknown,
): Promise<ReferentialResult<PaymentListItem>> {
  const parsed = paymentSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const payment = await createPayment(parsed.data as PaymentPayload);
    // Un règlement peut solder la facture : les deux écrans bougent.
    revalidateFinance('payments');
    revalidateFinance('invoices');
    revalidateFinance('invoices/[code]');
    return { ok: true, data: payment };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deletePaymentAction(id: string): Promise<ReferentialResult> {
  try {
    await deletePayment(id);
    revalidateFinance('payments');
    revalidateFinance('invoices');
    revalidateFinance('invoices/[code]');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Relevés de rémunération livreur ---------------------------------------- */

/**
 * Les cinq gestes du relevé revalident la FICHE LIVREUR : c'est là qu'ils se
 * font, l'onglet « Relevés » n'a pas d'écran à lui.
 */
function revalidateCourier(): void {
  revalidatePath('/[locale]/(app)/couriers/[id]', 'page');
}

export async function createCourierStatementAction(
  input: unknown,
): Promise<ReferentialResult<CourierStatementDetail>> {
  const parsed = courierStatementSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const statement = await createCourierStatement(parsed.data as CourierStatementPayload);
    revalidateCourier();
    return { ok: true, data: statement };
  } catch (error) {
    return toFailure(error);
  }
}

/** Lecture seule : ouvrir un relevé ne le modifie pas. */
export async function readCourierStatementAction(
  code: string,
): Promise<ReferentialResult<CourierStatementDetail>> {
  try {
    return { ok: true, data: await getCourierStatement(code) };
  } catch (error) {
    return toFailure(error);
  }
}

export async function recomputeCourierStatementAction(
  code: string,
): Promise<ReferentialResult<CourierStatementDetail>> {
  try {
    const statement = await recomputeCourierStatement(code);
    revalidateCourier();
    return { ok: true, data: statement };
  } catch (error) {
    return toFailure(error);
  }
}

export async function validateCourierStatementAction(
  code: string,
): Promise<ReferentialResult<CourierStatementDetail>> {
  try {
    const statement = await validateCourierStatement(code);
    revalidateCourier();
    return { ok: true, data: statement };
  } catch (error) {
    return toFailure(error);
  }
}

export async function payCourierStatementAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<CourierStatementDetail>> {
  const parsed = statementPaySchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const statement = await payCourierStatement(code, parsed.data as StatementPayPayload);
    revalidateCourier();
    return { ok: true, data: statement };
  } catch (error) {
    return toFailure(error);
  }
}

/** Le bon de remise d'un chèque — D-265. Inscrit dans l'historique par l'API. */
export async function downloadChequeSlipAction(id: string): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await downloadChequeSlip(id) };
  } catch (error) {
    return toFailure(error);
  }
}

/** Le bon de remise GROUPÉ par vendeur. Inscrit sur chaque chèque par l'API. */
export async function downloadChequeGroupSlipAction(
  ids: string[],
): Promise<ReferentialResult<{ file: string; filename: string }>> {
  try {
    return { ok: true, data: await downloadChequeGroupSlip(ids) };
  } catch (error) {
    return toFailure(error);
  }
}

/** La photo du chèque, pour la REGARDER — M1. Base64, comme les PDF. */
export async function chequePhotoAction(code: string): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await downloadChequePhoto(code) };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * La photo du chèque, attachée par le LIVREUR — C11 bloc 7, D-068.
 *
 * Elle revalide l'espace livreur ET le back-office : l'agent qui a réglé le
 * relevé doit voir la preuve arriver sans recharger sa page à la main.
 */
export async function attachChequePhotoAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<CourierStatementDetail>> {
  const parsed = chequePhotoSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const statement = await attachChequePhoto(code, parsed.data as ChequePhotoPayload);
    revalidateCourier();
    revalidatePath('/[locale]/(courier)/me/statements', 'page');
    return { ok: true, data: statement };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteCourierStatementAction(code: string): Promise<ReferentialResult> {
  try {
    await deleteCourierStatement(code);
    revalidateCourier();
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

export async function downloadCourierStatementPdfAction(
  code: string,
): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await downloadCourierStatementPdf(code) };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Bons de versement (client PRO) — C11 bloc 3 ---------------------------- */

/** Lot U (D-233) — le récapitulatif « En masse ». */
export async function codPayoutBulkCandidatesAction(query: {
  from?: string;
  to?: string;
  agencyId?: string;
}): Promise<ReferentialResult<CodPayoutBulkCandidate[]>> {
  try {
    return { ok: true, data: await getCodPayoutBulkCandidates(query) };
  } catch (error) {
    return toFailure(error);
  }
}

/** Lot U (D-233) — un BV en brouillon par client et par agence ; refus rendus. */
export async function createCodPayoutsBulkAction(
  input: unknown,
): Promise<ReferentialResult<CodPayoutBulkResult>> {
  const parsed = codPayoutBulkSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const result = await createCodPayoutsBulk(parsed.data);
    revalidateFinance('cod-payouts');
    return { ok: true, data: result };
  } catch (error) {
    return toFailure(error);
  }
}

export async function createCodPayoutAction(
  input: unknown,
): Promise<ReferentialResult<CodPayoutDetail>> {
  const parsed = codPayoutCreateSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const payout = await createCodPayout(parsed.data as CodPayoutCreatePayload);
    revalidateFinance('cod-payouts');
    return { ok: true, data: payout };
  } catch (error) {
    return toFailure(error);
  }
}

export async function addCodPayoutParcelsAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<CodPayoutDetail>> {
  const parsed = codPayoutParcelsSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const payout = await addCodPayoutParcels(code, parsed.data as CodPayoutParcelsPayload);
    revalidateFinance('cod-payouts/[code]');
    return { ok: true, data: payout };
  } catch (error) {
    return toFailure(error);
  }
}

export async function removeCodPayoutParcelAction(
  code: string,
  parcelId: string,
): Promise<ReferentialResult<CodPayoutDetail>> {
  try {
    const payout = await removeCodPayoutParcel(code, parcelId);
    revalidateFinance('cod-payouts/[code]');
    return { ok: true, data: payout };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Le VERSEMENT — le geste qui fait partir l'argent.
 *
 * Il change deux choses en base : le bon se fige, et ses colis sont marqués
 * versés. Les deux listes se revalident donc, sans quoi la pile de candidats
 * continuerait de proposer des colis déjà payés.
 */
export async function payCodPayoutAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<CodPayoutDetail>> {
  const parsed = codPayoutPaySchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const payout = await payCodPayout(code, parsed.data as CodPayoutPayPayload);
    revalidateFinance('cod-payouts/[code]');
    revalidateFinance('cod-payouts');
    return { ok: true, data: payout };
  } catch (error) {
    return toFailure(error);
  }
}

export async function downloadCodPayoutPdfAction(code: string): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await downloadCodPayoutPdf(code) };
  } catch (error) {
    return toFailure(error);
  }
}

export async function deleteCodPayoutAction(code: string): Promise<ReferentialResult> {
  try {
    await deleteCodPayout(code);
    revalidateFinance('cod-payouts');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Retours de fonds ------------------------------------------------------ */

export async function createRemittanceAction(
  input: unknown,
): Promise<ReferentialResult<RemittanceDetail>> {
  const parsed = remittanceCreateSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const remittance = await createRemittance(parsed.data as RemittanceCreatePayload);
    revalidateFinance('remittances');
    return { ok: true, data: remittance };
  } catch (error) {
    return toFailure(error);
  }
}

export async function addRemittanceParcelsAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<RemittanceDetail>> {
  const parsed = remittanceParcelsSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const remittance = await addRemittanceParcels(code, parsed.data as RemittanceParcelsPayload);
    revalidateFinance('remittances/[code]');
    return { ok: true, data: remittance };
  } catch (error) {
    return toFailure(error);
  }
}

export async function removeRemittanceParcelAction(
  code: string,
  parcelId: string,
): Promise<ReferentialResult<RemittanceDetail>> {
  try {
    const remittance = await removeRemittanceParcel(code, parcelId);
    revalidateFinance('remittances/[code]');
    return { ok: true, data: remittance };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * Le comptage de la caisse — C11 bloc 2.
 *
 * Le schéma partagé porte la règle « un écart exige un motif » : le formulaire
 * la fait respecter avant l'envoi, l'API la revérifie derrière, et les deux
 * lisent la MÊME définition. Deux copies auraient divergé au premier ajustement.
 */
export async function countRemittanceAction(
  code: string,
  input: unknown,
): Promise<ReferentialResult<RemittanceDetail>> {
  const parsed = remittanceCountSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const remittance = await countRemittance(code, parsed.data as RemittanceCountPayload);
    revalidateFinance('remittances/[code]');
    revalidateFinance('remittances');
    return { ok: true, data: remittance };
  } catch (error) {
    return toFailure(error);
  }
}

export async function downloadRemittancePdfAction(
  code: string,
): Promise<ReferentialResult<string>> {
  try {
    return { ok: true, data: await downloadRemittancePdf(code) };
  } catch (error) {
    return toFailure(error);
  }
}

export async function confirmRemittanceAction(
  code: string,
): Promise<ReferentialResult<RemittanceDetail>> {
  try {
    const remittance = await confirmRemittance(code);
    revalidateFinance('remittances/[code]');
    revalidateFinance('remittances');
    return { ok: true, data: remittance };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * « Rouvrir » une caisse fermée par le livreur — D-243.
 *
 * Le 409 remonte avec son motif : `courier-has-newer-remittance` dit à l'agence
 * de corriger les lignes sur place, ce qu'un « refusé » ne dirait pas.
 */
export async function reopenRemittanceAction(
  code: string,
): Promise<ReferentialResult<RemittanceDetail>> {
  try {
    const remittance = await reopenRemittance(code);
    revalidateFinance('remittances/[code]');
    revalidateFinance('remittances');
    return { ok: true, data: remittance };
  } catch (error) {
    return toFailure(error, 'remittances.reopen');
  }
}

export async function deleteRemittanceAction(code: string): Promise<ReferentialResult> {
  try {
    await deleteRemittance(code);
    revalidateFinance('remittances');
    return { ok: true, data: undefined };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Caisses journalières — chantier « Finance », points 3 et 4 ------------- */

/**
 * Le geste en masse — point 2.
 *
 * Rend le DÉTAIL de ce qui est passé et de ce qui ne l'est pas : l'écran doit
 * pouvoir nommer les chèques restés en arrière, sinon ils n'y retourneront
 * jamais.
 */
export async function bulkAdvanceChequesAction(input: {
  ids: string[];
  status: ChequeStatus;
  comment?: string;
}): Promise<ReferentialResult<ChequeBulkResult>> {
  try {
    const data = await bulkAdvanceCheques(input);
    revalidateFinance('cheques');
    return { ok: true, data };
  } catch (error) {
    return toFailure(error, 'bulkAdvanceCheques');
  }
}

/** Le règlement d'un écart — point 4. Le solde baisse d'autant. */
export async function settleCourierBalanceAction(
  courierId: string,
  input: { amount: number; method: 'cash' | 'transfer' | 'check'; note?: string },
): Promise<ReferentialResult<CourierBalance>> {
  try {
    const balance = await settleCourierBalance(courierId, input);
    revalidateFinance('couriers/[id]');
    return { ok: true, data: balance };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Factures clients en masse — point 5 ----------------------------------- */

export async function bulkInvoiceCandidatesAction(
  kind: InvoiceClientKind = 'normal',
): Promise<ReferentialResult<BulkInvoiceCandidate[]>> {
  try {
    return {
      ok: true,
      data: kind === 'pro' ? await getProBulkInvoiceCandidates() : await getBulkInvoiceCandidates(),
    };
  } catch (error) {
    return toFailure(error);
  }
}

export async function createBulkInvoicesAction(
  input: {
    clientIds: string[];
    agencyId?: string;
    note?: string;
  },
  kind: InvoiceClientKind = 'normal',
): Promise<ReferentialResult<BulkInvoiceResult>> {
  try {
    const result =
      kind === 'pro' ? await createProBulkInvoices(input) : await createBulkInvoices(input);
    revalidateFinance(invoicesRoute(kind));
    return { ok: true, data: result };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Ordre de virement groupé ---------------------------------------------- */

/**
 * Génère l'ordre de virement. Aucune facture n'est créée ni modifiée : rien à
 * revalider.
 */
export async function createTransferOrderAction(
  input: TransferOrderCreateInput,
): Promise<ReferentialResult<TransferOrderDetail>> {
  try {
    return { ok: true, data: await createTransferOrder(input) };
  } catch (error) {
    return toFailure(error);
  }
}

/** Le même ordre, en PDF ou en Excel — base64, le fichier est fait par le navigateur. */
export async function downloadTransferOrderAction(
  code: string,
  format: 'pdf' | 'xlsx',
): Promise<ReferentialResult<string>> {
  try {
    const data =
      format === 'pdf'
        ? await downloadTransferOrderPdf(code)
        : await downloadTransferOrderXlsx(code);
    return { ok: true, data };
  } catch (error) {
    return toFailure(error);
  }
}

/* --- Chèques reçus — point 9 ------------------------------------------------ */

export async function advanceChequeAction(
  id: string,
  step: 'advance' | 'reject' | 'cancel',
  comment: string,
): Promise<ReferentialResult<ChequeDetail>> {
  try {
    const cheque = await advanceCheque(id, step, { comment });
    revalidateFinance('cheques');
    return { ok: true, data: cheque };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * LES COLIS REMETTABLES D'UN LIVREUR — chantier « Retour de fonds unique ».
 *
 * Le formulaire de création en a besoin : depuis le point 5, **un bon ne
 * s'ouvre pas vide**. Un bon ouvert et vide occupe la place du seul bon ouvert
 * que le livreur peut avoir, et empêche l'ouverture automatique de fonctionner
 * pour lui — c'est ainsi que `RF-2609-0015` a vécu des semaines en production.
 */
export async function remittanceCandidatesAction(
  courierId: string,
): Promise<ReferentialResult<RemittanceCandidate[]>> {
  if (!courierId) return { ok: true, data: [] };

  try {
    return { ok: true, data: await getRemittanceCandidates({ courierId }) };
  } catch (error) {
    return toFailure(error, 'remittances.candidates');
  }
}
