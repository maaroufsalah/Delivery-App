'use client';

import {
  type BankAccount,
  CHEQUE_STATUS_COLORS,
  chequeCountsAsPaid,
  type CodPayoutCandidate,
  type CodPayoutDetail as Payout,
  PAYMENT_METHODS,
  type PaymentMethod,
} from '@delivery/shared';
import { Banknote, CheckCircle2, FileDown, Loader2, Plus, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge } from '@/components/data-table/cells';
import { ExportExcelButton } from '@/components/export-excel-button';
import { SearchableSelect } from '@/components/searchable-select';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { ParcelPicker } from '@/features/finance/parcel-picker';
import { Link } from '@/i18n/navigation';
import { useFinanceError } from '@/features/finance/use-finance-error';
import {
  addCodPayoutParcelsAction,
  downloadCodPayoutPdfAction,
  payCodPayoutAction,
  removeCodPayoutParcelAction,
} from '@/lib/actions/finance';
import { codPayoutExportAction } from '@/lib/actions/documents';
import { downloadBase64 } from '@/lib/download';

/**
 * Fiche d'un bon de versement — C11 bloc 3, D-061.
 *
 * On compose colis par colis, puis on verse. **Aucun montant ne se saisit** :
 * le total EST la somme des COD retenus. Un champ « montant » inviterait à en
 * retrancher quelque chose, ce que ce document refuse par construction — les
 * frais sont réclamés ailleurs, par la facture mensuelle.
 *
 * Le versement fait partir de l'argent : il demande donc un accord explicite,
 * annonce son montant, et ne se reprend pas.
 */
export function CodPayoutDetailScreen({
  payout,
  candidates,
  accounts,
  canWrite,
  canPay,
}: {
  payout: Payout;
  candidates: CodPayoutCandidate[];
  accounts: BankAccount[];
  canWrite: boolean;
  canPay: boolean;
}) {
  const t = useTranslations('finance.codPayouts');
  const tStatus = useTranslations('finance.codPayouts.status');
  const tMethod = useTranslations('finance.payments.method');
  const tChequeStatus = useTranslations('finance.cheques.status');
  const tCommon = useTranslations('common');
  // Le libellé « Exporter Excel » des fiches, le même que sur le circuit.
  const tExport = useTranslations('routes.detail');
  const format = useFormatter();
  const errorMessage = useFinanceError();

  const [current, setCurrent] = useState(payout);
  const [picking, setPicking] = useState(false);
  const [paying, setPaying] = useState(false);
  const [pending, startTransition] = useTransition();

  const [method, setMethod] = useState<PaymentMethod>(current.method);
  const [bankAccountId, setBankAccountId] = useState(
    current.bankAccountId ?? accounts.find((account) => account.isDefault)?.id ?? '',
  );
  const [reference, setReference] = useState(current.reference ?? '');

  const draft = current.status === 'draft';
  const money = (cents: number) => format.number(cents / 100, 'currency');

  const addParcels = (parcelIds: string[]) => {
    startTransition(async () => {
      const result = await addCodPayoutParcelsAction(current.code, { parcelIds });

      if (result.ok) {
        setCurrent(result.data);
        setPicking(false);
        toast.success(t('parcelsAdded', { count: parcelIds.length }));
        return;
      }

      toast.error(errorMessage(result));
    });
  };

  const removeParcel = (parcelId: string, parcelCode: string) => {
    startTransition(async () => {
      const result = await removeCodPayoutParcelAction(current.code, parcelId);
      if (result.ok) {
        setCurrent(result.data);
        toast.success(t('parcelRemoved', { code: parcelCode }));
      } else toast.error(errorMessage(result));
    });
  };

  const pay = () => {
    startTransition(async () => {
      const result = await payCodPayoutAction(current.code, {
        method,
        // Un versement en espèces ne part d'aucun compte : l'envoyer vide
        // plutôt que de traîner une sélection qui ne veut plus rien dire.
        bankAccountId: method === 'cash' ? '' : bankAccountId,
        reference,
        paidAt: '',
        proof: null,
      });
      setPaying(false);

      if (result.ok) {
        setCurrent(result.data);
        toast.success(t('paid', { code: current.code }));
      } else toast.error(errorMessage(result));
    });
  };

  const downloadPdf = () => {
    startTransition(async () => {
      const result = await downloadCodPayoutPdfAction(current.code);
      if (result.ok) downloadBase64(result.data, `${current.code}.pdf`, 'application/pdf');
      else toast.error(errorMessage(result));
    });
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        {current.status === 'paid' ? (
          <CellBadge label={tStatus('paid')} color="var(--chart-4)" icon={CheckCircle2} />
        ) : (
          <CellBadge label={tStatus('draft')} color="var(--chart-3)" />
        )}

        <span className="text-muted-foreground text-sm">
          {current.clientName} · {current.agencyName}
        </span>

        <span className="ms-auto flex flex-wrap gap-2">
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

          <Button
            size="sm"
            variant="outline"
            className="gap-1.5"
            disabled={pending}
            onClick={downloadPdf}
          >
            <FileDown className="size-4" />
            {t('pdf')}
          </Button>

          {/* Une ligne par colis ; pour tout compte qui lit le bon, vendeur compris. */}
          <ExportExcelButton
            label={tExport('exportExcel')}
            filename={current.code}
            disabled={pending || current.items.length === 0}
            onExport={() => codPayoutExportAction(current.code)}
          />

          {canPay && draft ? (
            <Button
              size="sm"
              className="gap-1.5"
              disabled={!current.payable}
              onClick={() => setPaying(true)}
            >
              <Banknote className="size-4" />
              {t('pay')}
            </Button>
          ) : null}
        </span>
      </div>

      <Card>
        <CardHeader className="flex-row items-center justify-between gap-3">
          <div>
            <CardTitle>{t('parcels')}</CardTitle>
            <CardDescription>
              {draft ? t('parcelsDraftHint') : t('parcelsFrozenHint')}
            </CardDescription>
          </div>
          <div className="text-end">
            <p className="text-muted-foreground text-xs">{t('total')}</p>
            <p className="text-2xl font-semibold tabular-nums">{money(current.amount)}</p>
          </div>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          {current.items.length === 0 ? (
            <p className="text-muted-foreground py-6 text-center text-sm">{t('noParcel')}</p>
          ) : (
            <ul className="divide-border divide-y">
              {current.items.map((item) => (
                <li key={item.parcelId} className="flex items-center gap-3 py-2.5">
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-mono text-sm">{item.parcelCode}</span>
                    <span className="text-muted-foreground block truncate text-xs">
                      {item.recipientName} · {item.destinationCityName}
                    </span>
                  </span>

                  <span className="shrink-0 text-sm font-medium tabular-nums">
                    {money(item.amount)}
                  </span>

                  {canWrite && draft ? (
                    <Button
                      size="icon"
                      variant="ghost"
                      className="text-destructive shrink-0"
                      disabled={pending}
                      aria-label={tCommon('delete')}
                      onClick={() => removeParcel(item.parcelId, item.parcelCode)}
                    >
                      <Trash2 className="size-4" />
                    </Button>
                  ) : null}
                </li>
              ))}
            </ul>
          )}

          {/*
            LA phrase du document, reprise à l'écran. Sans elle, « et mes frais,
            ils sont où ? » se pose à chaque bon — et la réponse est : sur la
            facture mensuelle, jamais ici.
          */}
          <p className="text-muted-foreground border-border border-t pt-3 text-xs">
            {t('noDeductionHint')}
          </p>
        </CardContent>
      </Card>

      {/*
        LES CHÈQUES REÇUS, POUR INFORMATION — lot S (D-231). Sous le total,
        jamais dedans (D-229) ; aucun bloc sans chèque. Même lecture que le
        PDF : l'état est celui du jour, le montant du bon ne bouge pas.
      */}
      {current.cheques.length > 0 ? (
        <Card>
          <CardHeader>
            <CardTitle>{t('cheques.title', { count: String(current.cheques.length) })}</CardTitle>
            <CardDescription>
              {current.cheques.some((cheque) => cheque.includedInPayout)
                ? t('cheques.mentionLegacy')
                : t('cheques.mention')}
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <ul className="divide-border divide-y">
              {current.cheques.map((cheque) => (
                <li key={cheque.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 py-2.5">
                  <Link
                    href={`/cheques/${encodeURIComponent(cheque.id)}`}
                    className="min-w-0 flex-1 underline-offset-4 hover:underline"
                  >
                    <span className="block truncate font-mono text-sm">
                      {cheque.number}
                      {cheque.includedInPayout ? ' *' : ''}
                    </span>
                    <span className="text-muted-foreground block truncate text-xs">
                      {cheque.bank ?? '—'} · {cheque.parcelCode}
                    </span>
                  </Link>

                  <CellBadge
                    label={tChequeStatus(cheque.status)}
                    color={CHEQUE_STATUS_COLORS[cheque.status]}
                  />

                  <span className="w-28 shrink-0 text-end text-sm font-medium tabular-nums">
                    {money(cheque.amount)}
                  </span>
                </li>
              ))}
            </ul>

            <div className="border-border flex items-baseline justify-between gap-3 border-t pt-3 text-sm">
              {/* D-237 : un chèque rejeté reste compté, il n'est pas versé en espèces. */}
              <span className="font-medium">{t('cheques.total')}</span>
              <span className="font-semibold tabular-nums">
                {money(
                  current.cheques
                    .filter((cheque) => chequeCountsAsPaid(cheque.status))
                    .reduce((sum, cheque) => sum + cheque.amount, 0),
                )}
              </span>
            </div>

            {current.cheques
              .filter((cheque) => cheque.status === 'rejete')
              .map((cheque) => (
                <p key={`rejected-${cheque.id}`} className="text-destructive text-xs">
                  {t('cheques.rejected', { number: cheque.number })}
                </p>
              ))}
          </CardContent>
        </Card>
      ) : null}

      {current.status === 'paid' ? (
        <Card>
          <CardHeader>
            <CardTitle>{t('payment')}</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="flex flex-col gap-2 text-sm">
              <div className="flex items-baseline justify-between gap-3">
                <dt className="text-muted-foreground">{t('fields.method')}</dt>
                <dd>{tMethod(current.method)}</dd>
              </div>

              {current.bankAccountLabel ? (
                <div className="flex items-baseline justify-between gap-3">
                  <dt className="text-muted-foreground">{t('fields.account')}</dt>
                  <dd>{current.bankAccountLabel}</dd>
                </div>
              ) : null}

              {current.reference ? (
                <div className="flex items-baseline justify-between gap-3">
                  <dt className="text-muted-foreground">{t('fields.reference')}</dt>
                  <dd className="font-mono">{current.reference}</dd>
                </div>
              ) : null}

              {current.paidAt ? (
                <div className="flex items-baseline justify-between gap-3">
                  <dt className="text-muted-foreground">{t('columns.paidAt')}</dt>
                  <dd>
                    {format.dateTime(new Date(current.paidAt), 'short')}
                    {current.paidByName ? ` · ${current.paidByName}` : ''}
                  </dd>
                </div>
              ) : null}
            </dl>
          </CardContent>
        </Card>
      ) : null}

      {current.note ? <p className="text-muted-foreground text-sm">{current.note}</p> : null}

      <ParcelPicker
        open={picking}
        onOpenChange={setPicking}
        title={t('addParcels')}
        description={t('candidatesHint')}
        pending={pending}
        onConfirm={addParcels}
        parcels={candidates.map((parcel) => ({
          id: parcel.id,
          code: parcel.code,
          recipientName: parcel.recipientName,
          context: parcel.destinationCityName,
          amount: parcel.codCollected,
        }))}
      />

      <Dialog open={paying} onOpenChange={setPaying}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('pay')}</DialogTitle>
            <DialogDescription>
              {t('payWarning', {
                count: String(current.items.length),
                amount: money(current.amount),
                client: current.clientName,
              })}
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-3">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="method">{t('fields.method')}</Label>
              <Select value={method} onValueChange={(value) => setMethod(value as PaymentMethod)}>
                <SelectTrigger id="method">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PAYMENT_METHODS.map((option) => (
                    <SelectItem key={option} value={option}>
                      {tMethod(option)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {method === 'cash' ? null : (
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="bankAccountId">{t('fields.account')}</Label>
                <SearchableSelect
                  options={accounts.map((account) => ({ value: account.id, label: account.label }))}
                  value={bankAccountId}
                  onValueChange={setBankAccountId}
                  placeholder={t('fields.account')}
                  id="bankAccountId"
                  className="w-full"
                />
              </div>
            )}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="reference">{t('fields.reference')}</Label>
              <Input
                id="reference"
                value={reference}
                onChange={(event) => setReference(event.target.value)}
                placeholder={t('fields.referencePlaceholder')}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setPaying(false)}>
              {tCommon('cancel')}
            </Button>
            <Button disabled={pending} onClick={pay} className="gap-1.5">
              {pending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <Banknote className="size-4" />
              )}
              {t('pay')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
