import type { Billability } from '../courier-pay';
import type {
  BillingType,
  CashRemittanceStatus,
  CodPayoutStatus,
  CourierStatementLineKind,
  CourierStatementStatus,
  InvoiceStatus,
  PayModel,
  PayPeriod,
  PaymentMethod,
  RemittanceStage,
} from '../enums';
import type { SettlementMode } from '../courier-pay';
import type { DashboardPeriod } from '../dashboard-period';
import type { FinancialStatus } from '../financial-statuses';
import type { ChequeStatus } from './cheque';
import type { AmountInCents, Id, IsoDateTime } from './common';

/**
 * Contrats du menu FACTURATION — DOMAIN.md §1.15 à §1.17.
 *
 * L'argent circule dans DEUX sens, et le legacy les mélangeait dans le même
 * écran d'administration :
 *
 * ```
 * Destinataire --COD--> Livreur --retour de fonds--> Société
 * Société --facture--> E-commerçant --règlement-->
 * ```
 *
 * Le **retour de fonds** (`recettes`) fait remonter le cash encaissé par le
 * livreur. La **facture** (`factures`) dit à l'e-commerçant ce que la société
 * lui doit — ou ce qu'il doit, quand ses colis ont échoué. Les deux ne se
 * rapprochent nulle part chez le legacy (§3.9) ; ici ils restent distincts,
 * mais le détail d'une facture sait dire si le cash est rentré.
 */

/* --- Référentiel bancaire ------------------------------------------------ */

export interface Bank {
  id: Id;
  name: string;
  /** Sigle affiché sur un relevé — `CIH`, `BP`, `AWB`. */
  code: string | null;
  isActive: boolean;
}

export interface BankAccount {
  id: Id;
  bankId: Id;
  bankName: string;
  label: string;
  /** RIB marocain à 24 chiffres. */
  rib: string | null;
  /** Le compte de la société, jamais celui de l'e-commerçant. */
  holder: string | null;
  isActive: boolean;
  /**
   * Compte pré-sélectionné à la saisie d'un virement (Paramètres > Finance).
   *
   * Porté par la LISTE des comptes plutôt que lu dans les réglages par chaque
   * écran : enregistrer un règlement ne demande pas le droit de lire la
   * configuration, et c'est très bien ainsi.
   */
  isDefault: boolean;
}

/* --- Facture ------------------------------------------------------------- */

/**
 * Un colis prêt à être facturé : livré, retourné ou refusé, et pas encore sur
 * une facture émise. C'est la pile d'entrée de l'écran de composition.
 */
export interface BillableParcelLine {
  id: Id;
  code: string;
  clientId: Id;
  clientName: string;
  recipientName: string;
  destinationCityName: string;
  statusId: number;
  statusAt: IsoDateTime;
  codAmount: AmountInCents;
  /** Frais HORS TAXES. */
  deliveryFee: AmountInCents;
  failureFee: AmountInCents | null;
  /** TVA sur les frais, au taux en vigueur (B-11). */
  vatAmount: AmountInCents;
  /** Encaissé moins frais TTC. Négatif quand l'e-commerçant doit de l'argent. */
  netDue: AmountInCents;
  /** Le cash de ce colis est-il remonté par un retour de fonds confirmé ? */
  cashReceived: boolean;
  /**
   * NON FACTURABLE / FACTURABLE / FACTURABLE À ZÉRO — point 6.
   *
   * Trois valeurs et non un booléen : en JavaScript `0` est falsy, et un
   * écran qui testerait `deliveryFee` aurait fait disparaître le colis
   * facturé zéro exactement comme un colis qu'on ne facture pas.
   */
  billability: Billability;
}

/**
 * UNE LIGNE DU SÉLECTEUR DE COLIS FACTURABLES, et son CAS — chantier
 * « Retours, BL Clients Pro », point 6.
 *
 * `billability` distingue `'billable'` de `'billable-free'` : un colis livré
 * dont les frais valent zéro est facturable, et l'écran doit pouvoir le DIRE
 * plutôt que de laisser l'agent croire à un oubli de saisie. C'est un geste
 * commercial, une remise, un rattrapage de litige — la ligne à zéro est
 * précisément celle qui prouve qu'il a été fait.
 */
export interface InvoiceLine {
  id: Id;
  parcelId: Id;
  /** Copies FIGÉES à l'émission : la ligne ne suit plus le colis. */
  parcelCode: string;
  /**
   * Le destinataire du colis — lot P. Relu sur le colis au rendu, PAS figé :
   * ce n'est pas un montant (D-169 ne s'applique pas), il sert à l'e-commerçant
   * à reconnaître sa commande. `null` pour un colis supprimé logiquement.
   */
  recipientName: string | null;
  /**
   * LES DONNÉES CLIENT DE LA LIGNE — chantier « Export Excel et données client
   * sur les factures » (11/11/2026, priorité de Badr). Comme le nom : relues sur
   * le colis au rendu, PAS figées — ce ne sont pas des montants. `null` pour un
   * colis supprimé logiquement, ou quand la donnée n'existe pas.
   */
  /** La référence commande du vendeur (`externalCode`). */
  externalCode?: string | null;
  recipientPhone?: string | null;
  destinationCityName?: string | null;
  /** La date du dernier « Livré » du colis ; `null` pour une ligne non livrée. */
  deliveredAt?: IsoDateTime | null;
  statusId: number;
  codAmount: AmountInCents;
  /** Frais HORS TAXES. */
  deliveryFee: AmountInCents;
  failureFee: AmountInCents;
  /** TVA sur les frais de cette ligne. */
  vatAmount: AmountInCents;
  /**
   * DÉJÀ RÉGLÉ PAR CHÈQUE — chantier « Chèques », point 3.
   *
   * Le destinataire a payé une part en papier, et ce papier va au vendeur sans
   * passer par la société : la facture ne peut pas le lui redevoir.
   */
  chequePaid: AmountInCents;
  /** Le numéro du chèque, pour que la ligne se rapproche du papier. */
  chequeNumber: string | null;
  /**
   * Le chèque a été REJETÉ — D-237 : il reste retranché (Speedy n'a encaissé
   * que les espèces), le papier est rendu au vendeur. La facture le dit en
   * toutes lettres : « rendu au vendeur, non payé par Speedy ».
   */
  chequeRejected: boolean;
  /** Encaissé, moins les chèques déjà remis, moins les frais TTC. */
  netDue: AmountInCents;
}

export interface InvoiceListItem {
  id: Id;
  code: string;
  status: InvoiceStatus;
  clientId: Id;
  clientName: string;
  /**
   * Le client est Client Pro (`isPro`, badge de /clients) — recette 3,
   * point 4 : dit dans quel sous-menu la facture se range.
   */
  clientIsPro: boolean;
  agencyId: Id;
  agencyName: string;
  parcelCount: number;
  totalCod: AmountInCents;
  /** Frais HORS TAXES. */
  totalDeliveryFee: AmountInCents;
  totalFailureFee: AmountInCents;
  /** TVA sur les frais, au taux GELÉ à l'émission (B-11). */
  totalVat: AmountInCents;
  /** Taux appliqué, en centièmes de pourcent : `2000` = 20,00 %. */
  vatRate: number;
  /**
   * Lot R (D-230) — taux de TVA PRO, en centièmes : la TVA est calculée une
   * fois sur le total HT (pied « Total HT, TVA, Montant TVA, Total TTC »).
   * Courant sur un brouillon, gelé à l'émission. `null` ou absent : facture non
   * pro, ou émise avant le lot — rendue comme avant.
   */
  proVatRate?: number | null;
  /**
   * Ce que la société doit à l'e-commerçant.
   *
   * `normal` : COD moins frais TTC. `professional` : les frais TTC en négatif,
   * parce que le COD a été versé à part (D-061) — c'est l'e-commerçant qui
   * doit, et `totalCod` n'est là que pour mémoire.
   */
  totalDue: AmountInCents;
  /**
   * Les lignes HORS COLIS, hors taxes, signées — lot Argent 2, A2-1 (D-181).
   * Elles font partie des frais : `totalDue` les a déjà retranchées (TTC).
   */
  totalAdjustment: AmountInCents;
  /** Régime de la facture, recopié du client à la création — D-061. */
  billingType: BillingType;
  /** Bornes de la période, sur une facture MENSUELLE. `null` sinon. */
  periodStart: IsoDateTime | null;
  periodEnd: IsoDateTime | null;
  /** Somme des règlements enregistrés. */
  totalPaid: AmountInCents;
  issuedAt: IsoDateTime | null;
  dueAt: IsoDateTime | null;
  paidAt: IsoDateTime | null;
  createdAt: IsoDateTime;
}

/** Le groupe des brouillons, clé de son en-tête dans la liste groupée (D-254). */
export const INVOICE_DRAFT_DAY = 'draft';

/**
 * L'EN-TÊTE D'UN JOUR de la liste des factures — D-254 (« Groupes générés »
 * de l'ancienne appli). Les factures ÉMISES ce jour-là (jour de la société),
 * aux filtres de la liste ; `day = 'draft'` pour les brouillons. Des sommes
 * de montants gelés, aucun calcul nouveau.
 */
export interface InvoiceDaySummary {
  /** `AAAA-MM-JJ`, ou `INVOICE_DRAFT_DAY`. */
  day: string;
  invoiceCount: number;
  parcelCount: number;
  /** Somme des `totalDue`, la colonne « Dû » de la liste. */
  totalDue: AmountInCents;
  /** Règlements enregistrés sur ces factures, par méthode. */
  paidCash: AmountInCents;
  paidCheque: AmountInCents;
  paidTransfer: AmountInCents;
  /**
   * Les factures émises du jour qu'aucun ordre de virement ne porte encore :
   * ce que « Ordre de virement du jour » envoie. Vide pour les brouillons.
   */
  transferableCodes: string[];
}

/** Une ligne de facture hors colis — lot Argent 2, A2-1 (D-181). */
export interface InvoiceAdjustmentLine {
  id: Id;
  kind: 'adjustment' | 'pickup-note-fee' | 'other';
  /** Hors taxes, SIGNÉ : positif = dû par le vendeur. */
  amount: AmountInCents;
  vatAmount: AmountInCents;
  reason: string;
  /** Facture d'origine, bon BR-… */
  reference: string | null;
  createdByName: string | null;
  createdAt: IsoDateTime;
}

export interface InvoiceDetail extends InvoiceListItem {
  note: string | null;
  issuedByName: string | null;
  lines: InvoiceLine[];
  /** Les lignes hors colis, dans l'ordre de leur saisie. */
  adjustments: InvoiceAdjustmentLine[];
  /**
   * Brouillon : les lignes hors colis s'ajoutent et se retirent. L'écran les
   * offre en plus à qui porte `finance.validate` ; le serveur refuse le reste.
   */
  adjustable: boolean;
  payments: PaymentListItem[];
  /** Émettable : encore en brouillon et porte au moins une ligne. */
  issuable: boolean;
  /** Reste à régler — `totalDue - totalPaid`, jamais recalculé à l'écran. */
  balance: AmountInCents;
}

/* --- Règlement ----------------------------------------------------------- */

/**
 * Un chèque, tel qu'il se relit — C11 bloc 7, D-067.
 *
 * Partagé par les deux règlements : celui d'un client et la paie d'un livreur
 * ne se saisissent pas au même écran, mais un chèque est un chèque.
 */
export interface ChequeDetails {
  chequeNumber: string | null;
  chequeBank: string | null;
  /** Date d'ÉMISSION — pas celle du règlement. Un chèque peut être post-daté. */
  chequeDate: IsoDateTime | null;
  /** Nom d'origine de l'image. Le contenu se télécharge par sa route. */
  proofName: string | null;
}

export interface PaymentListItem extends ChequeDetails {
  id: Id;
  invoiceId: Id | null;
  invoiceCode: string | null;
  clientId: Id;
  clientName: string;
  agencyId: Id;
  agencyName: string;
  amount: AmountInCents;
  method: PaymentMethod;
  bankAccountId: Id | null;
  bankAccountLabel: string | null;
  reference: string | null;
  note: string | null;
  paidAt: IsoDateTime;
  createdAt: IsoDateTime;
}

/* --- Retour de fonds ----------------------------------------------------- */

/** Colis livré dont le COD n'est encore sur aucun retour de fonds confirmé. */
export interface RemittanceCandidate {
  id: Id;
  code: string;
  clientName: string;
  recipientName: string;
  destinationCityName: string;
  /** Ce que le livreur a encaissé au pas de porte (§3.8). */
  codCollected: AmountInCents;
  /**
   * La commission FIGÉE du colis (C11 bloc 1, D-059).
   *
   * Affichée même pour un livreur `monthly` — elle dit ce qu'il a gagné. Ce qui
   * change d'un mode à l'autre, c'est si elle est DÉDUITE ici ou reportée sur
   * le relevé, et c'est `commissionDeducted` qui le dit.
   */
  courierCommission: AmountInCents;
  statusAt: IsoDateTime;
}

export interface RemittanceItem {
  parcelId: Id;
  parcelCode: string;
  clientName: string;
  recipientName: string;
  /** Montant GELÉ à la composition : la recette ne suit pas un colis qui bouge. */
  amount: AmountInCents;
  /** Ce qui a été encaissé en billets sur ce colis. */
  cash: AmountInCents;
  /** Ce qui l'a été en papier. `cash + cheque = amount`. */
  cheque: AmountInCents;
  /**
   * Les papiers eux-mêmes.
   *
   * Un tableau, bien que la base tienne « un chèque par colis » par un index
   * unique : l'écran doit pouvoir en déplier plusieurs le jour où la règle
   * changera, et un tableau à un élément ne ment pas, là où un objet seul
   * aurait figé la règle dans le contrat.
   */
  cheques: RemittanceCheque[];
  /** Commission déduite SUR CETTE LIGNE. Zéro en mode `monthly`. */
  commission: AmountInCents;
}

/**
 * UN CHÈQUE À REMETTRE — chantier « Chèques », point 1.
 *
 * Ce que l'agence doit recevoir en PAPIER, à côté des billets. Le numéro
 * d'abord : c'est par lui qu'on retrouve un chèque contesté, et « (non
 * relevé) » quand le livreur ne l'a pas noté au pas de porte.
 */
export interface RemittanceCheque {
  id: Id;
  number: string;
  bank: string | null;
  amount: AmountInCents;
  status: ChequeStatus;
  parcelId: Id;
  parcelCode: string;
}

export interface RemittanceListItem {
  id: Id;
  code: string;
  status: CashRemittanceStatus;
  /**
   * LE STATUT AFFICHÉ — D-243. Déduit par l'API de `status`, de la clôture du
   * livreur et du versement des colis ; aucun écran ne le recalcule.
   */
  stage: RemittanceStage;
  /** Quand le livreur a fermé sa caisse, et qui. `null` : jamais fermée. */
  courierClosedAt: IsoDateTime | null;
  courierClosedByName: string | null;
  courierId: Id;
  courierName: string;
  cityId: Id;
  cityName: string;
  agencyId: Id;
  agencyName: string;
  parcelCount: number;
  /** Ce que la remise porte réellement : le déclaré s'il existe, le net sinon. */
  amount: AmountInCents;

  /**
   * Le calcul — C11 bloc 2. Aucun de ces trois montants ne se saisit.
   *
   * ```
   * net attendu = COD encaissé − commissions déduites
   * ```
   *
   * Les commissions valent zéro pour un livreur `monthly` : il rend tout, elles
   * passent sur son relevé (D-058). C'est le seul endroit du produit où le mode
   * de règlement se voit à l'œil nu.
   */
  expectedCod: AmountInCents;
  /**
   * Le détail de l'encaissement. `totalCash + totalCheque = expectedCod`.
   *
   * Les deux sont rangés plutôt qu'un seul et une soustraction : le
   * rapprochement ne lit que les chèques, et la soustraction se serait faite à
   * l'écran, donc quatre fois différemment.
   */
  totalCash: AmountInCents;
  totalCheque: AmountInCents;
  /** Combien de PAPIERS, pas combien de dirhams — on les compte à la main. */
  chequeCount: number;
  expectedCommission: AmountInCents;
  /**
   * Ce que le livreur pose sur la table, EN ESPÈCES : `totalCash` moins la
   * commission déduite. **Le chèque n'y entre pas** — c'est l'argent du
   * vendeur, il se remet en papier (chantier « Chèques », point 1).
   */
  expectedNet: AmountInCents;
  /** Ce que le livreur a posé sur la table. `null` = pas encore compté. */
  declaredAmount: AmountInCents | null;
  /** Déclaré moins attendu. Négatif : il manque de l'argent. Zéro si non compté. */
  variance: AmountInCents;
  varianceReason: string | null;
  /** Le mode du livreur, recopié : un reçu doit rester lisible quand il change. */
  settlementMode: SettlementMode;

  confirmedAt: IsoDateTime | null;
  confirmedByName: string | null;
  createdAt: IsoDateTime;
}

/**
 * UNE LIGNE DE « À REMETTRE PAR LIVREUR » — chantier « Retour de fonds
 * unique », point 6.
 *
 * Un livreur, son bon OUVERT, et ce qu'il doit poser sur la table. Il y a au
 * plus une ligne par livreur, l'index partiel le garantit (D-116).
 */
export interface RemittanceOpenRow {
  remittanceId: Id;
  code: string;
  courierId: Id;
  courierName: string;
  agencyName: string;
  /** Espèces moins commission déduite — ce que l'agence attend en BILLETS. */
  toHandOver: AmountInCents;
  /** Et à côté, en papier : le montant et le nombre de chèques. */
  chequeAmount: AmountInCents;
  chequeCount: number;
  parcelCount: number;
  /** Quand le bon s'est ouvert — à la première livraison, le plus souvent. */
  openedAt: IsoDateTime;
  /**
   * Fermée par le livreur — D-243 : l'argent est prêt, il attend l'agence.
   * `null` : la caisse est encore ouverte, il y verse ses livraisons.
   */
  courierClosedAt: IsoDateTime | null;
}

/**
 * LE TABLEAU DE BORD DES RETOURS DE FONDS — point 6.
 *
 * **L'écran ne calcule rien.** Chaque montant est arrivé sommé du serveur, sur
 * l'ENSEMBLE de la période : un total recalculé au rendu ne verrait que la page
 * affichée, et mentirait dès la seconde.
 *
 * **Les cartes suivent la période, la liste des bons ouverts NON.** Un bon
 * ouvert est un état, pas un flux : le borner à septembre aurait caché le
 * livreur qui garde l'argent d'août — exactement celui qu'il faut voir.
 */
export interface RemittanceSummary {
  period: DashboardPeriod;
  /** Bornes RÉSOLUES `AAAA-MM-JJ`, telles que le serveur les a lues. */
  from: string;
  to: string;
  courierId: Id | null;
  /**
   * Les cartes. Les bons ANNULÉS n'y entrent pas : ils ne portent aucun argent,
   * et les compter aurait gonflé le nombre de bons sans gonfler un dirham.
   */
  totals: {
    /** Ce que les livreurs ont encaissé — espèces et chèques confondus. */
    collected: AmountInCents;
    /** La part en billets, et la part en papier — point 1. */
    cash: AmountInCents;
    cheque: AmountInCents;
    /** Espèces moins commissions : ce que l'agence attend en BILLETS. */
    toHandOver: AmountInCents;
    /** Ce qui a été réellement compté sur la table. */
    handed: AmountInCents;
    /** Remis moins attendu. Négatif : il manque de l'argent. */
    variance: AmountInCents;
    /** Nombre de bons sur la période, ouverts et clôturés. */
    count: number;
    openCount: number;
    confirmedCount: number;
  };
  /** Les livreurs qui portent encore de l'argent, hors période. */
  open: RemittanceOpenRow[];
}

export interface RemittanceDetail extends RemittanceListItem {
  note: string | null;
  items: RemittanceItem[];
  /** Tous les chèques du bon, à remettre en main propre — point 1. */
  cheques: RemittanceCheque[];
  /** Confirmable : encore en brouillon et porte au moins un colis. */
  confirmable: boolean;
  /**
   * Fermée par le livreur ET rouvrable : il n'a pas déjà une autre caisse
   * ouverte (D-243). Quand c'est faux sur une caisse fermée, l'agence corrige
   * directement ses lignes.
   */
  reopenable: boolean;
}

/**
 * CE QUE LE LIVREUR PORTE — chantier « Retour de fonds unique », point 7.
 *
 * Un OBJET, et non `RemittanceDetail | null` nu. Nest sérialise un `null`
 * rendu par un contrôleur en corps VIDE, et `response.json()` lève dessus :
 * les deux clients ne rattrapent que le 204. « Pas de bon ouvert » est une
 * réponse ordinaire — elle doit voyager comme une réponse, pas comme un vide
 * que chaque client interprète à sa façon.
 *
 * Trouvé par la recette sur la production, pas par un test : aucun double ne
 * reproduit la sérialisation de Nest.
 */
export interface CourierOpenRemittance {
  remittance: RemittanceDetail | null;
}

/* --- Bon de versement (client PRO) ---------------------------------------- */

/**
 * C11 bloc 3, D-061 — le COD d'un e-commerçant PRO, versé INTÉGRALEMENT.
 *
 * Ni une facture ni une recette. Les trois flux d'argent du produit restent
 * distincts :
 *
 * ```
 * Recette   : Livreur  --cash-->   Société            (CashRemittance)
 * Versement : Société  --COD-->    E-commerçant PRO   (CodPayout)
 * Facture   : Société  --frais-->  E-commerçant       (Invoice)
 * ```
 *
 * Aucune déduction n'apparaît nulle part dans ces types, et ce n'est pas un
 * oubli : c'est tout l'objet du document. Les frais sont réclamés à part, par
 * la facture mensuelle.
 */

/** Colis remboursé par le livreur, dont le COD n'est encore sur aucun bon versé. */
export interface CodPayoutCandidate {
  id: Id;
  code: string;
  recipientName: string;
  destinationCityName: string;
  /** Ce que la société a encaissé pour ce colis (§3.8). */
  codCollected: AmountInCents;
  /** Quand le livreur a rendu le cash — la date qui rend le colis versable. */
  reimbursedAt: IsoDateTime | null;
  statusAt: IsoDateTime;
}

export interface CodPayoutItem {
  parcelId: Id;
  parcelCode: string;
  recipientName: string;
  destinationCityName: string;
  /** Montant GELÉ à la composition : le versement ne suit pas un colis qui bouge. */
  amount: AmountInCents;
}

export interface CodPayoutListItem {
  id: Id;
  code: string;
  status: CodPayoutStatus;
  clientId: Id;
  clientName: string;
  agencyId: Id;
  agencyName: string;
  parcelCount: number;
  /** Somme des COD versés. AUCUNE déduction. */
  amount: AmountInCents;
  method: PaymentMethod;
  bankAccountId: Id | null;
  bankAccountLabel: string | null;
  reference: string | null;
  /** Nom d'origine de la pièce jointe. Le contenu vit dans le stockage. */
  proofName: string | null;
  paidAt: IsoDateTime | null;
  paidByName: string | null;
  createdAt: IsoDateTime;
}

/**
 * Un chèque reçu pour un colis du bon — lot S (D-231). POUR INFORMATION : il
 * n'entre dans aucun montant du bon (D-229), il voyage à part, vers le vendeur.
 */
export interface CodPayoutCheque {
  id: Id;
  number: string;
  bank: string | null;
  amount: AmountInCents;
  parcelCode: string;
  /** L'état ACTUEL, lu à chaque ouverture : le chèque avance après le versement. */
  status: ChequeStatus;
  /**
   * Bon versé AVANT le lot Q : la ligne GELÉE vaut le COD entier, la part
   * chèque y a donc été versée en espèces. Le bon le dit au lieu d'imprimer
   * « non inclus », qui serait faux. Aucun montant n'est réécrit.
   */
  includedInPayout: boolean;
}

export interface CodPayoutDetail extends CodPayoutListItem {
  note: string | null;
  items: CodPayoutItem[];
  /** Versable : encore en brouillon et porte au moins un colis. */
  payable: boolean;
  /** Les chèques non annulés de ses colis, dans l'ordre des lignes (lot S). */
  cheques: CodPayoutCheque[];
}

/**
 * Une ligne du récapitulatif « En masse » — lot U (D-233) : un client, une
 * agence, ce que SON futur bon versera (le même calcul que le BV unitaire).
 */
export interface CodPayoutBulkCandidate {
  clientId: Id;
  clientName: string;
  agencyId: Id;
  agencyName: string;
  parcelCount: number;
  /** Espèces à verser = Σ des lignes (COD encaissé − chèque compté, D-229). */
  cash: AmountInCents;
  /** Chèques des colis retenus, POUR INFORMATION : jamais versés. */
  chequeCount: number;
  chequeAmount: AmountInCents;
  /** Toujours 0 : un BV ne déduit aucun frais (D-061), facturés à part. */
  fees: AmountInCents;
  total: AmountInCents;
}

export interface CodPayoutBulkResult {
  created: Array<{
    clientId: Id;
    clientName: string;
    agencyName: string;
    code: string;
    parcelCount: number;
    amount: AmountInCents;
  }>;
  /** Motifs : `client-unknown`, `client-not-professional`, `nothing-to-pay`, `agency-out-of-scope`, `error`. */
  skipped: Array<{ clientId: Id; clientName: string; reason: string }>;
  total: AmountInCents;
}

/* --- Tableau de bord ------------------------------------------------------ */

/**
 * Le point du jour de la facturation.
 *
 * Quatre chiffres, quatre questions du matin : où est le cash, ce qui est
 * rentré sans être facturé, ce qui attend un règlement et depuis quand, et ce
 * que les frais ont rapporté ce mois-ci.
 *
 * Le COD n'est PAS du chiffre d'affaires : c'est de l'argent qui traverse la
 * société. Seuls les frais lui appartiennent, et c'est pourquoi ils ont leur
 * propre indicateur.
 */
export interface FinanceDashboard {
  /** Cash encaissé et pas encore remonté, par livreur. Le plus gros en tête. */
  cashToRemit: Array<{
    courierId: Id;
    courierName: string;
    parcelCount: number;
    amount: AmountInCents;
  }>;
  cashToRemitTotal: AmountInCents;

  /** Cash rentré par une recette confirmée, dont le colis n'est pas facturé. */
  remittedNotInvoiced: {
    parcelCount: number;
    amount: AmountInCents;
  };

  /** Factures émises et non soldées. */
  unpaidInvoices: {
    count: number;
    /** Reste à régler, toutes factures confondues. */
    amount: AmountInCents;
    /** Parmi elles, celles émises depuis plus de 30 jours. */
    overdueCount: number;
    oldestIssuedAt: IsoDateTime | null;
    /**
     * Âge de la plus ancienne, en jours.
     *
     * Calculé côté serveur : l'écran ne lit pas l'horloge au rendu — c'est
     * impur, et deux rendus successifs donneraient deux réponses.
     */
    oldestAgeDays: number | null;
  };

  /** Frais facturés depuis le 1er du mois — le CA propre du transporteur. */
  feeRevenueThisMonth: AmountInCents;
}

/* --- Le tableau de bord financier ----------------------------------------- */

/** Un compte et un montant. Le couple qui revient dans tout le tableau. */
export interface FinanceBucket {
  count: number;
  amount: AmountInCents;
}

/**
 * LE TABLEAU DE BORD FINANCIER — chantier « Finance », point 10, D-112.
 *
 * Six blocs, six requêtes agrégées, **aucun calcul à l'écran**. La règle n'est
 * pas esthétique : un total recalculé au rendu se recalcule sur les 25 lignes
 * de la page courante et ment dès la seconde page. Ici chaque chiffre est une
 * somme faite en base, sur l'ensemble de la période.
 *
 * **Les deux axes ne se mélangent jamais** (D-105) : `parcelsByFinancialStatus`
 * répond « où en est l'argent », `deliveredCount` répond « où en est le
 * colis ». Ce sont deux questions, et les additionner n'aurait aucun sens.
 *
 * **Le solde des livreurs ignore la période, et c'est voulu** : une dette est
 * un ÉTAT, pas un flux. La borner à septembre aurait affiché « 0 » à un livreur
 * qui doit 4 000 dirhams depuis août.
 */
export interface FinanceOverview {
  /** La période choisie — le sélecteur partagé du chantier « Filtre de période ». */
  period: DashboardPeriod;
  /** Bornes INCLUSES, `AAAA-MM-JJ`, résolues dans le fuseau du tenant. */
  from: string;
  to: string;
  /** Le livreur sur lequel le tableau est borné, ou `null` pour tous. */
  courierId: Id | null;

  /** Bloc 1 — les bons de retour de fonds de la période (D-115). */
  cash: {
    /** Encaissé par les livreurs. `cash` + `cheque` = `collected`. */
    collected: AmountInCents;
    cash: AmountInCents;
    cheque: AmountInCents;
    /** Les commissions déduites à la remise (D-058). */
    commissions: AmountInCents;
    /** À remettre : encaissé moins commissions. */
    expected: AmountInCents;
    /** Réellement remis, compté au comptoir. */
    handed: AmountInCents;
    /** Somme SIGNÉE des écarts. Négatif : il manque de l'argent. */
    variance: AmountInCents;
    openCount: number;
    validatedCount: number;
  };

  /** Bloc 2 — ce que les livreurs doivent, au grand livre. Un SOLDE. */
  balances: {
    /** Signé : négatif, le livreur doit. */
    total: AmountInCents;
    rows: Array<{ courierId: Id; courierName: string; balance: AmountInCents }>;
  };

  /** Bloc 3 — les chèques reçus des destinataires (D-108). */
  cheques: {
    received: FinanceBucket;
    /** Ni encaissés, ni rejetés, ni annulés : encore en route vers la banque. */
    pending: FinanceBucket;
    cashed: FinanceBucket;
    rejected: FinanceBucket;
  };

  /** Bloc 4 — l'axe FINANCIER des colis de la période. */
  parcelsByFinancialStatus: Array<{
    status: FinancialStatus;
    count: number;
    /** Le COD porté par ces colis. */
    amount: AmountInCents;
  }>;

  /** Bloc 5 — l'axe LOGISTIQUE, compté à part. */
  deliveredCount: number;

  /**
   * Bloc 6 — les factures e-commerçants de la période.
   *
   * `open` ne compte que les factures ÉMISES : un brouillon ne réclame rien.
   */
  clientInvoices: { open: FinanceBucket; paid: FinanceBucket };

  /**
   * Bloc 7 — les relevés livreurs de la période.
   *
   * `validated` inclut les relevés déjà PAYÉS : un relevé payé a été validé, et
   * une pile qui les perdrait sous-compterait ce qui a été engagé.
   */
  courierStatements: { open: FinanceBucket; validated: FinanceBucket };
}

/* --- Paramètres > Finance ------------------------------------------------- */

/**
 * Tout ce que la facturation lit et que personne ne devrait coder en dur — D-019.
 *
 * Le regroupement n'est pas cosmétique : frais d'échec, frais de ramassage et
 * seuil de gratuité vivaient dans Paramètres > Société, à côté des langues et
 * du logo. Ce sont des politiques d'ARGENT, elles se règlent avec les autres
 * politiques d'argent — et un opérateur qui ajuste un tarif ne doit pas passer
 * par l'écran d'identité de l'entreprise.
 */
export interface FinanceSettings {
  /**
   * Taux de TVA des factures NON pro, en POURCENT au bord (B-14) : `20` =
   * 20 %. Inchangé par le lot R ; 0 = la TVA ne vaut que pour les pros.
   */
  vatRate: number;
  /**
   * Lot R (D-230) — taux de TVA par DÉFAUT des Clients Pro, en POURCENT au
   * bord. 20 % d'usine ; une fiche Pro peut le remplacer par le sien.
   */
  proVatRate: number;

  /** Préfixes des numéros de document. Vides = les valeurs d'usine. */
  invoicePrefix: string;
  remittancePrefix: string;
  statementPrefix: string;
  /** Préfixe des bons de versement — C11 bloc 3. `BV-2609-0001` d'usine. */
  codPayoutPrefix: string;
  /**
   * Compteur remis à zéro chaque ANNÉE plutôt que chaque mois.
   *
   * Le compteur mensuel donne `FA-2609-0001` ; l'annuel donne `FA-26-0001`.
   * Les deux existent en cabinet comptable, et le choix ne se rattrape pas
   * après coup — d'où le réglage.
   */
  numberingYearlyReset: boolean;

  /** DOMAIN.md §3.3 — frais d'échec, selon que le colis restait ou non en ville. */
  failureFeeSameCity: AmountInCents;
  failureFeeCrossCity: AmountInCents;
  /** §3.4 — ramassage, et seuil au-delà duquel il est offert. */
  pickupFee: AmountInCents;
  freePickupThreshold: number;

  /** D-020 — ce que vaut un colis retourné pour le livreur. Zéro par défaut. */
  courierReturnFee: AmountInCents;
  /**
   * Ce que vaut pour le livreur une course SANS issue payante — C11 bloc 1.
   *
   * Zéro d'usine, comme `courierReturnFee` et pour la même raison : le legacy
   * ne payait rien, et supposer l'inverse coûterait de l'argent réel à chaque
   * remise de fonds. Une société qui dédommage ses livreurs pour un
   * déplacement perdu pose la valeur ; les autres n'ont rien à faire.
   */
  courierFailedCommission: AmountInCents;
  /** Rythme de paie proposé par défaut. La fiche livreur peut le surcharger. */
  courierPayPeriod: PayPeriod;

  /** Mentions imprimées en pied de facture. Vide = la mention par défaut. */
  invoiceFooter: string;
  /**
   * Mentions de pied de BON DE VERSEMENT — C11 blocs 3 et 8.
   *
   * Séparées de celles de la facture : les deux documents ne disent pas la
   * même chose et ne s'adressent pas au même service chez le client.
   * Réutiliser la mention de facture ferait imprimer des conditions de
   * règlement sur un document qui en CONSTATE un.
   */
  codPayoutFooter: string;
  /** Compte pré-sélectionné à la saisie d'un virement. */
  defaultBankAccountId: Id | null;

  /**
   * Rétention du journal des modifications, en JOURS — C11 blocs 5 et 8.
   *
   * **Zéro veut dire ILLIMITÉ**, et c'est le défaut : effacer un journal
   * d'audit est irréversible, et personne ne devrait le découvrir après coup.
   */
  auditRetentionDays: number;

  /**
   * Rétention des PIÈCES JOINTES, en jours — LECTURE SEULE.
   *
   * Elle n'est pas un `Setting` mais une variable d'hébergement (D-066) : le
   * volume est partagé entre les tenants, et les fichiers d'avant C11 ne
   * portent pas de société dans leur chemin. Elle figure ici pour Être LUE —
   * un administrateur doit savoir combien de temps ses images de chèques
   * restent — et l'écran dit où elle se règle.
   */
  uploadsRetentionDays: number;
}

/* --- Relevé de rémunération livreur --------------------------------------- */

/**
 * DOMAIN.md §5 question n°3 et D-020.
 *
 * Le legacy n'avait pas de rémunération livreur : `facture_livreur_colis`
 * compte UNE ligne. Le relevé est une construction v2, et son contrat dit
 * comment il a été calculé — mode, rythme, bornes — parce qu'un relevé de mars
 * doit rester lisible quand le réglage change en avril.
 */
export interface CourierStatementLine {
  id: Id;
  parcelId: Id;
  kind: CourierStatementLineKind;
  /** Copies FIGÉES à la validation : la ligne ne suit plus le colis. */
  parcelCode: string;
  cityName: string;
  statusId: number;
  amount: AmountInCents;
  /**
   * Cette commission a-t-elle DÉJÀ été retenue à une remise de fonds ?
   *
   * La ligne reste affichée dans les deux cas — elle dit le travail fait — mais
   * une ligne déjà déduite n'entre pas dans le net à payer (C11 bloc 6, D-058).
   */
  deducted: boolean;
  /** Date de la course : le relevé se lit dans l'ordre du travail. */
  workedAt: IsoDateTime;
}

export interface CourierStatementListItem {
  id: Id;
  code: string;
  status: CourierStatementStatus;
  courierId: Id;
  courierName: string;
  agencyId: Id;
  agencyName: string;
  periodStart: IsoDateTime;
  periodEnd: IsoDateTime;
  periodKind: PayPeriod;
  payModel: PayModel;
  /** Le second axe : quand les commissions se règlent (C11 bloc 6, D-058). */
  settlementMode: SettlementMode;
  lineCount: number;
  /** Part fixe, recopiée de la fiche livreur (`SALARY` et `HYBRID`). */
  baseAmount: AmountInCents;
  /**
   * COD encaissé sur la période — INFORMATIF.
   *
   * Ce n'est pas l'argent du livreur, c'est celui qu'il a fait transiter. Il
   * figure parce que c'est la première chose qu'il vérifie, et parce qu'un
   * relevé qui ne parle que de commissions ne se rapproche d'aucune remise.
   */
  codCollected: AmountInCents;
  /** Toutes les commissions gagnées sur la période, déduites ou non. */
  commissionsTotal: AmountInCents;
  /** Celles que le livreur a DÉJÀ gardées, retenues aux remises de fonds. */
  commissionsDeducted: AmountInCents;
  /** Ce qui RESTE dû sur les courses. Zéro en mode `SALARY`. */
  variableAmount: AmountInCents;
  /** Ce qui est réellement dû : le fixe plus ce qui reste. */
  totalDue: AmountInCents;
  validatedAt: IsoDateTime | null;
  validatedByName: string | null;
  paidAt: IsoDateTime | null;
  createdAt: IsoDateTime;
}

export interface CourierStatementDetail extends CourierStatementListItem, ChequeDetails {
  note: string | null;
  /** Par quel canal le livreur a été payé — C11 bloc 7, D-067. */
  paymentMethod: PaymentMethod;
  bankAccountId: Id | null;
  bankAccountLabel: string | null;
  reference: string | null;
  lines: CourierStatementLine[];
  /** Validable : encore en brouillon. */
  validatable: boolean;
}

/* -------------------------------------------------------------------------- */
/* Factures clients EN MASSE — chantier « Finance », point 5                  */
/* -------------------------------------------------------------------------- */

/**
 * Un e-commerçant prêt à être facturé, et de combien.
 *
 * C'est la liste que l'écran coche. Elle ne propose QUE des clients qui ont
 * quelque chose à facturer : en proposer d'autres aurait obligé l'agent à
 * découvrir après coup que sa sélection n'a rien produit.
 */
export interface BulkInvoiceCandidate {
  clientId: Id;
  clientName: string;
  agencyId: Id;
  agencyName: string;
  /** Colis en « remis par le livreur » et sur aucune facture. */
  parcelCount: number;
  /** Ce que la facture réclamera, TVA comprise. Estimation à l'instant du calcul. */
  totalDue: AmountInCents;
}

/**
 * Ce qu'une création en masse a produit.
 *
 * Les REFUS sont rendus autant que les succès, et nommés : une création en
 * masse qui rendrait « 7 factures créées » sur 9 clients cochés laisserait
 * chercher les deux manquants.
 */
export interface BulkInvoiceResult {
  created: Array<{
    clientId: Id;
    clientName: string;
    code: string;
    parcelCount: number;
    /** Lot D, D5 (D-186) : les colis ont rejoint le brouillon déjà ouvert du vendeur. */
    reused?: boolean;
  }>;
  skipped: Array<{ clientId: Id; clientName: string; reason: string }>;
}

/* -------------------------------------------------------------------------- */
/* Ordre de virement groupé — chantier « Caisse, OV, Petite caisse », point 2  */
/* -------------------------------------------------------------------------- */

/**
 * Une ligne de l'ordre de virement : un client.
 *
 * `due` est le net des factures (chèques transmis déduits, règle
 * `netDueFor`), `fees` les frais facturés TVA comprise, et `collected` leur
 * somme : la ligne s'additionne toujours, `collected − fees = due`.
 */
export interface TransferOrderLine {
  clientId: Id;
  clientName: string;
  parcelCount: number;
  collected: AmountInCents;
  fees: AmountInCents;
  due: AmountInCents;
}

export interface TransferOrderDetail {
  code: string;
  source: 'selection' | 'invoices';
  lines: TransferOrderLine[];
  invoiceCodes: string[];
  totalCollected: AmountInCents;
  totalFees: AmountInCents;
  totalDue: AmountInCents;
  createdAt: IsoDateTime;
  createdByName: string | null;
}
