'use client';

import { PARCEL_TEXT_MIN } from '@delivery/shared';
import { Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { toast } from 'sonner';
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
import { Textarea } from '@/components/ui/textarea';
import { createExchangeAction } from '@/lib/actions/exchanges';

/**
 * OUVRIR UN ÉCHANGE — chantier « Finance », point 6.
 *
 * **Le colis d'origine se saisit par son CODE.** C'est ce que l'agent a sous
 * les yeux — sur le carton, dans le message du client, dans sa liste. Lui
 * demander de le choisir dans une liste déroulante de trois mille lignes aurait
 * été une recherche déguisée en formulaire.
 *
 * **La destination est FACULTATIVE.** Vide, le colis neuf repart à la même
 * porte : c'est le cas courant, et le taire ferait ressaisir une adresse qu'on
 * a déjà. Renseignée, elle décrit la nouvelle — le client a déménagé, ou
 * demande la livraison au bureau.
 *
 * **Le montant peut être nul, et même négatif.** Un échange à somme nulle est
 * l'ordinaire ; un échange contre un article moins cher rend de l'argent
 * (D-101).
 */
export function ExchangeForm({
  onDone,
  onCreated,
}: {
  onDone: () => void;
  onCreated: (code: string) => void;
}) {
  const t = useTranslations('exchanges.form');
  const tCommon = useTranslations('common');

  const [originalParcelCode, setOriginalParcelCode] = useState('');
  const [returnedProduct, setReturnedProduct] = useState('');
  const [newProduct, setNewProduct] = useState('');
  const [deliveryAddress, setDeliveryAddress] = useState('');
  const [districtName, setDistrictName] = useState('');
  const [amount, setAmount] = useState('0');
  const [fees, setFees] = useState('0');
  const [note, setNote] = useState('');
  const [saving, setSaving] = useState(false);

  const submit = async () => {
    setSaving(true);

    const result = await createExchangeAction({
      originalParcelCode: originalParcelCode.trim().toUpperCase(),
      returnedProduct: returnedProduct.trim(),
      newProduct: newProduct.trim(),
      deliveryAddress: deliveryAddress.trim(),
      // Texte libre, facultatif — chantier « Quartier en saisie libre ».
      destinationDistrictName: districtName.trim(),
      amount: Math.round(Number(amount) * 100),
      fees: Math.round(Number(fees) * 100),
      note: note.trim(),
    });

    setSaving(false);

    if (result.ok) {
      toast.success(t('created', { code: result.data.code }));
      onDone();
      onCreated(result.data.code);
      return;
    }

    toast.error(result.error);
  };

  const incomplete =
    originalParcelCode.trim() === '' ||
    returnedProduct.trim().length < PARCEL_TEXT_MIN ||
    newProduct.trim().length < PARCEL_TEXT_MIN;

  return (
    <Dialog open onOpenChange={(next) => (next ? null : onDone())}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{t('title')}</DialogTitle>
          <DialogDescription>{t('hint')}</DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="originalParcelCode">{t('originalParcel')}</Label>
            <Input
              id="originalParcelCode"
              value={originalParcelCode}
              autoComplete="off"
              spellCheck={false}
              className="font-mono"
              placeholder={t('originalParcelPlaceholder')}
              onChange={(event) => setOriginalParcelCode(event.target.value)}
            />
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="returnedProduct">{t('returnedProduct')}</Label>
              <Input
                id="returnedProduct"
                value={returnedProduct}
                onChange={(event) => setReturnedProduct(event.target.value)}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="newProduct">{t('newProduct')}</Label>
              <Input
                id="newProduct"
                value={newProduct}
                onChange={(event) => setNewProduct(event.target.value)}
              />
            </div>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="deliveryAddress">{t('deliveryAddress')}</Label>
            <Input
              id="deliveryAddress"
              value={deliveryAddress}
              placeholder={t('deliveryAddressPlaceholder')}
              onChange={(event) => setDeliveryAddress(event.target.value)}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="exchange-district">{t('district')}</Label>
            <Input
              id="exchange-district"
              value={districtName}
              maxLength={120}
              placeholder={t('districtPlaceholder')}
              onChange={(event) => setDistrictName(event.target.value)}
            />
          </div>

          <div className="grid gap-3 sm:grid-cols-2">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="exchange-amount">{t('amount')}</Label>
              <Input
                id="exchange-amount"
                type="number"
                step="0.01"
                value={amount}
                onChange={(event) => setAmount(event.target.value)}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="exchange-fees">{t('fees')}</Label>
              <Input
                id="exchange-fees"
                type="number"
                min={0}
                step="0.01"
                value={fees}
                onChange={(event) => setFees(event.target.value)}
              />
            </div>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="exchange-note">{t('note')}</Label>
            <Textarea
              id="exchange-note"
              rows={2}
              value={note}
              onChange={(event) => setNote(event.target.value)}
            />
          </div>
        </div>

        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={onDone} disabled={saving}>
            {tCommon('cancel')}
          </Button>
          <Button onClick={submit} disabled={saving || incomplete} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : null}
            {t('submit')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
