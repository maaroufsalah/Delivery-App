import type { DashboardGranularity, DashboardPeriod, ResolvedDashboardWindow } from '../dashboard-period';
import type { ParcelStatusGroup } from '../statuses';
import type { AmountInCents, Id, IsoDateTime } from './common';
import type { ParcelListItem } from './parcel';
import type { RemittanceAlert, StaffAlertGroup, StaleRouteAlert, StuckParcelAlert } from './staff-app';

/**
 * LE TABLEAU DE BORD DE PILOTAGE — chantier « Dashboard de pilotage »
 * (`docs/chantiers/dashboard.md`, D-157 à D-163).
 *
 * Un contrat par section, un endpoint par contrat, tous sous `/dashboard`. Le
 * serveur agrège, l'écran affiche : aucun de ces types ne porte une liste de
 * colis à additionner côté navigateur — quand une liste existe (`latest`,
 * `items`), c'est un EXTRAIT borné, jamais la population.
 *
 * Les montants sont en centimes (`AmountInCents`), les taux en RATIO 0..1 (`null`
 * quand le dénominateur est nul : l'écran affiche « — », pas « 0 % »), les
 * délais en heures décimales.
 */

/* --- Filtres --------------------------------------------------------------- */

/**
 * Les filtres globaux, communs à toutes les sections.
 *
 * `agencyId` AFFINE la portée et ne l'élargit jamais ; `cityId` est la ville de
 * DESTINATION ; `courierId` et `clientId` sont ignorés quand la portée les a
 * déjà fixés (D-082).
 */
export interface DashboardFilters {
  period?: DashboardPeriod;
  /** Bornes INCLUSES `AAAA-MM-JJ`, lues seulement quand `period === 'custom'`. */
  from?: string;
  to?: string;
  agencyId?: Id;
  cityId?: Id;
  courierId?: Id;
  clientId?: Id;
}

/* --- Section 1 : KPI ---------------------------------------------------------- */

/** Les sept compteurs de colis, calculés sur une fenêtre. */
export interface DashboardParcelCounts {
  total: number;
  /** `statusId = 4`. */
  delivered: number;
  /** Ni terminal, ni retour, ni échec définitif : initial ∪ transit ∪ pivot ∪ retry. */
  inProgress: number;
  /** Physiquement en mouvement : 7, 12, 13, 17. Sous-ensemble de `inProgress`. */
  inTransit: number;
  /** Retourné, refusé, ou dans le parcours RL- : 5, 6, 20, 21, 22, 23, 24 (D-161). */
  returned: number;
  /** Familles `failure` ∪ `retry`. */
  failed: number;
  /** Famille `terminal` : 4, 5, 23. */
  closed: number;
  /** Livrés / (Livrés + Retours). `null` si le dénominateur est nul. */
  successRate: number | null;
  /** Retours / (Livrés + Retours). `null` si le dénominateur est nul. */
  returnRate: number | null;
}

/** Un point de la série « Colis par jour », ventilé par famille de statut. */
export type DashboardDayPoint = { date: string } & Record<ParcelStatusGroup, number>;

export interface DashboardCityPoint {
  cityId: Id;
  cityName: string;
  count: number;
}

export interface DashboardOverviewAlerts {
  /** Colis ayant dépassé le seuil de relances téléphoniques. */
  stuckInRetry: StuckParcelAlert[];
  /** Circuits ouverts depuis plus de 48 h. */
  staleRoutes: StaleRouteAlert[];
}

/** `GET /dashboard` — la vue d'ensemble, enrichie en place. */
export interface DashboardOverview {
  window: ResolvedDashboardWindow;
  /** La fenêtre de comparaison, de même longueur, décalée d'un cran ; `null` pour `all`. */
  previousWindow: Pick<ResolvedDashboardWindow, 'from' | 'to'> | null;
  kpis: DashboardParcelCounts & {
    /** Colis créés AUJOURD'HUI, quelle que soit la période. */
    todayCount: number;
    /** Alias historique de `delivered`, lu par la recette et l'ancien écran. */
    deliveredCount: number;
    /** COD des colis livrés pas encore remboursés au client (règle §3.8). */
    codToRemit: AmountInCents;
  };
  /** Les mêmes compteurs sur la fenêtre précédente, pour l'évolution. */
  previous: DashboardParcelCounts | null;
  daily: DashboardDayPoint[];
  byCity: DashboardCityPoint[];
  latest: ParcelListItem[];
  alerts: DashboardOverviewAlerts;
}

/* --- Section 3 : évolution ---------------------------------------------------- */

export const DASHBOARD_SERIES_RANGES = ['7d', '30d', '12m'] as const;
export type DashboardSeriesRange = (typeof DASHBOARD_SERIES_RANGES)[number];

export interface DashboardSeriesPoint {
  /** Premier jour du seau, `AAAA-MM-JJ`. */
  date: string;
  delivered: number;
  returns: number;
  inProgress: number;
  failures: number;
}

/**
 * `GET /dashboard/series?range=` — l'évolution des colis, ANCRÉE sur la borne
 * haute de la période globale (D-157).
 */
export interface DashboardSeries {
  range: DashboardSeriesRange;
  granularity: DashboardGranularity;
  from: string;
  to: string;
  /** Les instants UTC des bornes, comme `ResolvedDashboardWindow` (recette). */
  startAt?: string;
  endAt?: string;
  points: DashboardSeriesPoint[];
}

/* --- Section 4 : livreurs ----------------------------------------------------- */

export interface DashboardCourierRow {
  rank: number;
  courierId: Id;
  courierName: string;
  /** Colis de la fenêtre qui lui sont attribués. */
  assigned: number;
  delivered: number;
  returned: number;
  successRate: number | null;
  /** Ramassé → livré, en heures ; `null` sans colis mesurable. */
  avgDelayHours: number | null;
  codCollected: AmountInCents;
  /** Livrés sur la fenêtre précédente ; `null` pour `all`. */
  previousDelivered: number | null;
}

export const DASHBOARD_COURIER_SORTS = ['delivered', 'successRate', 'codCollected', 'assigned'] as const;
export type DashboardCourierSort = (typeof DASHBOARD_COURIER_SORTS)[number];

/* --- Section 5 : villes ------------------------------------------------------- */

export interface DashboardCityRow {
  cityId: Id;
  cityName: string;
  total: number;
  delivered: number;
  returned: number;
  successRate: number | null;
  returnRate: number | null;
  codCollected: AmountInCents;
}

export const DASHBOARD_CITY_SORTS = ['total', 'successRate', 'returnRate', 'codCollected'] as const;
export type DashboardCitySort = (typeof DASHBOARD_CITY_SORTS)[number];

/* --- Section 6 : clients ------------------------------------------------------ */

export interface DashboardClientRow {
  rank: number;
  clientId: Id;
  clientName: string;
  isPro: boolean;
  total: number;
  delivered: number;
  returned: number;
  successRate: number | null;
  codCollected: AmountInCents;
}

export const DASHBOARD_CLIENT_RANKS = ['volume', 'cod', 'successRate'] as const;
export type DashboardClientRank = (typeof DASHBOARD_CLIENT_RANKS)[number];

export interface DashboardMostActiveClient {
  clientId: Id;
  clientName: string;
  /** Colis sur la fenêtre courante. */
  current: number;
  /** Colis sur la fenêtre précédente ; `null` pour `all`. */
  previous: number | null;
}

export interface DashboardClients {
  rank: DashboardClientRank;
  items: DashboardClientRow[];
  total: number;
  page: number;
  pageSize: number;
  mostActive: DashboardMostActiveClient | null;
}

/** `GET /dashboard/clients/:id` — le détail d'un client sur la fenêtre. */
export interface DashboardClientDetail {
  clientId: Id;
  clientName: string;
  isPro: boolean;
  window: ResolvedDashboardWindow;
  totals: Omit<DashboardClientRow, 'rank'> & {
    returnRate: number | null;
    /** COD des colis `REMBOURSE` : versé par BV- ou facture réglée. */
    codReversed: AmountInCents;
    avgDelayHours: number | null;
  };
  previous: Pick<DashboardClientRow, 'total' | 'delivered' | 'returned'> | null;
  series: DashboardSeriesPoint[];
  granularity: DashboardGranularity;
  latest: ParcelListItem[];
}

/* --- Sections 2 et 7 : finance ------------------------------------------------ */

export interface DashboardMoneySplit {
  total: AmountInCents;
  cash: AmountInCents;
  cheque: AmountInCents;
}

/** `GET /dashboard/finance` — gardé par `finance.read` (D-163). */
export interface DashboardFinance {
  /** Encaissé par les livreurs sur les colis de la fenêtre (`route_parcels.codCollected`). */
  collected: DashboardMoneySplit;
  /** Colis `REMBOURSE` : l'e-commerçant a été payé. */
  reversed: AmountInCents;
  toRemit: {
    /** `ENCAISSE` : encore chez les livreurs. */
    couriers: DashboardMoneySplit;
    /** `REMIS_LIVREUR` ∪ `FACTURE` : rentré, pas encore reversé au client. */
    remitted: DashboardMoneySplit;
  };
  /** `ENCAISSE` depuis plus de `thresholdHours` après la livraison. */
  late: { count: number; amount: AmountInCents; thresholdHours: number };
  /** Règlements clients (`payments.paidAt`) de la fenêtre. */
  settlements: { count: number; amount: AmountInCents };
  /** Ordres de virement (OV-) créés dans la fenêtre. */
  transferOrders: { count: number; amount: AmountInCents };
  /** Écarts arrêtés à la clôture des RF- de la fenêtre. */
  cashVariance: { count: number; amount: AmountInCents };
  /** Solde dû par les livreurs, un ÉTAT : ignore la période. Négatif = ils doivent. */
  courierBalance: AmountInCents;
  previous: {
    collected: AmountInCents;
    reversed: AmountInCents;
    settlements: AmountInCents;
    transferOrders: AmountInCents;
    cashVariance: AmountInCents;
  } | null;
}

/* --- Section 8 : circuits ----------------------------------------------------- */

/** `GET /dashboard/routes` — circuits de LIVRAISON, créés dans la fenêtre. */
export interface DashboardRoutes {
  /** Non clôturés, toutes dates (un état). */
  open: number;
  /** Clôturés dans la fenêtre. */
  closed: number;
  /** Ouverts depuis plus de 24 h / 48 h (états). */
  late24: number;
  late48: number;
  avgParcelsPerRoute: number | null;
  avgDeliveredPerRoute: number | null;
  avgReturnsPerRoute: number | null;
  avgCodPerRoute: AmountInCents | null;
  /** Création → clôture, en heures, sur les clôturés de la fenêtre. */
  avgDurationHours: number | null;
  previous: { closed: number; avgDurationHours: number | null } | null;
}

/* --- Section 9 : délais ------------------------------------------------------- */

export interface DashboardDelayPoint {
  date: string;
  avgHours: number | null;
  count: number;
}

/** `GET /dashboard/delays` — ramassé (3 ou 18) → livré (4), sur les livrés de la fenêtre. */
export interface DashboardDelays {
  delivered: number;
  /** Livrés qui ont un point de départ. */
  measured: number;
  /** Livrés sans événement 3 ni 18 : exclus, jamais estimés (D-159). */
  withoutPickup: number;
  avgHours: number | null;
  medianHours: number | null;
  /** Parts des `measured` : `within24 + within48 + over48 = 1`. */
  within24: number | null;
  within48: number | null;
  over48: number | null;
  granularity: DashboardGranularity;
  series: DashboardDelayPoint[];
  previous: {
    avgHours: number | null;
    within24: number | null;
    within48: number | null;
    over48: number | null;
  } | null;
}

/* --- Section 10 : alertes ----------------------------------------------------- */

export interface CourierRateAlert {
  courierId: Id;
  courierName: string;
  delivered: number;
  returned: number;
  rate: number;
}

export interface CodNotRemittedAlert {
  courierId: Id;
  courierName: string;
  count: number;
  amount: AmountInCents;
  /** Âge du plus ancien encaissement non remis, en heures. */
  oldestHours: number;
}

export interface StuckParcelAgeAlert {
  code: string;
  recipientName: string;
  statusId: number;
  days: number;
}

export interface PerformanceDropAlert {
  courierId: Id;
  courierName: string;
  currentRate: number;
  previousRate: number;
}

export interface CityFailureAlert {
  cityId: Id;
  cityName: string;
  total: number;
  failed: number;
  rate: number;
}

/** Les seuils effectivement appliqués — réglables (D-160), affichés sur le bloc. */
export interface DashboardAlertThresholds {
  codLateHours: number;
  stuckParcelDays: number;
  courierReturnRate: number;
  cityFailureRate: number;
  performanceDropPts: number;
  minParcels: number;
}

/**
 * `GET /dashboard/alerts` — sept familles, chacune bornée à un extrait.
 *
 * Les deux familles d'ARGENT (`codNotRemitted`, `cashVariances`) ne sont
 * rendues qu'à qui porte `finance.read` : absentes, et non pas vides — « 0 écart
 * de caisse » laisserait croire qu'il n'y en a pas (même règle que
 * `StaffAlertsService`).
 */
export interface DashboardAlerts {
  /** 🔴 Livreur avec taux de retour supérieur au seuil. */
  courierReturnRate: StaffAlertGroup<CourierRateAlert>;
  /** 🟠 COD encaissé non remis depuis plus de N heures. */
  codNotRemitted?: StaffAlertGroup<CodNotRemittedAlert> & { amount: AmountInCents };
  /** 🔴 Circuit de livraison ouvert depuis plus de 48 h. */
  staleRoutes: StaffAlertGroup<StaleRouteAlert>;
  /** 🟠 Colis non clos sans mouvement depuis plus de N jours. */
  stuckParcels: StaffAlertGroup<StuckParcelAgeAlert>;
  /** 🔴 Écart de caisse arrêté à la clôture d'un RF-. */
  cashVariances?: StaffAlertGroup<RemittanceAlert>;
  /** 🟠 Baisse du taux de réussite d'un livreur entre les deux fenêtres. */
  performanceDrops: StaffAlertGroup<PerformanceDropAlert>;
  /** 🔴 Ville dont le taux d'échec dépasse le seuil. */
  cityFailureRate: StaffAlertGroup<CityFailureAlert>;
  thresholds: DashboardAlertThresholds;
  generatedAt: IsoDateTime;
}

/* --- Section 16 : export ------------------------------------------------------ */

export const DASHBOARD_EXPORT_SECTIONS = ['couriers', 'cities', 'clients'] as const;
export type DashboardExportSection = (typeof DASHBOARD_EXPORT_SECTIONS)[number];

export const DASHBOARD_EXPORT_FORMATS = ['xlsx', 'csv'] as const;
export type DashboardExportFormat = (typeof DASHBOARD_EXPORT_FORMATS)[number];
