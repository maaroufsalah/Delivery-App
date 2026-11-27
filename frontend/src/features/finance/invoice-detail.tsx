'use client';

import type { BankAccount, BillableParcelLine, InvoiceDetail as Invoice } from '@delivery/shared';
import { Banknote, CheckCircle2, FileCheck2, Loader2, Plus, Printer, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge } from '@/components/data-table/cells';
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { ExportExcelButton } from '@/components/export-excel-button';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { EntityHistory } from '@/features/audit/entity-history';
import { InvoiceAdjustmentsCard } from '@/features/finance/invoice-adjustments';
import { ParcelPicker } from '@/features/finance/parcel-picker';
import { PaymentForm } from '@/features/finance/payment-form';
import { useFinanceError } from '@/features/finance/use-finance-error';
import {
  addInvoiceParcelsAction,
  downloadInvoicePdfAction,
  exportInvoiceAction,
  issueInvoiceAction,
  removeInvoiceParcelAction,
} from '@/lib/actions/finance';
import { downloadBase64 } from '@/lib/download';

const STATUS_TONE: Record<Invoice['status'], string> = {
  draft: 'var(--chart-3)',
  issued: 'var(--chart-1)',
  paid: 'var(--chart-4)',
  // Chantier « Report des factures négatives » : le net vit sur la facture suivante.
  'carried-over': 'var(--chart-5)',
};

/**
 * Fiche d'une facture.
 *
 * L'écran est construit autour du seul geste qui compte : **émettre**. Avant,
 * tout se modifie et rien n'est gelé ; après, plus rien ne bouge et les colis
 * sont marqués facturés. C'est pourquoi l'émission demande une confirmation et
 * annonce ce qu'elle fige, au lieu d'être un bouton de plus.
 *
 * L'e-commerçant arrive ici avec la lecture seule : il voit ses lignes, ses
 * totaux et ses règlements, et peut imprimer. Aucun bouton d'écriture ne lui
 * est proposé — un geste refusé en 403 se cache, il ne se grise pas.
 */
export function InvoiceDetailScreen({
  invoice,
  billable,
  accounts,
  canWrite,
  canValidate,
  canPay,
  internalView = true,
}: {
  invoice: Invoice;
  billable: BillableParcelLine[];
  accounts: BankAccount[];
  canWrite: boolean;
  canValidate: boolean;
  canPay: boolean;
  /** Le journal de la facture reste au back-office — étanchéité vendeur, point 2. */
  internalView?: boolean;
}) {
  const t = useTranslations('finance.invoices');
  const tStatus = useTranslations('finance.invoices.status');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const errorMessage = useFinanceError();

  const [current, setCurrent] = useState(invoice);
  const [picking, setPicking] = useState(false);
  const [issuing, setIssuing] = useState(false);
  const [paying, setPaying] = useState(false);
  const [dueAt, setDueAt] = useState('');
  const [pending, startTransition] = useTransition();

  const draft = current.status === 'draft';
  const money = (cents: number) => format.number(cents / 100, 'currency');
  /** Référence · téléphone · ville · livré le… — ce que la ligne sait de son destinataire. */
  const lineCustomer = (line: Invoice['lines'][number]): string[] =>
    [
      line.externalCode ? t('lineReference', { reference: line.externalCode }) : null,
      line.recipientPhone ?? null,
      line.destinationCityName ?? null,
      line.deliveredAt
        ? t('lineDelivered', {
            date: format.dateTime(new Date(line.deliveredAt), 'stamp'),
          })
        : null,
    ].filter((part): part is string => Boolean(part));

  // Les frais sont HORS TAXES et la TVA s'y ajoute (B-11) : ce qui est retenu à
  // l'e-commerçant, c'est le TTC, et c'est donc lui qu'on met en avant.
  // Les lignes hors colis sont des frais (D-181).
  const feesHt =
    current.totalDeliveryFee + current.totalFailureFee + (current.totalAdjustment ?? 0);
  const feesTtc = feesHt + current.totalVat;

  /*
   * Le RÉGIME change ce que l'écran annonce — C11 bloc 3, D-061.
   *
   * Une facture `professional` ne reverse RIEN : son COD a été versé à part,
   * intégralement, par un bon de versement. Elle réclame les frais, et son net
   * est donc négatif. Garder « Net dû » et un montant négatif ferait lire à
   * l'opérateur l'inverse de ce qui se passe.
   */
  const professional = current.billingType === 'professional';
  /*
   * Lot R (D-230) : le taux PRO de la facture (courant sur un brouillon, gelé à
   * l'émission). Renseigné, l'écran montre le pied Total HT / TVA / Montant TVA
   * / Total TTC ; absent (facture normale, ou émise avant le lot), rien ne change.
   */
  const proVatRate = current.proVatRate ?? null;
  const period =
    current.periodStart && current.periodEnd
      ? t('period', {
          start: format.dateTime(new Date(current.periodStart), 'short'),
          end: format.dateTime(new Date(current.periodEnd), 'short'),
        })
      : null;

  const addParcels = (parcelIds: string[]) => {
    startTransition(async () => {
      const result = await addInvoiceParcelsAction(current.code, { parcelIds });

      if (result.ok) {
        setCurrent(result.data);
        setPicking(false);
        toast.success(t('parcelsAdded', { count: parcelIds.length }));
        return;
      }

      // Le dialogue reste ouvert : le lot est refusé en entier, et l'opérateur
      // doit pouvoir décocher le colis fautif sans tout recommencer.
      toast.error(errorMessage(result));
    });
  };

  const removeParcel = (parcelId: string, parcelCode: string) => {
    startTransition(async () => {
      const result = await removeInvoiceParcelAction(current.code, parcelId);
      if (result.ok) {
        setCurrent(result.data);
        toast.success(t('parcelRemoved', { code: parcelCode }));
      } else toast.error(errorMessage(result));
    });
  };

  /*
   * Le PDF passe par une Server Action : le cookie de session est `httpOnly` et
   * posé sur l'origine du web, un lien direct vers l'API partirait sans lui.
   * C'est le geste que l'e-commerçant vient chercher — il l'a sans permission
   * d'écriture.
   */
  const print = () => {
    startTransition(async () => {
      const result = await downloadInvoicePdfAction(current.code);

      if (result.ok) downloadBase64(result.data, `${current.code}.pdf`, 'application/pdf');
      else toast.error(errorMessage(result));
    });
  };

  const issue = () => {
    startTransition(async () => {
      const result = await issueInvoiceAction(current.code, { dueAt });
      setIssuing(false);

      if (result.ok) {
        setCurrent(result.data);
        toast.success(t('issued', { code: current.code }));
      } else toast.error(errorMessage(result));
    });
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <CellBadge
          label={tStatus(current.status)}
          color={STATUS_TONE[current.status]}
          icon={current.status === 'paid' ? CheckCircle2 : undefined}
        />
        <span className="text-muted-foreground text-sm">
          {current.clientName} · {current.agencyName}
          {period ? ` · ${period}` : ''}
        </span>
        {/* Reportée : où est passé le net négatif (chantier « Report des factures négatives »). */}
        {current.status === 'carried-over' ? (
          <span className="text-muted-foreground basis-full text-sm">
            {current.carriedIntoCode
              ? t('carriedInto', { code: current.carriedIntoCode })
              : t('carriedPending')}
          </span>
        ) : null}

        <span className="ms-auto flex flex-wrap gap-2">
          <Button
            size="sm"
            variant="outline"
            className="gap-1.5"
            disabled={pending}
            onClick={print}
          >
            <Printer className="size-4" />
            {t('pdf')}
          </Button>

          {/*
            « EXPORTER » — à côté de « Imprimer », factures clients et clients
            pro (11/11/2026, priorité de Badr). Le vendeur l'a aussi sur les
            siennes : comme l'impression, il ne demande aucune permission
            d'écriture. Fichier `Facture-<numéro>.xlsx`, deux feuilles.
          */}
          <ExportExcelButton
            label={t('export')}
            filename={`Facture-${current.code}`}
            disabled={pending}
            onExport={() => exportInvoiceAction(current.code)}
          />

          {canWrite && draft ? (
            <Button
              size="sm"
              variant="outline"
              className="gap-1.5"
              onClick={() => setPicking(true)}
            >
              <Plus className="size-4" />
              {t('addParcels')}
            </Button>
          ) : null}

          {canValidate && draft ? (
            <Button
              size="sm"
              className="gap-1.5"
              disabled={!current.issuable}
              onClick={() => setIssuing(true)}
            >
              <FileCheck2 className="size-4" />
              {t('issue')}
            </Button>
          ) : null}

          {canPay && current.status === 'issued' ? (
            <Button size="sm" className="gap-1.5" onClick={() => setPaying(true)}>
              <Banknote className="size-4" />
              {t('recordPayment')}
            </Button>
          ) : null}
        </span>
      </div>

      <KpiGrid>
        <KpiCard
          label={professional ? t('kpi.codPro') : t('kpi.cod')}
          value={money(current.totalCod)}
          hint={professional ? t('kpi.codProHint') : undefined}
          icon={Banknote}
        />
        <KpiCard
          label={professional ? t('kpi.feesPro') : t('kpi.fees')}
          value={money(feesTtc)}
          hint={
            current.vatRate > 0 || proVatRate !== null
              ? t('kpi.feesBreakdown', { ht: money(feesHt), vat: money(current.totalVat) })
              : undefined
          }
          icon={FileCheck2}
          tone="var(--chart-2)"
        />
        <KpiCard
          label={professional ? t('kpi.duePro') : t('kpi.due')}
          // Le net d'une facture pro est négatif : on l'annonce POSITIF sous un
          // libellé qui dit le sens, comme sur le PDF. Et c'est le dû ENTIER —
          // frais ET chèques reçus directement (audit du 03/10, E1) : annoncer
          // les frais seuls laissait une facture qui ne se soldait jamais.
          value={money(professional ? Math.abs(current.totalDue) : current.totalDue)}
          hint={
            professional && -current.totalDue - feesTtc > 0
              ? t('kpi.dueCheques', { amount: money(-current.totalDue - feesTtc) })
              : undefined
          }
          icon={Banknote}
          tone="var(--chart-1)"
        />
        <KpiCard
          label={t('kpi.balance')}
          value={money(current.balance)}
          hint={t('kpi.paid', { amount: money(current.totalPaid) })}
          icon={CheckCircle2}
          tone={current.balance === 0 ? 'var(--chart-4)' : 'var(--chart-3)'}
        />
      </KpiGrid>

      <Card>
        <CardHeader>
          <CardTitle>{t('lines')}</CardTitle>
          <CardDescription>{draft ? t('linesDraftHint') : t('linesFrozenHint')}</CardDescription>
        </CardHeader>
        <CardContent>
          {current.lines.length === 0 ? (
            <p className="text-muted-foreground py-6 text-center text-sm">{t('noLine')}</p>
          ) : (
            <ul className="divide-border divide-y">
              {current.lines.map((line) => (
                <li key={line.id} className="flex items-center gap-3 py-2.5">
                  <span className="min-w-0 flex-1">
                    <span className="block truncate">
                      <span className="font-mono text-sm">{line.parcelCode}</span>
                      {/* Lot P : le destinataire, pour reconnaître la commande. */}
                      {line.recipientName ? (
                        <span className="text-muted-foreground text-xs">
                          {' · '}
                          <bdi>{line.recipientName}</bdi>
                        </span>
                      ) : null}
                    </span>
                    {/*
                      LES DONNÉES CLIENT DE LA LIGNE — 11/11/2026 : référence
                      commande, téléphone, ville, date de livraison. Les mêmes
                      que sur le PDF et dans l'Excel ; rien si le colis n'en a
                      pas (ou s'il a été supprimé).
                    */}
                    {lineCustomer(line).length > 0 ? (
                      <span className="text-muted-foreground block truncate text-xs">
                        <bdi>{lineCustomer(line).join(' · ')}</bdi>
                      </span>
                    ) : null}
                    <span className="text-muted-foreground block truncate text-xs">
                      {/*
                        LES FRAIS DE LIVRAISON, EN CLAIR — chantier « Retours,
                        BL Clients Pro », point 6 : « apparaît dans le détail de
                        la facture avec Frais de livraison : 0,00 MAD ».

                        Ils étaient noyés dans un total « frais TTC » qui
                        mélangeait livraison, échec et TVA. Un colis livré à
                        zéro franc y disparaissait derrière la TVA d'un autre
                        poste, et le geste commercial qu'il représente devenait
                        invisible à celui qui le relit.
                      */}
                      {t('lineDetail', {
                        cod: money(line.codAmount),
                        deliveryFee: money(line.deliveryFee),
                        fee: money(line.deliveryFee + line.failureFee + line.vatAmount),
                      })}
                    </span>

                    {/*
                      DONT RÉGLÉ PAR CHÈQUE — chantier « Chèques », point 3.

                      Le numéro autant que le montant : c'est par le numéro
                      qu'un vendeur retrouve le papier qu'on lui a remis, et
                      une ligne qui dit « 300 de moins » sans dire lequel est
                      une ligne qu'il conteste.
                    */}
                    {line.chequePaid > 0 ? (
                      <span className="text-muted-foreground block text-xs">
                        {/*
                          Lot Q : en pro, le chèque n'est plus dans le net — le
                          BV ne l'a pas versé. Pour mémoire, sauf sur une ligne
                          d'avant le lot qui le réclame encore (net < −frais).
                        */}
                        {t(
                          professional &&
                            line.netDue >= -(line.deliveryFee + line.failureFee + line.vatAmount)
                            ? 'lineChequePro'
                            : 'lineCheque',
                          {
                            amount: money(line.chequePaid),
                            number: line.chequeNumber ?? tCommon('unknown'),
                          },
                        )}
                      </span>
                    ) : null}

                    {/* D-237 : un chèque rejeté reste retranché ; le papier est rendu au vendeur. */}
                    {line.chequeRejected ? (
                      <span className="text-destructive block text-xs">
                        {t('lineChequeRejected', {
                          number: line.chequeNumber ?? tCommon('unknown'),
                        })}
                      </span>
                    ) : null}
                  </span>

                  <span className="shrink-0 text-sm font-medium tabular-nums">
                    {money(line.netDue)}
                  </span>

                  {canWrite && draft ? (
                    <Button
                      size="icon"
                      variant="ghost"
                      className="text-destructive shrink-0"
                      disabled={pending}
                      aria-label={tCommon('delete')}
                      onClick={() => removeParcel(line.parcelId, line.parcelCode)}
                    >
                      <Trash2 className="size-4" />
                    </Button>
                  ) : null}
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <InvoiceAdjustmentsCard
        invoice={current}
        canAdjust={canValidate}
        onChange={setCurrent}
        internalView={internalView}
      />

      {proVatRate !== null ? (
        <Card>
          <CardHeader>
            <CardTitle>{t('proTotals.title')}</CardTitle>
            <CardDescription>{t('proTotals.hint')}</CardDescription>
          </CardHeader>
          <CardContent>
            <dl className="grid gap-1.5 text-sm sm:max-w-sm sm:ms-auto">
              {[
                [t('proTotals.ht'), money(feesHt)],
                [
                  t('proTotals.rate'),
                  proVatRate === 0
                    ? t('proTotals.exempt')
                    : t('proTotals.percent', { rate: String(proVatRate / 100) }),
                ],
                [t('proTotals.vat'), money(current.totalVat)],
                [t('proTotals.ttc'), money(feesTtc)],
              ].map(([label, value], index) => (
                <div
                  key={label}
                  className={
                    index === 3
                      ? 'flex justify-between gap-3 border-t pt-1.5 font-medium'
                      : 'flex justify-between gap-3'
                  }
                >
                  <dt className="text-muted-foreground">{label}</dt>
                  <dd className="tabular-nums">{value}</dd>
                </div>
              ))}
            </dl>
          </CardContent>
        </Card>
      ) : null}

      {current.payments.length > 0 ? (
        <Card>
          <CardHeader>
            <CardTitle>{t('payments')}</CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="divide-border divide-y">
              {current.payments.map((payment) => (
                <li key={payment.id} className="flex items-center gap-3 py-2.5 text-sm">
                  <span className="min-w-0 flex-1">
                    <span className="block">
                      {format.dateTime(new Date(payment.paidAt), 'short')}
                    </span>
                    <span className="text-muted-foreground block truncate text-xs">
                      {payment.bankAccountLabel ?? payment.reference ?? '-'}
                    </span>
                  </span>
                  <span className="shrink-0 font-medium tabular-nums">{money(payment.amount)}</span>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      ) : null}

      {current.note ? <p className="text-muted-foreground text-sm">{current.note}</p> : null}

      <ParcelPicker
        open={picking}
        onOpenChange={setPicking}
        title={t('addParcels')}
        description={t('billableHint')}
        pending={pending}
        onConfirm={addParcels}
        parcels={billable.map((parcel) => ({
          id: parcel.id,
          code: parcel.code,
          recipientName: parcel.recipientName,
          context: parcel.destinationCityName,
          amount: parcel.netDue,
          /*
            LE COLIS À ZÉRO FRANC SE DIT — chantier « Retours, BL Clients Pro »,
            point 6.

            `billability` vaut trois choses, pas deux : « facturable à zéro »
            n'est pas « non facturable ». L'agent qui compose sa facture voit
            donc la mention plutôt que de se demander s'il a oublié une saisie —
            un colis livré sans frais est un geste commercial, pas un trou.
          */
          note:
            parcel.billability === 'billable-free'
              ? t('freeFee')
              : parcel.cashReceived
                ? t('cashIn')
                : t('cashPending'),
        }))}
      />

      <Dialog open={issuing} onOpenChange={setIssuing}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('issue')}</DialogTitle>
            <DialogDescription>
              {t('issueWarning', {
                count: String(current.lines.length),
                amount: money(current.totalDue),
              })}
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-2">
            <Label htmlFor="dueAt">{t('fields.dueAt')}</Label>
            <Input
              id="dueAt"
              type="date"
              value={dueAt}
              onChange={(event) => setDueAt(event.target.value)}
            />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setIssuing(false)}>
              {tCommon('cancel')}
            </Button>
            <Button disabled={pending} onClick={issue} className="gap-1.5">
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('issue')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {paying ? (
        <PaymentForm
          accounts={accounts}
          invoice={current}
          onDone={() => setPaying(false)}
          onCreated={(payment) =>
            setCurrent((invoiceState) => ({
              ...invoiceState,
              // Optimiste et borné : le solde suit, le reste attend la
              // revalidation. On ne recalcule pas l'état de la facture ici —
              // c'est le serveur qui décide si un règlement la solde.
              totalPaid: invoiceState.totalPaid + payment.amount,
              payments: [payment, ...invoiceState.payments],
            }))
          }
        />
      ) : null}

      {/*
        C11 bloc 5 (D-064) : une CARTE en bas de fiche, et non un onglet.
        Les quatre fiches concernées n'ont pas la même structure — deux
        colonnes ici, des onglets ailleurs, un formulaire plein là — et une
        carte se pose pareil sur les quatre. L'historique se lit au même
        endroit d'un écran à l'autre, ce qui vaut mieux qu'un onglet ici et
        une carte là.
      */}
      {internalView ? <EntityHistory entity="Invoice" entityId={current.id} /> : null}
    </div>
  );
}
