'use client';

import { DELIVERED, findParcelStatus, sumCents } from '@delivery/shared';
import { ChevronRight, Package, X } from 'lucide-react';
import { useFormatter, useLocale, useTranslations } from 'next-intl';
import { useMemo, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
import { QuickDeliver } from '@/features/courier/quick-deliver';
import { Link } from '@/i18n/navigation';
import type { CourierParcelFilters } from '@/features/courier/courier-parcel-rows';
import { type BulkOutcomeResult, reportOutcomesAction } from '@/lib/actions/courier';
import { cn } from '@/lib/utils';

/** Une ligne de « Mes colis », telle que la page serveur la prépare. */
export interface CourierParcelRow {
  id: string;
  code: string;
  recipientName: string;
  destinationCityName: string;
  deliveryAddress: string;
  statusId: number;
  codAmount: number;
  /** `codToCollect()` — DOMAIN.md §3.8, calculé par la page, jamais ici. */
  dueInCents: number;
  /** Attendu en chèque : jamais en un clic ni en masse. */
  cheque: boolean;
  /** Les issues que le SERVEUR propose (`courierOutcomes` de `GET /parcels`). */
  outcomes: number[];
}

/** Ce que « Charger plus » ajoute — `COURIER_PARCELS_STEP` côté serveur. */
const STEP = 25;

/** Les longueurs proposées ; `all` = tous (`?limit=all`, lu par la page). */
const SIZES = ['25', '50', '100', 'all'] as const;

/** 14 « Reporté » : la seule issue qui porte une date (même règle que `OutcomeDialog`). */
const POSTPONED = 14;

/**
 * LA LISTE DE « MES COLIS », AVEC SA SÉLECTION — 21/11/2026.
 *
 * **Cochable = le serveur propose au moins une issue.** « Tout cocher » ne
 * prend que ceux-là : cocher un colis qui n'a rien à recevoir n'apprendrait
 * qu'un refus.
 *
 * **« Livrer la sélection » = espèces, COD complet**, la règle de la masse du
 * mobile : les colis attendus en chèque sont écartés AVANT l'envoi, et la
 * confirmation dit combien. Le total affiché additionne les dus déjà calculés
 * par la page ; aucun montant ne part vers l'API, le serveur écrit le dû.
 *
 * **« Changer le statut »** ne montre que les issues proposées par le serveur
 * à TOUS les colis cochés (hors « Livré », qui a son bouton) : une issue
 * absente d'un seul d'entre eux serait refusée pour lui.
 *
 * **Aucun plafond.** L'action rejoue la route unitaire colis par colis
 * (`reportOutcomesAction`) et rend « X réussis, Y refusés » avec le motif.
 *
 * **On coche ce qu'on voit** (correction du 21/11/2026) : « Tout cocher »
 * ne prend que les éligibles AFFICHÉS, jamais un colis hors de l'écran. Pour
 * en traiter beaucoup, le livreur choisit la longueur de la liste (25 / 50 /
 * 100 / Tous) ou « Charger plus », puis coche. La confirmation liste les
 * codes : ce qui part est ce qu'il a lu.
 */
export function CourierParcelList({
  rows,
  total,
  limit,
  filters,
}: {
  rows: CourierParcelRow[];
  total: number;
  /** `null` : tous les colis sont demandés. */
  limit: number | null;
  filters: CourierParcelFilters;
}) {
  const t = useTranslations('courier.parcels');
  const tStatus = useTranslations('status');
  const tCommon = useTranslations('common');
  const locale = useLocale();
  const format = useFormatter();

  const [picked, setPicked] = useState<ReadonlySet<string>>(new Set());
  const [dialog, setDialog] = useState<'deliver' | 'status' | null>(null);
  const [chosen, setChosen] = useState<number | null>(null);
  const [scheduledAt, setScheduledAt] = useState('');
  const [comment, setComment] = useState('');
  const [result, setResult] = useState<BulkOutcomeResult | null>(null);
  const [pending, startTransition] = useTransition();

  const money = (cents: number) =>
    format.number(cents / 100, { style: 'currency', currency: 'MAD' });

  const eligible = useMemo(() => rows.filter((row) => row.outcomes.length > 0), [rows]);
  const complete = rows.length >= total;

  /*
   * La sélection EFFECTIVE : ce qui est coché ET affiché ET encore éligible.
   * Après une action, la page se relit au serveur ; un colis livré quitte la
   * liste, et sa case avec lui, sans état à nettoyer à la main.
   */
  const selected = useMemo(
    () => eligible.filter((row) => picked.has(row.code)),
    [eligible, picked],
  );

  const allChecked = eligible.length > 0 && selected.length === eligible.length;
  // Les éligibles AFFICHÉS, et eux seuls (correction du 21/11/2026).
  const toggleAll = () =>
    setPicked(allChecked ? new Set() : new Set(eligible.map((row) => row.code)));
  const toggle = (code: string) =>
    setPicked((current) => {
      const next = new Set(current);
      if (next.has(code)) next.delete(code);
      else next.add(code);
      return next;
    });

  /** La même URL, filtres compris, à une autre longueur. */
  const hrefFor = (size: string) => {
    const params = new URLSearchParams();
    if (filters.search) params.set('search', filters.search);
    if (filters.statusId) params.set('statusId', String(filters.statusId));
    if (filters.statusGroup) params.set('statusGroup', filters.statusGroup);
    params.set('limit', size);
    return `/me/parcels?${params.toString()}`;
  };
  const currentSize = limit === null ? 'all' : String(limit);

  const deliverable = selected.filter((row) => row.outcomes.includes(DELIVERED));
  const toDeliver = deliverable.filter((row) => !row.cheque);
  const chequeCount = deliverable.length - toDeliver.length;
  const notDeliverable = selected.length - deliverable.length;
  const cashCents = sumCents(toDeliver.map((row) => row.dueInCents));

  const common = useMemo(() => {
    const [first, ...others] = selected;
    if (!first) return [];
    return first.outcomes.filter(
      (statusId) =>
        statusId !== DELIVERED && others.every((row) => row.outcomes.includes(statusId)),
    );
  }, [selected]);

  const close = () => {
    setDialog(null);
    setChosen(null);
    setScheduledAt('');
    setComment('');
  };

  const send = (codes: string[], statusId: number) => {
    startTransition(async () => {
      const response = await reportOutcomesAction(
        codes,
        {
          statusId,
          ...(comment.trim() ? { comment: comment.trim() } : {}),
          ...(statusId === POSTPONED && scheduledAt ? { scheduledAt } : {}),
        },
        locale,
      );

      if (!response.ok) {
        toast.error(response.error);
        return;
      }

      const { succeeded, refused } = response.data;
      setResult(response.data);
      // Les refusés restent cochés : c'est sur eux que le livreur revient.
      const kept = new Set(refused.map((line) => line.code));
      setPicked((current) => new Set([...current].filter((code) => kept.has(code))));
      close();

      const summary = t('bulkResult', { ok: succeeded.length, ko: refused.length });
      if (refused.length === 0) toast.success(summary);
      else toast.warning(summary);
    });
  };

  const statusName = (statusId: number) => {
    const definition = findParcelStatus(statusId);
    return definition ? tStatus(definition.key) : String(statusId);
  };

  /** L'action de la ligne : « Livrer », le lien chèque vers la fiche, ou rien. */
  const action = (row: CourierParcelRow) => {
    if (!row.outcomes.includes(DELIVERED)) return null;

    if (row.cheque) {
      return (
        <Link
          href={`/me/parcels/${row.code}`}
          className="text-primary text-xs font-medium underline-offset-4 hover:underline"
        >
          {t('chequeOnFile')}
        </Link>
      );
    }

    return <QuickDeliver code={row.code} dueInCents={row.dueInCents} />;
  };

  const rowBox = (row: CourierParcelRow) =>
    row.outcomes.length > 0 ? (
      <Checkbox
        checked={picked.has(row.code)}
        onCheckedChange={() => toggle(row.code)}
        aria-label={t('selectRow', { code: row.code })}
        className="size-5"
      />
    ) : (
      <span className="size-5 shrink-0" aria-hidden />
    );

  const badge = (statusId: number) => {
    const definition = findParcelStatus(statusId);
    return definition ? (
      <Badge variant="outline" style={{ borderColor: definition.color, color: definition.color }}>
        {tStatus(definition.key)}
      </Badge>
    ) : null;
  };

  return (
    <div className="flex flex-col gap-3">
      {/* --- La barre de sélection : la même pour le tableau et les cartes --- */}
      {eligible.length > 0 ? (
        <div className="bg-background sticky top-0 z-10 flex flex-wrap items-center gap-2 rounded-xl border p-2">
          <label className="flex min-h-9 cursor-pointer items-center gap-2 px-2 text-sm">
            <Checkbox checked={allChecked} onCheckedChange={toggleAll} className="size-5" />
            {t('selectAllShown', { count: eligible.length })}
          </label>

          {/* « N colis cochés », lisible d'un coup d'œil : c'est ce qui partira. */}
          <span
            className={cn(
              'rounded-md px-2 py-1 text-sm font-semibold tabular-nums',
              selected.length > 0 ? 'bg-primary text-primary-foreground' : 'text-muted-foreground',
            )}
            aria-live="polite"
          >
            {t('selectedCount', { count: selected.length })}
          </span>

          {selected.length > 0 ? (
            <>
              <span className="ms-auto flex flex-wrap gap-2">
                <Button size="sm" variant="ghost" onClick={() => setPicked(new Set())}>
                  {t('clearSelection')}
                </Button>
                <Button size="sm" variant="outline" onClick={() => setDialog('status')}>
                  {t('changeStatus')}
                </Button>
                <Button size="sm" onClick={() => setDialog('deliver')}>
                  {t('deliverSelection')}
                </Button>
              </span>
            </>
          ) : null}
        </div>
      ) : null}

      {/* --- Le résultat de la dernière action, motif par motif ------------- */}
      {result ? (
        <Card>
          <CardContent className="flex flex-col gap-2 p-4 text-sm">
            <div className="flex items-center justify-between gap-2">
              <span className="font-medium">
                {t('bulkResult', { ok: result.succeeded.length, ko: result.refused.length })}
              </span>
              <Button
                size="icon"
                variant="ghost"
                aria-label={t('dismiss')}
                onClick={() => setResult(null)}
              >
                <X className="size-4" aria-hidden />
              </Button>
            </div>
            {result.refused.length > 0 ? (
              <ul className="text-muted-foreground flex flex-col gap-1">
                {result.refused.map((line) => (
                  <li key={line.code}>
                    <Link href={`/me/parcels/${line.code}`} className="font-mono font-medium">
                      {line.code}
                    </Link>{' '}
                    : {line.error}
                  </li>
                ))}
              </ul>
            ) : null}
          </CardContent>
        </Card>
      ) : null}

      {/* --- Desktop : un tableau, comme le reste du back-office ------------ */}
      <div className="hidden overflow-hidden rounded-xl border lg:block">
        <table className="w-full text-sm">
          <thead className="bg-muted/50 text-muted-foreground">
            <tr>
              <th className="w-10 px-4 py-2.5">
                <span className="sr-only">{t('selectAll')}</span>
              </th>
              <th className="px-4 py-2.5 text-start font-medium">{t('code')}</th>
              <th className="px-4 py-2.5 text-start font-medium">{t('recipient')}</th>
              <th className="px-4 py-2.5 text-start font-medium">{t('city')}</th>
              <th className="px-4 py-2.5 text-end font-medium">{t('cod')}</th>
              <th className="px-4 py-2.5 text-start font-medium">{t('status')}</th>
              <th className="px-4 py-2.5 text-end font-medium">
                <span className="sr-only">{t('actions')}</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr
                key={row.id}
                className={cn('hover:bg-muted/40 border-t', picked.has(row.code) && 'bg-muted/40')}
              >
                <td className="px-4 py-2.5">{rowBox(row)}</td>
                <td className="px-4 py-2.5">
                  <Link href={`/me/parcels/${row.code}`} className="font-mono font-medium">
                    {row.code}
                  </Link>
                </td>
                <td className="px-4 py-2.5">{row.recipientName}</td>
                <td className="px-4 py-2.5">{row.destinationCityName}</td>
                <td className="px-4 py-2.5 text-end tabular-nums">{money(row.codAmount)}</td>
                <td className="px-4 py-2.5">{badge(row.statusId)}</td>
                <td className="px-4 py-2.5 text-end">{action(row)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* --- Mobile web : des cartes, comme sur le téléphone ---------------- */}
      <ul className="flex flex-col gap-2 lg:hidden">
        {rows.map((row) => (
          <li key={row.id}>
            <Card className={cn(picked.has(row.code) && 'border-primary')}>
              <CardContent className="flex items-center gap-3 p-4">
                {rowBox(row)}

                <span className="bg-primary/10 text-primary flex size-10 shrink-0 items-center justify-center rounded-lg">
                  <Package className="size-5" aria-hidden />
                </span>

                {/*
                  LE CODE en tête, et les deux lignes ÉTIQUETÉES — maquette 3,
                  et la même carte que le téléphone.
                */}
                <span className="min-w-0 flex-1">
                  <span className="flex items-center justify-between gap-2">
                    <Link
                      href={`/me/parcels/${row.code}`}
                      className="truncate font-mono text-sm font-semibold"
                    >
                      {row.code}
                    </Link>
                    {badge(row.statusId)}
                  </span>

                  <span className="text-muted-foreground block truncate text-xs">
                    {t('client')} : {row.recipientName}
                  </span>
                  <span className="text-muted-foreground block truncate text-xs">
                    {t('addressLabel')} : {row.deliveryAddress}, {row.destinationCityName}
                  </span>
                </span>

                {action(row)}

                <ChevronRight
                  className="text-muted-foreground size-4 shrink-0 rtl:rotate-180"
                  aria-hidden
                />
              </CardContent>
            </Card>
          </li>
        ))}
      </ul>

      {/* --- Tous les colis : la liste s'allonge, la page ne remonte pas ---- */}
      <div className="flex flex-col items-center gap-2 py-2">
        <span className="text-muted-foreground text-xs tabular-nums">
          {t('shownOf', { shown: rows.length, total })}
        </span>
        <div className="flex flex-wrap items-center justify-center gap-2">
          {complete || limit === null ? null : (
            <Button asChild variant="outline">
              <Link href={hrefFor(String(limit + STEP))} scroll={false}>
                {t('loadMore')}
              </Link>
            </Button>
          )}
          {/* Le nombre de lignes affichées : on affiche ce qu'on veut, on coche ce qu'on voit. */}
          <nav aria-label={t('pageSize')} className="flex items-center gap-1 text-sm">
            <span className="text-muted-foreground px-1">{t('pageSize')}</span>
            {SIZES.map((size) => (
              <Button
                key={size}
                asChild
                size="sm"
                variant={currentSize === size ? 'default' : 'ghost'}
              >
                <Link
                  href={hrefFor(size)}
                  scroll={false}
                  aria-current={currentSize === size ? 'true' : undefined}
                >
                  {size === 'all' ? t('pageSizeAll') : size}
                </Link>
              </Button>
            ))}
          </nav>
        </div>
      </div>

      {/* --- Livrer la sélection ------------------------------------------- */}
      <Dialog open={dialog === 'deliver'} onOpenChange={(open) => (open ? null : close())}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('deliverSelection')}</DialogTitle>
            <DialogDescription>
              {t('bulkDeliverConfirm', { count: toDeliver.length, amount: money(cashCents) })}
            </DialogDescription>
          </DialogHeader>

          <CodeList codes={toDeliver.map((row) => row.code)} />

          {chequeCount > 0 || notDeliverable > 0 ? (
            <ul className="text-muted-foreground flex flex-col gap-1 text-sm">
              {chequeCount > 0 ? <li>{t('chequeExcluded', { count: chequeCount })}</li> : null}
              {notDeliverable > 0 ? (
                <li>{t('notDeliverableExcluded', { count: notDeliverable })}</li>
              ) : null}
            </ul>
          ) : null}

          <DialogFooter>
            <Button variant="outline" onClick={close} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            <Button
              disabled={pending || toDeliver.length === 0}
              onClick={() =>
                send(
                  toDeliver.map((row) => row.code),
                  DELIVERED,
                )
              }
            >
              {t('confirm')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* --- Changer le statut --------------------------------------------- */}
      <Dialog open={dialog === 'status'} onOpenChange={(open) => (open ? null : close())}>
        <DialogContent className="max-h-[90dvh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{t('changeStatus')}</DialogTitle>
            <DialogDescription>
              {chosen === null
                ? t('chooseStatus')
                : t('bulkStatusConfirm', {
                    count: selected.length,
                    status: statusName(chosen),
                  })}
            </DialogDescription>
          </DialogHeader>

          <CodeList codes={selected.map((row) => row.code)} />

          {common.length === 0 ? (
            <p className="text-muted-foreground text-sm">{t('noCommonOutcome')}</p>
          ) : (
            <div className="flex flex-col gap-2">
              {common.map((statusId) => {
                const target = findParcelStatus(statusId);
                if (!target) return null;

                return (
                  <button
                    key={statusId}
                    type="button"
                    aria-pressed={chosen === statusId}
                    onClick={() => setChosen(statusId)}
                    className={cn(
                      'flex min-h-12 items-center gap-3 rounded-xl border px-3 text-start text-sm',
                      chosen === statusId ? 'border-primary bg-muted' : 'hover:bg-muted/60',
                    )}
                  >
                    <span
                      className="size-2.5 shrink-0 rounded-full"
                      style={{ backgroundColor: target.color }}
                    />
                    {tStatus(target.key)}
                  </button>
                );
              })}
            </div>
          )}

          {chosen === POSTPONED ? (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="bulk-scheduled">{t('scheduledAt')}</Label>
              <Input
                id="bulk-scheduled"
                type="date"
                value={scheduledAt}
                onChange={(event) => setScheduledAt(event.target.value)}
              />
            </div>
          ) : null}

          {chosen !== null ? (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="bulk-comment">{t('comment')}</Label>
              <Textarea
                id="bulk-comment"
                value={comment}
                maxLength={255}
                onChange={(event) => setComment(event.target.value)}
              />
            </div>
          ) : null}

          <DialogFooter>
            <Button variant="outline" onClick={close} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            <Button
              disabled={pending || chosen === null}
              onClick={() =>
                chosen === null
                  ? undefined
                  : send(
                      selected.map((row) => row.code),
                      chosen,
                    )
              }
            >
              {t('confirm')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

/** Les codes concernés, lus avant de confirmer : ce qui part est ce qu'on a lu. */
function CodeList({ codes }: { codes: string[] }) {
  const t = useTranslations('courier.parcels');
  if (codes.length === 0) return null;

  return (
    <div className="flex flex-col gap-1">
      <span className="text-sm font-medium">{t('concernedCodes', { count: codes.length })}</span>
      <ul className="bg-muted/40 flex max-h-40 flex-wrap gap-x-3 gap-y-1 overflow-y-auto rounded-lg border p-2 font-mono text-xs">
        {codes.map((code) => (
          <li key={code}>{code}</li>
        ))}
      </ul>
    </div>
  );
}
