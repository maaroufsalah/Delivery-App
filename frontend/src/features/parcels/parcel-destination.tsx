'use client';

import type { City, District, ParcelDetail } from '@delivery/shared';
import { Loader2, MapPinned } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
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
import { CitySelect } from '@/components/city-select';
import { useRouteError } from '@/features/routes/use-route-error';
import { changeDestinationAction } from '@/lib/actions/parcels';
import { DistrictInput } from './district-input';

/** Obligatoire, sans longueur minimale (12/11) : comme le schéma, seul le vide est refusé. */
const MIN_REASON = 1;

/**
 * CHANGER LA DESTINATION — chantier « Finance », point 7.
 *
 * **Le motif est obligatoire**, comme pour les frais (D-063) : une adresse qui
 * change sans justification est indéfendable devant l'e-commerçant dont le
 * colis a fait deux jours de plus.
 *
 * **Les champs sont préremplis et tous facultatifs.** On corrige le plus
 * souvent un seul élément — un étage manquant, un numéro mal relevé — et
 * imposer de tout ressaisir aurait fabriqué des erreurs de recopie.
 *
 * **La sortie de circuit est ANNONCÉE.** Quand la ville change, le colis quitte
 * les tournées où il attendait ; le serveur le dit dans sa réponse, et l'écran
 * le répète. Le taire aurait laissé l'agent croire que le livreur l'a encore.
 */
export function ParcelDestinationEditor({
  parcel,
  cities,
  districts = [],
  canEdit,
}: {
  parcel: ParcelDetail;
  cities: City[];
  /** Quartiers connus, SUGGÉRÉS — chantier « Quartier en saisie libre ». */
  districts?: District[];
  canEdit: boolean;
}) {
  const t = useTranslations('parcels.destination');
  const tCommon = useTranslations('common');
  // Lot L-bis (D-221) : facturé, remis, verrouillé — les refus du transfert, traduits.
  const translateError = useRouteError();

  const [open, setOpen] = useState(false);
  const [recipientName, setRecipientName] = useState(parcel.recipientName);
  const [recipientPhone, setRecipientPhone] = useState(parcel.recipientPhone);
  const [deliveryAddress, setDeliveryAddress] = useState(parcel.deliveryAddress);
  const [destinationCityId, setDestinationCityId] = useState(parcel.destinationCityId);
  const initialDistrict = parcel.destinationDistrictName ?? '';
  const [districtName, setDistrictName] = useState(initialDistrict);
  const [instructions, setInstructions] = useState('');
  const [reason, setReason] = useState('');
  const [pending, startTransition] = useTransition();

  if (!canEdit) return null;

  const submit = () => {
    startTransition(async () => {
      const result = await changeDestinationAction(parcel.code, {
        recipientName: recipientName.trim(),
        recipientPhone: recipientPhone.trim(),
        deliveryAddress: deliveryAddress.trim(),
        destinationCityId,
        /*
         * Le quartier n'est envoyé que s'il a CHANGÉ : renvoyer le libellé lu
         * sur la fiche le recopierait en texte sur un colis qui n'avait qu'un
         * lien vers le référentiel. `''` le retire.
         */
        ...(districtName.trim() !== initialDistrict.trim()
          ? { destinationDistrictName: districtName.trim() }
          : {}),
        instructions: instructions.trim(),
        reason: reason.trim(),
      });

      if (!result.ok) {
        toast.error(translateError(result));
        return;
      }

      toast.success(t('saved'));

      /*
        L'alerte est un toast À PART, et volontairement : elle ne dit pas que
        le geste a réussi, elle dit une CONSÉQUENCE que l'agent doit traiter.
      */
      const dropped = result.data.droppedFromRoutes ?? [];
      if (dropped.length > 0) {
        toast.warning(t('droppedFromRoutes', { routes: dropped.join(', ') }));
      }

      const repricedTo = result.data.repricedTo;
      if (repricedTo !== null && repricedTo !== undefined) {
        toast.info(
          t('repriced', {
            from: ((result.data.repricedFrom ?? 0) / 100).toFixed(2),
            to: (repricedTo / 100).toFixed(2),
          }),
        );
      }

      setOpen(false);
      setReason('');
      setInstructions('');
    });
  };

  return (
    <>
      <Button
        type="button"
        size="sm"
        variant="outline"
        className="w-fit gap-1.5"
        onClick={() => setOpen(true)}
      >
        <MapPinned className="size-4" aria-hidden />
        {t('edit')}
      </Button>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{t('edit')}</DialogTitle>
            <DialogDescription>{t('hint')}</DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-3">
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="dest-name">{t('recipientName')}</Label>
                <Input
                  id="dest-name"
                  value={recipientName}
                  onChange={(event) => setRecipientName(event.target.value)}
                />
              </div>

              <div className="flex flex-col gap-1.5">
                <Label htmlFor="dest-phone">{t('recipientPhone')}</Label>
                <Input
                  id="dest-phone"
                  value={recipientPhone}
                  onChange={(event) => setRecipientPhone(event.target.value)}
                />
                {/* D-244 : un à trois numéros dans le même champ. */}
                <p className="text-muted-foreground text-xs">{t('recipientPhoneHint')}</p>
              </div>
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="dest-city">{t('city')}</Label>
              <CitySelect
                id="dest-city"
                cities={cities}
                value={destinationCityId}
                onValueChange={setDestinationCityId}
              />
              {destinationCityId !== parcel.destinationCityId ? (
                <p className="text-muted-foreground text-xs">{t('cityWarning')}</p>
              ) : null}
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="dest-district">{t('district')}</Label>
              <DistrictInput
                id="dest-district"
                value={districtName}
                onChange={(event) => setDistrictName(event.target.value)}
                districts={districts}
                cityId={destinationCityId}
                placeholder={t('districtPlaceholder')}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="dest-address">{t('address')}</Label>
              <Input
                id="dest-address"
                value={deliveryAddress}
                onChange={(event) => setDeliveryAddress(event.target.value)}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="dest-instructions">{t('instructions')}</Label>
              <Input
                id="dest-instructions"
                value={instructions}
                placeholder={t('instructionsPlaceholder')}
                onChange={(event) => setInstructions(event.target.value)}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="dest-reason">{t('reason')}</Label>
              <Textarea
                id="dest-reason"
                rows={2}
                value={reason}
                placeholder={t('reasonPlaceholder')}
                onChange={(event) => setReason(event.target.value)}
              />
            </div>
          </div>

          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => setOpen(false)} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            <Button
              onClick={submit}
              disabled={pending || reason.trim().length < MIN_REASON}
              className="gap-1.5"
            >
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {tCommon('save')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
