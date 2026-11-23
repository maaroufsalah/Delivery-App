import type { CandidatePage } from '@delivery/shared';

/**
 * LA LECTURE PAR PAGES DU SÉLECTEUR DE CANDIDATS, HORS DE REACT — recette 6,
 * point 1.
 *
 * CIR-2610-0013 (livraison) restait sur « Chargement… » avec un appel toutes
 * les ~100 ms. L'effet de chargement de `CandidatePicker` dépendait de
 * FONCTIONS (`load`, le traducteur d'erreurs) recréées à chaque rendu : chaque
 * rendu relançait la lecture. Sur un circuit de livraison, `load` pose en plus
 * la liste des quartiers AVANT le contrôle de génération — chaque réponse,
 * même périmée, rendait la page, donc relançait un appel, donc périmait la
 * réponse suivante : la liste n'arrivait jamais.
 *
 * Ici, une lecture ne part que pour une raison NOMMÉE : des critères qui
 * changent (`setCriteria`), une page de plus demandée (`loadMore`). Changer de
 * fonction de lecture (`setLoader`) ne lit rien. Le compte des appels se teste
 * sans navigateur (`candidate-feed.spec.ts`, suite de l'API).
 */
export interface CandidateQuery {
  search: string;
  page: number;
}

export type FeedResult<T> =
  | { ok: true; data: CandidatePage<T> }
  | { ok: false; [key: string]: unknown };

export interface FeedState<T> {
  items: T[];
  page: number;
  total: number;
  selectable: number;
  loading: boolean;
  failed: boolean;
}

export const EMPTY_FEED: FeedState<never> = {
  items: [],
  page: 0,
  total: 0,
  selectable: 0,
  loading: false,
  failed: false,
};

export class CandidateFeed<T, R extends FeedResult<T> = FeedResult<T>> {
  private generation = 0;
  private criteria: string | null = null;
  private search = '';
  private current: FeedState<T> = EMPTY_FEED;

  constructor(
    private load: (query: CandidateQuery) => Promise<R>,
    private readonly onChange: (state: FeedState<T>) => void,
    private onFailure: (result: Exclude<R, { ok: true }>) => void,
  ) {}

  get state(): FeedState<T> {
    return this.current;
  }

  /** La fonction de lecture du dernier rendu. Ne lit RIEN. */
  setLoader(
    load: (query: CandidateQuery) => Promise<R>,
    onFailure: (result: Exclude<R, { ok: true }>) => void,
  ): void {
    this.load = load;
    this.onFailure = onFailure;
  }

  /** Une recherche ou des filtres : UNE lecture de la page 1, s'ils ont changé. */
  setCriteria(search: string, filtersKey: string): void {
    const criteria = `${search}\u0000${filtersKey}`;
    if (criteria === this.criteria) return;
    this.criteria = criteria;
    this.search = search;
    void this.fetch(1, true);
  }

  /** La page suivante : UNE lecture, et aucune tant qu'une autre est en vol. */
  loadMore(): void {
    const { loading, items, total, page } = this.current;
    if (loading || this.criteria === null || items.length >= total) return;
    void this.fetch(page + 1, false);
  }

  private set(next: Partial<FeedState<T>>): void {
    this.current = { ...this.current, ...next };
    this.onChange(this.current);
  }

  private async fetch(page: number, reset: boolean): Promise<void> {
    const run = ++this.generation;
    this.set({ loading: true, failed: false });
    let result: R;
    try {
      result = await this.load({ search: this.search, page });
    } catch {
      if (run === this.generation) this.set({ loading: false, failed: true });
      return;
    }
    // Une réponse d'une recherche dépassée ne remplace pas la courante.
    if (run !== this.generation) return;
    if (!result.ok) {
      this.set({ loading: false, failed: true });
      this.onFailure(result as Exclude<R, { ok: true }>);
      return;
    }
    const data = (result as Extract<R, { ok: true }>).data;
    this.set({
      loading: false,
      items: reset ? data.items : [...this.current.items, ...data.items],
      page: data.page,
      total: data.total,
      selectable: data.selectableTotal,
    });
  }
}
