import type { AmountInCents, IsoDateTime } from './common';

/**
 * LE STOCK D'UN LIVREUR — lot M, D-222.
 *
 * Ce qu'il détient à l'instant T, physiquement ou en argent, calculé à la
 * demande depuis les statuts et les circuits réels : aucune table, aucun cache.
 * Un colis tombe dans UN bloc (`classifyCourierStock`, `courier-stock.ts`).
 */
export type CourierStockBlock =
  /** 18 — ramassé par lui, pas encore validé à l'agence. */
  | 'picked-up'
  /** 13 (et 7 sur un de ses circuits de livraison) — en cours de livraison. */
  | 'out-for-delivery'
  /** 16 — en attente de livraison, sur un de ses circuits non clos. */
  | 'awaiting'
  /** Échecs et relances encore chez lui (D-153), retours en route. */
  | 'failed-or-returning'
  /** 4 — livré, COD encaissé pas encore remis (« MAD en main »). */
  | 'delivered-unremitted';

/** Les compteurs d'un livreur — la ligne de la liste, l'en-tête du détail, le PDF. */
export interface CourierStockCounters {
  /** Colis EN MAIN : les quatre premiers blocs. */
  inHand: number;
  pickedUp: number;
  outForDelivery: number;
  awaiting: number;
  failedOrReturning: number;
  deliveredUnremitted: number;
  /** COD encaissé non remis — égal à « MAD en main » du mobile. */
  collectedUnremittedInCents: AmountInCents;
  /** COD attendu des colis en main encore livrables. */
  toCollectInCents: AmountInCents;
  lastMovementAt: IsoDateTime | null;
}

export interface CourierStockRow extends CourierStockCounters {
  courierId: string;
  courierName: string;
  courierPhone: string;
  agencyId: string | null;
  agencyName: string | null;
}

export interface CourierStockPage {
  items: CourierStockRow[];
  total: number;
  page: number;
  pageSize: number;
  /** Totaux de TOUTE la liste filtrée, pas de la page. */
  totals: CourierStockCounters;
}

export interface CourierStockParcel {
  id: string;
  code: string;
  recipientName: string;
  recipientPhone: string;
  destinationCityId: string;
  destinationCityName: string;
  clientId: string;
  clientName: string;
  /** Ce que le colis vaut au pas de porte (`codToCollect`, DOMAIN.md §3.8). */
  codInCents: AmountInCents;
  statusId: number;
  block: CourierStockBlock;
  routeId: string | null;
  routeCode: string | null;
  assignedAt: IsoDateTime | null;
  lastMovementAt: IsoDateTime | null;
}

/** Le récapitulatif par statut : nombre de colis et leur COD. */
export interface CourierStockStatusLine {
  statusId: number;
  count: number;
  codInCents: AmountInCents;
}

export interface CourierStockOption {
  value: string;
  label: string;
}

export interface CourierStockDetail {
  courier: {
    id: string;
    name: string;
    phone: string;
    agencyId: string | null;
    agencyName: string | null;
  };
  /** Compteurs de la liste FILTRÉE (D4). */
  counters: CourierStockCounters;
  byStatus: CourierStockStatusLine[];
  parcels: {
    items: CourierStockParcel[];
    total: number;
    page: number;
    pageSize: number;
  };
  /** Les valeurs des menus, tirées du stock NON filtré : on ne filtre que sur ce qui existe. */
  options: {
    statuses: number[];
    clients: CourierStockOption[];
    cities: CourierStockOption[];
    routes: CourierStockOption[];
  };
}
