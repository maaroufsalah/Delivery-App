'use client';

import {
  type Client,
  type ProductAttributeItem,
  type ProductDetail,
  type ProductInput,
  type ProductPayload,
  productSchema,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { SearchableSelect } from '@/components/searchable-select';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { FormSheet } from '@/features/referential/form-sheet';
import { saveProductAction } from '@/lib/actions/warehouse';

/**
 * Fiche produit.
 *
 * Les dimensions se saisissent en MILLIMÈTRES et le poids en GRAMMES, entiers.
 * Le legacy stockait des décimaux en centimètres et en kilos, ce qui donnait
 * des `0.001` kg sur les petits articles et des arrondis différents selon
 * l'écran. Un entier ne s'arrondit pas.
 */
export function ProductForm({
  product,
  clients,
  attributes,
  lockedClientId,
  onDone,
}: {
  product: ProductDetail | null;
  clients: Client[];
  attributes: ProductAttributeItem[];
  /** Renseigné pour un compte e-commerçant : il ne choisit pas. */
  lockedClientId?: string;
  onDone: (product?: ProductDetail) => void;
}) {
  const t = useTranslations('warehouse.products');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);

  const form = useForm<ProductInput, unknown, ProductPayload>({
    resolver: zodResolver(productSchema),
    defaultValues: {
      clientId: product?.clientId ?? lockedClientId ?? '',
      reference: product?.reference ?? '',
      name: product?.name ?? '',
      lengthMm: product?.lengthMm ?? null,
      widthMm: product?.widthMm ?? null,
      heightMm: product?.heightMm ?? null,
      weightG: product?.weightG ?? null,
      // Lus en centimes, saisis en dirhams : la conversion reste à la frontière.
      price: product?.price == null ? null : product.price / 100,
      declaredValue: product?.declaredValue == null ? null : product.declaredValue / 100,
      internalStock: product?.internalStock ?? null,
      attributeValueIds: product?.attributes.map((entry) => entry.id) ?? [],
      isActive: product?.isActive ?? true,
    },
    mode: 'onBlur',
  });

  const selected = form.watch('attributeValueIds') ?? [];

  const toggleValue = (valueId: string, checked: boolean) => {
    const next = checked
      ? [...new Set([...selected, valueId])]
      : selected.filter((entry) => entry !== valueId);
    form.setValue('attributeValueIds', next, { shouldDirty: true });
  };

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await saveProductAction(product?.id ?? null, values);
    setSaving(false);

    if (result.ok) {
      toast.success(
        product ? t('updated', { name: values.name }) : t('created', { name: values.name }),
      );
      onDone(result.data);
      return;
    }

    if (result.field === 'reference') form.setError('reference', { message: result.error });
    else toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
  });

  const numberField = (
    name: 'lengthMm' | 'widthMm' | 'heightMm' | 'weightG' | 'internalStock',
    label: string,
  ) => (
    <FormField
      control={form.control}
      name={name}
      render={({ field }) => (
        <FormItem>
          <FormLabel>{label}</FormLabel>
          <FormControl>
            <Input
              type="number"
              min={0}
              step={1}
              value={field.value ?? ''}
              onChange={(event) =>
                field.onChange(event.target.value === '' ? null : event.target.valueAsNumber)
              }
            />
          </FormControl>
          <FormMessage />
        </FormItem>
      )}
    />
  );

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={product ? t('edit') : t('new')}
      description={t('formHint')}
      onSubmit={submit}
      submitting={saving}
    >
      <Form {...form}>
        <form className="flex flex-col gap-4" onSubmit={submit}>
          {lockedClientId ? null : (
            <FormField
              control={form.control}
              name="clientId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.client')}</FormLabel>
                  <FormControl>
                    <SearchableSelect
                      options={clients.map((client) => ({ value: client.id, label: client.name }))}
                      value={field.value}
                      onValueChange={field.onChange}
                      placeholder={t('fields.client')}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          )}

          <div className="grid grid-cols-2 gap-3">
            <FormField
              control={form.control}
              name="reference"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.reference')}</FormLabel>
                  <FormControl>
                    <Input {...field} className="font-mono" />
                  </FormControl>
                  <FormDescription>{t('fields.referenceHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.name')}</FormLabel>
                  <FormControl>
                    <Input {...field} autoFocus />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <div>
            <p className="mb-2 text-sm font-medium">{t('fields.dimensions')}</p>
            <div className="grid grid-cols-3 gap-3">
              {numberField('lengthMm', t('fields.length'))}
              {numberField('widthMm', t('fields.width'))}
              {numberField('heightMm', t('fields.height'))}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            {numberField('weightG', t('fields.weight'))}
            {numberField('internalStock', t('fields.internalStock'))}
          </div>

          <div className="grid grid-cols-2 gap-3">
            <FormField
              control={form.control}
              name="price"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.price')}</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      min={0}
                      step="0.01"
                      value={field.value ?? ''}
                      onChange={(event) =>
                        field.onChange(
                          event.target.value === '' ? null : event.target.valueAsNumber,
                        )
                      }
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="declaredValue"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.declaredValue')}</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      min={0}
                      step="0.01"
                      value={field.value ?? ''}
                      onChange={(event) =>
                        field.onChange(
                          event.target.value === '' ? null : event.target.valueAsNumber,
                        )
                      }
                    />
                  </FormControl>
                  <FormDescription>{t('fields.declaredValueHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          {attributes.length === 0 ? null : (
            <FormField
              control={form.control}
              name="attributeValueIds"
              render={() => (
                <FormItem>
                  <FormLabel>{t('attributes')}</FormLabel>
                  <FormDescription>{t('attributesHint')}</FormDescription>
                  <div className="flex flex-col gap-3 rounded-lg border p-3">
                    {attributes.map((attribute) => (
                      <div key={attribute.id} className="flex flex-col gap-1">
                        <p className="text-muted-foreground text-xs font-semibold uppercase">
                          {attribute.name}
                        </p>
                        <div className="flex flex-wrap gap-2">
                          {attribute.values.map((value) => (
                            <label
                              key={value.id}
                              className="hover:bg-accent/60 flex cursor-pointer items-center gap-1.5 rounded-md border px-2 py-1 text-sm"
                            >
                              <Checkbox
                                checked={selected.includes(value.id)}
                                onCheckedChange={(checked) =>
                                  toggleValue(value.id, checked === true)
                                }
                              />
                              {value.value}
                            </label>
                          ))}
                        </div>
                      </div>
                    ))}
                  </div>
                  <FormMessage />
                </FormItem>
              )}
            />
          )}

          <FormField
            control={form.control}
            name="isActive"
            render={({ field }) => (
              <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                <div className="flex flex-col gap-0.5">
                  <FormLabel>{t('fields.active')}</FormLabel>
                  <FormDescription>{t('fields.activeHint')}</FormDescription>
                </div>
                <FormControl>
                  <Switch checked={field.value} onCheckedChange={field.onChange} />
                </FormControl>
              </FormItem>
            )}
          />
        </form>
      </Form>
    </FormSheet>
  );
}
