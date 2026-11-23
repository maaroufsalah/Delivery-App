import type { Id, IsoDateTime } from './common';

/**
 * LE JOURNAL LISIBLE — recette SpeedyTrans 4, point 4 (D-133).
 *
 * Une COUCHE DE PRÉSENTATION sur `audit_logs` : rien de stocké ne change. Chaque
 * ligne répond à « qui a fait quoi, sur quoi, quand » en langage métier ; les
 * écritures techniques d'une même requête (l'événement créé avec le changement
 * de statut, les notifications…) forment UNE ligne.
 *
 * L'API ne rend ni libellé ni format : des CLÉS (type d'objet, action, champ,
 * valeur d'énumération) et des valeurs TYPÉES (centimes, date ISO, statut),
 * que l'écran traduit en fr / en / ar. L'export Excel, lui, est rendu en
 * français par l'API, sur les mêmes clés.
 */

/** Les familles d'objets, pour le filtre « Type d'objet ». */
export const JOURNAL_CATEGORIES = [
  'parcels',
  'notes',
  'routes',
  'cash',
  'invoices',
  'payouts',
  'cheques',
  'returns',
  'claims',
  'warehouse',
  'accounts',
  'settings',
  'other',
] as const;
export type JournalCategory = (typeof JOURNAL_CATEGORIES)[number];

/**
 * L'action DÉDUITE du changement, pas du type d'écriture : poser `deletedAt`
 * est une suppression, changer `statusId` un changement de statut.
 */
export const JOURNAL_ACTIONS = [
  'create',
  'update',
  'delete',
  'status',
  'amount',
  'validate',
  'issue',
  'pay',
  'close',
  'cancel',
  'login',
  'login-failed',
  /** D-257 : le client d'un colis changé par l'administrateur (D-256). */
  'client-change',
] as const;
export type JournalAction = (typeof JOURNAL_ACTIONS)[number];

/** Les deux vues du journal : les modifications, ou les connexions (filtre dédié). */
export const JOURNAL_VIEWS = ['changes', 'logins'] as const;
export type JournalView = (typeof JOURNAL_VIEWS)[number];

export const JOURNAL_SOURCES = ['web', 'mobile', 'import', 'auto'] as const;
export type JournalSource = (typeof JOURNAL_SOURCES)[number];

/** Le rôle de l'auteur, en clair. */
export const JOURNAL_ROLES = ['admin', 'staff', 'merchant', 'courier', 'platform', 'other'] as const;
export type JournalRole = (typeof JOURNAL_ROLES)[number];

/**
 * Une valeur, TYPÉE pour que l'écran la formate dans sa langue. Les
 * références (ville, agence, livreur, client) arrivent déjà résolues en noms :
 * jamais un identifiant.
 */
export type JournalValue =
  | { kind: 'text'; text: string }
  | { kind: 'money'; cents: number }
  | { kind: 'date'; iso: IsoDateTime }
  | { kind: 'number'; value: number }
  | { kind: 'bool'; value: boolean }
  /** Statut de colis : la clé du référentiel, lue sous `status.<key>`. */
  | { kind: 'parcel-status'; key: string }
  /** Valeur d'énumération, en clé filaire (`awaiting-validation`). */
  | { kind: 'enum'; value: string }
  | null;

export interface JournalChange {
  /** Clé de champ, lue sous `settings.journal.fields.<field>`. */
  field: string;
  before: JournalValue;
  after: JournalValue;
}

export interface JournalObject {
  /** Clé de type, lue sous `settings.journal.objects.<type>` : `parcel`, `pickup-note`… */
  type: string;
  /** La référence métier : code, nom, nom et prénom. Jamais un identifiant. */
  reference: string | null;
  /** Chemin de la fiche dans le back-office, sans la langue : `/parcels/CAS…`. */
  href: string | null;
}

export interface JournalEntry {
  /** La clé du GROUPE (la requête), pas une ligne de `audit_logs`. */
  id: string;
  at: IsoDateTime;
  action: JournalAction;
  /**
   * Le changement qui NOMME l'action : de quel statut vers lequel, de quel
   * montant vers lequel. Absent pour une création, une suppression…
   */
  headline: JournalChange | null;
  object: JournalObject;
  /** Combien d'objets du même type la même requête a touchés (≥ 1). */
  count: number;
  /** `null` = aucun auteur : tâche automatique, ou connexion refusée. */
  user: { name: string; role: JournalRole; email: string | null } | null;
  source: JournalSource;
  /** L'agence concernée, en clair, quand l'objet en porte une. */
  agency: string | null;
  category: JournalCategory;
}

export interface JournalEntryDetail extends JournalEntry {
  /** Les seuls champs MÉTIER modifiés, avant → après. */
  changes: JournalChange[];
  /** Le motif, quand il en existe un (prix, mode de paiement, destination, statut). */
  reason: string | null;
  /** L'adresse retenue (D-130). Dans le détail seulement. */
  ip: string | null;
  /** Les autres objets touchés par la même requête. */
  related: JournalObject[];
}

export interface JournalQuery {
  page?: number;
  pageSize?: number;
  view?: JournalView;
  category?: JournalCategory;
  action?: JournalAction;
  userId?: Id;
  from?: string;
  to?: string;
  /** Une référence (code de colis, de bon…) ou un e-mail d'auteur. */
  search?: string;
}
