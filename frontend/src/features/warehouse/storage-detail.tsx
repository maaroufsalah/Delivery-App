'use client';

import type { StorageRequestDetail as StorageDetailData } from '@delivery/shared';
import { CheckCircle2, Download, Loader2, PackagePlus, XCircle } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge } from '@/components/data-table/cells';
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
import { useWarehouseError } from '@/features/warehouse/use-warehouse-error';
import { decideStorageRequestAction, downloadStorageReceiptAction } from '@/lib/actions/warehouse';

/**
 * Fiche d'une demande d'entreposage.
 *
 * La validation est le geste qui fait ENTRER la marchandise : le récapitulatif
 * dit combien d'unités vont bouger, et le dit avant, parce que le geste ne
 * s'annule pas. Le refus, lui, exige un motif — le statut « Refusée » du
 * legacy comptait 0 occurrence, faute de pouvoir dire pourquoi.
 */
export function StorageDetailScreen({
  request,
  canDecide,
}: {
  request: StorageDetailData;
  /** `warehouse.validate` : un e-commerçant ne tranche pas sa propre demande. */
  canDecide: boolean;
}) {
  const t = useTranslations('warehouse.storage');
  const tStatus = useTranslations('warehouse.storage.status');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const errorMessage = useWarehouseError();

  const [current, setCurrent] = useState(request);
  const [approving, setApproving] = useState(false);
  const [refusing, setRefusing] = useState(false);
  const [reason, setReason] = useState('');
  const [pending, startTransition] = useTransition();

  const decide = (approve: boolean) => {
    startTransition(async () => {
      const result = await decideStorageRequestAction(current.code, {
        approve,
        refusalReason: approve ? '' : reason.trim(),
      });

      setApproving(false);
      setRefusing(false);

      if (result.ok) {
        setCurrent(result.data);
        toast.success(
          approve ? t('approved', { code: current.code }) : t('refused', { code: current.code }),
        );
        return;
      }

      toast.error(errorMessage(result));
    });
  };

  const receipt = () => {
    startTransition(async () => {
      const result = await downloadStorageReceiptAction(current.code);
      if (!result.ok) {
        toast.error(errorMessage(result));
        return;
      }

      const bytes = Uint8Array.from(atob(result.data), (char) => char.charCodeAt(0));
      const url = URL.createObjectURL(new Blob([bytes], { type: 'application/pdf' }));
      const link = document.createElement('a');
      link.href = url;
      link.download = `${current.code}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    });
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        {current.status === 'validated' ? (
          <CellBadge label={tStatus('validated')} color="var(--chart-4)" icon={CheckCircle2} />
        ) : current.status === 'refused' ? (
          <CellBadge label={tStatus('refused')} color="var(--destructive)" icon={XCircle} />
        ) : (
          <CellBadge label={tStatus('pending')} color="var(--chart-3)" />
        )}

        <span className="text-muted-foreground text-sm">
          {current.clientName} · {current.agencyName}
        </span>

        {current.decidedAt ? (
          <span className="text-muted-foreground text-xs">
            {format.dateTime(new Date(current.decidedAt), 'stamp')}
            {current.decidedByName ? ` · ${current.decidedByName}` : ''}
          </span>
        ) : null}

        <div className="ms-auto flex flex-wrap items-center gap-2">
          <Button
            size="sm"
            variant="outline"
            className="gap-1.5"
            disabled={pending}
            onClick={receipt}
          >
            <Download className="size-4" />
            <span className="hidden sm:inline">{t('receipt')}</span>
          </Button>

          {canDecide && current.decidable ? (
            <>
              <Button
                size="sm"
                variant="outline"
                className="gap-1.5"
                disabled={pending}
                onClick={() => setRefusing(true)}
              >
                <XCircle className="size-4" />
                <span className="hidden sm:inline">{t('refuse')}</span>
              </Button>
              <Button
                size="sm"
                className="gap-1.5"
                disabled={pending}
                onClick={() => setApproving(true)}
              >
                <CheckCircle2 className="size-4" />
                <span className="hidden sm:inline">{t('approve')}</span>
              </Button>
            </>
          ) : null}
        </div>
      </div>

      {current.refusalReason ? (
        <div className="border-destructive/30 bg-destructive/5 rounded-lg border p-3 text-sm">
          <span className="font-medium">{t('refusalReason')}</span> : {current.refusalReason}
        </div>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <PackagePlus className="size-4" />
            {t('columns.lines')}
            <CellBadge label={String(current.lineCount)} />
          </CardTitle>
          {current.note ? <CardDescription>{current.note}</CardDescription> : null}
        </CardHeader>

        <CardContent className="flex flex-col gap-2">
          {current.items.length === 0 ? (
            <p className="text-muted-foreground text-sm">{t('noLines')}</p>
          ) : (
            <>
              {current.items.map((item) => (
                <div
                  key={item.id}
                  className="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-lg border p-3 text-sm"
                >
                  <span className="font-mono text-[13px]">{item.productReference ?? '-'}</span>
                  <span className="min-w-0 flex-1 truncate">{item.productName}</span>
                  <span className="tabular-nums">{item.quantity}</span>
                </div>
              ))}

              <div className="flex items-center justify-between border-t pt-3 text-sm font-medium">
                <span>{t('totalQuantity')}</span>
                <span className="tabular-nums">{current.totalQuantity}</span>
              </div>
            </>
          )}
        </CardContent>
      </Card>

      <Dialog open={approving} onOpenChange={setApproving}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t('approveTitle')}</DialogTitle>
            <DialogDescription>
              {t('approveHint', { count: current.totalQuantity })}
            </DialogDescription>
          </DialogHeader>

          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => setApproving(false)} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            <Button onClick={() => decide(true)} disabled={pending} className="gap-1.5">
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('approve')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={refusing} onOpenChange={setRefusing}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t('refuseTitle')}</DialogTitle>
          </DialogHeader>

          <div className="flex flex-col gap-2">
            <Label htmlFor="refusal-reason">{t('fields.refusalReason')}</Label>
            <Input
              id="refusal-reason"
              autoFocus
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
            <p className="text-muted-foreground text-xs">{t('fields.refusalReasonHint')}</p>
          </div>

          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => setRefusing(false)} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            {/* Refuser sans motif n'apprend rien au client : le bouton attend. */}
            <Button
              variant="destructive"
              onClick={() => decide(false)}
              disabled={pending || reason.trim().length === 0}
              className="gap-1.5"
            >
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('refuse')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
