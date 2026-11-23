import type { AmountInCents, Id, IsoDateTime } from './common';

/**
 * L'ÉCHANGE DE COLIS — chantier « Finance », point 6, D-110.
 *
 * **Un objet, pas un statut.** Un échange relie TROIS choses : le colis
 * d'origine, le produit qui revient, et le colis neuf qui repart. Le coller sur
 * le statut du colis aurait rejoué la dette n°1 de DOMAIN.md une troisième fois
 * — après la facturation (D-105) et après le retour (D-098).
 */

/**
 * Cinq marches et une sortie, et elles suivent la MARCHANDISE.
 *
 * « Nouveau colis créé » dit qu'un carton existe ; « ancien récupéré » qu'il a
 * changé de mains. Un statut « en cours » n'aurait rien appris à l'agent qui
 * décroche pour répondre au client.
 */
export const EXCHANGE_STATUSES = [
  'demande',
  'nouveau-colis-cree',
  'ancien-recupere',
  'nouveau-livre',
  'cloture',
  'annule',
] as const;

export type ExchangeStatus = (typeof EXCHANGE_STATUSES)[number];

export const EXCHANGE_STATUS_COLORS: Record<ExchangeStatus, `#${string}`> = {
  demande: '#D35400',
  'nouveau-colis-cree': '#2980B9',
  'ancien-recupere': '#8C7AE6',
  'nouveau-livre': '#3498DB',
  cloture: '#6AB04C',
  annule: '#D63031',
};

/**
 * L'échange peut-il encore être annulé ?
 *
 * Tant que le NOUVEAU colis n'est pas livré. Après, il y a deux cartons chez
 * deux personnes, et l'affaire se dénoue par des mouvements tracés — pas par
 * l'effacement d'un dossier qui est devenu une preuve.
 */
export function canCancelExchange(status: ExchangeStatus): boolean {
  return status === 'demande' || status === 'nouveau-colis-cree' || status === 'ancien-recupere';
}

export interface ExchangeListItem {
  id: Id;
  code: string;
  status: ExchangeStatus;

  originalParcelId: Id;
  originalParcelCode: string;
  newParcelId: Id | null;
  newParcelCode: string | null;

  clientName: string;
  returnedProduct: string;
  newProduct: string;
  oldDestination: string;
  newDestination: string;

  amount: AmountInCents;
  fees: AmountInCents;

  courierId: Id | null;
  courierName: string | null;

  requestedAt: IsoDateTime;
  pickedUpAt: IsoDateTime | null;
  deliveredAt: IsoDateTime | null;
  closedAt: IsoDateTime | null;
  cancelledAt: IsoDateTime | null;
}

export interface ExchangeEvent {
  id: Id;
  status: ExchangeStatus;
  comment: string | null;
  authorName: string | null;
  createdAt: IsoDateTime;
}

export interface ExchangeDetail extends ExchangeListItem {
  note: string | null;
  events: ExchangeEvent[];

  /**
   * Les gestes que le SERVEUR autorise, calculés là et nulle part ailleurs.
   *
   * Même raison que les sept drapeaux d'un bon de retour (D-102) : trois
   * espaces qui recopieraient la machine à états, ce sont trois façons de se
   * tromper.
   */
  pickupable: boolean;
  deliverable: boolean;
  closable: boolean;
  cancellable: boolean;
}

/**
 * Le lien qu'une fiche colis affiche — point 6, « relation navigable ».
 *
 * Il dit ce que CE colis est dans l'échange : l'origine ou le neuf. Sans ce
 * `role`, la fiche montrerait un code sans savoir s'il désigne ce qui part ou
 * ce qui revient.
 */
export interface ParcelExchangeLink {
  exchangeId: Id;
  exchangeCode: string;
  status: ExchangeStatus;
  role: 'original' | 'new' | 'return';
  /** L'autre colis du dossier, quand il existe. */
  counterpartCode: string | null;
  returnedProduct: string;
  newProduct: string;

  /**
   * LE BLOC « DEMANDE DE RETOUR/ÉCHANGE » — chantier « Étiquette orange »,
   * point 2 : « code, statut, date, lien ».
   *
   * La date manquait, et c'est elle qu'on cherche en premier quand un client
   * rappelle : « vous aviez demandé l'échange quand ? ».
   */
  requestedAt: IsoDateTime;
  /**
   * Le colis de RETOUR, `RET-<code>` — point 2.
   *
   * `counterpartCode` ne porte que le colis d'échange : l'entité Échange n'a
   * qu'un `newParcelId`, et lui en ajouter un second aurait fait migrer une
   * table pour une information que la relation `referenceParcelId` porte déjà.
   * `null` sur les dossiers ouverts au bureau, qui n'en fabriquent pas.
   */
  returnParcelCode: string | null;
}
