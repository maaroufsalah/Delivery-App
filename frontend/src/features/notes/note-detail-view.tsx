'use client';

import type { BulkJob } from '@delivery/shared';
import { BulkJobProgress } from '@/features/bulk-jobs/bulk-job-progress';
import {
  type DeliveryNoteDetail,
  NOTE_STATUS_COLORS,
  type NoteParcelClientCorrection,
} from '@delivery/shared';
import { Building2, CheckCheck, Loader2, Plus, RotateCcw, Store, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useSearchParams } from 'next/navigation';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { BackButton } from '@/components/back-button';
import { CartonCell, CartonTotal } from '@/components/carton-count';
import { CellBadge } from '@/components/data-table/cells';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { NoteComposer } from '@/features/notes/note-composer';
import { NoteLabelsButton } from '@/features/notes/note-labels-button';
import { LabelJobsPanel, useLabelJobs } from '@/features/parcels/label-jobs-panel';
import { ExportExcelButton } from '@/components/export-excel-button';
import { PrintButton } from '@/components/print-button';
import {
  deliveryNoteExportAction,
  deliveryNotePdfAction,
  parcelLabelAction,
} from '@/lib/actions/documents';
import { newestFirst } from '@/lib/newest-first';
import { ParcelStatusBadge } from '@/features/parcels/status-badge';
import { useRouteError } from '@/features/routes/use-route-error';
import { Link, useRouter } from '@/i18n/navigation';
import {
  addDeliveryNoteParcelsAction,
  deleteDeliveryNoteAction,
  removeDeliveryNoteParcelAction,
  restoreRemovedNoteParcelAction,
  validateDeliveryNoteAction,
} from '@/lib/actions/routes';

/**
 * Détail d'un bon — DOMAIN.md §1.12. Même écran pour un bon de ramassage et un
 * bon de dépôt : seuls la route de retour et le vocabulaire diffèrent.
 *
 * « Valider le bon » passe tous les colis au statut 3 « Colis ramassé »
 * (§2.3, `BonLivraisonController.php:175`) et, au-delà de 5 colis, exonère le
 * ramassage (§3.4). L'opération est refusée sur un bon déjà validé : le legacy
 * la rejouait, et décrémentait les frais une seconde fois.
 */
export function NoteDetailView({
  note,
  canManage = true,
  bulkJob = null,
}: {
  note: DeliveryNoteDetail;
  /** Lot W2 (D-239) : la dernière tâche de masse du bon, relue par la page. */
  bulkJob?: BulkJob | null;
  /**
   * `notes.write` — composer le bon et le VALIDER.
   *
   * Un vendeur lit ses bons (`notes.read`) et les DEMANDE (`notes.request`) ;
   * il ne les compose pas et ne les valide pas — valider, c'est attester que
   * le transporteur est passé prendre la marchandise, et celui qui la donne ne
   * peut pas certifier qu'elle a été prise (D-072).
   *
   * Les trois boutons lui étaient pourtant offerts, et chacun rendait
   * « Permission insuffisante pour cette opération ». Un bouton qui échoue
   * toujours n'est pas une sécurité, c'est une promesse fausse : l'API refusait
   * déjà, c'est l'écran qui mentait.
   */
  canManage?: boolean;
}) {
  const t = useTranslations('notes.detail');
  /** « Client corrigé le … par …, avant : … » — D-257, la même phrase que le PDF. */
  const correctionLabel = (correction: NoteParcelClientCorrection) => {
    const date = format.dateTime(new Date(correction.at), 'stamp');
    if (correction.byName && correction.fromClientName)
      return t('clientCorrectedFull', {
        date,
        by: correction.byName,
        from: correction.fromClientName,
      });
    if (correction.byName) return t('clientCorrectedBy', { date, by: correction.byName });
    return t('clientCorrectedOn', { date });
  };
  const tNotes = useTranslations('notes');
  const tStatus = useTranslations('noteStatus');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();
  const translateError = useRouteError();
  const tJobs = useTranslations('bulkJobs');
  // Le libellé « Exporter Excel » des fiches, le même que sur le circuit.
  const tExport = useTranslations('routes.detail');

  const [pending, startTransition] = useTransition();
  const [composing, setComposing] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  // Les planches d'étiquettes préparées en arrière-plan (plus de 500 colis).
  const { jobs: labelJobs, add: addLabelJob } = useLabelJobs();

  /*
   * LA VALIDATION PARTIELLE — Recette 5, point 7, validé par Anass. On coche
   * ce qui est arrivé ; le reste est retiré du bon (et supprimé logiquement),
   * après une confirmation qui dit les nombres. Cochés d'emblée : ce que le
   * livreur a déjà ramassé (18) s'il est passé, sinon tout le bon — le cas
   * courant est « tout est là ».
   */
  const selectable = canManage && (note.validatable || note.correctable);
  const defaultChecked = () => {
    const picked = note.parcels
      .filter((parcel) => parcel.statusId === 18)
      .map((parcel) => parcel.id);
    return new Set(picked.length > 0 ? picked : note.parcels.map((parcel) => parcel.id));
  };
  const [checked, setChecked] = useState<Set<string>>(defaultChecked);
  /*
   * Recette 6, point 2 : après une validation (ou une correction), le bon
   * rechargé repart de SON choix par défaut — la sélection d'avant portait sur
   * un autre état du bon. Recalculé pendant le rendu, pas dans un effet.
   */
  const noteState = `${note.status}|${note.parcels.map((parcel) => parcel.id).join(',')}`;
  const [seenNoteState, setSeenNoteState] = useState(noteState);
  if (seenNoteState !== noteState) {
    setSeenNoteState(noteState);
    setChecked(defaultChecked());
  }
  const [confirmingPartial, setConfirmingPartial] = useState(false);
  // Ouverte d'emblée quand on vient du lien « X colis retirés du bon » d'un circuit.
  const openRemoved = useSearchParams().get('removed') === '1';
  const [showRemoved, setShowRemoved] = useState(openRemoved);
  const total = note.parcelCount + note.removedCount;
  const missing = note.parcels.length - checked.size;
  const allChecked = note.parcels.length > 0 && missing === 0;

  function toggle(parcelId: string) {
    setChecked((current) => {
      const next = new Set(current);
      if (next.has(parcelId)) next.delete(parcelId);
      else next.add(parcelId);
      return next;
    });
  }

  function toggleAll() {
    setChecked(allChecked ? new Set() : new Set(note.parcels.map((parcel) => parcel.id)));
  }

  const backHref = note.kind === 'pickup' ? '/pickup-notes' : '/dropoff-notes';
  // Un bon validé ne se recompose pas : ses colis sont déjà passés au statut 3.
  const editable = canManage && note.status !== 'validated';

  /*
   * SUPPRIMER — point 0 du chantier « Retours, BL Clients Pro ».
   *
   * Le bouton est offert au vendeur comme à l'agent : c'est le vendeur qui
   * sème un bon vide en cliquant deux fois, et c'est lui qui doit pouvoir le
   * retirer. Les deux passent par des endpoints différents, sous des clés
   * différentes ; l'écran ne choisit que le chemin.
   *
   * Un bon VALIDÉ n'affiche rien : ses colis sont passés au statut 3, et le
   * bon est devenu la preuve du ramassage. Les deux autres refus (bon pris par
   * le livreur, bon porté par un circuit) viennent de l'API avec leur motif —
   * l'écran ne les devine pas, il les traduit.
   */
  const deletable = note.status !== 'validated';

  function onDelete() {
    startTransition(async () => {
      const result = await deleteDeliveryNoteAction(note.code, canManage ? 'staff' : 'merchant');

      if (result.ok) {
        toast.success(t('deleted'));
        setConfirmingDelete(false);
        router.push(backHref);
      } else {
        toast.error(translateError(result));
      }
    });
  }

  /** Le bouton : tout coché → direct ; sinon la confirmation chiffrée d'abord. */
  function onValidate() {
    if (checked.size === 0) return;
    if (missing > 0 || note.correctable) {
      setConfirmingPartial(true);
      return;
    }
    sendValidation();
  }

  function sendValidation() {
    const ids = missing > 0 || note.correctable ? [...checked] : undefined;
    const validatedCount = checked.size;
    const removedNow = missing;
    startTransition(async () => {
      const result = await validateDeliveryNoteAction(note.code, ids);
      if (result.ok && result.data) {
        // Lot W2 (D-239) : plus de 300 colis — la validation se fait par paquets ; la page la suit.
        setConfirmingPartial(false);
        toast.info(tJobs('started', { count: String(result.data.job.total) }));
        router.refresh();
      } else if (result.ok) {
        setConfirmingPartial(false);
        // Recette 5, point 8 : le message dit les nombres.
        toast.success(
          removedNow > 0
            ? `${t('validatedCount', { count: validatedCount, total: String(note.parcels.length) })} · ${t('removedCount', { count: removedNow })}`
            : t('validatedCount', { count: validatedCount, total: String(note.parcels.length) }),
        );
        router.refresh();
      } else {
        toast.error(translateError(result));
      }
    });
  }

  function onRestore(parcelId: string, code: string) {
    startTransition(async () => {
      const result = await restoreRemovedNoteParcelAction(note.code, parcelId);
      if (result.ok) {
        toast.success(t('restoredToast', { code }));
        router.refresh();
      } else {
        toast.error(translateError(result));
      }
    });
  }

  function onRemove(parcelId: string) {
    startTransition(async () => {
      const result = await removeDeliveryNoteParcelAction(note.code, parcelId);
      if (!result.ok) toast.error(translateError(result));
    });
  }

  return (
    <div className="flex flex-col gap-6">
      <BulkJobProgress job={bulkJob} />
      <LabelJobsPanel jobs={labelJobs} />
      <header className="flex flex-col gap-4">
        {/*
          « RETOUR » RECULE — chantier « Bon de retour livreur », point 4.

          Le lien vers la liste renvoyait à son PREMIER écran : l'agent qui
          venait de filtrer trois mille colis, page 7, recommençait. Les
          filtres, la recherche, la page et le tri vivent dans l'URL — reculer
          les rend intacts. Le `fallbackHref` ne sert qu'aux liens ouverts de
          nulle part.
        */}
        <BackButton fallbackHref={backHref} label={t('backToList')} />

        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0">
            <h1 className="font-mono text-xl font-semibold tracking-tight sm:text-2xl">
              {note.code}
            </h1>
            <div className="mt-2 flex flex-wrap items-center gap-2">
              <CellBadge label={tStatus(note.status)} color={NOTE_STATUS_COLORS[note.status]} />
              <Badge variant="secondary" className="rounded-full font-normal">
                {tNotes(`kind.${note.kind}`)}
              </Badge>
              {/* A2-5 (D-182) : le frais facturé pour ce bon, ou « offert ». */}
              {note.pickupFeeCharged ? (
                <Badge variant="outline" className="rounded-full font-normal">
                  {tNotes('pickupFeeCharged', {
                    amount: format.number(note.pickupFeeCharged / 100, 'currency'),
                  })}
                </Badge>
              ) : note.pickupWaived ? (
                <Badge variant="outline" className="rounded-full font-normal">
                  {tNotes('waived')}
                </Badge>
              ) : null}
              <span className="text-muted-foreground text-xs tabular-nums">
                {format.dateTime(new Date(note.createdAt), 'stamp')}
              </span>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            {editable ? (
              <Button
                variant="outline"
                size="sm"
                className="gap-1.5"
                onClick={() => setComposing(true)}
                disabled={pending}
              >
                <Plus className="size-4" aria-hidden />
                {t('addParcels')}
              </Button>
            ) : null}
            {canManage && note.correctable ? (
              <Button
                size="sm"
                variant="outline"
                className="gap-1.5"
                disabled={missing === 0 || checked.size === 0 || pending}
                onClick={onValidate}
              >
                {pending ? <Loader2 className="size-4 animate-spin" /> : null}
                {t('correct')}
              </Button>
            ) : canManage ? (
              <Button
                size="sm"
                className="gap-1.5"
                disabled={!note.validatable || checked.size === 0 || pending}
                onClick={onValidate}
              >
                {pending ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  <CheckCheck className="size-4" aria-hidden />
                )}
                {note.status === 'validated'
                  ? t('alreadyValidated')
                  : t('validateCount', { count: String(checked.size) })}
              </Button>
            ) : null}
            <PrintButton
              kind="document"
              icon="file"
              subject={note.code}
              label={t('print')}
              disabled={pending}
              onPrint={() => deliveryNotePdfAction(note.code)}
            />
            {/* Une ligne par colis ; pour tout compte qui lit le bon, vendeur compris. */}
            <ExportExcelButton
              label={tExport('exportExcel')}
              filename={note.code}
              disabled={pending || note.parcels.length === 0}
              onExport={() => deliveryNoteExportAction(note.code)}
            />
            {/*
              LES ÉTIQUETTES DE TOUT LE BON — remarques de Badr du 12/11, point a.
              À côté de « Imprimer le bon », pour l'admin comme pour le vendeur :
              le sélecteur A4 / 10×10 de la liste des colis, 10×10 par défaut.
            */}
            <NoteLabelsButton
              noteCode={note.code}
              parcelCodes={note.parcels.map((parcel) => parcel.code)}
              label={t('labels')}
              onJob={addLabelJob}
              disabled={pending}
            />
            {deletable ? (
              <Button
                variant="ghost"
                size="sm"
                className="text-muted-foreground hover:text-destructive gap-1.5"
                onClick={() => setConfirmingDelete(true)}
                disabled={pending}
              >
                <Trash2 className="size-4" aria-hidden />
                {t('delete')}
              </Button>
            ) : null}
          </div>
        </div>

        {canManage && note.validatable ? (
          <p className="bg-muted/60 text-muted-foreground rounded-md px-3 py-2 text-xs">
            {/* Un BD- est un DÉPÔT : pas de phrase sur le ramassage offert. */}
            {note.kind === 'dropoff'
              ? t('validateHintDropoff', { count: String(note.parcelCount) })
              : t('validateHint', {
                  count: String(note.parcelCount),
                  threshold: String(note.pickupRule?.freeFrom ?? 5),
                  fee: format.number((note.pickupRule?.fee ?? 0) / 100, 'currency'),
                })}
          </p>
        ) : null}
        {canManage && note.correctable ? (
          <p className="bg-muted/60 text-muted-foreground rounded-md px-3 py-2 text-xs">
            {t('correctHint')}
          </p>
        ) : null}
      </header>

      <div className="grid gap-6 lg:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle>{t('summary')}</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <Field icon={Store} label={t('client')} value={note.clientName} />
            <Field icon={Building2} label={t('city')} value={note.cityName} />
            <div>
              <p className="text-muted-foreground text-xs">{t('parcelCount')}</p>
              <p className="text-sm tabular-nums">{format.number(total, 'integer')}</p>
            </div>
            {/* « 150/200 ramassés » — Recette 5, points 7 et 8. */}
            <div>
              <p className="text-muted-foreground text-xs">{t('picked')}</p>
              <p className="text-sm font-medium tabular-nums">
                {t('pickedValue', { picked: String(note.pickedCount), total: String(total) })}
              </p>
              <p className="text-muted-foreground text-xs tabular-nums">
                {t('remaining', {
                  count: Math.max(total - note.pickedCount - note.removedCount, 0),
                })}
                {note.removedCount > 0
                  ? ` · ${t('removedCount', { count: note.removedCount })}`
                  : ''}
              </p>
            </div>
            {note.validatedAt ? (
              <div>
                <p className="text-muted-foreground text-xs">{t('validatedAt')}</p>
                <p className="text-sm tabular-nums">
                  {format.dateTime(new Date(note.validatedAt), 'stamp')}
                </p>
              </div>
            ) : null}
          </CardContent>
        </Card>

        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="flex flex-wrap items-center gap-x-3 gap-y-1">
              {tCommon('shipments')}
              {/* « 3 expéditions · 6 cartons », ou « 3 expéditions » — libellé de Badr, 11/11. */}
              <CartonTotal lines={note.parcels} />
            </CardTitle>
            <CardDescription>{t('parcelsHint')}</CardDescription>
          </CardHeader>
          <CardContent>
            {note.parcels.length === 0 ? (
              <p className="text-muted-foreground text-sm">{t('noParcels')}</p>
            ) : (
              <ul className="flex flex-col gap-2">
                {selectable ? (
                  <li className="bg-muted/40 flex items-center gap-3 rounded-lg border px-3 py-2">
                    <Checkbox
                      id="note-check-all"
                      checked={allChecked ? true : checked.size > 0 ? 'indeterminate' : false}
                      onCheckedChange={toggleAll}
                      disabled={pending}
                    />
                    <label htmlFor="note-check-all" className="flex-1 text-sm font-medium">
                      {t('checkAll')}
                    </label>
                    <span className="text-muted-foreground text-xs tabular-nums">
                      {t('checked', {
                        done: checked.size,
                        total: String(note.parcels.length),
                      })}
                    </span>
                  </li>
                ) : null}
                {newestFirst(note.parcels).map((parcel) => (
                  <li
                    key={parcel.id}
                    className="flex flex-wrap items-start gap-3 rounded-lg border p-3"
                  >
                    {selectable ? (
                      <Checkbox
                        className="mt-0.5"
                        checked={checked.has(parcel.id)}
                        onCheckedChange={() => toggle(parcel.id)}
                        disabled={pending}
                        aria-label={parcel.code}
                      />
                    ) : null}
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <Link
                          href={`/parcels/${parcel.code}`}
                          className="hover:text-primary font-mono text-sm font-medium underline-offset-2 hover:underline"
                        >
                          {parcel.code}
                        </Link>
                        <ParcelStatusBadge statusId={parcel.statusId} />
                        <CartonCell line={parcel} lines={note.parcels} />
                        {/*
                          D-257 : le client du colis a été corrigé — sur tout bon
                          qui le porte. Le motif au survol (agence seulement).
                        */}
                        {parcel.clientCorrection ? (
                          <span title={parcel.clientCorrection.reason ?? undefined}>
                            <CellBadge
                              label={correctionLabel(parcel.clientCorrection)}
                              color="var(--warning)"
                            />
                          </span>
                        ) : parcel.clientCorrected ? (
                          <CellBadge label={t('clientCorrected')} color="var(--warning)" />
                        ) : null}
                      </div>
                      <p className="text-muted-foreground mt-0.5 truncate text-xs">
                        {parcel.description}
                      </p>
                      <p className="text-muted-foreground truncate text-xs">
                        {parcel.recipientName} ·{' '}
                        {[parcel.destinationDistrictName, parcel.destinationCityName]
                          .filter(Boolean)
                          .join(' · ')}
                      </p>
                    </div>

                    <div className="flex shrink-0 items-center gap-2">
                      <div className="flex flex-col items-end">
                        <span className="text-sm font-medium tabular-nums">
                          {format.number(parcel.codAmount / 100, 'currency')}
                        </span>
                        <span className="text-muted-foreground text-xs tabular-nums">
                          {format.number(parcel.deliveryFee / 100, 'currency')}
                        </span>
                      </div>

                      {/*
                        L'ÉTIQUETTE DU COLIS, depuis le bon — recette 3, point 1.
                        Le même bouton, le même PDF et le même gabarit tenant que
                        la fiche colis (10×10 par défaut). Toujours là, bon
                        validé compris ; l'API la borne à la portée du compte.
                      */}
                      <PrintButton
                        kind="label"
                        subject={parcel.code}
                        icon="download"
                        iconOnly
                        variant="ghost"
                        label={t('downloadLabel', { code: parcel.code })}
                        className="text-muted-foreground"
                        onPrint={(labelFormat) => parcelLabelAction(parcel.code, labelFormat)}
                      />

                      {editable ? (
                        <Button
                          variant="ghost"
                          size="icon"
                          className="text-muted-foreground hover:text-destructive size-8"
                          onClick={() => onRemove(parcel.id)}
                          disabled={pending}
                          aria-label={t('removeParcel', { code: parcel.code })}
                        >
                          <Trash2 className="size-4" aria-hidden />
                        </Button>
                      ) : null}
                    </div>
                  </li>
                ))}
              </ul>
            )}

            {/*
              LES NON RAMASSÉS — masqués par défaut, un lien les montre
              (Recette 5, point 7). L'agence peut restaurer un colis retiré
              par erreur — il revient « En attente de ramassage », hors bon —
              TANT QUE le bon n'est pas validé ni son circuit clôturé (recette
              6, point 3) : ensuite la liste est en lecture seule.
            */}
            {canManage && note.removedParcels.length > 0 ? (
              <div className="mt-4 flex flex-col gap-2">
                <button
                  type="button"
                  className="text-primary w-fit text-sm font-medium underline-offset-2 hover:underline"
                  onClick={() => setShowRemoved((open) => !open)}
                >
                  {showRemoved
                    ? t('hideRemoved')
                    : t('showRemoved', { count: note.removedParcels.length })}
                </button>
                {showRemoved && note.status === 'validated' ? (
                  <p className="text-muted-foreground text-xs">{t('removedReadOnly')}</p>
                ) : null}
                {showRemoved ? (
                  <ul id="removed" className="flex flex-col gap-2">
                    {note.removedParcels.map((parcel) => (
                      <li
                        key={parcel.id}
                        className="bg-muted/30 flex flex-wrap items-center gap-3 rounded-lg border border-dashed p-3"
                      >
                        <div className="min-w-0 flex-1">
                          <p className="font-mono text-sm font-medium">{parcel.code}</p>
                          <p className="text-muted-foreground truncate text-xs">
                            {parcel.recipientName} ·{' '}
                            {parcel.removedByName
                              ? t('removedBy', {
                                  date: format.dateTime(new Date(parcel.removedAt), 'stamp'),
                                  name: parcel.removedByName,
                                })
                              : t('removedAt', {
                                  date: format.dateTime(new Date(parcel.removedAt), 'stamp'),
                                })}
                          </p>
                          <p className="text-muted-foreground text-xs">{parcel.reason}</p>
                        </div>
                        {parcel.restoredAt ? (
                          <Badge variant="outline" className="rounded-full font-normal">
                            {t('restored')}
                          </Badge>
                        ) : canManage && parcel.restorable ? (
                          <Button
                            variant="outline"
                            size="sm"
                            className="gap-1.5"
                            disabled={pending}
                            onClick={() => onRestore(parcel.id, parcel.code)}
                          >
                            <RotateCcw className="size-4" aria-hidden />
                            {t('restore')}
                          </Button>
                        ) : null}
                      </li>
                    ))}
                  </ul>
                ) : null}
              </div>
            ) : null}

            {/*
              D-257 : LES COLIS SORTIS DU BON — client changé ou supprimé. Ils
              restent lisibles dans l'historique du bon, sans compter dans ses
              totaux (le bon ne les porte plus).
            */}
            {note.departedParcels && note.departedParcels.length > 0 ? (
              <div className="mt-4 flex flex-col gap-2">
                <p className="text-sm font-medium">
                  {t('departedTitle', { count: note.departedParcels.length })}
                </p>
                <ul className="flex flex-col gap-2">
                  {note.departedParcels.map((parcel) => (
                    <li
                      key={`${parcel.code}-${parcel.at}`}
                      className="bg-muted/30 flex flex-col gap-0.5 rounded-lg border border-dashed p-3"
                      title={parcel.reason ?? undefined}
                    >
                      <p className="font-mono text-sm font-medium">{parcel.code}</p>
                      <p className="text-muted-foreground text-xs">
                        {parcel.recipientName} ·{' '}
                        {t(parcel.kind === 'deleted' ? 'departedDeleted' : 'departedClientChange', {
                          date: format.dateTime(new Date(parcel.at), 'stamp'),
                          by: parcel.byName ?? '-',
                        })}
                      </p>
                      {parcel.reason ? (
                        <p className="text-muted-foreground text-xs">{parcel.reason}</p>
                      ) : null}
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
          </CardContent>
        </Card>
      </div>

      <Dialog open={confirmingPartial} onOpenChange={setConfirmingPartial}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {note.correctable
                ? t('correctTitle', { missing })
                : t('partialTitle', { count: checked.size, total: String(note.parcels.length) })}
            </DialogTitle>
            <DialogDescription>
              {t('partialBody', {
                missing,
                count: String(checked.size),
                total: String(note.parcels.length),
              })}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setConfirmingPartial(false)}
              disabled={pending}
            >
              {tCommon('cancel')}
            </Button>
            <Button variant="destructive" onClick={sendValidation} disabled={pending}>
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {note.correctable ? t('correct') : t('partialConfirm')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={confirmingDelete} onOpenChange={setConfirmingDelete}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('delete')}</DialogTitle>
            <DialogDescription>{t('deleteConfirm', { code: note.code })}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmingDelete(false)} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            <Button variant="destructive" onClick={onDelete} disabled={pending}>
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('delete')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {composing ? (
        <NoteComposer
          clientId={note.clientId}
          onSubmit={(selection) => addDeliveryNoteParcelsAction(note.code, selection)}
          onDone={() => {
            setComposing(false);
            router.refresh();
          }}
        />
      ) : null}
    </div>
  );
}

function Field({
  icon: Icon,
  label,
  value,
}: {
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  value: string;
}) {
  return (
    <div className="flex items-start gap-3">
      <Icon className="text-muted-foreground mt-0.5 size-4 shrink-0" aria-hidden />
      <div className="min-w-0">
        <p className="text-muted-foreground text-xs">{label}</p>
        <p className="text-sm">{value}</p>
      </div>
    </div>
  );
}
