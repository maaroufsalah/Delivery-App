import type {
  BillingType,
  ClientDocumentKind,
  ClientType,
  LabelTemplate,
  PayModel,
  PayPeriod,
  UserRole,
  VehicleType,
} from '../enums';
import type { SettlementMode } from '../courier-pay';
import type { PermissionKey } from '../permissions';
import type { AmountInCents, Id, IsoDateTime } from './common';

/**
 * Contrats du menu COMPTES — utilisateurs, rôles, agences, e-commerçants,
 * livreurs.
 *
 * Comme pour le référentiel, ces types sont la forme d'ADMINISTRATION : ils
 * portent les compteurs et les rattachements dont l'écran de gestion a besoin.
 * Les formes légères de `directory.ts` (`Client`, `Courier`, `Agency`) restent
 * celles des listes déroulantes, et elles ne changent pas.
 *
 * DOMAIN.md §1.4 — le legacy mélangeait compte et personne : le RIB du livreur
 * vivait sur `users`, et `users.agences` était un `varchar` sérialisé à la main
 * (`"[1,4]"`). Ici un compte (`User`) est une identité de connexion, une fiche
 * (`Client`, `Courier`) est une personne morale ou physique, et le lien entre
 * les deux est explicite et facultatif.
 */

export interface UserListItem {
  id: Id;
  /** `null` pour un compte livreur qui se connecte par son nom d'utilisateur. */
  email: string | null;
  /** Identifiant de connexion alternatif, en minuscules — recette 3, point 5. */
  username: string | null;
  firstName: string;
  lastName: string;
  phone: string | null;
  jobTitle: string | null;
  roles: UserRole[];
  /** Agence PRINCIPALE — celle qui cadre la portée des requêtes. */
  agencyId: Id | null;
  agencyName: string | null;
  /** Agences additionnelles (pivot `UserAgency`), remplaçant du CSV legacy. */
  agencyIds: Id[];
  isActive: boolean;
  /** Compte invité qui n'a pas encore choisi son mot de passe. */
  mustChangePassword: boolean;
  lastLoginAt: IsoDateTime | null;
  invitedAt: IsoDateTime | null;
  createdAt: IsoDateTime;
}

export interface UserDetail extends UserListItem {
  nationalId: string | null;
  /** Fiche métier liée, quand le compte en a une. */
  clientId: Id | null;
  courierId: Id | null;
}

/**
 * Réponse d'invitation — le mot de passe temporaire n'est renvoyé QU'ICI.
 *
 * Il n'est stocké nulle part en clair et ne peut plus être relu : le
 * réafficher demanderait de le conserver, et un mot de passe conservé en clair
 * est un mot de passe fuité. Si l'écran est fermé trop vite, on réinitialise.
 */
export interface UserInvitation {
  user: UserListItem;
  temporaryPassword: string;
}

/**
 * Ce que rend `POST /auth/forgot-password` — C12 bloc 2b.
 *
 * **La même réponse, que l'adresse existe ou non.** Distinguer les deux cas
 * ferait de cette route un annuaire des comptes de la société : il suffirait
 * d'essayer des adresses pour savoir lesquelles sont ouvertes.
 *
 * Elle porte donc ce qu'il faut dire à TOUT LE MONDE : qui appeler. Tant que
 * B-13 (l'envoi d'e-mail) n'est pas livré, c'est la seule issue réelle, et
 * l'écran l'affiche au lieu de promettre un message qui ne partira pas.
 */
export interface ForgotPasswordResult {
  /** La demande est enregistrée. Toujours vrai : elle l'est dans tous les cas. */
  ok: true;
  /** Téléphone de la société, tel que Paramètres > Société le porte. */
  supportPhone: string | null;
  companyName: string;
  /**
   * L'espace a-t-il un canal d'envoi d'e-mail câblé ?
   *
   * Faux tant que le fournisseur est « log » (B-13). Ce n'est PAS « un message
   * vous a été envoyé » : ce drapeau ne dit rien de l'adresse saisie, et ne
   * peut donc pas servir à deviner si elle existe.
   */
  emailEnabled: boolean;
}

/** Matrice rôle × permission, telle que l'écran la dessine. */
export interface RoleMatrix {
  roles: UserRole[];
  permissions: Array<{ key: PermissionKey; module: string; label: string }>;
  /** Clés accordées, par rôle. */
  granted: Record<UserRole, PermissionKey[]>;
}

/**
 * Les droits d'UN compte — « Dernières corrections », point 1 (modèle Wimo).
 *
 * `effective` = `fromRoles ∪ grants − revokes`, tel que l'API le calcule pour
 * le garde et le menu. L'écran s'en sert pour dire l'ORIGINE de chaque case :
 * « par le rôle » ou « spécifique ».
 */
export interface UserPermissions {
  userId: Id;
  roles: UserRole[];
  /** Super-administrateur : ses droits ne se modifient pas compte par compte. */
  locked: boolean;
  permissions: Array<{ key: PermissionKey; module: string; label: string }>;
  fromRoles: PermissionKey[];
  grants: PermissionKey[];
  revokes: PermissionKey[];
  effective: PermissionKey[];
}

export interface AgencyListItem {
  id: Id;
  name: string;
  cityId: Id;
  cityName: string;
  phone: string | null;
  email: string | null;
  address: string | null;
  /** `null` = hérite de la société (D-019 : la portée agence surcharge). */
  currency: string | null;
  timezone: string | null;
  /**
   * Les villes qu'elle DESSERT, en plus de la sienne — retouches Anass, point 4.
   *
   * Vide = elle ne couvre que sa propre ville. Le rattachement d'un nouvel
   * e-commerçant s'y lit avant de retomber sur le siège.
   */
  coveredCityIds: Id[];
  isActive: boolean;
  /** La plus ancienne agence de la société : le « Siège », jamais supprimable. */
  isHeadquarters: boolean;
  userCount: number;
  parcelCount: number;
}

export interface ClientDiscountItem {
  id: Id;
  /** Vide, avec `cityIds` vide = remise applicable à toutes les zones. */
  zoneIds: Id[];
  /** Lot K (D-218) — villes de destination visées, avant les zones. */
  cityIds: Id[];
  /** La remise ÉCRASE le tarif, elle ne s'en soustrait pas (DOMAIN.md §3.1). */
  flatRate: AmountInCents | null;
  sameCityRate: AmountInCents | null;
  pickupFee: AmountInCents | null;
  isActive: boolean;
}

export interface ClientPickupAddressItem {
  id: Id;
  label: string;
  address: string;
  cityId: Id;
  cityName: string;
  districtId: Id | null;
  districtName: string | null;
  phone: string | null;
  isDefault: boolean;
}

export interface ClientDocumentItem {
  id: Id;
  name: string;
  kind: ClientDocumentKind;
  mimeType: string;
  sizeBytes: number;
  createdAt: IsoDateTime;
  uploadedByName: string | null;
}

export interface ClientListItem {
  id: Id;
  name: string;
  legalName: string | null;
  type: ClientType;
  /** ICE marocain. */
  taxId: string | null;
  phone: string;
  email: string;
  cityId: Id;
  cityName: string;
  agencyId: Id | null;
  agencyName: string | null;
  isActive: boolean;
  /** C11 bloc 3 — régime de facturation. `normal` pour tout l'existant. */
  billingType: BillingType;
  /** Une facture par MOIS plutôt qu'à la demande. Indépendant du régime. */
  monthlyBilling: boolean;
  /**
   * CLIENT PRO — chantier « Retours, BL Clients Pro », point 4.
   *
   * L'interrupteur du BL, et rien d'autre. Distinct de
   * `billingType: 'professional'`, qui décide de la facture (D-061) : un client
   * peut réclamer ses bons de livraison signés sans changer de régime.
   */
  isPro: boolean;
  /**
   * CLIENT INTERNE — un compte vendeur de la société elle-même. Comme un
   * Client Pro, exclu du report automatique des factures négatives (décision
   * d'Anass, 26/11/2026). Posé par l'administration seule.
   */
  isInternal: boolean;
  /**
   * Lot R (D-230) — taux de TVA du Client Pro, en centièmes (`2000` = 20 %,
   * `0` = exonéré). `null` = le défaut société. Sans effet si `isPro` est faux.
   */
  vatRate: number | null;
  /**
   * Lot T (D-232) — conditions de ramassage PROPRES au client : frais par bon
   * (centimes) et « gratuit à partir de » (colis). `null` = la valeur globale
   * de Paramètres > Finance ; `0` autorisé. Posées par l'administration.
   */
  pickupFee: AmountInCents | null;
  freePickupThreshold: number | null;
  /**
   * DOMAIN.md §1.5 — `validation_user` / `validation_date`. Un client saisi
   * n'est pas un client validé : l'ouverture du compte est un acte de gestion.
   */
  validatedAt: IsoDateTime | null;
  validatedByName: string | null;
  /**
   * C13 bloc 2 — la demande a été REFUSÉE, et par qui.
   *
   * La fiche survit au refus : c'est elle que l'agent retrouve quand la
   * personne rappelle, et le motif est ce qu'il lui dira. Exclusif de
   * `validatedAt` — on ne peut pas être les deux à la fois, mais on peut passer
   * de l'un à l'autre.
   */
  rejectedAt: IsoDateTime | null;
  rejectedByName: string | null;
  rejectionReason: string | null;
  /** Compte MERCHANT lié, quand il a été créé depuis la fiche. */
  userId: Id | null;
  parcelCount: number;
  createdAt: IsoDateTime;
}

export interface ClientDetail extends ClientListItem {
  address: string | null;
  notes: string | null;
  discounts: ClientDiscountItem[];
  pickupAddresses: ClientPickupAddressItem[];
  documents: ClientDocumentItem[];
}

/** Grille de paie par ville — DOMAIN.md §1.6, `livreur_villes`. */
export interface CourierCityRate {
  cityId: Id;
  cityName: string;
  pickupFee: AmountInCents;
  deliveryFee: AmountInCents;
}

export interface CourierListItem {
  id: Id;
  firstName: string;
  lastName: string;
  phone: string;
  cityId: Id;
  cityName: string;
  agencyId: Id | null;
  agencyName: string | null;
  vehicleType: VehicleType;
  payModel: PayModel;
  /** C11 D-058 — l'autre axe : quand la commission est réglée. */
  settlementMode: SettlementMode;
  payPeriod: PayPeriod;
  isActive: boolean;
  validatedAt: IsoDateTime | null;
  validatedByName: string | null;
  /**
   * C13 bloc 2 — la demande a été REFUSÉE, et par qui.
   *
   * La fiche survit au refus : c'est elle que l'agent retrouve quand la
   * personne rappelle, et le motif est ce qu'il lui dira. Exclusif de
   * `validatedAt` — on ne peut pas être les deux à la fois, mais on peut passer
   * de l'un à l'autre.
   */
  rejectedAt: IsoDateTime | null;
  rejectedByName: string | null;
  rejectionReason: string | null;
  userId: Id | null;
  parcelCount: number;
}

export interface CourierDetail extends CourierListItem {
  nationalId: string | null;
  bankName: string | null;
  rib: string | null;
  /** Part fixe mensuelle. Ne sert qu'aux modes `salary` et `hybrid` (D-020). */
  fixedSalary: AmountInCents | null;
  cities: CourierCityRate[];
}

/**
 * Réglages métier de Paramètres > Société — D-019.
 *
 * Ce sont des politiques COMMERCIALES : elles varient d'un client à l'autre et
 * d'une agence à l'autre, donc elles vivent en `Setting` et pas en constante.
 * `pricing.ts` les reçoit en paramètre ; il n'en connaît aucune par cœur.
 */
export interface BusinessSettings {
  /** Préfixe du code colis, devant le couple de villes (DOMAIN.md §3.2). */
  parcelCodePrefix: string;
  localesEnabled: string[];
  localesDefault: string;
  /**
   * Délai de traitement d'une réclamation, en jours (C7 bloc 2). `0` = pas de
   * délai, donc pas de badge « en retard » : tout le monde n'en veut pas, et
   * inventer une valeur ferait rougir un tableau de bord pour une politique
   * que personne n'a choisie (D-019).
   */
  claimResolutionDays: number;

  /**
   * L'ÉTIQUETTE — chantier « Étiquette orange », point 1.
   *
   * `labelTemplate` choisit le gabarit ; `labelBrandColor` vide retombe sur la
   * couleur de marque de la société ; `labelBanner` vide n'imprime aucun
   * bandeau — la phrase nomme la société, et un défaut l'aurait fait imprimer
   * sur les étiquettes de toutes les autres.
   */
  labelTemplate: LabelTemplate;
  labelBrandColor: string;
  labelBanner: string;
}
