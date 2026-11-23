'use client';

import {
  RETURN_HANDOVER_STATUS_COLORS,
  type ReturnHandoverDetail as HandoverDetail,
} from '@delivery/shared';
import {
  Ban,
  CheckCircle2,
  History,
  Loader2,
  PackageCheck,
  PackageSearch,
  Plus,
  ScanLine,
  Send,
  Trash2,
  Truck,
} from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useEffect, useRef, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CartonCell, CartonTotal } from '@/components/carton-count';
import { CellBadge } from '@/components/data-table/cells';
import { ExportExcelButton } from '@/components/export-excel-button';
import { PrintButton } from '@/components/print-button';
import { Badge } from '@/components/ui/badge';
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
import { CorrectedMark } from '@/features/parcels/corrected-mark';
import { HandoverComposer } from '@/features/returns/handover-composer';
import { useReturnError } from '@/features/returns/use-return-error';
import { useRouter } from '@/i18n/navigation';
import { returnHandoverExportAction } from '@/lib/actions/documents';
import { newestFirst } from '@/lib/newest-first';
import {
  cancelHandoverAction,
  closeHandoverAction,
  confirmHandoverAction,
  deleteHandoverAction,
  downloadHandoverPdfAction,
  handOverHandoverAction,
  pickupHandoverAction,
  removeHandoverParcelAction,
  requestHandoverAction,
  scanHandoverParcelAction,
} from '@/lib/actions/returns';

/**
 * Fiche d'un bon de retour colis — l'écran de quai, et maintenant le parcours
 * entier (chantier « Retours, BL Clients Pro », points 1 et 2).
 *
 * **La douchette n'a rien perdu.** Le champ de scan prend le focus à
 * l'ouverture et le REPREND après chaque ajout, réussi ou non ; il n'est pas
 * dans un `<form>`, et `Enter` est intercepté — une douchette termine sa
 * lecture par `Enter`, et dans un formulaire cela aurait rechargé la page au
 * premier colis. Aucun délai de grâce non plus : une douchette tape en 30 ms,
 * et un débounce couperait le code en deux tronçons.
 *
 * **Les cinq gestes du parcours sont des BOUTONS que le serveur autorise.**
 * `pickable`, `handoverable`, `confirmable`, `closable`, `cancellable` viennent
 * du détail ; l'écran ne recalcule aucun d'entre eux. Trois espaces recopiant
 * la même machine à états, ce sont trois façons de se tromper.
 */
export function HandoverDetailScreen({ handover }: { handover: HandoverDetail }) {
  const t = useTranslations('returns.handovers');
  const tStatus = useTranslations('returns.handovers.status');
  const tOrigin = useTranslations('returns.handovers.origin');
  const tCommon = useTranslations('common');
  // Le libellé « Exporter Excel » des fiches, le même que sur le circuit.
  const tExport = useTranslations('routes.detail');
  const errorMessage = useReturnError();
  const format = useFormatter();
  const router = useRouter();

  const inputRef = useRef<HTMLInputElement>(null);
  const [current, setCurrent] = useState(handover);
  const [code, setCode] = useState('');
  const [composing, setComposing] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [dialog, setDialog] = useState<
    null | 'request' | 'pickup' | 'handover' | 'confirm' | 'close' | 'cancel' | 'delete'
  >(null);
  const [pending, startTransition] = useTransition();

  const editable = current.editable;

  // Le focus est repris à chaque rendu tant que le bon se compose : après un
  // ajout, après un retrait, après une erreur. L'agent ne repose pas la
  // douchette pour cliquer.
  useEffect(() => {
    if (editable) inputRef.current?.focus();
  }, [editable, current.items.length]);

  const scan = () => {
    const value = code.trim();
    if (value === '' || pending) return;

    startTransition(async () => {
      const result = await scanHandoverParcelAction(current.code, { code: value, note: '' });

      if (result.ok) {
        setCurrent(result.data);
        setCode('');
        toast.success(t('added', { code: value.toUpperCase() }));
        return;
      }

      // Le code reste dans le champ : l'agent voit CE qu'il a scanné, et peut
      // le corriger au clavier plutôt que de rescanner à l'aveugle.
      toast.error(errorMessage(result));
    });
  };

  const remove = (parcelId: string, parcelCode: string) => {
    startTransition(async () => {
      const result = await removeHandoverParcelAction(current.code, parcelId);
      if (result.ok) {
        setCurrent(result.data);
        toast.success(t('removed', { code: parcelCode }));
      } else toast.error(errorMessage(result));
    });
  };

  /**
   * LA SUPPRESSION D'UN BROUILLON — chantier « Bon de retour livreur »,
   * point 3.
   *
   * Elle ne passe pas par `advance` : les six autres gestes rendent un bon à
   * afficher, celle-ci n'en rend aucun. On revient d'où l'on vient —
   * `router.back()`, la même règle que le bouton « Retour » du point 4 — parce
   * que la fiche qu'on regarde vient de cesser d'exister.
   */
  const destroy = () => {
    startTransition(async () => {
      const result = await deleteHandoverAction(current.code);
      setDialog(null);

      if (result.ok) {
        toast.success(t('deleted'));
        router.back();
        return;
      }

      toast.error(errorMessage(result));
    });
  };

  /** Les cinq transitions passent par ici : même forme, même gestion du refus. */
  const advance = (
    action: () => Promise<Awaited<ReturnType<typeof confirmHandoverAction>>>,
    successKey:
      'requestDone' | 'pickupDone' | 'handoverDone' | 'confirmed' | 'closeDone' | 'cancelDone',
  ) => {
    startTransition(async () => {
      const result = await action();
      setDialog(null);

      if (result.ok) {
        setCurrent(result.data);
        toast.success(t(successKey, { code: current.code }));
      } else toast.error(errorMessage(result));
    });
  };

  return (
    <div className="flex flex-col gap-4">
      {/* --- L'état, l'origine, et ce qu'on peut faire ------------------ */}
      <div className="flex flex-wrap items-center gap-2">
        <CellBadge
          label={tStatus(current.status)}
          color={RETURN_HANDOVER_STATUS_COLORS[current.status]}
        />

        <Badge variant="secondary" className="rounded-full font-normal">
          {current.origin === 'admin' ? t('createdByAdmin') : t('createdByCourier')}
        </Badge>

        <span className="text-muted-foreground text-sm">
          {current.courierName} · {current.agencyName}
        </span>

        {current.clientNames.length > 0 ? (
          <span className="text-muted-foreground truncate text-xs">
            {current.clientNames.join(', ')}
          </span>
        ) : null}

        <div className="ms-auto flex flex-wrap items-center gap-2">
          <PrintButton
            kind="document"
            icon="file"
            subject={current.code}
            label={t('pdf')}
            disabled={pending}
            onPrint={() => downloadHandoverPdfAction(current.code)}
          />

          {/* Une ligne par colis ; pour tout compte qui lit le bon, livreur compris. */}
          <ExportExcelButton
            label={tExport('exportExcel')}
            filename={current.code}
            disabled={pending || current.items.length === 0}
            onExport={() => returnHandoverExportAction(current.code)}
          />

          {/*
            « DEMANDER LE RETOUR » — chantier « Bon de retour livreur »,
            point 2. Il sort le brouillon du comptoir de l'ombre : tant qu'il
            n'est pas demandé, le livreur ne sait pas qu'on l'attend.
          */}
          {current.requestable ? (
            <Button
              size="sm"
              className="gap-1.5"
              disabled={pending}
              onClick={() => setDialog('request')}
            >
              <Send className="size-4" />
              {t('request')}
            </Button>
          ) : null}

          {current.pickable ? (
            <Button
              size="sm"
              variant="outline"
              className="gap-1.5"
              disabled={pending}
              onClick={() => setDialog('pickup')}
            >
              <Truck className="size-4" />
              {t('pickup')}
            </Button>
          ) : null}

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

          {current.confirmable ? (
            <Button
              size="sm"
              className="gap-1.5"
              disabled={pending}
              onClick={() => setDialog('confirm')}
            >
              <CheckCircle2 className="size-4" />
              {t('confirm')}
            </Button>
          ) : null}

          {current.closable ? (
            <Button
              size="sm"
              className="gap-1.5"
              disabled={pending}
              onClick={() => setDialog('close')}
            >
              <CheckCircle2 className="size-4" />
              {t('close')}
            </Button>
          ) : null}

          {current.deletable ? (
            <Button
              size="sm"
              variant="ghost"
              className="text-muted-foreground hover:text-destructive gap-1.5"
              disabled={pending}
              onClick={() => setDialog('delete')}
            >
              <Trash2 className="size-4" />
              {t('delete')}
            </Button>
          ) : null}

          {current.cancellable ? (
            <Button
              size="sm"
              variant="ghost"
              className="text-muted-foreground hover:text-destructive gap-1.5"
              disabled={pending}
              onClick={() => setDialog('cancel')}
            >
              <Ban className="size-4" />
              {t('cancel')}
            </Button>
          ) : null}
        </div>
      </div>

      {/* --- La douchette, tant que le bon se compose -------------------- */}
      {editable ? (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <ScanLine className="size-4" />
              {t('scan')}
            </CardTitle>
            <CardDescription>{t('scanHint')}</CardDescription>
          </CardHeader>

          <CardContent className="flex flex-col gap-3">
            {/*
              Pas de <form> : une douchette finit par Enter, et un formulaire
              aurait rechargé la page au premier colis.
            */}
            <div className="flex gap-2">
              <Label htmlFor="scan-input" className="sr-only">
                {t('scan')}
              </Label>
              <Input
                id="scan-input"
                ref={inputRef}
                value={code}
                autoFocus
                autoComplete="off"
                spellCheck={false}
                inputMode="text"
                placeholder={t('scanPlaceholder')}
                className="font-mono"
                onChange={(event) => setCode(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key !== 'Enter') return;
                  event.preventDefault();
                  scan();
                }}
              />
              <Button onClick={scan} disabled={pending || code.trim() === ''} className="gap-1.5">
                {pending ? <Loader2 className="size-4 animate-spin" /> : null}
                {tCommon('save')}
              </Button>
            </div>

            {/*
              L'autre geste : l'agent qui compose la demande du lendemain n'a
              aucun carton sous la main à scanner.
            */}
            <Button
              variant="outline"
              className="gap-1.5"
              disabled={pending}
              onClick={() => setComposing(true)}
            >
              <Plus className="size-4" />
              {t('addParcels')}
            </Button>
          </CardContent>
        </Card>
      ) : null}

      {/* --- Les colis --------------------------------------------------- */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <PackageSearch className="size-4" />
            {tCommon('shipments')}
            {/* « 3 expéditions · 6 cartons », ou « 3 expéditions » — il remplace la pastille du nombre. */}
            <CartonTotal lines={current.items} />
          </CardTitle>
          {current.note ? <CardDescription>{current.note}</CardDescription> : null}
        </CardHeader>

        <CardContent className="flex flex-col gap-2">
          {current.items.length === 0 ? (
            <div className="flex flex-col items-center gap-1 py-8 text-center">
              <p className="font-medium">{t('noItems')}</p>
              <p className="text-muted-foreground text-sm">{t('noItemsHint')}</p>
            </div>
          ) : (
            newestFirst(current.items).map((item) => (
              <div
                key={item.id}
                className="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-lg border p-3 text-sm"
              >
                <span className="font-mono text-[13px] font-medium tracking-tight">
                  {item.parcelCode}
                </span>
                <CorrectedMark at={item.correctedAt} />

                <span className="min-w-0 flex-1 truncate">
                  {item.recipientName}
                  <span className="text-muted-foreground"> · {item.clientName}</span>
                </span>

                <CartonCell line={item} lines={current.items} />

                {editable ? (
                  <Button
                    size="icon"
                    variant="ghost"
                    disabled={pending}
                    aria-label={tCommon('delete')}
                    onClick={() => remove(item.parcelId, item.parcelCode)}
                  >
                    <Trash2 className="text-destructive size-4" />
                  </Button>
                ) : null}
              </div>
            ))
          )}
        </CardContent>
      </Card>

      {/* --- LE JOURNAL — point 1 : « chaque transition journalisée » ---- */}
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
                  style={{ backgroundColor: RETURN_HANDOVER_STATUS_COLORS[step.status] }}
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

          <p className="text-muted-foreground mt-4 text-xs">
            {t('createdBy')} : {current.createdByName ?? tOrigin(current.origin)}
          </p>
        </CardContent>
      </Card>

      {composing ? (
        <HandoverComposer
          handover={current}
          onDone={(next) => {
            setComposing(false);
            if (next) setCurrent(next);
          }}
        />
      ) : null}

      {/* --- Les sept confirmations, une seule boîte -------------------- */}
      <Dialog open={dialog !== null} onOpenChange={(open) => (open ? null : setDialog(null))}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{dialog ? t(`${dialog}Title`) : ''}</DialogTitle>
            <DialogDescription>
              {dialog === 'confirm'
                ? t('confirmHint', { count: current.items.length })
                : dialog
                  ? t(`${dialog}Hint`)
                  : ''}
            </DialogDescription>
          </DialogHeader>

          {dialog === 'cancel' ? (
            <div>
              <Label htmlFor="cancel-reason">{t('cancelReason')}</Label>
              <Input
                id="cancel-reason"
                value={cancelReason}
                onChange={(event) => setCancelReason(event.target.value)}
                className="mt-1.5"
              />
            </div>
          ) : null}

          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => setDialog(null)} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            <Button
              variant={dialog === 'cancel' || dialog === 'delete' ? 'destructive' : 'default'}
              disabled={pending}
              className="gap-1.5"
              onClick={() => {
                if (dialog === 'request') {
                  advance(() => requestHandoverAction(current.code), 'requestDone');
                } else if (dialog === 'pickup') {
                  advance(() => pickupHandoverAction(current.code, {}), 'pickupDone');
                } else if (dialog === 'handover') {
                  advance(() => handOverHandoverAction(current.code), 'handoverDone');
                } else if (dialog === 'confirm') {
                  advance(() => confirmHandoverAction(current.code), 'confirmed');
                } else if (dialog === 'close') {
                  advance(() => closeHandoverAction(current.code), 'closeDone');
                } else if (dialog === 'cancel') {
                  advance(
                    () => cancelHandoverAction(current.code, { reason: cancelReason }),
                    'cancelDone',
                  );
                } else if (dialog === 'delete') {
                  destroy();
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
