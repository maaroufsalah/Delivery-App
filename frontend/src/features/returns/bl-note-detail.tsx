'use client';

import {
  BL_NOTE_STATUS_COLORS,
  DELIVERY_SLIP_STATUS_COLORS,
  type BlNoteDetail as NoteDetail,
} from '@delivery/shared';
import { CheckCircle2, FileText, History, Loader2, PackageCheck, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge } from '@/components/data-table/cells';
import { PrintButton } from '@/components/print-button';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import { useReturnError } from '@/features/returns/use-return-error';
import { Link } from '@/i18n/navigation';
import {
  closeBlNoteAction,
  downloadBlNotePdfAction,
  handOverBlNoteAction,
  receiveBlNoteAction,
  removeBlNoteSlipAction,
  returnBlNoteAction,
} from '@/lib/actions/bl';

/**
 * FICHE D'UN BON BL — chantier « Retours, BL Clients Pro », point 5.
 *
 * **Les deux actions de l'admin sont SÉPARÉES**, et c'est la demande explicite
 * de la mission. Recevoir une liasse et la rendre sont deux gestes, faits deux
 * jours différents, souvent par deux personnes : un seul bouton aurait attesté
 * la seconde en faisant la première, et le jour où un vendeur dit « je n'ai
 * jamais eu mes bons », il n'y aurait rien à opposer.
 *
 * **Les quatre boutons viennent du SERVEUR** (`handoverable`, `receivable`,
 * `returnable`, `closable`). L'écran ne recalcule aucune transition : trois
 * espaces qui recopieraient la machine à états en feraient trois versions.
 */
export function BlNoteDetailScreen({ note }: { note: NoteDetail }) {
  const t = useTranslations('returns.bl');
  const tStatus = useTranslations('returns.bl.status');
  const tSlip = useTranslations('returns.bl.slipStatus');
  const tCommon = useTranslations('common');
  const errorMessage = useReturnError();
  const format = useFormatter();

  const [current, setCurrent] = useState(note);
  const [returnedToName, setReturnedToName] = useState('');
  const [dialog, setDialog] = useState<null | 'handover' | 'receive' | 'return' | 'close'>(null);
  const [pending, startTransition] = useTransition();

  const advance = (
    action: () => Promise<Awaited<ReturnType<typeof receiveBlNoteAction>>>,
    successKey: 'handoverDone' | 'receiveDone' | 'returnDone' | 'closeDone',
  ) => {
    startTransition(async () => {
      const result = await action();
      setDialog(null);

      if (result.ok) {
        setCurrent(result.data);
        toast.success(t(successKey));
      } else toast.error(errorMessage(result));
    });
  };

  const removeSlip = (slipId: string) => {
    startTransition(async () => {
      const result = await removeBlNoteSlipAction(current.code, slipId);
      if (result.ok) {
        setCurrent(result.data);
        toast.success(t('removed'));
      } else toast.error(errorMessage(result));
    });
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <CellBadge label={tStatus(current.status)} color={BL_NOTE_STATUS_COLORS[current.status]} />

        <span className="text-muted-foreground text-sm">
          {current.clientName} · {current.courierName} · {current.agencyName}
        </span>

        <div className="ms-auto flex flex-wrap items-center gap-2">
          <PrintButton
            kind="document"
            icon="file"
            subject={current.code}
            label={t('pdf')}
            disabled={pending}
            onPrint={() => downloadBlNotePdfAction(current.code)}
          />

          {current.handoverable ? (
            <Button
              size="sm"
              variant="outline"
              className="gap-1.5"
              disabled={pending}
              onClick={() => setDialog('handover')}
            >
              <PackageCheck className="size-4" />
              {t('handover')}
            </Button>
          ) : null}

          {/* PREMIÈRE action de l'admin : « Confirmer la réception ». */}
          {current.receivable ? (
            <Button
              size="sm"
              className="gap-1.5"
              disabled={pending}
              onClick={() => setDialog('receive')}
            >
              <CheckCircle2 className="size-4" />
              {t('receive')}
            </Button>
          ) : null}

          {/* SECONDE action, et elle est séparée : « Remise au vendeur ». */}
          {current.returnable ? (
            <Button
              size="sm"
              className="gap-1.5"
              disabled={pending}
              onClick={() => setDialog('return')}
            >
              <CheckCircle2 className="size-4" />
              {t('return')}
            </Button>
          ) : null}

          {current.closable ? (
            <Button
              size="sm"
              variant="outline"
              className="gap-1.5"
              disabled={pending}
              onClick={() => setDialog('close')}
            >
              <CheckCircle2 className="size-4" />
              {t('close')}
            </Button>
          ) : null}
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <FileText className="size-4" />
            {t('slips')}
            <CellBadge label={String(current.items.length)} />
          </CardTitle>
          {current.note ? <CardDescription>{current.note}</CardDescription> : null}
        </CardHeader>

        <CardContent className="flex flex-col gap-2">
          {current.items.length === 0 ? (
            <div className="flex flex-col items-center gap-1 py-8 text-center">
              <p className="font-medium">{t('noSlips')}</p>
              <p className="text-muted-foreground text-sm">{t('noSlipsHint')}</p>
            </div>
          ) : (
            current.items.map((slip) => (
              <div
                key={slip.id}
                className="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-lg border p-3 text-sm"
              >
                <Link
                  href={`/parcels/${slip.parcelCode}`}
                  className="hover:text-primary font-mono text-[13px] font-medium underline-offset-2 hover:underline"
                >
                  {slip.parcelCode}
                </Link>

                <span className="min-w-0 flex-1 truncate">
                  {slip.recipientName}
                  <span className="text-muted-foreground">
                    {' · '}
                    {slip.reference ?? t('noReference')}
                  </span>
                </span>

                <CellBadge
                  label={tSlip(slip.status)}
                  color={DELIVERY_SLIP_STATUS_COLORS[slip.status]}
                />

                {current.editable ? (
                  <Button
                    size="icon"
                    variant="ghost"
                    disabled={pending}
                    aria-label={tCommon('delete')}
                    onClick={() => removeSlip(slip.id)}
                  >
                    <Trash2 className="text-destructive size-4" />
                  </Button>
                ) : null}
              </div>
            ))
          )}
        </CardContent>
      </Card>

      {/* --- Le journal — « chacune journalisée » ---------------------- */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <History className="size-4" />
            {t('journal')}
          </CardTitle>
          <CardDescription>{t('journalHint')}</CardDescription>
        </CardHeader>

        <CardContent>
          <ol className="flex flex-col gap-3">
            {current.steps.map((step) => (
              <li key={step.id} className="flex flex-wrap items-baseline gap-x-3 gap-y-1 text-sm">
                <span
                  className="size-2 shrink-0 rounded-full"
                  style={{ backgroundColor: BL_NOTE_STATUS_COLORS[step.status] }}
                  aria-hidden
                />
                <span className="font-medium">{tStatus(step.status)}</span>
                <span className="text-muted-foreground text-xs tabular-nums">
                  {format.dateTime(new Date(step.createdAt), 'stamp')}
                </span>
                {step.authorName ? (
                  <span className="text-muted-foreground text-xs">{step.authorName}</span>
                ) : null}
                {step.comment ? (
                  <span className="text-muted-foreground w-full text-xs">{step.comment}</span>
                ) : null}
              </li>
            ))}
          </ol>
        </CardContent>
      </Card>

      <Dialog open={dialog !== null} onOpenChange={(open) => (open ? null : setDialog(null))}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{dialog ? t(`${dialog}Title`) : ''}</DialogTitle>
            <DialogDescription>{dialog ? t(`${dialog}Hint`) : ''}</DialogDescription>
          </DialogHeader>

          {dialog === 'return' ? (
            <div>
              <Label htmlFor="bl-returned-to">{t('returnedToName')}</Label>
              <Input
                id="bl-returned-to"
                value={returnedToName}
                onChange={(event) => setReturnedToName(event.target.value)}
                className="mt-1.5"
              />
            </div>
          ) : null}

          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => setDialog(null)} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            <Button
              className="gap-1.5"
              disabled={pending || (dialog === 'return' && returnedToName.trim().length < 2)}
              onClick={() => {
                if (dialog === 'handover') {
                  advance(() => handOverBlNoteAction(current.code), 'handoverDone');
                } else if (dialog === 'receive') {
                  advance(() => receiveBlNoteAction(current.code), 'receiveDone');
                } else if (dialog === 'return') {
                  advance(() => returnBlNoteAction(current.code, { returnedToName }), 'returnDone');
                } else if (dialog === 'close') {
                  advance(() => closeBlNoteAction(current.code), 'closeDone');
                }
              }}
            >
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {dialog ? t(dialog) : ''}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
