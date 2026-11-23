'use client';

import type { Agency, ProductListItem, StockDetail as StockDetailData } from '@delivery/shared';
import { ArrowDownRight, ArrowUpRight, History, SlidersHorizontal } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState } from 'react';
import { CellBadge } from '@/components/data-table/cells';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { StockAdjustDialog } from '@/features/warehouse/stock-adjust-dialog';

/**
 * Une ligne de stock et son journal.
 *
 * C'est l'écran qui répond à « pourquoi trois ? ». Chaque mouvement porte sa
 * cause — entreposage validé, colis créé, ajustement motivé — et le solde
 * figé APRÈS lui. Le legacy ne gardait que la quantité courante : la question
 * n'avait pas de réponse.
 */
export function StockDetailScreen({
  stock,
  products,
  agencies,
  canAdjust = true,
}: {
  stock: StockDetailData;
  products: ProductListItem[];
  agencies: Agency[];
  /** Lot V, V2 (D-234) : le vendeur consulte ; l'API lui refuse l'ajustement (403). */
  canAdjust?: boolean;
}) {
  const t = useTranslations('warehouse.stock');
  const tKind = useTranslations('warehouse.stock.kinds');
  const format = useFormatter();

  const [current, setCurrent] = useState(stock);
  const [adjusting, setAdjusting] = useState(false);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-2xl font-semibold tabular-nums">{current.quantity}</span>

        {current.lowStock ? <CellBadge label={t('lowStock')} color="var(--chart-3)" /> : null}

        <span className="text-muted-foreground text-sm">
          {current.clientName} · {current.agencyName}
        </span>

        {canAdjust ? (
          <Button
            size="sm"
            variant="outline"
            className="ms-auto gap-1.5"
            onClick={() => setAdjusting(true)}
          >
            <SlidersHorizontal className="size-4" />
            {t('adjust')}
          </Button>
        ) : null}
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <History className="size-4" />
            {t('history')}
          </CardTitle>
          <CardDescription>{t('historyHint')}</CardDescription>
        </CardHeader>

        <CardContent className="flex flex-col gap-2">
          {current.movements.length === 0 ? (
            <p className="text-muted-foreground text-sm">{t('noMovements')}</p>
          ) : (
            current.movements.map((movement) => (
              <div
                key={movement.id}
                className="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-lg border p-3 text-sm"
              >
                <span
                  className={
                    movement.quantity < 0
                      ? 'text-destructive inline-flex items-center gap-1 tabular-nums'
                      : 'inline-flex items-center gap-1 text-[var(--chart-4)] tabular-nums'
                  }
                >
                  {movement.quantity < 0 ? (
                    <ArrowDownRight className="size-4" />
                  ) : (
                    <ArrowUpRight className="size-4" />
                  )}
                  {movement.quantity > 0 ? `+${movement.quantity}` : movement.quantity}
                </span>

                <CellBadge label={tKind(movement.kind)} />

                <span className="min-w-0 flex-1 truncate">
                  {/* La cause d'abord : c'est elle qu'on cherche en relisant. */}
                  {movement.storageRequestCode ?? movement.parcelCode ?? movement.reason ?? '-'}
                </span>

                <span className="text-muted-foreground text-xs">
                  {t('balance')}{' '}
                  <span className="text-foreground tabular-nums">{movement.balance}</span>
                </span>

                <span className="text-muted-foreground shrink-0 text-xs">
                  {format.dateTime(new Date(movement.createdAt), 'stamp')}
                  {movement.authorName ? ` · ${movement.authorName}` : ''}
                </span>
              </div>
            ))
          )}
        </CardContent>
      </Card>

      {canAdjust && adjusting ? (
        <StockAdjustDialog
          products={products}
          agencies={agencies}
          productId={current.productId}
          agencyId={current.agencyId}
          onClose={() => setAdjusting(false)}
          onDone={setCurrent}
        />
      ) : null}
    </div>
  );
}
