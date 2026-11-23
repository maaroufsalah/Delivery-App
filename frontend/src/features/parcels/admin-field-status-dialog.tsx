'use client';

import {
  ADMIN_FIELD_STATUS_MAX,
  type AdminFieldStatusResult,
  adminFieldTargets,
  findParcelStatus,
} from '@delivery/shared';
import { ListChecks, Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
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
import { Textarea } from '@/components/ui/textarea';
import { ParcelStatusBadge } from '@/features/parcels/status-badge';
import { STATUSES_WITH_DATE } from '@/features/parcels/status-dialogs';
import { useRouter } from '@/i18n/navigation';
import type { ParcelLabelSelection } from '@/lib/actions/documents';
import { adminFieldStatusAction } from '@/lib/actions/parcels';

/**
 * « STATUT LIBRE POUR L'ADMIN » — D-245.
 *
 * D'un statut de terrain à un autre (injoignable, reporté, refusé…), sur UN
 * colis depuis sa fiche ou sur une SÉLECTION depuis la liste. Commentaire
 * obligatoire, sans longueur minimale : seul le vide est refusé.
 *
 * L'écran ne porte AUCUNE règle. La liste des cibles vient du référentiel
 * partagé (`adminFieldTargets`, `adminFieldStatusIds`) ; tout le reste (rôle,
 * facture, argent encaissé, bon de retour, circuit) est jugé par le serveur,
 * colis par colis, et son motif est affiché tel qu'il revient.
 */
export function AdminFieldStatusDialog({
  open,
  onOpenChange,
  targets,
  selection,
  count,
  single,
  onDone,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Les statuts proposés : ceux du référentiel partagé, sans filtre d'écran. */
  targets: number[];
  /** Les colis visés : des codes, ou toute la liste filtrée (D-176). */
  selection: ParcelLabelSelection;
  /** Combien de colis la sélection compte, pour la borne des 300. */
  count: number;
  /** La fiche d'un colis : son code et son statut courant. Absent en masse. */
  single?: { code: string; statusId: number };
  /** Succès COMPLET (aucun refus) : la liste y vide sa sélection. */
  onDone?: () => void;
}) {
  const t = useTranslations('parcels.fieldStatus');
  const tStatus = useTranslations('status');
  const router = useRouter();

  const [statusId, setStatusId] = useState('');
  const [comment, setComment] = useState('');
  const [scheduledDate, setScheduledDate] = useState('');
  const [saving, setSaving] = useState(false);
  /* Le récapitulatif d'un geste de masse qui a connu des refus : il reste lu. */
  const [outcome, setOutcome] = useState<AdminFieldStatusResult | null>(null);

  const needsDate = STATUSES_WITH_DATE.includes(Number(statusId));
  /* Dit AVANT l'envoi ; l'API refuse de toute façon (422 `selection-too-large`). */
  const tooMany = count > ADMIN_FIELD_STATUS_MAX;

  const submit = async () => {
    setSaving(true);
    const result = await adminFieldStatusAction(
      {
        statusId: Number(statusId),
        comment,
        ...(needsDate && scheduledDate ? { scheduledDate } : {}),
      },
      selection,
    );
    setSaving(false);

    if (!result.ok) {
      toast.error(
        result.error === 'invalid'
          ? t('invalid')
          : result.error === 'unexpected'
            ? t('failed')
            : result.error,
      );
      return;
    }

    const { changed, refused } = result.data;
    if (changed.length > 0) router.refresh();

    if (refused.length === 0) {
      toast.success(single ? t('done') : t('bulkDone', { count: changed.length }));
      onDone?.();
      onOpenChange(false);
      return;
    }

    if (single) {
      // Un seul colis : son refus est LE message, le dialogue reste ouvert.
      toast.error(refused[0]?.message ?? t('failed'));
      return;
    }

    setOutcome(result.data);
  };

  return (
    <Dialog open={open} onOpenChange={(next) => (saving ? undefined : onOpenChange(next))}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t('title')}</DialogTitle>
          <DialogDescription>{t('description')}</DialogDescription>
        </DialogHeader>

        {outcome ? (
          <>
            <p className="text-sm font-medium" role="status">
              {t('summary', { changed: outcome.changed.length, refused: outcome.refused.length })}
            </p>

            <div className="flex flex-col gap-2">
              <p className="text-muted-foreground text-xs">{t('refusedTitle')}</p>
              <ul className="max-h-64 divide-y overflow-y-auto rounded-md border text-sm">
                {outcome.refused.map((refusal) => (
                  <li key={refusal.code} className="flex flex-col gap-0.5 px-3 py-2">
                    <span className="font-mono text-xs">{refusal.code}</span>
                    <span className="text-muted-foreground">{refusal.message}</span>
                  </li>
                ))}
              </ul>
            </div>

            <DialogFooter>
              <Button onClick={() => onOpenChange(false)}>{t('close')}</Button>
            </DialogFooter>
          </>
        ) : (
          <>
            {single ? (
              <div className="flex flex-wrap items-center gap-2 text-sm">
                <span className="bg-muted/60 rounded-md px-2 py-1 font-mono text-xs">
                  {single.code}
                </span>
                <span className="text-muted-foreground">{t('current')}</span>
                <ParcelStatusBadge statusId={single.statusId} />
              </div>
            ) : (
              <p className="bg-muted/60 rounded-md px-3 py-2 text-sm">
                {t('bulkSubject', { count })}
              </p>
            )}

            {tooMany ? (
              <p className="text-destructive text-sm" role="alert">
                {t('tooMany', { max: String(ADMIN_FIELD_STATUS_MAX), count: String(count) })}
              </p>
            ) : null}

            <div className="flex flex-col gap-2">
              <Label>{t('newStatus')}</Label>
              <SearchableSelect
                options={targets.map((id) => {
                  const status = findParcelStatus(id);
                  return {
                    value: String(id),
                    label: status ? tStatus(status.key) : `#${id}`,
                    color: status?.color,
                  };
                })}
                value={statusId}
                onValueChange={setStatusId}
                placeholder={t('pick')}
                className="w-full"
              />
              {single ? null : <p className="text-muted-foreground text-xs">{t('bulkHint')}</p>}
            </div>

            {needsDate ? (
              <div className="flex flex-col gap-2">
                <Label htmlFor="field-status-date">{t('scheduledDate')}</Label>
                <Input
                  id="field-status-date"
                  type="date"
                  value={scheduledDate}
                  onChange={(event) => setScheduledDate(event.target.value)}
                />
              </div>
            ) : null}

            <div className="flex flex-col gap-2">
              <Label htmlFor="field-status-comment">{t('comment')}</Label>
              <Textarea
                id="field-status-comment"
                rows={3}
                maxLength={500}
                value={comment}
                onChange={(event) => setComment(event.target.value)}
              />
              <p className="text-muted-foreground text-xs">{t('commentHint')}</p>
            </div>

            <DialogFooter className="gap-2 sm:gap-2">
              <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={saving}>
                {t('cancel')}
              </Button>
              <Button
                onClick={submit}
                disabled={saving || tooMany || statusId === '' || comment.trim().length === 0}
                className="gap-1.5"
              >
                {saving ? <Loader2 className="size-4 animate-spin" /> : null}
                {t('confirm')}
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

/**
 * Le bouton de la FICHE. La page ne le rend qu'à un SUPER_ADMIN (l'API refuse
 * tout autre compte en 403) ; il ne s'affiche que si le statut du colis est un
 * statut de terrain, c'est-à-dire si `adminFieldTargets` propose quelque chose.
 */
export function AdminFieldStatusButton({ code, statusId }: { code: string; statusId: number }) {
  const t = useTranslations('parcels.fieldStatus');
  const [open, setOpen] = useState(false);
  const targets = adminFieldTargets(statusId);

  if (targets.length === 0) return null;

  return (
    <>
      <Button variant="outline" size="sm" className="gap-1.5" onClick={() => setOpen(true)}>
        <ListChecks className="size-4" aria-hidden />
        <span>{t('action')}</span>
      </Button>

      {/* Monté à l'ouverture : chaque geste repart d'un formulaire vide. */}
      {open ? (
        <AdminFieldStatusDialog
          open
          onOpenChange={setOpen}
          targets={targets}
          selection={{ codes: [code] }}
          count={1}
          single={{ code, statusId }}
        />
      ) : null}
    </>
  );
}
