'use client';

import {
  type BulkJob,
  filterRouteParcels,
  findParcelStatus,
  isBulkJobAccepted,
} from '@delivery/shared';
import { BulkJobProgress } from '@/features/bulk-jobs/bulk-job-progress';
import {
  type City,
  type Courier,
  NOTE_STATUS_COLORS,
  ROUTE_STATUS_COLORS,
  type RouteDetail,
  type RouteParcel,
  routeParcelAwaitsOutcome,
  routeTargetsSender,
} from '@delivery/shared';
import {
  CheckCheck,
  ClipboardList,
  Loader2,
  Lock,
  MapPin,
  Pencil,
  Plus,
  Search,
  Send,
  Store,
  Trash2,
  Truck,
} from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { BackButton } from '@/components/back-button';
import { CartonCell, CartonTotal } from '@/components/carton-count';
import { CellBadge } from '@/components/data-table/cells';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { SearchableSelect } from '@/components/searchable-select';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ExportExcelButton } from '@/components/export-excel-button';
import { PrintButton } from '@/components/print-button';
import { ParcelStatusBadge } from '@/features/parcels/status-badge';
import { CorrectedMark } from '@/features/parcels/corrected-mark';
import { ParcelOutcomeDialog } from '@/features/routes/parcel-outcome-dialog';
import { RouteBulkDialog } from '@/features/routes/route-bulk-dialog';
import { ParcelTransferDialog } from '@/features/routes/parcel-transfer-dialog';
import { RouteComposer } from '@/features/routes/route-composer';
import { RouteNoteComposer } from '@/features/routes/route-note-composer';
import { routeStopOf, RouteStopActions } from '@/features/routes/route-stop';
import { useRouteError } from '@/features/routes/use-route-error';
import { RouteDraftDialogs } from '@/features/routes/route-draft-actions';
import { Link, useRouter } from '@/i18n/navigation';
import { routeExportAction, routeLabelsAction, routeSheetAction } from '@/lib/actions/documents';
import { newestFirst } from '@/lib/newest-first';
import { NoteValidateDialog } from '@/features/notes/note-validate-dialog';
import {
  closeRouteAction,
  dispatchRouteAction,
  removeRouteNoteAction,
  removeRouteParcelAction,
} from '@/lib/actions/routes';

/**
 * Détail d'un circuit — DOMAIN.md §1.13.
 *
 * L'écran suit la vie du circuit, et change avec elle :
 *  - en BROUILLON on compose (ajouter, retirer) et on fait partir ;
 *  - une fois PARTI, on ne compose plus, on rend compte colis par colis ;
 *  - la clôture n'ouvre que lorsque TOUS les colis sont confirmés — le legacy
 *    laissait clôturer à tout moment, et les colis restés en main du livreur
 *    disparaissaient du suivi.
 *
 * Le départ est le geste qui manquait à toute la chaîne : c'est lui qui pose
 * `parcel.courierId`, sans quoi aucun retour ne peut être remis (C3).
 */
export function RouteDetailView({
  route,
  cities = [],
  couriers = [],
  transferCouriers = null,
  bulkJob = null,
}: {
  route: RouteDetail;
  /** D-173 — pour modifier un brouillon ; vides sur un circuit parti. */
  cities?: City[];
  couriers?: Courier[];
  /**
   * « Transférer (X) » — lot I, I1 (D-211) : les livreurs proposés, lus par la
   * page pour un circuit de livraison parti quand le compte compose les
   * circuits. `null` : pas de bouton.
   */
  transferCouriers?: Courier[] | null;
  /** Lot W2 (D-239) : la dernière tâche de masse du circuit, relue par la page. */
  bulkJob?: BulkJob | null;
}) {
  const t = useTranslations('routes.detail');
  const tForm = useTranslations('routes.form');
  /** D-173 — le dialogue de brouillon ouvert : modifier ou supprimer. */
  const tJobs = useTranslations('bulkJobs');
  const [draftMode, setDraftMode] = useState<'edit' | 'delete' | null>(null);
  const tNotes = useTranslations('routes.notes');
  const tCommon = useTranslations('common');
  const tBulk = useTranslations('routes.bulk');
  const tNoteStatus = useTranslations('noteStatus');
  const tStatus = useTranslations('routeStatus');
  const tKind = useTranslations('routeKind');
  const tParcelStatus = useTranslations('status');
  const format = useFormatter();
  const router = useRouter();
  const translateError = useRouteError();

  const [pending, startTransition] = useTransition();
  const [composing, setComposing] = useState(false);
  /** La composition par BONS, distincte de celle par colis — point 5. */
  const [composingNotes, setComposingNotes] = useState(false);
  const [reporting, setReporting] = useState<RouteParcel | null>(null);
  /** Le bon en cours de validation depuis le circuit — Recette 5, point 7. */
  const [validating, setValidating] = useState<string | null>(null);
  /** D-145 : la confirmation de la clôture d'un ramassage incomplet. */
  const [confirmingClose, setConfirmingClose] = useState(false);
  /** D-146 : les colis cochés, et la boîte de l'action en masse. */
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [bulkOpen, setBulkOpen] = useState(false);
  /** Lot I, I1 : la boîte « Transférer » des cochés. */
  const [transferOpen, setTransferOpen] = useState(false);
  const tTransfer = useTranslations('routes.transfer');
  const tNotesDetail = useTranslations('notes.detail');

  const closed = route.status === 'closed';
  /*
   * LE TOTAL compte les colis retirés des bons (non remis) : « 62/199
   * ramassés », et pas « 62/62 » — Recette 5, points 7 et 8.
   */
  const routeTotal = route.parcelCount + route.removedCount;
  const progress = routeTotal === 0 ? 0 : route.confirmedCount / routeTotal;
  const deliveredCount = route.parcels.filter((parcel) => parcel.statusId === 4).length;

  /** Les colis d'un bon, pris dans ceux du circuit. */
  const noteParcels = (note: RouteDetail['notes'][number]) => {
    const ids = new Set(note.parcelIds);
    return route.parcels.filter((parcel) => ids.has(parcel.id));
  };
  /** Ramassés : en main du livreur (18), reçus (3), ou confirmés sur le circuit. */
  const notePicked = (note: RouteDetail['notes'][number]) =>
    note.status === 'validated'
      ? note.parcelCount
      : noteParcels(note).filter(
          (parcel) => parcel.confirmed || parcel.statusId === 18 || parcel.statusId === 3,
        ).length;
  const validatingNote = route.notes.find((note) => note.id === validating);
  /** Ramassage et retour vont chez l'expéditeur — C12 bloc 4. */
  const targetsSender = routeTargetsSender(route.kind);

  /*
   * LA SÉLECTION — chantier « Sélection en masse sur les circuits » (D-146).
   *
   * La case d'une ligne SÉLECTIONNE le colis ; elle ne l'atteste plus « en
   * main » d'un clic (l'attestation reste dans « Rendre compte », colis par
   * colis). Sur deux cents colis, cocher puis agir une fois remplace deux cents
   * gestes. Les cochés qui ont quitté le circuit sont oubliés.
   *
   * Seuls les colis qui ATTENDENT encore l'action se cochent (recette 6,
   * point 2) : un colis déjà ramassé, ou déjà rendu compte, n'est plus proposé,
   * et le bouton compte ce qui reste vraiment à faire.
   */
  /*
   * LA RECHERCHE ET LE FILTRE DE STATUT — chantier « Affichage web et
   * étiquette », point 2. À l'écran, sans requête : la fiche a déjà toutes les
   * lignes du circuit.
   *
   * « Tout cocher » ne coche que les lignes AFFICHÉES, et l'action en masse ne
   * porte que sur les cochées encore affichées : un colis coché puis masqué par
   * la recherche ne part pas dans un geste que l'agent ne voit plus. Effacer la
   * recherche le fait réapparaître, toujours coché.
   */
  const [query, setQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>(ALL_STATUSES);
  const statusId = statusFilter === ALL_STATUSES ? undefined : Number(statusFilter);
  const visibleParcels = filterRouteParcels(route.parcels, query, statusId);
  const filtering = query.trim() !== '' || statusId !== undefined;
  const statusOptions = [...new Set(route.parcels.map((parcel) => parcel.statusId))]
    .sort((a, b) => a - b)
    .map((id) => {
      const status = findParcelStatus(id);
      return {
        value: String(id),
        label: status ? tParcelStatus(status.key) : `#${id}`,
        color: status?.color,
      };
    });

  const actionable = !(route.editable || closed) && route.parcels.length > 0;
  const allSelectable = actionable
    ? route.parcels.filter((parcel) => routeParcelAwaitsOutcome(route.kind, parcel))
    : [];
  const visibleIds = new Set(visibleParcels.map((parcel) => parcel.id));
  const selectable = allSelectable.filter((parcel) => visibleIds.has(parcel.id));
  const selectedParcels = selectable.filter((parcel) => selected.has(parcel.id));
  const allSelected = selectable.length > 0 && selectedParcels.length === selectable.length;

  function toggleSelected(parcelId: string, on: boolean) {
    setSelected((current) => {
      const next = new Set(current);
      if (on) next.add(parcelId);
      else next.delete(parcelId);
      return next;
    });
  }

  function onRemoveNote(noteId: string) {
    startTransition(async () => {
      const result = await removeRouteNoteAction(route.code, noteId);
      if (!result.ok) toast.error(translateError(result));
    });
  }

  function onRemove(parcelId: string) {
    startTransition(async () => {
      const result = await removeRouteParcelAction(route.code, parcelId);
      if (!result.ok) toast.error(translateError(result));
    });
  }

  function onDispatch() {
    startTransition(async () => {
      const result = await dispatchRouteAction(route.code);
      if (result.ok && isBulkJobAccepted(result.data)) {
        // Lot W2 (D-239) : plus de 300 colis — le départ se fait par paquets ; la page le suit.
        toast.info(tJobs('started', { count: String(result.data.job.total) }));
        router.refresh();
      } else if (result.ok) {
        toast.success(t('dispatched', { count: route.parcelCount }));
      } else {
        toast.error(translateError(result));
      }
    });
  }

  /*
   * LA CLÔTURE D'UN RAMASSAGE AVEC DES COLIS NON RAMASSÉS — D-145. Ils ne
   * bloquent plus : la clôture les retire de leur bon (non remis par le
   * vendeur), après une confirmation qui les NOMME. Ailleurs, la clôture part
   * directement — le serveur refuse ce qui manque, et le dit.
   */
  const unpicked = targetsSender && route.kind === 'pickup' ? route.pendingParcels : [];

  /*
   * LE MESSAGE AU-DESSUS DU CIRCUIT, utile — D-145. Ramassage : combien de colis
   * ne sont pas ramassés, et ce que la clôture en fera ; rien de ramassé : il
   * n'y a rien à clôturer. Livraison : combien manquent, et LESQUELS.
   */
  const closeHint =
    route.kind === 'pickup'
      ? route.closurePicked === 0
        ? t('closeNothingPicked')
        : unpicked.length > 0
          ? t('closeUnpickedHint', { count: unpicked.length, total: String(routeTotal) })
          : !route.closable
            ? t('closeLocked')
            : null
      : !route.closable
        ? route.pendingParcels.length > 0
          ? t('closeMissing', {
              count: route.pendingParcels.length,
              total: String(routeTotal),
              codes:
                route.pendingParcels
                  .slice(0, 8)
                  .map((parcel) => parcel.code)
                  .join(', ') + (route.pendingParcels.length > 8 ? '…' : ''),
            })
          : t('closeLocked')
        : null;

  function onClose(removeUnpicked = false) {
    if (unpicked.length > 0 && !removeUnpicked) {
      setConfirmingClose(true);
      return;
    }
    startTransition(async () => {
      const result = await closeRouteAction(route.code, removeUnpicked);
      if (result.ok) {
        setConfirmingClose(false);
        toast.success(t('closed'));
      } else {
        toast.error(translateError(result));
      }
    });
  }

  return (
    <div className="flex flex-col gap-6">
      <BulkJobProgress job={bulkJob} />
      <header className="flex flex-col gap-4">
        {/*
          « RETOUR » RECULE — chantier « Bon de retour livreur », point 4.

          Le lien vers la liste renvoyait à son PREMIER écran : l'agent qui
          venait de filtrer trois mille colis, page 7, recommençait. Les
          filtres, la recherche, la page et le tri vivent dans l'URL — reculer
          les rend intacts. Le `fallbackHref` ne sert qu'aux liens ouverts de
          nulle part.
        */}
        <BackButton fallbackHref="/routes" label={t('backToList')} />

        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0">
            <h1 className="font-mono text-xl font-semibold tracking-tight sm:text-2xl">
              {route.code}
            </h1>
            <div className="mt-2 flex flex-wrap items-center gap-2">
              <CellBadge label={tStatus(route.status)} color={ROUTE_STATUS_COLORS[route.status]} />
              <Badge variant="secondary" className="rounded-full font-normal">
                {tKind(route.kind)}
              </Badge>
              <span className="text-muted-foreground text-xs tabular-nums">
                {format.dateTime(new Date(route.statusAt), 'stamp')}
              </span>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            {/* D-173 : modifier / supprimer, sur un BROUILLON seulement. */}
            {route.status === 'draft' ? (
              <>
                <Button
                  variant="outline"
                  size="sm"
                  className="gap-1.5"
                  onClick={() => setDraftMode('edit')}
                  disabled={pending}
                >
                  <Pencil className="size-4" aria-hidden />
                  {tForm('edit')}
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  className="text-destructive gap-1.5"
                  onClick={() => setDraftMode('delete')}
                  disabled={pending}
                >
                  <Trash2 className="size-4" aria-hidden />
                  {tForm('delete')}
                </Button>
              </>
            ) : null}
            {route.editable ? (
              <>
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
                <Button
                  size="sm"
                  className="gap-1.5"
                  onClick={onDispatch}
                  disabled={!route.dispatchable || pending}
                  title={route.dispatchable ? undefined : t('dispatchLocked')}
                >
                  {pending ? (
                    <Loader2 className="size-4 animate-spin" />
                  ) : (
                    <Send className="flip-rtl size-4" aria-hidden />
                  )}
                  {t('dispatch')}
                </Button>
              </>
            ) : route.kind === 'delivery' ? (
              /*
               * Un circuit de LIVRAISON ne se clôt plus à la main — Recette 7,
               * point 8 (D-155) : il se clôt tout seul quand chaque colis est
               * livré ou rentré par un bon de retour reçu. Le ramassage garde
               * son bouton (D-145).
               */
              <Badge variant={closed ? 'secondary' : 'outline'} className="gap-1.5">
                {closed ? <Lock className="size-3.5" aria-hidden /> : null}
                {closed ? t('alreadyClosed') : t('autoCloseHint')}
              </Badge>
            ) : (
              <Button
                onClick={() => onClose()}
                size="sm"
                disabled={!route.closable || pending}
                className="gap-1.5"
                title={route.closable ? undefined : (closeHint ?? t('closeLocked'))}
              >
                {pending ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : closed ? (
                  <Lock className="size-4" aria-hidden />
                ) : (
                  <CheckCheck className="size-4" aria-hidden />
                )}
                {closed ? t('alreadyClosed') : t('close')}
              </Button>
            )}

            <PrintButton
              kind="document"
              icon="file"
              subject={route.code}
              label={t('sheet')}
              disabled={pending}
              onPrint={() => routeSheetAction(route.code)}
            />

            {/* Une ligne par colis — le même classeur que l'export des listes. */}
            <ExportExcelButton
              label={t('exportExcel')}
              filename={route.code}
              disabled={pending || route.parcels.length === 0}
              onExport={() => routeExportAction(route.code)}
            />

            <PrintButton
              kind="label"
              subject={route.code}
              count={route.parcelCount}
              label={t('labels')}
              onPrint={(format) => routeLabelsAction(route.code, format)}
            />
          </div>
        </div>

        {route.editable ? (
          <p className="bg-muted/60 text-muted-foreground rounded-md px-3 py-2 text-xs">
            {route.dispatchable ? t('draftHint') : t('dispatchLocked')}
          </p>
        ) : !closed && closeHint ? (
          <p className="bg-muted/60 text-muted-foreground rounded-md px-3 py-2 text-xs">
            {closeHint}
          </p>
        ) : null}
      </header>

      {/* LA CONFIRMATION QUI NOMME LES COLIS RETIRÉS — D-145. */}
      <Dialog
        open={confirmingClose}
        onOpenChange={(next) => (pending ? undefined : setConfirmingClose(next))}
      >
        <DialogContent className="max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{t('closeConfirmTitle', { code: route.code })}</DialogTitle>
            <DialogDescription>
              {t('closeConfirm', {
                picked: String(route.closurePicked),
                total: String(routeTotal),
                count: unpicked.length,
              })}
            </DialogDescription>
          </DialogHeader>
          <ul className="flex flex-col gap-1.5">
            {unpicked.map((parcel) => (
              <li
                key={parcel.id}
                className="flex items-center justify-between gap-3 rounded-md border px-3 py-2"
              >
                <span className="font-mono text-sm">{parcel.code}</span>
                {parcel.noteCode ? (
                  <span className="text-muted-foreground font-mono text-xs">{parcel.noteCode}</span>
                ) : null}
              </li>
            ))}
          </ul>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmingClose(false)} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            <Button variant="destructive" onClick={() => onClose(true)} disabled={pending}>
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('closeConfirmAction')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-1">
          <CardHeader>
            <CardTitle>{t('summary')}</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <Field icon={Truck} label={t('courier')} value={route.courierName} />
            <Field icon={MapPin} label={t('city')} value={route.cityName} />

            <div>
              <div className="mb-1.5 flex items-baseline justify-between gap-2 text-sm">
                <span className="text-muted-foreground text-xs">{t('progress')}</span>
                <span className="tabular-nums">
                  {targetsSender
                    ? tNotesDetail('pickedValue', {
                        picked: String(route.confirmedCount),
                        total: String(routeTotal),
                      })
                    : `${route.confirmedCount} / ${routeTotal}`}
                </span>
              </div>
              {/* Barre de progression maison : `w-*` arbitraire en style inline,
                  parce que le pourcentage n'est pas une classe compilable. */}
              <div className="bg-muted h-2 w-full overflow-hidden rounded-full">
                <div
                  className="bg-primary h-full rounded-full transition-[width]"
                  style={{ width: `${Math.round(progress * 100)}%` }}
                  role="progressbar"
                  aria-valuenow={route.confirmedCount}
                  aria-valuemin={0}
                  aria-valuemax={routeTotal}
                  aria-label={t('progress')}
                />
              </div>
              <p className="text-muted-foreground mt-1 text-xs tabular-nums">
                {targetsSender
                  ? null
                  : route.kind === 'delivery' && !closed
                    ? `${t('autoProgress', { delivered: String(route.deliveredCount), toReturn: String(route.toReturnCount) })}${
                        route.pendingHandoverCount > 0
                          ? ` (${t('autoPending', { count: route.pendingHandoverCount })})`
                          : ''
                      } · `
                    : `${t('deliveredValue', { delivered: String(deliveredCount), total: String(route.parcelCount) })} · `}
                {tNotesDetail('remaining', {
                  count: Math.max(route.parcelCount - route.confirmedCount, 0),
                })}
                {/* Recette 6, point 3 : un LIEN vers la liste des non ramassés du bon. */}
                {route.removedCount > 0 && !route.notes.some((note) => note.removedCount > 0)
                  ? ` · ${tNotesDetail('removedCount', { count: route.removedCount })}`
                  : null}
                {route.notes
                  .filter((note) => note.removedCount > 0)
                  .map((note) => (
                    <span key={note.id}>
                      {' · '}
                      <Link
                        href={`/pickup-notes/${note.code}?removed=1`}
                        title={note.code}
                        className="text-primary underline-offset-2 hover:underline"
                      >
                        {tNotesDetail('removedCount', { count: note.removedCount })}
                      </Link>
                    </span>
                  ))}
              </p>
            </div>

            {route.codCollectedTotal > 0 && !targetsSender ? (
              <div>
                <p className="text-muted-foreground text-xs">{t('collected')}</p>
                <p className="text-sm font-medium tabular-nums">
                  {format.number(route.codCollectedTotal / 100, 'currency')}
                </p>
              </div>
            ) : null}

            {route.exitDate ? (
              <div>
                <p className="text-muted-foreground text-xs">{t('exitDate')}</p>
                <p className="text-sm tabular-nums">
                  {format.dateTime(new Date(route.exitDate), 'stamp')}
                </p>
              </div>
            ) : null}
          </CardContent>
        </Card>

        {/*
          LES BONS DU CIRCUIT — retouches Anass, point 5.

          La carte n'existe que sur un ramassage, et seulement s'il en porte ou
          s'il peut en porter : sur une tournée de livraison, un encadré vide
          intitulé « Bons » ne dirait rien à personne.
        */}
        {route.kind === 'pickup' && (route.notes.length > 0 || route.editable) ? (
          <Card className="lg:col-span-3">
            <CardHeader className="flex flex-row items-start justify-between gap-3">
              <div className="min-w-0">
                <CardTitle>{tNotes('title')}</CardTitle>
                <CardDescription>{tNotes('hint')}</CardDescription>
              </div>
              {route.editable ? (
                <Button
                  variant="outline"
                  size="sm"
                  className="gap-1.5"
                  onClick={() => setComposingNotes(true)}
                  disabled={pending}
                >
                  <Plus className="size-4" aria-hidden />
                  {tNotes('addNotes')}
                </Button>
              ) : null}
            </CardHeader>
            <CardContent>
              {route.notes.length === 0 ? (
                <p className="text-muted-foreground text-sm">{tNotes('none')}</p>
              ) : (
                <ul className="flex flex-col gap-2">
                  {route.notes.map((note) => (
                    <li
                      key={note.id}
                      className="flex flex-wrap items-start gap-3 rounded-lg border p-3"
                    >
                      <ClipboardList
                        className="text-muted-foreground mt-0.5 size-4 shrink-0"
                        aria-hidden
                      />
                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <Link
                            href={`/pickup-notes/${note.code}`}
                            className="hover:text-primary font-mono text-sm font-medium underline-offset-2 hover:underline"
                          >
                            {note.code}
                          </Link>
                          <CellBadge
                            label={tNoteStatus(note.status)}
                            color={NOTE_STATUS_COLORS[note.status]}
                          />
                        </div>
                        {/* LE NOM DU VENDEUR : c'est chez lui que le livreur va. */}
                        <p className="mt-0.5 flex items-center gap-1.5 text-sm">
                          <Store className="text-muted-foreground size-3.5 shrink-0" aria-hidden />
                          <span className="truncate">{note.clientName}</span>
                        </p>
                        <p className="text-muted-foreground truncate text-xs">
                          {note.pickupAddress || note.cityName}
                        </p>
                      </div>

                      <div className="flex shrink-0 items-center gap-2">
                        {/* « ramassés X/Y » — Recette 5, points 7 et 8. */}
                        <span className="text-sm font-medium tabular-nums">
                          {tNotesDetail('pickedValue', {
                            picked: String(notePicked(note)),
                            total: String(note.parcelCount + note.removedCount),
                          })}
                        </span>

                        {/* VALIDER LE BON D'ICI, en partie si besoin (point 7). */}
                        {!route.editable && !closed && note.status !== 'validated' ? (
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => setValidating(note.id)}
                            disabled={pending}
                          >
                            {tNotesDetail('validate')}
                          </Button>
                        ) : null}

                        {route.editable ? (
                          <Button
                            variant="ghost"
                            size="icon"
                            className="text-muted-foreground hover:text-destructive size-8"
                            onClick={() => onRemoveNote(note.id)}
                            disabled={pending}
                            aria-label={tNotes('remove', { code: note.code })}
                          >
                            <Trash2 className="size-4" aria-hidden />
                          </Button>
                        ) : null}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        ) : null}

        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="flex flex-wrap items-center gap-x-3 gap-y-1">
              {tCommon('shipments')}
              {/* « 3 expéditions · 6 cartons », ou « 3 expéditions » — libellé de Badr, 11/11. */}
              <CartonTotal lines={route.parcels} />
            </CardTitle>
            <CardDescription>
              {route.editable ? t('parcelsDraftHint') : t('parcelsHint')}
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            {route.parcels.length > 0 ? (
              <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
                <div className="relative min-w-0 flex-1 sm:min-w-[280px]">
                  <Search className="text-muted-foreground pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2" />
                  <Input
                    value={query}
                    onChange={(event) => setQuery(event.target.value)}
                    placeholder={t('searchShipments')}
                    aria-label={tCommon('search')}
                    className="ps-9"
                  />
                </div>
                <SearchableSelect
                  options={statusOptions}
                  value={statusFilter}
                  onValueChange={setStatusFilter}
                  emptyOption={{ value: ALL_STATUSES, label: t('allStatuses') }}
                  aria-label={t('allStatuses')}
                  className="w-full sm:w-56"
                />
                {filtering ? (
                  <span className="text-muted-foreground shrink-0 text-xs tabular-nums">
                    {t('shownCount', {
                      shown: String(visibleParcels.length),
                      total: String(route.parcels.length),
                    })}
                  </span>
                ) : null}
              </div>
            ) : null}

            {/* « TOUT COCHER » et l'action en masse — D-146, comme sur les bons. */}
            {actionable && allSelectable.length === 0 ? (
              <p className="bg-muted/40 text-muted-foreground rounded-lg border px-3 py-2 text-sm">
                {route.kind === 'pickup' ? tBulk('nonePicked') : tBulk('noneReport')}
              </p>
            ) : null}
            {selectable.length > 0 ? (
              <div className="bg-muted/40 flex flex-wrap items-center gap-3 rounded-lg border px-3 py-2">
                <Checkbox
                  id="route-select-all"
                  checked={
                    allSelected ? true : selectedParcels.length > 0 ? 'indeterminate' : false
                  }
                  onCheckedChange={() =>
                    setSelected(
                      allSelected ? new Set() : new Set(selectable.map((parcel) => parcel.id)),
                    )
                  }
                  disabled={pending}
                />
                <label htmlFor="route-select-all" className="text-sm font-medium">
                  {tBulk('selectAll')}
                </label>
                <span className="text-muted-foreground text-xs tabular-nums">
                  {tBulk('selected', {
                    count: selectedParcels.length,
                    total: String(selectable.length),
                  })}
                </span>
                {transferCouriers && route.kind === 'delivery' ? (
                  <Button
                    size="sm"
                    variant="outline"
                    className="ms-auto"
                    disabled={selectedParcels.length === 0 || pending}
                    onClick={() => setTransferOpen(true)}
                  >
                    {tTransfer('button', { count: String(selectedParcels.length) })}
                  </Button>
                ) : null}
                <Button
                  size="sm"
                  className={transferCouriers && route.kind === 'delivery' ? undefined : 'ms-auto'}
                  disabled={selectedParcels.length === 0 || pending}
                  onClick={() => setBulkOpen(true)}
                >
                  {route.kind === 'pickup'
                    ? tBulk('pickedButton', { count: String(selectedParcels.length) })
                    : tBulk('reportButton', { count: String(selectedParcels.length) })}
                </Button>
              </div>
            ) : null}

            {route.parcels.length === 0 ? (
              <p className="text-muted-foreground text-sm">{t('noParcels')}</p>
            ) : visibleParcels.length === 0 ? (
              <p className="text-muted-foreground text-sm">{t('noMatch')}</p>
            ) : (
              <ul className="flex flex-col gap-2">
                {/* Les plus récents en premier, à l'écran seulement : la feuille de route garde l'ordre du sac. */}
                {newestFirst(visibleParcels).map((parcel) => {
                  const stop = routeStopOf(route.kind, parcel);

                  return (
                    <li
                      key={parcel.id}
                      className="dense:py-2 flex items-start gap-3 rounded-lg border p-3 sm:items-center"
                    >
                      {allSelectable.length === 0 ? null : (
                        <Checkbox
                          checked={selected.has(parcel.id)}
                          disabled={pending || !routeParcelAwaitsOutcome(route.kind, parcel)}
                          onCheckedChange={(value) => toggleSelected(parcel.id, value === true)}
                          aria-label={t('confirmParcel', { code: parcel.code })}
                          className="mt-0.5 sm:mt-0"
                        />
                      )}

                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <Link
                            href={`/parcels/${parcel.code}`}
                            className="hover:text-primary font-mono text-sm font-medium underline-offset-2 hover:underline"
                          >
                            {parcel.code}
                          </Link>
                          <ParcelStatusBadge statusId={parcel.statusId} />
                          <CartonCell line={parcel} lines={route.parcels} />
                          <CorrectedMark at={parcel.correctedAt} />
                        </div>
                        {/*
                        L'EXPÉDITEUR sur un ramassage ou un retour, le
                        destinataire sur une livraison — C12 bloc 4. C'est
                        `routeStopOf()` qui tranche, et il le fait pour les
                        trois écrans à la fois.
                      */}
                        <p className="text-muted-foreground mt-0.5 truncate text-xs">
                          {stop.name} · {stop.phone}
                        </p>
                        <p className="text-muted-foreground truncate text-xs">{stop.place}</p>
                        {parcel.note ? (
                          <p className="text-muted-foreground mt-1 truncate text-xs italic">
                            {parcel.note}
                          </p>
                        ) : null}
                      </div>

                      <div className="flex shrink-0 items-center gap-2">
                        <RouteStopActions stop={stop} />

                        {/* On n'encaisse rien en ramassant : afficher un montant
                          sur une tournée de ramassage n'annoncerait rien de
                          vrai au livreur. */}
                        {targetsSender ? null : (
                          <span className="text-sm font-medium tabular-nums">
                            {format.number(
                              (parcel.codCollected ?? parcel.codToCollect) / 100,
                              'currency',
                            )}
                          </span>
                        )}

                        {route.editable ? (
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
                        ) : closed ? null : (
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => setReporting(parcel)}
                            disabled={pending}
                          >
                            {t('report')}
                          </Button>
                        )}
                      </div>
                    </li>
                  );
                })}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>

      {composing ? (
        <RouteComposer
          code={route.code}
          kind={route.kind}
          onDone={() => {
            setComposing(false);
            router.refresh();
          }}
        />
      ) : null}

      {composingNotes ? (
        <RouteNoteComposer
          code={route.code}
          onDone={() => {
            setComposingNotes(false);
            router.refresh();
          }}
        />
      ) : null}

      {validatingNote ? (
        <NoteValidateDialog
          key={validatingNote.id}
          noteCode={validatingNote.code}
          parcels={noteParcels(validatingNote)}
          open
          onOpenChange={(open) => (open ? undefined : setValidating(null))}
        />
      ) : null}

      {bulkOpen && selectedParcels.length > 0 ? (
        <RouteBulkDialog
          routeCode={route.code}
          kind={route.kind}
          parcels={selectedParcels}
          open
          onOpenChange={(next) => (next ? undefined : setBulkOpen(false))}
          onDone={() => {
            setBulkOpen(false);
            setSelected(new Set());
            router.refresh();
          }}
        />
      ) : null}

      {transferOpen && transferCouriers && selectedParcels.length > 0 ? (
        <ParcelTransferDialog
          parcelIds={selectedParcels.map((parcel) => parcel.id)}
          couriers={transferCouriers}
          currentCourierId={route.courierId}
          open
          onOpenChange={(next) => (next ? undefined : setTransferOpen(false))}
          onDone={() => {
            setTransferOpen(false);
            setSelected(new Set());
            router.refresh();
          }}
        />
      ) : null}

      {reporting ? (
        <ParcelOutcomeDialog
          routeCode={route.code}
          kind={route.kind}
          parcel={reporting}
          open
          onOpenChange={(next) => {
            if (!next) setReporting(null);
          }}
        />
      ) : null}

      {draftMode ? (
        <RouteDraftDialogs
          route={route}
          cities={cities}
          couriers={couriers}
          locked={route.parcels.length + route.notes.length > 0}
          editing={draftMode === 'edit'}
          deleting={draftMode === 'delete'}
          onClose={() => setDraftMode(null)}
          onDeleted={() => router.push('/routes')}
        />
      ) : null}
    </div>
  );
}

/** Sentinelle « tous les statuts » : Radix et cmdk refusent une valeur vide. */
const ALL_STATUSES = '__all__';

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
