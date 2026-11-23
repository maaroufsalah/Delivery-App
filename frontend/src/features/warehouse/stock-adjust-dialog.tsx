'use client';

import type { Agency, ProductListItem, StockDetail } from '@delivery/shared';
import { Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { SearchableSelect } from '@/components/searchable-select';
import { Button } from '@/components/ui/button';
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
import { adjustStockAction } from '@/lib/actions/warehouse';

/**
 * Ajustement manuel d'une quantité.
 *
 * La variation est SIGNÉE : `-3` retire trois pièces. On ne demande pas une
 * quantité cible, parce qu'un écart se raisonne en « il en manque trois », pas
 * en « il devrait y en avoir dix-sept ».
 *
 * Le motif est obligatoire, et c'est tout l'intérêt du geste : le legacy
 * écrasait `produit_stock.quantity` sans rien demander, et un écart
 * d'inventaire n'y était plus explicable trois mois plus tard.
 */
export function StockAdjustDialog({
  products,
  agencies,
  productId: initialProductId,
  agencyId: initialAgencyId,
  onClose,
  onDone,
}: {
  products: ProductListItem[];
  agencies: Agency[];
  /** Pré-remplis quand on ajuste depuis une ligne existante. */
  productId?: string;
  agencyId?: string;
  onClose: () => void;
  onDone: (stock: StockDetail) => void;
}) {
  const t = useTranslations('warehouse.stock');
  const tCommon = useTranslations('common');
  const errorMessage = useWarehouseError();

  const [productId, setProductId] = useState(initialProductId ?? '');
  const [agencyId, setAgencyId] = useState(initialAgencyId ?? '');
  const [quantity, setQuantity] = useState('');
  const [reason, setReason] = useState('');
  const [pending, startTransition] = useTransition();

  const parsed = Number(quantity);
  const valid =
    productId !== '' &&
    agencyId !== '' &&
    Number.isInteger(parsed) &&
    parsed !== 0 &&
    reason.trim().length > 0;

  const submit = () => {
    startTransition(async () => {
      const result = await adjustStockAction({
        productId,
        agencyId,
        quantity: parsed,
        reason: reason.trim(),
      });

      if (result.ok) {
        toast.success(t('adjusted'));
        onClose();
        onDone(result.data);
        return;
      }

      toast.error(errorMessage(result));
    });
  };

  return (
    <Dialog
      open
      onOpenChange={(next) => {
        if (!next) onClose();
      }}
    >
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t('adjustTitle')}</DialogTitle>
          <DialogDescription>{t('adjustHint')}</DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="adjust-product">{t('fields.product')}</Label>
            <SearchableSelect
              options={products.map((product) => ({
                value: product.id,
                label: `${product.reference ? `${product.reference}, ` : ''}${product.name}`,
                keywords: [product.reference],
              }))}
              value={productId}
              onValueChange={setProductId}
              placeholder={t('fields.product')}
              disabled={Boolean(initialProductId)}
              id="adjust-product"
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="adjust-agency">{t('fields.agency')}</Label>
            <SearchableSelect
              options={agencies.map((agency) => ({ value: agency.id, label: agency.name }))}
              value={agencyId}
              onValueChange={setAgencyId}
              placeholder={t('fields.agency')}
              disabled={Boolean(initialAgencyId)}
              id="adjust-agency"
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="adjust-quantity">{t('fields.quantity')}</Label>
            <Input
              id="adjust-quantity"
              type="number"
              step={1}
              value={quantity}
              onChange={(event) => setQuantity(event.target.value)}
            />
            <p className="text-muted-foreground text-xs">{t('fields.quantityHint')}</p>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="adjust-reason">{t('fields.reason')}</Label>
            <Input
              id="adjust-reason"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
          </div>
        </div>

        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={onClose} disabled={pending}>
            {tCommon('cancel')}
          </Button>
          <Button onClick={submit} disabled={pending || !valid} className="gap-1.5">
            {pending ? <Loader2 className="size-4 animate-spin" /> : null}
            {t('adjust')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
