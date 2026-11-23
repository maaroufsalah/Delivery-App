'use client';

import {
  allowedTransitions,
  findParcelStatus,
  type RouteParcel,
  routeExpectedOutcome,
} from '@delivery/shared';
import { Loader2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
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
import { routeStopOf } from '@/features/routes/route-stop';
import { useRouteError } from '@/features/routes/use-route-error';
import { confirmRouteParcelAction } from '@/lib/actions/routes';

const KEEP = '__keep__';
const DELIVERED = 4;
const POSTPONED = 14;

/**
 * Le livreur rend compte d'un colis.
 *
 * Deux gestes dans une seule boîte, parce qu'ils n'en font qu'un au pas de
 * porte : « je l'ai bien en main » (le pivot legacy `boxe_colis.confirmed`) et
 * « voici ce qu'il est devenu ». Le legacy séparait les deux écrans, et les
 * colis confirmés mais jamais soldés s'accumulaient dans les tournées closes.
 *
 * Les issues proposées ne sont pas une liste écrite ici : elles viennent de
 * `transitions.ts`, la même machine à états que l'API opposera à la requête.
 * Proposer un choix que le serveur refusera est la meilleure façon d'apprendre
 * à un livreur à ne plus faire confiance à son application.
 */
export function ParcelOutcomeDialog({
  routeCode,
  kind,
  parcel,
  open,
  onOpenChange,
}: {
  routeCode: string;
  /**
   * Le TYPE du circuit — C12 bloc 3.
   *
   * Il décide de deux choses : qui la boîte NOMME en tête (l'expéditeur sur un
   * ramassage), et quelle issue arrive en premier dans la liste. Les issues
   * possibles, elles, restent celles de la machine à états — on ne les
   * restreint pas, on les ORDONNE.
   */
  kind: string;
  parcel: RouteParcel;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const t = useTranslations('routes.outcome');
  const tStatus = useTranslations('status');
  const format = useFormatter();
  const translateError = useRouteError();

  const [confirmed, setConfirmed] = useState(parcel.confirmed);
  const [statusId, setStatusId] = useState<string>(KEEP);
  const [scheduledAt, setScheduledAt] = useState('');
  const [comment, setComment] = useState(parcel.note ?? '');
  const [saving, setSaving] = useState(false);

  const stop = routeStopOf(kind, parcel);

  /*
   * Les issues de la machine à états, celle du TYPE en tête.
   *
   * « Ramassé » d'abord sur un ramassage, « Livré » d'abord sur une livraison :
   * c'est l'issue de neuf courses sur dix, et la chercher au milieu d'une
   * liste de quinze est un temps perdu à chaque colis.
   */
  const expected = routeExpectedOutcome(kind);
  const outcomes = allowedTransitions(parcel.statusId).sort(
    (a, b) => Number(b === expected) - Number(a === expected),
  );
  const target = statusId === KEEP ? undefined : Number(statusId);

  const submit = async () => {
    setSaving(true);
    const result = await confirmRouteParcelAction(routeCode, parcel.id, {
      confirmed,
      ...(target === undefined ? {} : { statusId: target }),
      // La date convenue n'a de sens que pour un report : l'envoyer ailleurs
      // reviendrait à dater un événement qui n'a pas lieu.
      ...(target === POSTPONED && scheduledAt ? { scheduledAt } : {}),
      comment,
    });
    setSaving(false);

    if (result.ok) {
      toast.success(t('saved', { code: parcel.code }));
      onOpenChange(false);
      return;
    }

    toast.error(translateError(result));
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="font-mono">{parcel.code}</DialogTitle>
          <DialogDescription>
            {stop.name} · {stop.phone}
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-4">
          <label className="flex items-center gap-3 rounded-lg border p-3 text-sm">
            <Checkbox
              checked={confirmed}
              onCheckedChange={(value) => setConfirmed(value === true)}
            />
            <span>{t('inHand')}</span>
          </label>

          <div className="flex flex-col gap-2">
            <Label htmlFor="outcome">{t('result')}</Label>
            <SearchableSelect
              options={outcomes.map((id) => {
                const status = findParcelStatus(id);
                return { value: String(id), label: status ? tStatus(status.key) : `#${id}` };
              })}
              value={statusId}
              onValueChange={setStatusId}
              placeholder={t('result')}
              emptyOption={{ value: KEEP, label: t('keep') }}
              id="outcome"
            />
            {outcomes.length === 0 ? (
              <p className="text-muted-foreground text-xs">{t('noOutcome')}</p>
            ) : null}
          </div>

          {target === DELIVERED ? (
            <p className="bg-muted/60 rounded-md px-3 py-2 text-xs">
              {t('codHint', {
                amount: format.number(parcel.codToCollect / 100, 'currency'),
              })}
            </p>
          ) : null}

          {target === POSTPONED ? (
            <div className="flex flex-col gap-2">
              <Label htmlFor="scheduledAt">{t('scheduledAt')}</Label>
              <Input
                id="scheduledAt"
                type="date"
                value={scheduledAt}
                onChange={(event) => setScheduledAt(event.target.value)}
              />
            </div>
          ) : null}

          <div className="flex flex-col gap-2">
            <Label htmlFor="comment">{t('comment')}</Label>
            <Textarea
              id="comment"
              rows={2}
              maxLength={255}
              value={comment}
              onChange={(event) => setComment(event.target.value)}
              placeholder={t('commentPlaceholder')}
            />
          </div>
        </div>

        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            {t('cancel')}
          </Button>
          <Button onClick={submit} disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : null}
            {t('save')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
