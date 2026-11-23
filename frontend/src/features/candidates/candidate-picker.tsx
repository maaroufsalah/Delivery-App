'use client';

import type { CandidatePage, SelectionPayload } from '@delivery/shared';
import { Loader2, Search } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { type ReactNode, useEffect, useRef, useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { FormSheet } from '@/features/referential/form-sheet';
import { useRouteError } from '@/features/routes/use-route-error';
import type { ReferentialResult } from '@/lib/actions/action-result';
import { type CandidateQuery, CandidateFeed, EMPTY_FEED, type FeedState } from './candidate-feed';

export type { CandidateQuery };

/**
 * CHOISIR DANS UNE LISTE DE CANDIDATS, SANS PLAFOND CACHÉ — chantier
 * « Plafond de 200 ».
 *
 * BR-2610-0002 avait 358 candidats ; la modale en chargeait 200 d'un coup et
 * affichait « 0 / 200 ». Ce sélecteur, commun à tous les « Ajouter des colis »
 * (bons, circuits, bons de retour) :
 *
 *  - lit la liste PAR PAGES de 100, au défilement, avec son TOTAL réel ;
 *  - cherche AU SERVEUR (code, destinataire, téléphone, ville…) ;
 *  - « Tout cocher » coche TOUS les candidats de la recherche, chargés ou non :
 *    la sélection part en critère (`all`, `search`, `exclude`), que le serveur
 *    relit dans la transaction d'ajout — « Ajouter 260 colis » ajoute 260 colis.
 *
 * Les éléments grisés (`lockedReason`) se voient, ne se cochent pas, et
 * « Tout cocher » les laisse de côté (`selectableTotal`).
 */
type Selection = { mode: 'some'; ids: Set<string> } | { mode: 'all'; excluded: Set<string> };

export function CandidatePicker<T extends { id: string }>({
  title,
  description,
  searchPlaceholder,
  load,
  renderItem,
  lockedReason,
  toolbar,
  filtersKey = '',
  submit,
  submitLabel,
  successMessage,
  onDone,
}: {
  title: string;
  description?: string;
  searchPlaceholder?: string;
  /** Une page de candidats, lue au serveur (Server Action). */
  load: (query: CandidateQuery) => Promise<ReferentialResult<CandidatePage<T>>>;
  renderItem: (item: T) => ReactNode;
  /** La raison d'un élément grisé, ou `null` s'il se coche. */
  lockedReason?: (item: T) => string | null;
  /** Filtres propres à l'écran (un quartier…), au-dessus de la liste. */
  toolbar?: ReactNode;
  /** Change quand les filtres de `toolbar` changent : la liste repart de la page 1. */
  filtersKey?: string;
  /** L'ajout. La sélection part en ids, ou en « tout » pour la recherche courante. */
  submit: (selection: SelectionPayload) => Promise<ReferentialResult<unknown>>;
  submitLabel: (count: number) => string;
  successMessage: (count: number) => string;
  onDone: () => void;
}) {
  const t = useTranslations('candidates');
  const translateError = useRouteError();

  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [feedState, setFeedState] = useState<FeedState<T>>(EMPTY_FEED);
  const { items, total, selectable, loading, failed } = feedState;
  const [saving, setSaving] = useState(false);
  const [selection, setSelection] = useState<Selection>({ mode: 'some', ids: new Set() });
  const sentinel = useRef<HTMLDivElement>(null);

  /*
   * La lecture vit HORS de React (`CandidateFeed`, recette 6 point 1) : `load`
   * et le traducteur d'erreurs sont recréés à chaque rendu par les écrans, et
   * les mettre dans les dépendances d'un effet relançait la lecture à chaque
   * rendu — la boucle de CIR-2610-0013. Le dernier `load` est simplement
   * remis au flux après chaque rendu ; seuls les critères déclenchent un appel.
   */
  const [feed] = useState(
    () =>
      new CandidateFeed<T, ReferentialResult<CandidatePage<T>>>(load, setFeedState, (failure) =>
        toast.error(translateError(failure)),
      ),
  );
  useEffect(() => {
    feed.setLoader(load, (failure) => toast.error(translateError(failure)));
  });

  // La recherche part au serveur 300 ms après la dernière frappe.
  useEffect(() => {
    const timer = setTimeout(() => setQuery(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

  // Nouvelle recherche, nouveaux filtres : une sélection « tout » (liée à
  // l'ancienne recherche) est remise à zéro — pendant le rendu, pas dans un
  // effet, pour ne pas rendre deux fois.
  const criteria = `${query}\u0000${filtersKey}`;
  const [seenCriteria, setSeenCriteria] = useState(criteria);
  if (seenCriteria !== criteria) {
    setSeenCriteria(criteria);
    if (selection.mode === 'all') setSelection({ mode: 'some', ids: new Set() });
  }

  // … et la liste repart de la page 1 — UN appel par critère (des chaînes,
  // jamais une fonction, dans les dépendances).
  useEffect(() => {
    feed.setCriteria(query, filtersKey);
  }, [feed, query, filtersKey]);

  const hasMore = items.length < total;

  // Le défilement charge la page suivante quand le bas de la liste approche ;
  // le flux refuse une deuxième lecture tant qu'une est en vol.
  useEffect(() => {
    const node = sentinel.current;
    if (!node || !hasMore) return;
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) feed.loadMore();
      },
      { rootMargin: '200px' },
    );
    observer.observe(node);
    return () => observer.disconnect();
  }, [feed, hasMore]);

  const isLocked = (item: T) => (lockedReason ? lockedReason(item) !== null : false);
  const isChecked = (item: T) =>
    selection.mode === 'all'
      ? !selection.excluded.has(item.id) && !isLocked(item)
      : selection.ids.has(item.id);
  const count =
    selection.mode === 'all'
      ? Math.max(selectable - selection.excluded.size, 0)
      : selection.ids.size;
  const allChecked = selection.mode === 'all' && selection.excluded.size === 0 && selectable > 0;

  const toggle = (item: T, on: boolean) => {
    setSelection((current) => {
      if (current.mode === 'all') {
        const excluded = new Set(current.excluded);
        if (on) excluded.delete(item.id);
        else excluded.add(item.id);
        return { mode: 'all', excluded };
      }
      const ids = new Set(current.ids);
      if (on) ids.add(item.id);
      else ids.delete(item.id);
      return { mode: 'some', ids };
    });
  };

  const toggleAll = (on: boolean) =>
    setSelection(on ? { mode: 'all', excluded: new Set() } : { mode: 'some', ids: new Set() });

  const send = async () => {
    if (count === 0) return;
    const payload: SelectionPayload =
      selection.mode === 'all'
        ? { all: true, search: query || undefined, exclude: [...selection.excluded] }
        : { ids: [...selection.ids] };
    const sent = count;

    setSaving(true);
    const result = await submit(payload);
    setSaving(false);

    if (result.ok) {
      toast.success(successMessage(sent));
      onDone();
      return;
    }
    toast.error(translateError(result));
  };

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={title}
      description={description}
      onSubmit={send}
      submitting={saving}
      submitLabel={submitLabel(count)}
    >
      <div className="flex flex-col gap-4">
        <div className="flex flex-col gap-2 sm:flex-row">
          <div className="relative flex-1">
            <Search
              className="text-muted-foreground pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2"
              aria-hidden
            />
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder={searchPlaceholder ?? t('searchPlaceholder')}
              className="ps-9"
              aria-label={searchPlaceholder ?? t('searchPlaceholder')}
            />
          </div>
          {toolbar}
        </div>

        <label className="bg-muted/50 flex items-center gap-3 rounded-lg px-3 py-2 text-sm">
          <Checkbox
            checked={allChecked ? true : count > 0 ? 'indeterminate' : false}
            onCheckedChange={(value) => toggleAll(value === true)}
            disabled={selectable === 0}
            aria-label={t('selectAll')}
          />
          <span className="flex-1">{t('selectAllCount', { count: selectable })}</span>
          <span className="text-muted-foreground text-xs tabular-nums">
            {t('counter', { count, total: String(total) })}
          </span>
        </label>

        {selection.mode === 'all' && hasMore ? (
          <p className="text-muted-foreground text-xs">{t('allMatching', { count })}</p>
        ) : null}

        {!loading && items.length === 0 ? (
          <p className="text-muted-foreground rounded-lg border border-dashed p-6 text-center text-sm">
            {failed ? t('failed') : query ? t('noMatch') : t('none')}
          </p>
        ) : (
          <ul className="flex flex-col gap-2">
            {items.map((item) => {
              const reason = lockedReason?.(item) ?? null;
              return (
                <li key={item.id}>
                  <label
                    className={
                      reason
                        ? 'flex cursor-not-allowed items-start gap-3 rounded-lg border p-3 opacity-60'
                        : 'hover:bg-muted/40 flex cursor-pointer items-start gap-3 rounded-lg border p-3'
                    }
                  >
                    <Checkbox
                      checked={isChecked(item)}
                      onCheckedChange={(value) => toggle(item, value === true)}
                      disabled={reason !== null}
                      className="mt-0.5"
                      aria-label={item.id}
                    />
                    <div className="min-w-0 flex-1">
                      {renderItem(item)}
                      {reason ? <p className="text-destructive mt-0.5 text-xs">{reason}</p> : null}
                    </div>
                  </label>
                </li>
              );
            })}
          </ul>
        )}

        <div ref={sentinel} />
        {loading ? (
          <p className="text-muted-foreground flex items-center justify-center gap-2 text-xs">
            <Loader2 className="size-4 animate-spin" /> {t('loading')}
          </p>
        ) : hasMore ? (
          <Button variant="outline" size="sm" onClick={() => feed.loadMore()}>
            {t('loadMore', { shown: String(items.length), total: String(total) })}
          </Button>
        ) : null}
      </div>
    </FormSheet>
  );
}
