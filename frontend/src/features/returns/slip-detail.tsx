'use client';

import {
  RETURN_REASONS,
  type ReturnParcelLine,
  type ReturnReason,
  type ReturnSlipDetail as SlipDetail,
} from '@delivery/shared';
import { CheckCircle2, Download, FileText, Loader2, PackagePlus, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CartonCell, CartonTotal } from '@/components/carton-count';
import { CellBadge } from '@/components/data-table/cells';
import { ExportExcelButton } from '@/components/export-excel-button';
import { SearchableSelect } from '@/components/searchable-select';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { SlipComposer } from '@/features/returns/slip-composer';
import { useReturnError } from '@/features/returns/use-return-error';
import { returnSlipExportAction } from '@/lib/actions/documents';
import { newestFirst } from '@/lib/newest-first';
import {
  addSlipParcelAction,
  closeSlipAction,
  downloadSlipPdfAction,
  removeSlipParcelAction,
} from '@/lib/actions/returns';

/**
 * Fiche d'un bon de retour.
 *
 * Ici on ne scanne pas : les colis éligibles sont déjà connus — ceux de cet
 * e-commerçant, reçus en agence et pas encore rendus. On les CHOISIT dans une
 * liste, avec leur motif, parce que le motif est une décision humaine que
 * personne ne lit sur une étiquette.
 *
 * Les frais affichés sont ceux gelés à l'ajout de la ligne. Ils ne se
 * recalculent pas : un bon déjà remis ne doit pas changer de montant le jour où
 * quelqu'un modifie un tarif (DOMAIN.md §4 dette n°17).
 */
export function SlipDetailScreen({
  slip,
  available,
}: {
  slip: SlipDetail;
  /**
   * Colis RENDABLES de cet e-commerçant — retouches Anass, point 7.
   *
   * « Tous les colis dont le statut est différent de Ramassé, Ramassé par le
   * livreur, Livré et En cours de livraison. » La liste ne montrait que
   * `22 Récupéré par le livreur` : un colis annulé par le vendeur ou revenu
   * hors zone n'apparaissait nulle part, et l'agence le rendait sans bon.
   */
  available: ReturnParcelLine[];
}) {
  const t = useTranslations('returns.slips');
  const tStatus = useTranslations('returns.slips.status');
  const tReason = useTranslations('returns.slips.reasons');
  const tCommon = useTranslations('common');
  // Le libellé « Exporter Excel » des fiches, le même que sur le circuit.
  const tExport = useTranslations('routes.detail');
  const errorMessage = useReturnError();
  const format = useFormatter();

  const [current, setCurrent] = useState(slip);
  const [parcelId, setParcelId] = useState('');
  /** La multi-sélection du point 7 : une feuille, une recherche, un motif. */
  const [composing, setComposing] = useState(false);
  const [reason, setReason] = useState<ReturnReason>('refused');
  const [closing, setClosing] = useState(false);
  const [receivedBy, setReceivedBy] = useState('');
  const [pending, startTransition] = useTransition();

  const editable = current.status === 'draft';
  const listed = new Set(current.items.map((item) => item.parcelId));
  const selectable = available.filter((parcel) => !listed.has(parcel.id));

  const add = () => {
    if (!parcelId) return;

    startTransition(async () => {
      const result = await addSlipParcelAction(current.code, { parcelId, reason });

      if (result.ok) {
        const code = available.find((parcel) => parcel.id === parcelId)?.code ?? '';
        setCurrent(result.data);
        setParcelId('');
        toast.success(t('added', { code }));
      } else toast.error(errorMessage(result));
    });
  };

  const remove = (id: string, code: string) => {
    startTransition(async () => {
      const result = await removeSlipParcelAction(current.code, id);
      if (result.ok) {
        setCurrent(result.data);
        toast.success(t('removed', { code }));
      } else toast.error(errorMessage(result));
    });
  };

  const close = () => {
    startTransition(async () => {
      const result = await closeSlipAction(current.code, {
        receivedByName: receivedBy.trim(),
        proof: null,
      });

      if (result.ok) {
        setCurrent(result.data);
        setClosing(false);
        toast.success(t('closed', { code: current.code }));
      } else toast.error(errorMessage(result));
    });
  };

  const printPdf = () => {
    startTransition(async () => {
      const result = await downloadSlipPdfAction(current.code);
      if (!result.ok) {
        toast.error(errorMessage(result));
        return;
      }

      // Le base64 revient de l'action ; le fichier est fabriqué ici, sans
      // route de proxy à maintenir — même chemin que l'export tarifaire.
      const bytes = Uint8Array.from(atob(result.data), (char) => char.charCodeAt(0));
      const url = URL.createObjectURL(new Blob([bytes], { type: 'application/pdf' }));
      const link = document.createElement('a');
      link.href = url;
      link.download = `${current.code}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    });
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        {current.status === 'delivered' ? (
          <CellBadge label={tStatus('delivered')} color="var(--chart-4)" icon={CheckCircle2} />
        ) : (
          <CellBadge label={tStatus('draft')} color="var(--chart-3)" />
        )}

        <span className="text-muted-foreground text-sm">
          {current.clientName} · {current.agencyName}
        </span>

        {current.deliveredAt ? (
          <span className="text-muted-foreground text-xs">
            {format.dateTime(new Date(current.deliveredAt), 'stamp')}
            {current.receivedByName ? ` · ${current.receivedByName}` : ''}
          </span>
        ) : null}

        <div className="ms-auto flex flex-wrap items-center gap-2">
          <Button
            size="sm"
            variant="outline"
            className="gap-1.5"
            disabled={pending}
            onClick={printPdf}
          >
            <Download className="size-4" />
            <span className="hidden sm:inline">{t('pdf')}</span>
          </Button>

          {/* Une ligne par colis ; pour tout compte qui lit le bon, vendeur compris. */}
          <ExportExcelButton
            label={tExport('exportExcel')}
            filename={current.code}
            disabled={pending || current.items.length === 0}
            onExport={() => returnSlipExportAction(current.code)}
          />

          {current.closable ? (
            <Button
              size="sm"
              className="gap-1.5"
              disabled={pending}
              onClick={() => setClosing(true)}
            >
              <CheckCircle2 className="size-4" />
              <span className="hidden sm:inline">{t('close')}</span>
            </Button>
          ) : null}
        </div>
      </div>

      {editable ? (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <PackagePlus className="size-4" />
              {t('add')}
            </CardTitle>
            <CardDescription>{t('addHint')}</CardDescription>
          </CardHeader>

          <CardContent className="flex flex-col gap-3">
            {/*
              LA MULTI-SÉLECTION D'ABORD — retouches Anass, point 7.

              C'est le geste courant : on rend une pile. Le formulaire unitaire
              reste en dessous pour le colis qui a son motif à lui, et il n'est
              pas redondant — le lot ne porte qu'un seul motif.
            */}
            <Button
              variant="outline"
              className="gap-1.5 self-start"
              onClick={() => setComposing(true)}
              disabled={pending || selectable.length === 0}
            >
              <PackagePlus className="size-4" aria-hidden />
              {t('addMany')}
            </Button>

            <div className="flex flex-col gap-2 sm:flex-row">
              <div className="min-w-0 flex-1">
                <Label htmlFor="slip-parcel" className="sr-only">
                  {t('add')}
                </Label>
                <SearchableSelect
                  options={selectable.map((parcel) => ({
                    value: parcel.id,
                    label: `${parcel.code}, ${parcel.recipientName}`,
                    keywords: [parcel.recipientPhone, parcel.externalCode],
                  }))}
                  value={parcelId}
                  onValueChange={setParcelId}
                  placeholder={selectable.length === 0 ? t('noAvailable') : t('add')}
                  disabled={selectable.length === 0}
                  id="slip-parcel"
                />
              </div>

              <div className="sm:w-52">
                <Label htmlFor="slip-reason" className="sr-only">
                  {t('fields.reason')}
                </Label>
                <Select value={reason} onValueChange={(value) => setReason(value as ReturnReason)}>
                  <SelectTrigger id="slip-reason">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {RETURN_REASONS.map((entry) => (
                      <SelectItem key={entry} value={entry}>
                        {tReason(entry)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <Button onClick={add} disabled={pending || !parcelId} className="gap-1.5">
                {pending ? <Loader2 className="size-4 animate-spin" /> : null}
                {tCommon('save')}
              </Button>
            </div>
          </CardContent>
        </Card>
      ) : null}

      {composing ? (
        <SlipComposer
          code={current.code}
          onDone={(next) => {
            setComposing(false);
            if (next) setCurrent(next);
          }}
        />
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <FileText className="size-4" />
            {tCommon('shipments')}
            {/* « 3 expéditions · 6 cartons », ou « 3 expéditions » — il remplace la pastille du nombre. */}
            <CartonTotal lines={current.items} />
          </CardTitle>
          <CardDescription>{t('feeHint')}</CardDescription>
        </CardHeader>

        <CardContent className="flex flex-col gap-2">
          {current.items.length === 0 ? (
            <div className="flex flex-col items-center gap-1 py-8 text-center">
              <p className="font-medium">{t('noItems')}</p>
              <p className="text-muted-foreground text-sm">{t('noItemsHint')}</p>
            </div>
          ) : (
            <>
              {newestFirst(current.items).map((item) => (
                <div
                  key={item.id}
                  className="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-lg border p-3 text-sm"
                >
                  <span className="font-mono text-[13px] font-medium tracking-tight">
                    {item.parcelCode}
                  </span>

                  <span className="min-w-0 flex-1 truncate">
                    {item.recipientName}
                    <span className="text-muted-foreground"> · {item.destinationCityName}</span>
                  </span>

                  <CartonCell line={item} lines={current.items} />

                  <CellBadge label={tReason(item.reason)} />

                  <span className="tabular-nums">
                    {format.number(item.feeApplied / 100, 'currency')}
                  </span>

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
              ))}

              <div className="flex items-center justify-between border-t pt-3 text-sm font-medium">
                <span>{t('totalFees')}</span>
                <span className="tabular-nums">
                  {format.number(current.totalFees / 100, 'currency')}
                </span>
              </div>
            </>
          )}
        </CardContent>
      </Card>

      <Dialog open={closing} onOpenChange={setClosing}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{t('closeTitle')}</DialogTitle>
            <DialogDescription>{t('closeHint', { count: current.items.length })}</DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-2">
            <Label htmlFor="received-by">{t('fields.receivedBy')}</Label>
            <Input
              id="received-by"
              autoFocus
              value={receivedBy}
              onChange={(event) => setReceivedBy(event.target.value)}
            />
            <p className="text-muted-foreground text-xs">{t('fields.receivedByHint')}</p>
          </div>

          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => setClosing(false)} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            {/* Le nom est obligatoire : c'est ce qui remplace le code de retour
                du legacy, dont la vérification était désactivée. */}
            <Button
              onClick={close}
              disabled={pending || receivedBy.trim().length < 2}
              className="gap-1.5"
            >
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('close')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
