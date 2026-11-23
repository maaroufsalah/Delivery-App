'use client';

import { Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
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
import { ParcelStatusBadge } from '@/features/parcels/status-badge';
import { useRouteError } from '@/features/routes/use-route-error';
import { useRouter } from '@/i18n/navigation';
import { validateDeliveryNoteAction } from '@/lib/actions/routes';

/**
 * VALIDER UN BON DEPUIS SON CIRCUIT — Recette 5, point 7, validé par Anass.
 *
 * Sur `/routes/:code`, l'agent réceptionne bon par bon : une case par colis,
 * « Tout cocher », « X / Y cochés ». Ce qui n'est pas coché est retiré du bon
 * (et supprimé logiquement) ; le dialogue le dit avec les nombres avant
 * l'envoi. Même endpoint que la fiche du bon : une seule règle.
 */
export function NoteValidateDialog({
  noteCode,
  parcels,
  open,
  onOpenChange,
}: {
  noteCode: string;
  parcels: Array<{ id: string; code: string; recipientName: string; statusId: number }>;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const t = useTranslations('notes.detail');
  const tCommon = useTranslations('common');
  const router = useRouter();
  const translateError = useRouteError();
  const tJobs = useTranslations('bulkJobs');
  const [pending, startTransition] = useTransition();
  const [checked, setChecked] = useState<Set<string>>(() => {
    const picked = parcels.filter((parcel) => parcel.statusId === 18).map((parcel) => parcel.id);
    return new Set(picked.length > 0 ? picked : parcels.map((parcel) => parcel.id));
  });

  const missing = parcels.length - checked.size;
  const allChecked = parcels.length > 0 && missing === 0;

  function toggle(id: string) {
    setChecked((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function submit() {
    const count = checked.size;
    const removed = missing;
    startTransition(async () => {
      const result = await validateDeliveryNoteAction(
        noteCode,
        removed > 0 ? [...checked] : undefined,
      );
      if (!result.ok) {
        toast.error(translateError(result));
        return;
      }
      if (result.data) {
        // Lot W2 (D-239) : plus de 300 colis — la validation se fait par paquets ; la page la suit.
        toast.info(tJobs('started', { count: String(result.data.job.total) }));
        onOpenChange(false);
        router.refresh();
        return;
      }
      toast.success(
        removed > 0
          ? `${t('validatedCount', { count, total: String(parcels.length) })} · ${t('removedCount', { count: removed })}`
          : t('validatedCount', { count, total: String(parcels.length) }),
      );
      onOpenChange(false);
      router.refresh();
    });
  }

  return (
    <Dialog open={open} onOpenChange={(next) => (pending ? undefined : onOpenChange(next))}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="font-mono">{noteCode}</DialogTitle>
          <DialogDescription>
            {t('checked', { done: checked.size, total: String(parcels.length) })}
          </DialogDescription>
        </DialogHeader>

        <div className="bg-muted/40 flex items-center gap-3 rounded-lg border px-3 py-2">
          <Checkbox
            id={`check-all-${noteCode}`}
            checked={allChecked ? true : checked.size > 0 ? 'indeterminate' : false}
            onCheckedChange={() =>
              setChecked(allChecked ? new Set() : new Set(parcels.map((parcel) => parcel.id)))
            }
            disabled={pending}
          />
          <label htmlFor={`check-all-${noteCode}`} className="flex-1 text-sm font-medium">
            {t('checkAll')}
          </label>
        </div>

        <ul className="flex flex-col gap-1.5">
          {parcels.map((parcel) => (
            <li key={parcel.id} className="flex items-center gap-3 rounded-md border px-3 py-2">
              <Checkbox
                checked={checked.has(parcel.id)}
                onCheckedChange={() => toggle(parcel.id)}
                disabled={pending}
                aria-label={parcel.code}
              />
              <span className="min-w-0 flex-1">
                <span className="block font-mono text-sm">{parcel.code}</span>
                <span className="text-muted-foreground block truncate text-xs">
                  {parcel.recipientName}
                </span>
              </span>
              <ParcelStatusBadge statusId={parcel.statusId} />
            </li>
          ))}
        </ul>

        {missing > 0 ? (
          <p className="text-destructive text-sm">
            {t('partialBody', {
              missing,
              count: String(checked.size),
              total: String(parcels.length),
            })}
          </p>
        ) : null}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={pending}>
            {tCommon('cancel')}
          </Button>
          <Button
            variant={missing > 0 ? 'destructive' : 'default'}
            onClick={submit}
            disabled={pending || checked.size === 0}
          >
            {pending ? <Loader2 className="size-4 animate-spin" /> : null}
            {missing > 0
              ? t('partialTitle', { count: checked.size, total: String(parcels.length) })
              : t('validateCount', { count: String(checked.size) })}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
