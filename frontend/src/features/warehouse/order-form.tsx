'use client';

import {
  type City,
  type Client,
  DELIVERY_ADDRESS_MIN,
  type District,
  normalizePhone,
  type OrderDetail,
  PARCEL_TEXT_MIN,
  PHONE_PATTERN,
  type ProductListItem,
} from '@delivery/shared';
import { Plus, Trash2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { CitySelect } from '@/components/city-select';
import { SearchableSelect } from '@/components/searchable-select';
import { DistrictInput } from '@/features/parcels/district-input';
import { FormSheet } from '@/features/referential/form-sheet';
import { useWarehouseError } from '@/features/warehouse/use-warehouse-error';
import { createOrderAction } from '@/lib/actions/warehouse';

interface Line {
  productId: string;
  quantity: number;
}

/**
 * Saisie manuelle d'une commande.
 *
 * La **référence externe** est facultative mais recommandée : c'est elle qui
 * porte l'idempotence avec le client, et qui permettra plus tard de retrouver
 * la commande dans la boutique d'origine. Les prix ne se saisissent pas ligne
 * par ligne : ils viennent du catalogue et sont GELÉS à la création — le
 * montant à encaisser ne doit pas suivre une variation de prix survenue après.
 */
export function OrderForm({
  clients,
  cities,
  districts,
  products,
  lockedClientId,
  onDone,
}: {
  clients: Client[];
  cities: City[];
  districts: District[];
  products: ProductListItem[];
  lockedClientId?: string;
  onDone: (order?: OrderDetail) => void;
}) {
  const t = useTranslations('warehouse.orders');
  const tCommon = useTranslations('common');
  const errorMessage = useWarehouseError();

  const [clientId, setClientId] = useState(lockedClientId ?? '');
  const [externalRef, setExternalRef] = useState('');
  const [recipientName, setRecipientName] = useState('');
  const [recipientPhone, setRecipientPhone] = useState('');
  const [cityId, setCityId] = useState('');
  const [districtName, setDistrictName] = useState('');
  const [address, setAddress] = useState('');
  const [lines, setLines] = useState<Line[]>([]);
  const [draftProduct, setDraftProduct] = useState('');
  const [draftQuantity, setDraftQuantity] = useState('1');
  const [saving, setSaving] = useState(false);

  const selectable = products.filter((product) => clientId === '' || product.clientId === clientId);
  const listed = new Set(lines.map((line) => line.productId));

  const total = lines.reduce((sum, line) => {
    const product = products.find((entry) => entry.id === line.productId);
    return sum + (product?.price ?? 0) * line.quantity;
  }, 0);

  const valid =
    clientId !== '' &&
    recipientName.trim().length >= PARCEL_TEXT_MIN &&
    // Lot L (D-220) : la règle du serveur — séparateurs retirés, puis chiffres et « + ».
    PHONE_PATTERN.test(normalizePhone(recipientPhone)) &&
    cityId !== '' &&
    address.trim().length >= DELIVERY_ADDRESS_MIN &&
    lines.length > 0;

  const addLine = () => {
    const quantity = Number(draftQuantity);
    if (!draftProduct || !Number.isInteger(quantity) || quantity < 1) return;

    setLines((current) => [...current, { productId: draftProduct, quantity }]);
    setDraftProduct('');
    setDraftQuantity('1');
  };

  const submit = async () => {
    setSaving(true);
    const result = await createOrderAction({
      clientId,
      agencyId: '',
      source: 'manual',
      externalRef,
      recipientName: recipientName.trim(),
      // Lot D, D1 : tel que saisi (le contrôle refuse déjà tout espace), jamais reformaté.
      recipientPhone: recipientPhone.trim(),
      destinationCityId: cityId,
      destinationDistrictName: districtName.trim(),
      deliveryAddress: address.trim(),
      note: '',
      lines: lines.map((line) => ({ ...line, unitPrice: null })),
    });
    setSaving(false);

    if (result.ok) {
      toast.success(t('created', { code: result.data.code }));
      onDone(result.data);
      return;
    }

    toast.error(errorMessage(result));
  };

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={t('new')}
      description={t('formHint')}
      onSubmit={submit}
      submitting={saving || !valid}
    >
      <div className="flex flex-col gap-4">
        {lockedClientId ? null : (
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="order-client">{t('fields.client')}</Label>
            <SearchableSelect
              options={clients.map((client) => ({ value: client.id, label: client.name }))}
              value={clientId}
              onValueChange={(value) => {
                setClientId(value);
                setLines([]);
              }}
              placeholder={t('fields.client')}
              id="order-client"
            />
          </div>
        )}

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="order-ref">{t('fields.externalRef')}</Label>
          <Input
            id="order-ref"
            value={externalRef}
            onChange={(event) => setExternalRef(event.target.value)}
          />
          <p className="text-muted-foreground text-xs">{t('fields.externalRefHint')}</p>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="order-recipient">{t('fields.recipientName')}</Label>
            <Input
              id="order-recipient"
              value={recipientName}
              onChange={(event) => setRecipientName(event.target.value)}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="order-phone">{t('fields.recipientPhone')}</Label>
            <Input
              id="order-phone"
              inputMode="tel"
              value={recipientPhone}
              onChange={(event) => setRecipientPhone(event.target.value)}
            />
          </div>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="order-city">{t('fields.city')}</Label>
            <CitySelect
              id="order-city"
              cities={cities}
              value={cityId}
              // Le quartier saisi reste : c'est un texte, le serveur le
              // rapproche des quartiers de la ville finalement choisie.
              onValueChange={setCityId}
              placeholder={t('fields.city')}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="order-district">{t('fields.district')}</Label>
            <DistrictInput
              id="order-district"
              value={districtName}
              onChange={(event) => setDistrictName(event.target.value)}
              districts={districts}
              cityId={cityId}
              placeholder={t('fields.districtPlaceholder')}
            />
          </div>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="order-address">{t('fields.address')}</Label>
          <Input
            id="order-address"
            value={address}
            onChange={(event) => setAddress(event.target.value)}
          />
        </div>

        <div className="flex flex-col gap-2 rounded-lg border p-3">
          <p className="text-sm font-medium">{t('addLine')}</p>

          <div className="flex flex-col gap-2 sm:flex-row">
            <SearchableSelect
              options={selectable
                .filter((product) => !listed.has(product.id))
                .map((product) => ({
                  value: product.id,
                  label: `${product.reference ? `${product.reference}, ` : ''}${product.name}`,
                  keywords: [product.reference],
                }))}
              value={draftProduct}
              onValueChange={setDraftProduct}
              placeholder={t('fields.product')}
              className="min-w-0 flex-1"
            />

            <Input
              type="number"
              min={1}
              step={1}
              className="sm:w-24"
              value={draftQuantity}
              onChange={(event) => setDraftQuantity(event.target.value)}
            />

            <Button type="button" variant="outline" onClick={addLine} disabled={!draftProduct}>
              <Plus className="size-4" />
            </Button>
          </div>

          {lines.length === 0 ? (
            <p className="text-muted-foreground text-xs">{t('noLinesHint')}</p>
          ) : (
            <>
              {lines.map((line) => {
                const product = products.find((entry) => entry.id === line.productId);
                return (
                  <div key={line.productId} className="flex items-center gap-2 text-sm">
                    <span className="min-w-0 flex-1 truncate">
                      {product?.name ?? line.productId}
                    </span>
                    <span className="tabular-nums">×{line.quantity}</span>
                    <Button
                      type="button"
                      size="icon"
                      variant="ghost"
                      aria-label={tCommon('delete')}
                      onClick={() =>
                        setLines((current) =>
                          current.filter((entry) => entry.productId !== line.productId),
                        )
                      }
                    >
                      <Trash2 className="text-destructive size-4" />
                    </Button>
                  </div>
                );
              })}

              <div className="flex items-center justify-between border-t pt-2 text-sm font-medium">
                <span>{t('total')}</span>
                <span className="tabular-nums">
                  {Math.floor(total / 100)},{String(total % 100).padStart(2, '0')}
                </span>
              </div>
            </>
          )}

          <p className="text-muted-foreground text-xs">{t('fields.unitPriceHint')}</p>
        </div>
      </div>
    </FormSheet>
  );
}
