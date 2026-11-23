'use client';

import type { ClientDetail as ClientDetailData } from '@delivery/shared';
import { Truck } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { setClientPickupTermsAction } from '@/lib/actions/accounts';
import { useRouter } from '@/i18n/navigation';

/** Les valeurs globales (Paramètres > Finance) ; `null` si non lisibles. */
export interface PickupDefaults {
  /** Centimes. */
  fee: number;
  freeFrom: number;
}

/**
 * LE BLOC « RAMASSAGE » DE L'ONGLET « TARIFS & REMISES » — lot T (D-232).
 *
 * Deux champs, vides = la valeur globale (affichée en grisé), `0` autorisé
 * (gratuit). L'administration seule les écrit (`finance.validate`, route
 * dédiée journalisée) ; les autres lisent les conditions effectives. Changer
 * ces valeurs n'agit que sur les BR- pas encore validés.
 */
export function ClientPickupTerms({
  client,
  defaults,
  canEdit,
}: {
  client: ClientDetailData;
  defaults: PickupDefaults | null;
  canEdit: boolean;
}) {
  const t = useTranslations('accounts.clients.pickupTerms');
  const format = useFormatter();
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  const [fee, setFee] = useState(client.pickupFee === null ? '' : String(client.pickupFee / 100));
  const [freeFrom, setFreeFrom] = useState(
    client.freePickupThreshold === null ? '' : String(client.freePickupThreshold),
  );

  const money = (cents: number) => format.number(cents / 100, 'currency');
  const effectiveFee = client.pickupFee ?? defaults?.fee ?? null;
  const effectiveFreeFrom = client.freePickupThreshold ?? defaults?.freeFrom ?? null;

  const parsedFee = fee.trim() === '' ? null : Number(fee.replace(',', '.'));
  const parsedFreeFrom = freeFrom.trim() === '' ? null : Number(freeFrom);
  const invalid =
    (parsedFee !== null && (!Number.isFinite(parsedFee) || parsedFee < 0)) ||
    (parsedFreeFrom !== null && (!Number.isInteger(parsedFreeFrom) || parsedFreeFrom < 0));

  const save = () => {
    if (invalid) return;
    startTransition(async () => {
      const result = await setClientPickupTermsAction(client.id, {
        pickupFee: parsedFee === null ? null : Math.round(parsedFee * 100),
        freePickupThreshold: parsedFreeFrom,
      });
      if (result.ok) {
        toast.success(t('saved'));
        router.refresh();
      } else toast.error(result.error);
    });
  };

  const summary =
    effectiveFee === null || effectiveFreeFrom === null
      ? null
      : effectiveFee === 0 || effectiveFreeFrom === 0
        ? t('summaryFree')
        : t('summary', { fee: money(effectiveFee), threshold: String(effectiveFreeFrom) });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <Truck className="size-4" aria-hidden />
          {t('title')}
        </CardTitle>
        <CardDescription>{t('hint')}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="client-pickup-fee">{t('fee')}</Label>
            {canEdit ? (
              <Input
                id="client-pickup-fee"
                inputMode="decimal"
                value={fee}
                onChange={(event) => setFee(event.target.value)}
                placeholder={defaults ? t('global', { value: money(defaults.fee) }) : ''}
                disabled={pending}
              />
            ) : (
              <p id="client-pickup-fee" className="text-sm">
                {client.pickupFee === null
                  ? defaults
                    ? t('global', { value: money(defaults.fee) })
                    : '—'
                  : money(client.pickupFee)}
              </p>
            )}
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="client-pickup-free-from">{t('freeFrom')}</Label>
            {canEdit ? (
              <Input
                id="client-pickup-free-from"
                inputMode="numeric"
                value={freeFrom}
                onChange={(event) => setFreeFrom(event.target.value)}
                placeholder={defaults ? t('global', { value: String(defaults.freeFrom) }) : ''}
                disabled={pending}
              />
            ) : (
              <p id="client-pickup-free-from" className="text-sm">
                {client.freePickupThreshold === null
                  ? defaults
                    ? t('global', { value: String(defaults.freeFrom) })
                    : '—'
                  : String(client.freePickupThreshold)}
              </p>
            )}
          </div>
        </div>

        {summary ? <p className="text-muted-foreground text-xs">{summary}</p> : null}
        {invalid ? <p className="text-destructive text-xs">{t('invalid')}</p> : null}

        {canEdit ? (
          <div className="flex justify-end">
            <Button size="sm" disabled={pending || invalid} onClick={save}>
              {t('save')}
            </Button>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}
