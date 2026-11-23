import type { BillingType, ClientType, VehicleType } from '../enums';
import type { AmountInCents, Id, IsoDateTime } from './common';

export interface City {
  id: Id;
  name: string;
  /**
   * LE NOM EN ARABE — chantier « Étiquette orange », point 1.
   *
   * « Ville de destination en grand (français + arabe). » Facultatif : aucune
   * translittération automatique ne serait juste sur les villes marocaines, et
   * l'étiquette se contente du français tant que la ligne est vide.
   */
  nameAr: string | null;
  /** Code 2-3 lettres, brique du code colis (DOMAIN.md §3.2). */
  code: string;
  zoneId: Id;
  isActive: boolean;
}

export interface Zone {
  id: Id;
  name: string;
  /**
   * Les deux tarifs INTERNES — B-20 bis, C14.
   *
   * **ABSENTS de la réponse pour un e-commerçant ou un livreur.** Ce ne sont
   * pas des champs à zéro : les clés ne sont pas sérialisées du tout, pour
   * qu'aucune lecture côté client — un `in`, un `??`, une colonne d'export — ne
   * puisse en déduire quoi que ce soit. Même mécanique que
   * `Parcel.reimbursedByCourier` (D-060), et pour la même raison : la grille
   * d'un transporteur est ce qu'il facture à SES clients, et la montrer à
   * l'un d'eux lui donne un levier sur une négociation qui n'est pas la
   * sienne.
   *
   * Le rendu est décidé par `canSeeInternalPricing()`, en un seul endroit.
   */
  baseRate?: AmountInCents;
  sameCityRate?: AmountInCents;
  /** Délai annoncé au public, en jours ouvrés. Il reste, lui : il est public. */
  deliveryDays: number;
}

/** DOMAIN.md §1.7 — 10 164 lignes en prod : c'est la maille de livraison réelle. */
export interface District {
  id: Id;
  cityId: Id;
  name: string;
}

export interface Client {
  id: Id;
  name: string;
  type: ClientType;
  /** ICE marocain. */
  taxId: string | null;
  phone: string;
  email: string;
  cityId: Id;
  /**
   * SON AGENCE DE RATTACHEMENT — retouches Anass, point 4.
   *
   * Elle voyage avec la forme LÉGÈRE pour la même raison que `billingType` :
   * un FORMULAIRE en a besoin. Le bon de ramassage et le colis préremplissent
   * leur agence depuis le client choisi, et aller chercher la fiche complète
   * à chaque changement de sélecteur serait un aller-retour par frappe.
   *
   * `null` sur les fiches saisies avant que la règle existe : la résolution
   * par la ville prend alors le relais, côté serveur.
   */
  agencyId: Id | null;
  isActive: boolean;
  /**
   * Régime de facturation — C11 bloc 3, D-061.
   *
   * Il voyage avec la forme LÉGÈRE parce qu'un sélecteur en a besoin : l'écran
   * des bons de versement ne doit proposer que des `professional`, et aller
   * chercher la fiche complète de chaque client pour le savoir serait une
   * requête par ligne de liste déroulante.
   */
  billingType: BillingType;
  /**
   * CLIENT PRO — point 4 du chantier « Retours, BL Clients Pro ».
   *
   * Il voyage avec la forme LÉGÈRE pour la même raison que `billingType` et
   * `agencyId` : un FORMULAIRE en a besoin. La question « BL à récupérer ? »
   * n'apparaît sur le formulaire colis que si le client choisi est Pro, et
   * aller chercher sa fiche complète à chaque changement de sélecteur serait un
   * aller-retour par frappe.
   */
  isPro: boolean;
  createdAt: IsoDateTime;
}

export interface Courier {
  id: Id;
  firstName: string;
  lastName: string;
  phone: string;
  cityId: Id;
  vehicleType: VehicleType;
  isActive: boolean;
  /**
   * Combien de villes il sert (sa grille, lot F, D-206). Au-delà de 0, le
   * formulaire de circuit ne demande plus de ville : le circuit propose les
   * colis de toutes ses villes. Facultatif : absent du jeu de démonstration (0).
   */
  cityCount?: number;
}

export interface Agency {
  id: Id;
  name: string;
  cityId: Id;
  phone: string;
  address: string;
}
