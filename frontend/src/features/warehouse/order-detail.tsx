'use client';

import type { OrderDetail as OrderDetailData } from '@delivery/shared';
import { CheckCircle2, Loader2, PackageCheck, ShoppingCart, XCircle } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge } from '@/components/data-table/cells';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useWarehouseError } from '@/features/warehouse/use-warehouse-error';
import { Link } from '@/i18n/navigation';
import {
  cancelOrderAction,
  confirmOrderAction,
  createOrderParcelAction,
} from '@/lib/actions/warehouse';

/**
 * Fiche d'une commande.
 *
 * « Créer le colis » est le geste central de C5, et il fait trois choses d'un
 * coup : le colis naît, ses lignes le suivent, et le stock SORT. D'où le
 * récapitulatif avant, et d'où l'irréversibilité annoncée — le legacy créait
 * le colis sans toucher au stock et sans garder trace de la commande, si bien
 * que deux passages donnaient deux colis.
 */
export function OrderDetailScreen({ order }: { order: OrderDetailData }) {
  const t = useTranslations('warehouse.orders');
  const tStatus = useTranslations('warehouse.orders.status');
  const tSource = useTranslations('warehouse.orders.sources');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const errorMessage = useWarehouseError();

  const [current, setCurrent] = useState(order);
  const [creating, setCreating] = useState(false);
  const [pickupAtSender, setPickupAtSender] = useState(true);
  const [fragile, setFragile] = useState(false);
  const [openOnDelivery, setOpenOnDelivery] = useState(false);
  const [pending, startTransition] = useTransition();

  const run = (
    action: () => Promise<
      { ok: true; data: OrderDetailData } | { ok: false; error: string; reason?: string }
    >,
    success: string,
  ) => {
    startTransition(async () => {
      const result = await action();
      setCreating(false);

      if (result.ok) {
        setCurrent(result.data);
        toast.success(success);
        return;
      }

      toast.error(errorMessage(result as Parameters<typeof errorMessage>[0]));
    });
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        {current.status === 'parcel-created' ? (
          <CellBadge label={tStatus('parcel-created')} color="var(--chart-4)" icon={PackageCheck} />
        ) : current.status === 'confirmed' ? (
          <CellBadge label={tStatus('confirmed')} color="var(--chart-1)" icon={CheckCircle2} />
        ) : current.status === 'cancelled' ? (
          <CellBadge label={tStatus('cancelled')} icon={XCircle} />
        ) : (
          <CellBadge label={tStatus('draft')} color="var(--chart-3)" />
        )}

        <CellBadge label={tSource(current.source)} />

        <span className="text-muted-foreground text-sm">
          {current.clientName}
          {current.externalRef ? ` · ${current.externalRef}` : ''}
        </span>

        <div className="ms-auto flex flex-wrap items-center gap-2">
          {current.parcelCode ? (
            <Button size="sm" variant="outline" asChild>
              <Link href={`/parcels/${current.parcelCode}`}>{t('seeParcel')}</Link>
            </Button>
          ) : null}

          {current.status === 'draft' ? (
            <>
              <Button
                size="sm"
                variant="outline"
                disabled={pending}
                onClick={() =>
                  run(() => cancelOrderAction(current.code), t('cancelled', { code: current.code }))
                }
              >
                {t('cancel')}
              </Button>
              <Button
                size="sm"
                className="gap-1.5"
                disabled={pending || current.lines.length === 0}
                onClick={() =>
                  run(
                    () => confirmOrderAction(current.code),
                    t('confirmed', { code: current.code }),
                  )
                }
              >
                <CheckCircle2 className="size-4" />
                {t('confirm')}
              </Button>
            </>
          ) : null}

          {current.parcelCreatable ? (
            <Button
              size="sm"
              className="gap-1.5"
              disabled={pending}
              onClick={() => setCreating(true)}
            >
              <PackageCheck className="size-4" />
              {t('createParcel')}
            </Button>
          ) : null}
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <ShoppingCart className="size-4" />
            {current.recipientName}
          </CardTitle>
          <CardDescription>
            {current.recipientPhone} · {current.destinationCityName}
            {current.destinationDistrictName ? ` · ${current.destinationDistrictName}` : ''} ·{' '}
            {current.deliveryAddress}
          </CardDescription>
        </CardHeader>

        <CardContent className="flex flex-col gap-2">
          {current.lines.length === 0 ? (
            <p className="text-muted-foreground text-sm">{t('noLines')}</p>
          ) : (
            <>
              {current.lines.map((line) => (
                <div
                  key={line.id}
                  className="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-lg border p-3 text-sm"
                >
                  <span className="font-mono text-[13px]">{line.productReference ?? '-'}</span>
                  <span className="min-w-0 flex-1 truncate">{line.productName}</span>
                  <span className="text-muted-foreground tabular-nums">×{line.quantity}</span>
                  <span className="tabular-nums">
                    {format.number((line.unitPrice * line.quantity) / 100, 'currency')}
                  </span>
                </div>
              ))}

              <div className="flex items-center justify-between border-t pt-3 text-sm font-medium">
                <span>{t('total')}</span>
                <span className="tabular-nums">
                  {format.number(current.totalAmount / 100, 'currency')}
                </span>
              </div>
            </>
          )}
        </CardContent>
      </Card>

      <Dialog open={creating} onOpenChange={setCreating}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t('createParcelTitle')}</DialogTitle>
            <DialogDescription>{t('createParcelHint')}</DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-2">
            <Toggle
              checked={pickupAtSender}
              onChange={setPickupAtSender}
              label={t('fields.pickupAtSender')}
            />
            <Toggle checked={fragile} onChange={setFragile} label={t('fields.fragile')} />
            <Toggle
              checked={openOnDelivery}
              onChange={setOpenOnDelivery}
              label={t('fields.openOnDelivery')}
            />
          </div>

          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => setCreating(false)} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            <Button
              className="gap-1.5"
              disabled={pending}
              onClick={() =>
                run(
                  () =>
                    createOrderParcelAction(current.code, {
                      pickupAtSender,
                      fragile,
                      openOnDelivery,
                    }),
                  t('parcelCreated', { code: current.code }),
                )
              }
            >
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('createParcel')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Toggle({
  checked,
  onChange,
  label,
}: {
  checked: boolean;
  onChange: (value: boolean) => void;
  label: string;
}) {
  return (
    <label className="hover:bg-accent/60 flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm">
      <Checkbox checked={checked} onCheckedChange={(value) => onChange(value === true)} />
      {label}
    </label>
  );
}
