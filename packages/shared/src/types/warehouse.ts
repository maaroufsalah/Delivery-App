import type { OrderSource, OrderStatus, StockMovementKind, StorageRequestStatus } from '../enums';
import type { AmountInCents, Id, IsoDateTime } from './common';

/**
 * Contrats du menu WAREHOUSE — DOMAIN.md §1.18.
 *
 * Quatre objets qui se tiennent : un **produit** appartient à un
 * e-commerçant ; son **stock** est compté par agence ; une **demande
 * d'entreposage** validée le fait entrer ; une **commande** transformée en
 * colis le fait sortir. Aucune quantité ne bouge sans un mouvement qui dit
 * pourquoi — c'est la différence avec le legacy, où `produit_stock.quantity`
 * était simplement écrasée.
 */

/* --- Produits ------------------------------------------------------------- */

/** Valeur d'attribut, telle qu'affichée sur une fiche produit. */
export interface ProductAttributeValueItem {
  id: Id;
  attributeId: Id;
  /** `Taille`, `Couleur` — les deux attributs du legacy (§1.18). */
  attributeName: string;
  value: string;
}

export interface ProductAttributeItem {
  id: Id;
  name: string;
  values: Array<{ id: Id; value: string }>;
}

export interface ProductListItem {
  id: Id;
  clientId: Id;
  clientName: string;
  reference: string | null;
  name: string;
  /** Millimètres entiers : un produit ne se mesure pas en flottants. */
  lengthMm: number | null;
  widthMm: number | null;
  heightMm: number | null;
  /** Grammes entiers. */
  weightG: number | null;
  price: AmountInCents | null;
  /** Valeur déclarée — sert à l'assurance, pas à la facturation. */
  declaredValue: AmountInCents | null;
  imageUrl: string | null;
  /**
   * Stock « interne » du legacy (`produits.stock`, NULL sur les 745 lignes).
   * Repris parce que la colonne existe, mais ce n'est PAS le stock réel : le
   * stock réel est la somme des `StockItem` par agence.
   */
  internalStock: number | null;
  /** Somme des quantités en agence, toutes agences confondues. */
  stockTotal: number;
  isActive: boolean;
  createdAt: IsoDateTime;
}

export interface ProductDetail extends ProductListItem {
  attributes: ProductAttributeValueItem[];
  stock: Array<{ agencyId: Id; agencyName: string; quantity: number; lowStock: boolean }>;
}

export interface ProductImportPreview {
  rows: number;
  valid: number;
  created: number;
  updated: number;
  errors: Array<{ row: number; column?: string; code: string; message: string }>;
  sample: Array<{ reference: string; name: string; price: AmountInCents | null }>;
}

/* --- Stock ---------------------------------------------------------------- */

export interface StockListItem {
  id: Id;
  productId: Id;
  productName: string;
  productReference: string | null;
  clientId: Id;
  clientName: string;
  agencyId: Id;
  agencyName: string;
  quantity: number;
  /** Sous le seuil paramétré (`warehouse.lowStockThreshold`, D-019). */
  lowStock: boolean;
  updatedAt: IsoDateTime;
}

export interface StockMovementItem {
  id: Id;
  kind: StockMovementKind;
  /** Signé : négatif pour une sortie. C'est la variation, pas un absolu. */
  quantity: number;
  /** Quantité APRÈS le mouvement, figée — l'historique doit se relire seul. */
  balance: number;
  reason: string | null;
  /** Ce qui a causé le mouvement, quand il en existe un. */
  storageRequestCode: string | null;
  parcelCode: string | null;
  authorName: string | null;
  createdAt: IsoDateTime;
}

export interface StockDetail extends StockListItem {
  movements: StockMovementItem[];
}

/* --- Entreposages --------------------------------------------------------- */

export interface StorageRequestItemLine {
  id: Id;
  productId: Id;
  productName: string;
  productReference: string | null;
  quantity: number;
}

export interface StorageRequestListItem {
  id: Id;
  code: string;
  status: StorageRequestStatus;
  clientId: Id;
  clientName: string;
  agencyId: Id;
  agencyName: string;
  lineCount: number;
  /** Somme des quantités demandées — ce qui va entrer en stock. */
  totalQuantity: number;
  requestedAt: IsoDateTime;
  decidedAt: IsoDateTime | null;
  decidedByName: string | null;
  createdAt: IsoDateTime;
}

export interface StorageRequestDetail extends StorageRequestListItem {
  note: string | null;
  /** Motif du refus. Refuser sans dire pourquoi n'apprend rien au client. */
  refusalReason: string | null;
  items: StorageRequestItemLine[];
  /** Décidable : encore en attente et porte au moins une ligne. */
  decidable: boolean;
}

/* --- Commandes ------------------------------------------------------------ */

export interface OrderLineItem {
  id: Id;
  productId: Id;
  productName: string;
  productReference: string | null;
  quantity: number;
  /** Prix unitaire GELÉ à la création de la ligne. */
  unitPrice: AmountInCents;
}

export interface OrderListItem {
  id: Id;
  code: string;
  status: OrderStatus;
  source: OrderSource;
  /**
   * Référence du système d'origine. Avec `clientId`, elle porte l'idempotence
   * de l'import : réimporter le même fichier ne crée pas de doublon.
   */
  externalRef: string | null;
  clientId: Id;
  clientName: string;
  agencyId: Id | null;
  agencyName: string | null;
  recipientName: string;
  recipientPhone: string;
  destinationCityId: Id;
  destinationCityName: string;
  lineCount: number;
  totalAmount: AmountInCents;
  /** Renseigné dès que le colis est créé : la commande ne s'en détache plus. */
  parcelId: Id | null;
  parcelCode: string | null;
  createdAt: IsoDateTime;
}

export interface OrderDetail extends OrderListItem {
  deliveryAddress: string;
  destinationDistrictId: Id | null;
  destinationDistrictName: string | null;
  note: string | null;
  lines: OrderLineItem[];
  /** Le colis peut être créé : commande confirmée, avec au moins une ligne. */
  parcelCreatable: boolean;
}

export interface OrderImportPreview {
  rows: number;
  valid: number;
  created: number;
  /** Lignes ignorées parce que `(clientId, externalRef)` existait déjà. */
  skipped: number;
  errors: Array<{ row: number; column?: string; code: string; message: string }>;
  sample: Array<{ externalRef: string; recipientName: string; totalAmount: AmountInCents }>;
}
