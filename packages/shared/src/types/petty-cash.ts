import type { DashboardPeriod } from '../dashboard-period';
import type { PettyCashDayStatus, PettyCashKind, PettyCashStatus } from '../schemas/petty-cash';
import type { AmountInCents, Id, IsoDateTime, Paginated } from './common';

/** Un mouvement de petite caisse — chantier « Caisse, OV, Petite caisse », point 3. */
export interface PettyCashMovement {
  id: Id;
  kind: PettyCashKind;
  /** Jour du mouvement, `AAAA-MM-JJ`. */
  date: string;
  amount: AmountInCents;
  agencyId: Id;
  agencyName: string;
  label: string | null;
  category: string | null;
  buyerId: Id | null;
  /** Recopié : le nom reste lisible si le compte part. */
  buyerName: string | null;
  note: string | null;
  proofName: string | null;
  createdByName: string | null;
  createdAt: IsoDateTime;
  updatedAt: IsoDateTime;
  /** D-168 — seul `validated` compte. */
  status: PettyCashStatus;
  refusalReason: string | null;
  /** Qui a validé ou refusé un brouillon, et quand ; `null` si validé d'office. */
  decidedByName: string | null;
  decidedAt: IsoDateTime | null;
  /**
   * D-249 — l'état de la journée de caisse du mouvement. `closed` : aucune
   * modification, validation ni suppression tant qu'elle n'est pas rouverte.
   */
  dayStatus: PettyCashDayStatus;
}

/**
 * LA JOURNÉE DE CAISSE — D-249. Pour une agence, ou la somme des agences de
 * la portée (administrateur sans filtre : `agencyId` vaut alors `null`).
 */
export interface PettyCashDaySummary {
  /** `AAAA-MM-JJ`, jour de la société. */
  day: string;
  agencyId: Id | null;
  /** `null` pour une ligne agrégée. */
  agencyName: string | null;
  /** Le nombre d'agences agrégées (1 pour une ligne d'agence). */
  agencies: number;
  openingBalance: AmountInCents;
  supplied: AmountInCents;
  spent: AmountInCents;
  closingBalance: AmountInCents;
  /** Brouillons de livreurs du jour, hors solde : « N à valider ». */
  pendingDrafts: number;
  /** Agrégée : `open` dès qu'une de ses agences l'est. */
  status: PettyCashDayStatus;
  closedAt: IsoDateTime | null;
  /** `automatic` : la veille de minuit ou la reprise ; sinon le nom de l'admin. */
  closedBy: 'automatic' | string | null;
  reopenedAt: IsoDateTime | null;
  reopenedByName: string | null;
  reopenReason: string | null;
}

export interface PettyCashDaysPage {
  items: PettyCashDaySummary[];
  /** Le jour de la société, `AAAA-MM-JJ`. */
  today: string;
  /** La journée en cours (bandeau) ; `null` si la caisse n'a encore rien. */
  current: PettyCashDaySummary | null;
}

/** Le détail d'une journée : la somme, et chaque agence. */
export interface PettyCashDayDetail {
  summary: PettyCashDaySummary;
  agencies: PettyCashDaySummary[];
}

/**
 * Le bandeau. `balance` est le solde de la caisse À CE JOUR (toutes dates,
 * filtre d'agence respecté) ; `supplied` et `spent` portent sur la période.
 */
export interface PettyCashTotals {
  balance: AmountInCents;
  supplied: AmountInCents;
  spent: AmountInCents;
  /** D-168 — brouillons de livreurs en attente, toutes dates, filtre d'agence respecté. */
  drafts: number;
}

export interface PettyCashPage extends Paginated<PettyCashMovement> {
  totals: PettyCashTotals;
  period: DashboardPeriod;
  /** Bornes RÉSOLUES `AAAA-MM-JJ`, pour le sélecteur de période. */
  from: string;
  to: string;
}

/** Ce que les formulaires et filtres proposent. */
export interface PettyCashMeta {
  categories: string[];
  /** Les membres de l'équipe qui peuvent avoir acheté. */
  buyers: Array<{ id: Id; name: string }>;
  /**
   * Les agences de la portée : toutes pour l'administrateur, la sienne sinon.
   * Servies ici pour que l'écran ne dépende pas de `directory.read`.
   */
  agencies: Array<{ id: Id; name: string }>;
}

/**
 * L'écran du LIVREUR — D-168. Ses dépenses seules, sans solde ni total de
 * caisse : il ne voit pas la caisse, il y déclare ce qu'il a payé.
 */
export interface PettyCashCourierPage extends Paginated<PettyCashMovement> {
  /** Ses brouillons en attente : le badge de la tuile. */
  drafts: number;
  /** Les catégories de la société, pour le formulaire. */
  categories: string[];
}
