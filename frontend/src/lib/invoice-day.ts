import { INVOICE_DRAFT_DAY, type InvoiceListItem } from '@delivery/shared';
import { localToday } from '@/lib/local-day';

/**
 * Le groupe d'une facture dans la liste groupée (D-254) : son jour d'émission,
 * au fuseau de la société, ou les brouillons. La page serveur demande ses
 * en-têtes avec la MÊME clé que l'écran range ses lignes.
 *
 * Sans API serveur : sert à la page (serveur) comme à l'écran (client).
 */
export function invoiceDayOf(
  invoice: Pick<InvoiceListItem, 'issuedAt'>,
  timeZone: string | undefined,
): string {
  return invoice.issuedAt ? localToday(timeZone, new Date(invoice.issuedAt)) : INVOICE_DRAFT_DAY;
}

/** Les jours d'une page, dans l'ordre, sans doublon — la requête des en-têtes. */
export function invoiceDaysOf(
  invoices: ReadonlyArray<Pick<InvoiceListItem, 'issuedAt'>>,
  timeZone: string | undefined,
): string[] {
  return [...new Set(invoices.map((invoice) => invoiceDayOf(invoice, timeZone)))];
}
