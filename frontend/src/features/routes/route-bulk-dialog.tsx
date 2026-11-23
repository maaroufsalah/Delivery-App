'use client';

import { isBulkJobAccepted } from '@delivery/shared';
import {
  allowedTransitions,
  findParcelStatus,
  type RouteParcel,
  routeExpectedOutcome,
  routeTargetsSender,
} from '@delivery/shared';
import { Loader2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
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
import { SearchableSelect } from '@/components/searchable-select';
import { useRouteError } from '@/features/routes/use-route-error';
import { bulkConfirmRouteAction } from '@/lib/actions/routes';

const DELIVERED = 4;
const POSTPONED = 14;
const PICKED_UP_BY_COURIER = 18;

/**
 * LE MÊME RÉSULTAT POUR LES COLIS COCHÉS — chantier « Sélection en masse sur
 * les circuits » (D-146).
 *
 * **Ramassage** : un seul geste, « ramassés par le livreur » (18) ; la clôture
 * fait le reste (18 → Ramassé, non ramassés retirés, D-145).
 *
 * **Livraison** : un résultat COMMUN à tous les colis cochés — l'intersection
 * des issues que la machine à états permet à chacun, « Livré » en tête. « Livré »
 * encaisse en espèces : la boîte annonce le total attendu, et un colis attendu
 * en chèque se livre seul (le serveur le refuse et le nomme).
 *
 * Le serveur rejoue l'action unitaire colis par colis, dans une transaction :
 * ce qu'on voit ici n'est qu'une annonce, la règle reste la sienne.
 */
export function RouteBulkDialog({
  routeCode,
  kind,
  parcels,
  open,
  onOpenChange,
  onDone,
}: {
  routeCode: string;
  kind: string;
  parcels: RouteParcel[];
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const t = useTranslations('routes.bulk');
  const tStatus = useTranslations('status');
  const format = useFormatter();
  const translateError = useRouteError();

  const pickup = routeTargetsSender(kind) && kind === 'pickup';

  // Les issues permises à TOUS les colis cochés, celle du type en tête.
  const expected = routeExpectedOutcome(kind);
  const common = parcels.reduce<number[] | null>((acc, parcel) => {
    const allowed = allowedTransitions(parcel.statusId);
    return acc === null ? allowed : acc.filter((id) => allowed.includes(id));
  }, null);
  const outcomes = (common ?? []).sort((a, b) => Number(b === expected) - Number(a === expected));

  const [statusId, setStatusId] = useState<string>(
    pickup ? String(PICKED_UP_BY_COURIER) : outcomes.includes(DELIVERED) ? String(DELIVERED) : '',
  );
  const [scheduledAt, setScheduledAt] = useState('');
  const [comment, setComment] = useState('');
  const [saving, setSaving] = useState(false);

  const target = statusId === '' ? null : Number(statusId);
  const cashExpected = parcels.reduce((sum, parcel) => sum + parcel.codToCollect, 0);
  const tJobs = useTranslations('bulkJobs');
  const statusLabel = (id: number) => {
    const status = findParcelStatus(id);
    return status ? tStatus(status.key) : `#${id}`;
  };

  const submit = async () => {
    if (target === null) return;
    setSaving(true);
    const result = await bulkConfirmRouteAction(routeCode, {
      parcelIds: parcels.map((parcel) => parcel.id),
      statusId: target,
      ...(target === POSTPONED && scheduledAt ? { scheduledAt } : {}),
      comment,
    });
    setSaving(false);

    if (result.ok) {
      const done = result.data;
      if (isBulkJobAccepted(done)) {
        // Lot W2 (D-239) : plus de 300 colis — la tâche continue ; la page la suit.
        toast.info(tJobs('started', { count: String(done.job.total) }));
        onDone();
        return;
      }
      toast.success(
        pickup
          ? t('donePicked', { count: done.count })
          : t('doneOutcome', { count: done.count, status: statusLabel(target) }),
      );
      onDone();
      return;
    }
    toast.error(translateError(result));
  };

  return (
    <Dialog open={open} onOpenChange={(next) => (saving ? undefined : onOpenChange(next))}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>
            {pickup
              ? t('pickedTitle', { count: parcels.length })
              : t('reportTitle', { count: parcels.length })}
          </DialogTitle>
          <DialogDescription>{pickup ? t('pickedHint') : t('reportHint')}</DialogDescription>
        </DialogHeader>

        {pickup ? null : (
          <div className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="bulk-outcome">{t('result')}</Label>
              <SearchableSelect
                options={outcomes.map((id) => ({ value: String(id), label: statusLabel(id) }))}
                value={statusId}
                onValueChange={setStatusId}
                placeholder={t('result')}
                id="bulk-outcome"
              />
              {outcomes.length === 0 ? (
                <p className="text-muted-foreground text-xs">{t('noCommonOutcome')}</p>
              ) : null}
            </div>

            {target === DELIVERED ? (
              <p className="bg-muted/60 rounded-md px-3 py-2 text-xs">
                {t('cashExpected', { amount: format.number(cashExpected / 100, 'currency') })}
                <br />
                {t('chequeNote')}
              </p>
            ) : null}

            {target === POSTPONED ? (
              <div className="flex flex-col gap-2">
                <Label htmlFor="bulk-scheduledAt">{t('scheduledAt')}</Label>
                <Input
                  id="bulk-scheduledAt"
                  type="date"
                  value={scheduledAt}
                  onChange={(event) => setScheduledAt(event.target.value)}
                />
              </div>
            ) : null}

            <div className="flex flex-col gap-2">
              <Label htmlFor="bulk-comment">{t('comment')}</Label>
              <Textarea
                id="bulk-comment"
                rows={2}
                maxLength={255}
                value={comment}
                onChange={(event) => setComment(event.target.value)}
              />
            </div>
          </div>
        )}

        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            {t('cancel')}
          </Button>
          <Button onClick={submit} disabled={saving || target === null} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : null}
            {pickup
              ? t('pickedAction', { count: String(parcels.length) })
              : t('reportAction', { count: String(parcels.length) })}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
