'use client';

import type {
  Client,
  ProductAttributeItem,
  ProductDetail as ProductDetailData,
} from '@delivery/shared';
import { ImageIcon, Loader2, Package, Pencil, Upload } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useRef, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge } from '@/components/data-table/cells';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ProductForm } from '@/features/warehouse/product-form';
import { useWarehouseError } from '@/features/warehouse/use-warehouse-error';
import { setProductImageAction } from '@/lib/actions/warehouse';

const ACCEPTED = ['image/jpeg', 'image/png', 'image/webp'];

/**
 * Fiche produit : identité, image, attributs, et stock PAR AGENCE.
 *
 * Le stock affiché ici est le seul vrai : la somme des `StockItem`. Le champ
 * `internalStock` hérité du legacy apparaît dans le formulaire, nommé pour ce
 * qu'il est — il valait NULL sur les 745 produits de production.
 */
export function ProductDetailScreen({
  product,
  clients,
  attributes,
  lockedClientId,
}: {
  product: ProductDetailData;
  clients: Client[];
  attributes: ProductAttributeItem[];
  lockedClientId?: string;
}) {
  const t = useTranslations('warehouse.products');
  const tStock = useTranslations('warehouse.stock');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const errorMessage = useWarehouseError();

  const inputRef = useRef<HTMLInputElement>(null);
  const [current, setCurrent] = useState(product);
  const [editing, setEditing] = useState(false);
  const [pending, startTransition] = useTransition();

  const upload = (file: File) => {
    if (!ACCEPTED.includes(file.type)) {
      toast.error(t('imageHint'));
      return;
    }

    startTransition(async () => {
      const bytes = new Uint8Array(await file.arrayBuffer());
      let binary = '';
      for (let index = 0; index < bytes.length; index += 8192) {
        binary += String.fromCharCode(...bytes.subarray(index, index + 8192));
      }

      const result = await setProductImageAction(current.id, {
        name: file.name,
        mimeType: file.type,
        content: btoa(binary),
      });

      if (result.ok) {
        setCurrent(result.data);
        toast.success(t('updated', { name: current.name }));
      } else toast.error(errorMessage(result));

      if (inputRef.current) inputRef.current.value = '';
    });
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        {current.isActive ? (
          <CellBadge label={tCommon('active')} color="var(--chart-4)" />
        ) : (
          <CellBadge label={tCommon('inactive')} />
        )}

        <span className="text-muted-foreground text-sm">
          {current.clientName}
          {current.reference ? ` · ${current.reference}` : ''}
        </span>

        <Button
          size="sm"
          variant="outline"
          className="ms-auto gap-1.5"
          onClick={() => setEditing(true)}
        >
          <Pencil className="size-4" />
          {tCommon('edit')}
        </Button>
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-1">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <ImageIcon className="size-4" />
              {t('image')}
            </CardTitle>
            <CardDescription>{t('imageHint')}</CardDescription>
          </CardHeader>

          <CardContent className="flex flex-col gap-3">
            {current.imageUrl ? (
              // eslint-disable-next-line @next/next/no-img-element -- image servie par l'API, dimensions inconnues
              <img
                src={current.imageUrl}
                alt={current.name}
                className="bg-muted max-h-56 w-full rounded-lg object-contain"
              />
            ) : (
              <div className="bg-muted text-muted-foreground flex h-40 items-center justify-center rounded-lg text-sm">
                {t('image')}
              </div>
            )}

            <input
              ref={inputRef}
              type="file"
              className="hidden"
              accept={ACCEPTED.join(',')}
              onChange={(event) => {
                const file = event.target.files?.[0];
                if (file) upload(file);
              }}
            />

            <Button
              variant="outline"
              className="gap-1.5"
              disabled={pending}
              onClick={() => inputRef.current?.click()}
            >
              {pending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <Upload className="size-4" />
              )}
              {t('imageUpload')}
            </Button>
          </CardContent>
        </Card>

        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <Package className="size-4" />
              {current.name}
            </CardTitle>
          </CardHeader>

          <CardContent className="grid gap-x-6 gap-y-3 text-sm sm:grid-cols-2">
            <Field label={t('fields.price')}>
              {current.price === null ? '-' : format.number(current.price / 100, 'currency')}
            </Field>
            <Field label={t('fields.declaredValue')}>
              {current.declaredValue === null
                ? '-'
                : format.number(current.declaredValue / 100, 'currency')}
            </Field>
            <Field label={t('fields.dimensions')}>
              {current.lengthMm === null && current.widthMm === null && current.heightMm === null
                ? '-'
                : `${current.lengthMm ?? '-'} × ${current.widthMm ?? '-'} × ${current.heightMm ?? '-'}`}
            </Field>
            <Field label={t('fields.weight')}>{current.weightG ?? '-'}</Field>

            {current.attributes.length > 0 ? (
              <div className="sm:col-span-2">
                <p className="text-muted-foreground mb-1 text-xs font-semibold uppercase">
                  {t('attributes')}
                </p>
                <div className="flex flex-wrap gap-1.5">
                  {current.attributes.map((entry) => (
                    <CellBadge key={entry.id} label={`${entry.attributeName} : ${entry.value}`} />
                  ))}
                </div>
              </div>
            ) : null}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('stockByAgency')}</CardTitle>
          <CardDescription>{t('internalStockHint')}</CardDescription>
        </CardHeader>

        <CardContent className="flex flex-col gap-2">
          {current.stock.length === 0 ? (
            <p className="text-muted-foreground text-sm">{t('noStock')}</p>
          ) : (
            current.stock.map((line) => (
              <div
                key={line.agencyId}
                className="flex items-center gap-3 rounded-lg border p-3 text-sm"
              >
                <span className="min-w-0 flex-1 truncate">{line.agencyName}</span>
                {line.lowStock ? (
                  <CellBadge label={tStock('lowStock')} color="var(--chart-3)" />
                ) : null}
                <span className="tabular-nums">{line.quantity}</span>
              </div>
            ))
          )}
        </CardContent>
      </Card>

      {editing ? (
        <ProductForm
          product={current}
          clients={clients}
          attributes={attributes}
          lockedClientId={lockedClientId}
          onDone={(updated) => {
            if (updated) setCurrent(updated);
            setEditing(false);
          }}
        />
      ) : null}
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="text-muted-foreground text-xs font-semibold uppercase">{label}</p>
      <p className="mt-0.5">{children}</p>
    </div>
  );
}
