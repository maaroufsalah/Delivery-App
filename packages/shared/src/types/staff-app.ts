import type { AmountInCents, Id, IsoDateTime } from './common';

/**
 * Ce que l'application du STAFF consomme — M3.
 *
 * Le pendant de `courier-app.ts` et `merchant-app.ts`. Un agent d'agence sur un
 * téléphone ne refait pas le back-office : il traite ce qui cloche. Ces
 * contrats servent donc **des alertes**, pas des tableaux.
 */

/**
 * Une famille d'alertes.
 *
 * `count` est le total réel, `items` un échantillon. La pastille d'un écran
 * mobile doit dire « 23 » même si la liste n'en montre que cinq : tronquer le
 * compte avec la liste ferait croire le problème plus petit qu'il n'est.
 */
export interface StaffAlertGroup<T> {
  count: number;
  items: T[];
}

/** Un colis qui s'enlise en relance téléphonique. */
export interface StuckParcelAlert {
  code: string;
  recipientName: string;
  attempts: number;
  statusId: number;
}

/** Un circuit resté ouvert trop longtemps. */
export interface StaleRouteAlert {
  code: string;
  courierName: string;
  cityName: string;
  hoursOpen: number;
}

/** Une remise de fonds : en attente de comptage, ou comptée avec un écart. */
export interface RemittanceAlert {
  code: string;
  courierName: string;
  cityName: string;
  /** Ce que la remise porte — attendu tant qu'elle n'est pas comptée. */
  amount: AmountInCents;
  /** Signé : négatif, il manque de l'argent. `0` tant que rien n'est déclaré. */
  variance: AmountInCents;
  varianceReason: string | null;
  createdAt: IsoDateTime;
}

/** Une réclamation qui attend une réponse. */
export interface ClaimAlert {
  code: string;
  subject: string;
  clientName: string;
  /** Vrai quand l'échéance est dépassée — c'est ce qui la fait remonter. */
  isLate: boolean;
  createdAt: IsoDateTime;
}

/** Un bon de ramassage demandé par un client et pas encore validé. */
export interface PendingNoteAlert {
  code: string;
  clientName: string;
  cityName: string;
  parcelCount: number;
  createdAt: IsoDateTime;
}

/**
 * Ce que l'écran d'accueil du staff affiche.
 *
 * **Chaque famille est absente quand la clé manque**, plutôt que vide : un
 * agent sans `finance.read` ne doit pas voir « 0 écart de caisse », qui laisse
 * croire qu'il n'y en a pas. La matrice est éditable (D-019), et un écran qui
 * ment sur ce qu'il ne peut pas lire est pire qu'un écran qui se tait.
 */
export interface StaffAlerts {
  stuckInRetry: StaffAlertGroup<StuckParcelAlert>;
  staleRoutes: StaffAlertGroup<StaleRouteAlert>;
  pendingNotes?: StaffAlertGroup<PendingNoteAlert>;
  pendingRemittances?: StaffAlertGroup<RemittanceAlert>;
  cashVariances?: StaffAlertGroup<RemittanceAlert>;
  openClaims?: StaffAlertGroup<ClaimAlert>;
  /** L'agence réellement interrogée — le sélecteur la reflète. */
  agencyId: Id | null;
}
