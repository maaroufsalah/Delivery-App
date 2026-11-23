'use client';

import type {
  PettyCashDayStatus,
  PettyCashDaySummary,
  PettyCashDaysPage,
  PettyCashKind,
} from '@delivery/shared';
import {
  ArrowDownCircle,
  CalendarCheck2,
  CalendarClock,
  FileSpreadsheet,
  FileText,
  Loader2,
  Lock,
  LockOpen,
  Plus,
} from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge } from '@/components/data-table/cells';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Textarea } from '@/components/ui/textarea';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import {
  closePettyCashDayAction,
  downloadPettyCashDayAction,
  reopenPettyCashDayAction,
} from '@/lib/actions/petty-cash';
import { downloadBase64 } from '@/lib/download';

/** Ouverte : la couleur d'attention du thème ; clôturée : la couleur de succès. */
const DAY_COLORS: Record<PettyCashDayStatus, string> = {
  open: 'var(--warning)',
  closed: 'var(--success)',
};

/** Un jour `AAAA-MM-JJ` affiché : midi UTC, le même jour dans tout fuseau marocain. */
const asDate = (day: string) => new Date(`${day}T12:00:00Z`);

/** Ouvrir le formulaire d'un mouvement, daté de la journée visée. */
export type AddMovement = (kind: PettyCashKind, date: string) => void;

/*
 * LES JOURNÉES DE CAISSE — D-249, puis D-252 (tableau unique, demande de
 * Badr du 14/11 : deux tableaux, c'était perturbant). Deux pièces, posées par
 * `PettyCashScreen` :
 *
 *  1. `DayBanner` — la journée en cours, TOUJOURS affichée, même sans
 *     journée encore (aucun mouvement aujourd'hui) : solde actuel, « Nouvelle
 *     dépense », « Approvisionner », « Clôturer la journée » ;
 *  2. `DayGroupHeader` — l'en-tête repliable d'une journée dans LE tableau des
 *     mouvements : date, agence, état, ouverture, approvisionné, dépensé,
 *     clôture, « N à valider », et ses gestes (Corriger ou Reclôturer, Excel,
 *     PDF, et les ajouts À SA DATE quand elle est ouverte).
 *
 * Un ajout vers une journée clôturée est grisé, avec l'infobulle qui dit quoi
 * faire : l'API le refuserait (409 `petty-cash-day-closed`).
 */

/** Les deux ajouts, grisés avec une infobulle quand la journée visée est clôturée. */
function AddButtons({
  date,
  closed,
  onAdd,
}: {
  date: string;
  closed: boolean;
  onAdd: AddMovement;
}) {
  const t = useTranslations('finance.pettyCash');

  const buttons = (
    <span className="flex flex-wrap gap-2" tabIndex={closed ? 0 : undefined}>
      <Button
        size="sm"
        className="gap-1.5"
        disabled={closed}
        onClick={() => onAdd('expense', date)}
      >
        <Plus className="size-4" />
        {t('expense')}
      </Button>
      <Button
        size="sm"
        variant="outline"
        className="gap-1.5"
        disabled={closed}
        onClick={() => onAdd('supply', date)}
      >
        <ArrowDownCircle className="size-4" />
        {t('supply')}
      </Button>
    </span>
  );

  if (!closed) return buttons;

  return (
    <Tooltip>
      <TooltipTrigger asChild>{buttons}</TooltipTrigger>
      <TooltipContent>{t('days.addBlocked')}</TooltipContent>
    </Tooltip>
  );
}

/** Clôturer, rouvrir, exporter : les gestes d'une journée, partagés par les deux blocs. */
function useDayActions(agencyId: string | undefined, today: string) {
  const t = useTranslations('finance.pettyCash.days');
  const format = useFormatter();
  const [reopening, setReopening] = useState<PettyCashDaySummary | null>(null);
  const [closing, setClosing] = useState<string | null>(null);
  const [downloading, setDownloading] = useState<'xlsx' | 'pdf' | null>(null);
  const [pending, startTransition] = useTransition();

  /*
   * La clôture se CONFIRME — chantier « Affichage », point c (D-254). Clôturer
   * le jour même envoie toute dépense saisie ensuite sur le lendemain (D-249
   * choix 7) : la règle était appliquée sans jamais être dite à l'écran.
   */
  const close = (day: string) => setClosing(day);

  const confirmClose = (day: string) => {
    startTransition(async () => {
      const result = await closePettyCashDayAction(day, agencyId);
      setClosing(null);
      if (result.ok) toast.success(t('closedToast'));
      else toast.error(result.error);
    });
  };

  const download = async (day: string, kind: 'xlsx' | 'pdf') => {
    setDownloading(kind);
    const result = await downloadPettyCashDayAction(day, kind, agencyId);
    setDownloading(null);

    if (!result.ok) {
      toast.error(result.error);
      return;
    }
    downloadBase64(
      result.data,
      `Petite-Caisse-Journee-${day}.${kind}`,
      kind === 'pdf'
        ? 'application/pdf'
        : 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    );
  };

  const dialog = (
    <>
      {reopening ? (
        <ReopenDialog day={reopening} agencyId={agencyId} onDone={() => setReopening(null)} />
      ) : null}
      {closing ? (
        <Dialog open onOpenChange={(next) => (next || pending ? null : setClosing(null))}>
          <DialogContent className="sm:max-w-md">
            <DialogHeader>
              <DialogTitle>
                {t('closeDialog.title', { date: format.dateTime(asDate(closing), 'date') })}
              </DialogTitle>
              <DialogDescription>
                {closing === today ? t('closeDialog.tomorrow') : t('closeDialog.past')}
              </DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <Button variant="outline" onClick={() => setClosing(null)} disabled={pending}>
                {t('closeDialog.cancel')}
              </Button>
              <Button onClick={() => confirmClose(closing)} disabled={pending} className="gap-1.5">
                {pending ? <Loader2 className="size-4 animate-spin" /> : null}
                {closing === today ? t('close') : t('reclose')}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      ) : null}
    </>
  );

  return { close, download, downloading, pending, reopen: setReopening, dialog };
}

function useClosedLine() {
  const t = useTranslations('finance.pettyCash.days');
  const format = useFormatter();

  return (summary: PettyCashDaySummary) =>
    summary.status === 'closed' && summary.closedAt
      ? t('closedOn', {
          date: format.dateTime(new Date(summary.closedAt), 'stamp'),
          by: summary.closedBy === 'automatic' ? t('automatic') : (summary.closedBy ?? '-'),
        })
      : undefined;
}

/* --- 1. La journée en cours ----------------------------------------------------- */

export function DayBanner({
  page,
  balance,
  agencyId,
  canWrite,
  onAdd,
}: {
  page: PettyCashDaysPage;
  /** Le solde actuel (validés, toutes dates), au filtre d'agence de la page. */
  balance: number;
  agencyId: string | undefined;
  canWrite: boolean;
  onAdd: AddMovement;
}) {
  const t = useTranslations('finance.pettyCash.days');
  const format = useFormatter();
  const actions = useDayActions(agencyId, page.today);
  const closedLine = useClosedLine();
  const money = (cents: number) => format.number(cents / 100, 'currency');

  const current = page.current;
  // Sans journée encore (aucun mouvement aujourd'hui), le jour est ouvert.
  const status: PettyCashDayStatus = current?.status ?? 'open';

  return (
    <Card>
      <CardContent className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex items-start gap-3">
          <CalendarClock className="text-muted-foreground mt-0.5 size-5 shrink-0" aria-hidden />
          <div className="flex flex-col gap-0.5">
            <p className="text-muted-foreground text-sm">{t('current')}</p>
            <p className="flex flex-wrap items-center gap-2 font-semibold">
              {format.dateTime(asDate(page.today), 'date')}
              <CellBadge label={t(`status.${status}`)} color={DAY_COLORS[status]} />
            </p>
            <p className="text-sm">
              {t('balanceNow')} <span className="font-semibold tabular-nums">{money(balance)}</span>
            </p>
            {current ? (
              <>
                <p className="text-muted-foreground text-sm">
                  {`${t('opening')} ${money(current.openingBalance)} · ${t('drafts', {
                    count: current.pendingDrafts,
                  })}`}
                </p>
                {closedLine(current) ? (
                  <p className="text-muted-foreground text-xs">{closedLine(current)}</p>
                ) : null}
              </>
            ) : (
              <p className="text-muted-foreground text-sm">{t('noCurrent')}</p>
            )}
          </div>
        </div>

        {canWrite ? (
          <div className="flex flex-wrap gap-2">
            <AddButtons date={page.today} closed={status === 'closed'} onAdd={onAdd} />
            {current?.status === 'open' ? (
              <Button
                size="sm"
                variant="outline"
                className="gap-1.5"
                onClick={() => actions.close(current.day)}
                disabled={actions.pending}
              >
                {actions.pending ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  <CalendarCheck2 className="size-4" />
                )}
                {t('close')}
              </Button>
            ) : null}
            {current?.status === 'closed' ? (
              <Button
                size="sm"
                variant="outline"
                className="gap-1.5"
                onClick={() => actions.reopen(current)}
              >
                <LockOpen className="size-4" />
                {t('correct')}
              </Button>
            ) : null}
          </div>
        ) : null}
      </CardContent>
      {actions.dialog}
    </Card>
  );
}

/* --- 2. L'en-tête d'une journée dans le tableau unique ---------------------------- */

export function DayGroupHeader({
  day,
  summary,
  today,
  agencyId,
  canWrite,
  onAdd,
}: {
  day: string;
  /** Absente : une journée hors de la liste chargée — la date seule s'affiche. */
  summary: PettyCashDaySummary | undefined;
  today: string;
  agencyId: string | undefined;
  canWrite: boolean;
  onAdd: AddMovement;
}) {
  const t = useTranslations('finance.pettyCash.days');
  const format = useFormatter();
  const actions = useDayActions(agencyId, today);
  const closedLine = useClosedLine();
  const money = (cents: number) => format.number(cents / 100, 'currency');

  const agency = summary
    ? (summary.agencyName ??
      (summary.agencies > 1 ? t('agencies', { count: summary.agencies }) : null))
    : null;

  return (
    <div className="flex flex-col gap-2 xl:flex-row xl:items-center xl:justify-between">
      <div className="flex min-w-0 flex-col gap-1">
        <p className="flex flex-wrap items-center gap-2">
          <span className="font-semibold">{format.dateTime(asDate(day), 'date')}</span>
          {agency ? <span className="text-muted-foreground text-xs">{agency}</span> : null}
          {summary ? (
            <CellBadge label={t(`status.${summary.status}`)} color={DAY_COLORS[summary.status]} />
          ) : null}
          {summary && summary.pendingDrafts > 0 ? (
            <CellBadge
              label={t('drafts', { count: summary.pendingDrafts })}
              color="var(--warning)"
            />
          ) : null}
          {summary?.reopenedAt && summary.status === 'open' ? (
            <span className="text-muted-foreground text-xs">{t('reopenedShort')}</span>
          ) : null}
        </p>

        {summary ? (
          <dl className="text-muted-foreground flex flex-wrap gap-x-4 gap-y-0.5 text-xs">
            {(
              [
                ['opening', summary.openingBalance],
                ['supplied', summary.supplied],
                // `0 - x` et non `-x` : rien dépensé s'affiche 0,00, pas « -0,00 ».
                ['spent', 0 - summary.spent],
                ['closing', summary.closingBalance],
              ] as const
            ).map(([key, value]) => (
              <div key={key} className="flex gap-1">
                <dt>{t(key)}</dt>
                <dd className="text-foreground font-semibold tabular-nums">{money(value)}</dd>
              </div>
            ))}
          </dl>
        ) : null}

        {summary && (closedLine(summary) || (summary.reopenedAt && summary.reopenReason)) ? (
          <p className="text-muted-foreground text-xs">
            {summary.status === 'open' && summary.reopenedAt && summary.reopenReason
              ? t('reopened', {
                  date: format.dateTime(new Date(summary.reopenedAt), 'stamp'),
                  name: summary.reopenedByName ?? '-',
                  reason: summary.reopenReason,
                })
              : closedLine(summary)}
          </p>
        ) : null}
      </div>

      {summary ? (
        <div className="flex flex-wrap gap-2">
          {canWrite && summary.status === 'open' ? (
            <AddButtons date={summary.day} closed={false} onAdd={onAdd} />
          ) : null}
          {canWrite && summary.status === 'closed' ? (
            <Button
              size="sm"
              variant="outline"
              className="gap-1.5"
              onClick={() => actions.reopen(summary)}
            >
              <LockOpen className="size-4" />
              {t('correct')}
            </Button>
          ) : null}
          {canWrite && summary.status === 'open' ? (
            <Button
              size="sm"
              variant="outline"
              className="gap-1.5"
              onClick={() => actions.close(summary.day)}
              disabled={actions.pending}
            >
              {actions.pending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <Lock className="size-4" />
              )}
              {summary.day === today ? t('close') : t('reclose')}
            </Button>
          ) : null}
          <Button
            size="sm"
            variant="outline"
            className="gap-1.5"
            onClick={() => actions.download(summary.day, 'xlsx')}
            disabled={actions.downloading !== null}
          >
            {actions.downloading === 'xlsx' ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <FileSpreadsheet className="size-4" />
            )}
            {t('excel')}
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="gap-1.5"
            onClick={() => actions.download(summary.day, 'pdf')}
            disabled={actions.downloading !== null}
          >
            {actions.downloading === 'pdf' ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <FileText className="size-4" />
            )}
            {t('pdf')}
          </Button>
        </div>
      ) : null}
      {actions.dialog}
    </div>
  );
}

/** « Corriger » : rouvrir la journée, motif OBLIGATOIRE, journalisé côté API. */
function ReopenDialog({
  day,
  agencyId,
  onDone,
}: {
  day: PettyCashDaySummary;
  agencyId: string | undefined;
  onDone: () => void;
}) {
  const t = useTranslations('finance.pettyCash.days');
  const tCommon = useTranslations('common');
  const tError = useTranslations('validation');
  const format = useFormatter();
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  // Obligatoire, sans longueur minimale (remarques de Badr du 12/11).
  const valid = reason.trim() !== '';

  const reopen = async () => {
    if (saving || !valid) return;
    setSaving(true);
    const result = await reopenPettyCashDayAction(day.day, { reason: reason.trim(), agencyId });
    setSaving(false);

    if (result.ok) {
      toast.success(t('reopenedToast'));
      onDone();
      return;
    }
    toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
  };

  return (
    <Dialog open onOpenChange={(next) => (next ? null : onDone())}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>
            {t('reopenDialog.title', { date: format.dateTime(asDate(day.day), 'date') })}
          </DialogTitle>
          <DialogDescription>{t('reopenDialog.hint')}</DialogDescription>
        </DialogHeader>

        <Textarea
          rows={3}
          maxLength={300}
          placeholder={t('reopenDialog.reason')}
          aria-label={t('reopenDialog.reason')}
          value={reason}
          onChange={(event) => setReason(event.target.value)}
        />

        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={onDone} disabled={saving}>
            {tCommon('cancel')}
          </Button>
          <Button onClick={reopen} disabled={saving || !valid} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : <LockOpen className="size-4" />}
            {t('reopenDialog.confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
