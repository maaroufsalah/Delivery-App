import {
  type DashboardPeriod,
  DEFAULT_DASHBOARD_PERIOD,
  isDashboardPeriod,
  type ResolvedDashboardWindow,
} from '@delivery/shared';

/**
 * LE CHOIX DE PÉRIODE, CÔTÉ CLIENT — chantier « Filtre de période ».
 *
 * ⚠️ **Ce fichier n'importe AUCUNE API serveur** : il est chargé par le
 * sélecteur, qui est un composant client. La lecture serveur du cookie vit dans
 * `dashboard-period.server.ts` (même frontière que `sidebar-preferences`).
 *
 * **Pourquoi un cookie et pas `localStorage`.** Le dernier choix doit être connu
 * AU RENDU SERVEUR, pour que le premier pixel affiche déjà la bonne période et
 * que l'API agrège la bonne fenêtre dès le premier appel. `localStorage` n'est
 * lisible qu'après hydratation : la page peindrait « ce mois » puis sauterait.
 * C'est le raisonnement de `sidebar-preferences.ts`, repris ici.
 */

/** Le dernier choix mémorisé, une valeur de `DASHBOARD_PERIODS`. */
export const PERIOD_COOKIE = 'dp.period';
/** Les bornes du dernier choix `custom`, `AAAA-MM-JJ..AAAA-MM-JJ`. */
export const PERIOD_RANGE_COOKIE = 'dp.periodrange';

/**
 * UNE MÉMOIRE PAR ÉCRAN — chantier « Retours recette 26/09 », point 1.
 *
 * Les deux tableaux de bord partagent la leur (`dashboard`) : c'est la même
 * question posée à deux écrans. La liste des colis a la sienne (`parcels`),
 * et pour une raison précise : son défaut est « depuis le début » — une liste
 * ne doit jamais masquer un colis par défaut — alors que celui du tableau de
 * bord est « ce mois ». Une mémoire commune aurait fait disparaître des colis
 * de la liste parce qu'on venait de regarder le mois sur le tableau de bord.
 */
export type PeriodMemory = 'dashboard' | 'parcels';

export function periodCookieNames(memory: PeriodMemory): { period: string; range: string } {
  return memory === 'dashboard'
    ? { period: PERIOD_COOKIE, range: PERIOD_RANGE_COOKIE }
    : { period: `dp.${memory}period`, range: `dp.${memory}periodrange` };
}

const ONE_YEAR_SECONDS = 60 * 60 * 24 * 365;

/** `null` plutôt qu'une valeur inventée si le cookie a été bricolé à la main. */
export function parsePeriod(value: string | undefined): DashboardPeriod | null {
  return isDashboardPeriod(value) ? value : null;
}

/** Une paire de dates `AAAA-MM-JJ`, ou `null` si la valeur est illisible. */
export function parseRange(value: string | undefined): { from: string; to: string } | null {
  if (!value) return null;
  const [from, to] = value.split('..');
  const ok = (day: string | undefined): day is string => !!day && /^\d{4}-\d{2}-\d{2}$/.test(day);
  return ok(from) && ok(to) ? { from, to } : null;
}

/**
 * La période EFFECTIVE au premier rendu, serveur et client confondus.
 *
 * L'URL l'emporte — un lien partagé ou un bouton Retour porte un choix
 * explicite. À défaut, le dernier choix mémorisé. À défaut, « ce mois ». Cette
 * fonction est PURE : elle est évaluée des deux côtés, et toute divergence
 * produirait un écart d'hydratation.
 */
export function effectivePeriod(
  urlPeriod: string | undefined,
  cookiePeriod: DashboardPeriod | null,
  fallback: DashboardPeriod = DEFAULT_DASHBOARD_PERIOD,
): DashboardPeriod {
  return parsePeriod(urlPeriod) ?? cookiePeriod ?? fallback;
}

/** Écriture du dernier choix, en `document.cookie`. Pas de Server Action. */
export function writePeriodPreference(
  period: DashboardPeriod,
  range?: { from: string; to: string },
  memory: PeriodMemory = 'dashboard',
) {
  if (typeof document === 'undefined') return;

  const secure = window.location.protocol === 'https:' ? '; Secure' : '';
  const base = `; Path=/; Max-Age=${ONE_YEAR_SECONDS}; SameSite=Lax${secure}`;
  const names = periodCookieNames(memory);

  document.cookie = `${names.period}=${period}${base}`;
  if (period === 'custom' && range) {
    document.cookie = `${names.range}=${range.from}..${range.to}${base}`;
  }
}

/**
 * LES FILTRES COMPLÉMENTAIRES DU TABLEAU DE BORD — chantier « Dashboard de
 * pilotage ». Agence, ville, livreur, client : mémorisés ENSEMBLE, dans un seul
 * cookie, pour la même raison que la période — le premier pixel doit être le
 * bon. L'URL l'emporte quand elle porte un filtre ; sinon le dernier état.
 *
 * Le cookie porte l'ÉTAT COMPLET, filtres vides compris : retirer un filtre
 * l'efface aussi de la mémoire, sinon il reviendrait au prochain passage.
 */
export const DASHBOARD_FILTERS_COOKIE = 'dp.dashfilters';

export const DASHBOARD_FILTER_KEYS = ['agencyId', 'cityId', 'courierId', 'clientId'] as const;
export type DashboardFilterKey = (typeof DASHBOARD_FILTER_KEYS)[number];
export type DashboardFilterValues = Partial<Record<DashboardFilterKey, string>>;

/** Le cookie est une chaîne de requête : `agencyId=…&cityId=…`, lisible à l'œil. */
export function parseDashboardFilters(value: string | undefined): DashboardFilterValues {
  if (!value) return {};
  const params = new URLSearchParams(value);
  const out: DashboardFilterValues = {};
  for (const key of DASHBOARD_FILTER_KEYS) {
    const found = params.get(key);
    if (found && /^[\w-]{1,64}$/.test(found)) out[key] = found;
  }
  return out;
}

export function writeDashboardFilterPreference(values: DashboardFilterValues) {
  if (typeof document === 'undefined') return;

  const params = new URLSearchParams();
  for (const key of DASHBOARD_FILTER_KEYS) {
    if (values[key]) params.set(key, values[key]);
  }
  const secure = window.location.protocol === 'https:' ? '; Secure' : '';
  document.cookie = `${DASHBOARD_FILTERS_COOKIE}=${encodeURIComponent(params.toString())}; Path=/; Max-Age=${ONE_YEAR_SECONDS}; SameSite=Lax${secure}`;
}

/**
 * Le SOUS-TITRE d'un tableau de bord : la période nommée et ses dates.
 *
 * Pur, et donc utilisable des deux côtés du rendu — `t` et `format` viennent de
 * next-intl, serveur (`getFormatter`) comme client (`useFormatter`). Le libellé
 * remplace l'ancien « Activité des 14 derniers jours » figé.
 *
 * « Aujourd'hui » n'affiche qu'une date ; les autres affichent l'intervalle. Un
 * jour unique où `from === to` se lit aussi comme une date seule — deux fois la
 * même borne n'apprend rien.
 */
export function periodSubtitle(
  window: Pick<ResolvedDashboardWindow, 'period' | 'from' | 'to'>,
  t: (key: DashboardPeriod) => string,
  format: { dateTime: (date: Date, formatName: 'date') => string },
): string {
  const day = (value: string) => format.dateTime(new Date(`${value}T12:00:00Z`), 'date');
  const name = t(window.period);

  if (window.period === 'today' || window.from === window.to) {
    return `${name} · ${day(window.to)}`;
  }

  return `${name} · ${day(window.from)} – ${day(window.to)}`;
}
