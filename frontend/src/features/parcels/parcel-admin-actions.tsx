'use client';

import type { ParcelClientChangePreview } from '@delivery/shared';
import { ArrowRightLeft, Loader2, Printer, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
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
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useRouter } from '@/i18n/navigation';
import {
  adminDeleteParcelAction,
  changeParcelClientAction,
  previewClientChangeAction,
} from '@/lib/actions/parcels';

/**
 * LES GESTES DE L'ADMINISTRATEUR SUR LA PROPRIÉTÉ D'UN COLIS — D-256.
 *
 * Deux boutons de la fiche colis, montrés au SUPER_ADMIN seul (le serveur
 * refuse les autres) : « Supprimer » tant que le colis n'est pas pris en
 * charge, « Changer de client » tant que son argent n'a pas bougé. Le motif
 * est exigé ; le changement passe toujours par un aperçu.
 */

/** « Supprimer » — même droit que le vendeur, motif en plus. */
export function AdminDeleteParcelButton({ code }: { code: string }) {
  const t = useTranslations('parcels.admin.delete');
  const tCommon = useTranslations('common');
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [pending, setPending] = useState(false);

  const submit = async () => {
    if (pending || reason.trim() === '') return;
    setPending(true);
    const result = await adminDeleteParcelAction(code, { reason });
    setPending(false);
    if (!result.ok) {
      toast.error(result.error === 'invalid' ? t('reasonRequired') : result.error);
      return;
    }
    toast.success(t('done', { code }));
    setOpen(false);
    router.push('/parcels');
  };

  return (
    <>
      <Button
        type="button"
        size="sm"
        variant="outline"
        className="text-destructive gap-1.5"
        onClick={() => setOpen(true)}
      >
        <Trash2 className="size-4" aria-hidden />
        <span className="hidden sm:inline">{t('action')}</span>
      </Button>
      <Dialog open={open} onOpenChange={(next) => (pending ? null : setOpen(next))}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t('title', { code })}</DialogTitle>
            <DialogDescription>{t('hint')}</DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-2">
            <Label htmlFor="admin-delete-reason">{t('reason')}</Label>
            <Textarea
              id="admin-delete-reason"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              maxLength={500}
              rows={3}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            <Button
              variant="destructive"
              onClick={submit}
              disabled={pending || reason.trim() === ''}
              className="gap-1.5"
            >
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('confirm')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

/** « Changer de client » — un client, un motif, l'aperçu, puis la validation. */
export function ChangeClientButton({
  code,
  clientId,
  clients,
}: {
  code: string;
  clientId: string;
  clients: ReadonlyArray<{ id: string; name: string; isActive?: boolean }>;
}) {
  const t = useTranslations('parcels.admin.client');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [target, setTarget] = useState('');
  const [reason, setReason] = useState('');
  const [preview, setPreview] = useState<ParcelClientChangePreview | null>(null);
  const [done, setDone] = useState(false);
  const [pending, setPending] = useState(false);

  const money = (cents: number) => format.number(cents / 100, 'currency');
  const options = clients
    .filter((client) => client.id !== clientId && client.isActive !== false)
    .map((client) => ({ value: client.id, label: client.name }));

  const reset = () => {
    setTarget('');
    setReason('');
    setPreview(null);
    setDone(false);
  };

  const loadPreview = async (next: string) => {
    setTarget(next);
    setPreview(null);
    if (!next) return;
    setPending(true);
    const result = await previewClientChangeAction(code, { clientId: next });
    setPending(false);
    if (!result.ok) {
      toast.error(result.error);
      return;
    }
    setPreview(result.data);
  };

  const submit = async () => {
    if (pending || !preview || reason.trim() === '') return;
    setPending(true);
    const result = await changeParcelClientAction(code, {
      clientId: target,
      reason,
      fingerprint: preview.fingerprint,
    });
    setPending(false);
    if (!result.ok) {
      toast.error(result.error === 'invalid' ? t('reasonRequired') : result.error);
      return;
    }
    toast.success(t('done', { name: preview.to.name }));
    // Le rappel reste à l'écran : l'étiquette imprimée porte l'ancien client.
    setDone(true);
    router.refresh();
  };

  // Une liste FERMÉE de champs : chacun a sa phrase, un champ inconnu s'affiche tel quel.
  const FIELD_KEYS = {
    pickupAddress: 'fields.pickupAddress',
    originCityId: 'fields.originCityId',
    agencyId: 'fields.agencyId',
    expectedPayment: 'fields.expectedPayment',
    blRequested: 'fields.blRequested',
  } as const;
  const fieldLabel = (field: string) =>
    field in FIELD_KEYS ? t(FIELD_KEYS[field as keyof typeof FIELD_KEYS]) : field;

  return (
    <>
      <Button
        type="button"
        size="sm"
        variant="outline"
        className="gap-1.5"
        onClick={() => {
          reset();
          setOpen(true);
        }}
      >
        <ArrowRightLeft className="size-4" aria-hidden />
        <span className="hidden sm:inline">{t('action')}</span>
      </Button>
      <Dialog open={open} onOpenChange={(next) => (pending ? null : setOpen(next))}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{t('title', { code })}</DialogTitle>
            <DialogDescription>{t('hint')}</DialogDescription>
          </DialogHeader>

          {done ? (
            <div className="bg-warning/10 border-warning/40 flex items-start gap-2 rounded-md border p-3 text-sm">
              <Printer className="mt-0.5 size-4 shrink-0" aria-hidden />
              <p>{t('reprint')}</p>
            </div>
          ) : (
            <div className="flex flex-col gap-4">
              <div className="flex flex-col gap-2">
                <Label>{t('newClient')}</Label>
                <SearchableSelect
                  options={options}
                  value={target}
                  onValueChange={(value) => void loadPreview(value)}
                  placeholder={t('choose')}
                  aria-label={t('newClient')}
                />
              </div>

              {pending && !preview ? (
                <Loader2 className="text-muted-foreground size-4 animate-spin" />
              ) : null}

              {preview ? (
                <div className="flex flex-col gap-2 rounded-md border p-3 text-sm">
                  <p>
                    <span className="text-muted-foreground">{t('from')}</span>{' '}
                    <span className="font-medium">{preview.from.name}</span>
                    {' → '}
                    <span className="font-medium">{preview.to.name}</span>
                  </p>
                  <p>
                    <span className="text-muted-foreground">{t('fee')}</span>{' '}
                    <span className="tabular-nums">{money(preview.feeBefore)}</span>
                    {' → '}
                    <span className="font-medium tabular-nums">{money(preview.feeAfter)}</span>
                  </p>
                  <p className="text-muted-foreground">
                    {t('codUnchanged', { amount: money(preview.codAmount) })}
                  </p>
                  {preview.changes
                    .filter((change) => !['clientId', 'deliveryFee'].includes(change.field))
                    .map((change) => (
                      <p key={change.field} className="text-muted-foreground">
                        {fieldLabel(change.field)}
                      </p>
                    ))}
                  {preview.notes.map((note) => (
                    <p key={note.code}>
                      {note.effect === 'detached'
                        ? t('noteDetached', { code: note.code })
                        : t('noteKept', { code: note.code })}
                    </p>
                  ))}
                  {!preview.beforePickup ? (
                    <p className="text-muted-foreground">{t('afterPickup')}</p>
                  ) : null}
                </div>
              ) : null}

              <div className="flex flex-col gap-2">
                <Label htmlFor="client-change-reason">{t('reason')}</Label>
                <Textarea
                  id="client-change-reason"
                  value={reason}
                  onChange={(event) => setReason(event.target.value)}
                  maxLength={500}
                  rows={2}
                />
              </div>
            </div>
          )}

          <DialogFooter>
            {done ? (
              <Button onClick={() => setOpen(false)}>{tCommon('close')}</Button>
            ) : (
              <>
                <Button variant="outline" onClick={() => setOpen(false)} disabled={pending}>
                  {tCommon('cancel')}
                </Button>
                <Button
                  onClick={submit}
                  disabled={pending || !preview || reason.trim() === ''}
                  className="gap-1.5"
                >
                  {pending && preview ? <Loader2 className="size-4 animate-spin" /> : null}
                  {t('confirm')}
                </Button>
              </>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
