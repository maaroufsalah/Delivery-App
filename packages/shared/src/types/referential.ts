import type { AmountInCents, Id, Paginated } from './common';

/**
 * Contrats du RÉFÉRENTIEL — les écrans d'administration de villes, zones,
 * quartiers, tarifs et types de livraison.
 *
 * Ils sont distincts des types de `directory.ts`, qui restent la forme LÉGÈRE
 * servie aux listes déroulantes de filtres. Une ville de filtre n'a pas besoin
 * de savoir combien de colis la référencent ; l'écran qui la supprime, si.
 *
 * Montants en centimes entiers (DOMAIN.md §4 dette n°2). Aucun flottant ne
 * traverse le réseau — le multiplicateur d'un type de livraison lui-même est
 * transporté en millièmes entiers.
 */

/**
 * Ce qui empêche une suppression, et en quelle quantité.
 *
 * DOMAIN.md §4 dette n°8 : le legacy supprimait en cascade — un quartier effacé
 * emportait l'adresse de livraison des colis qui le citaient. Ici la
 * suppression est refusée en 409 avec ce décompte, et l'écran propose la
 * désactivation à la place. Jamais de cascade silencieuse.
 */
export interface ReferenceCount {
  /** Clé de traduction : `references.parcels`, `references.districts`, … */
  entity: string;
  count: number;
}

export interface CityListItem {
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
  zoneName: string;
  isActive: boolean;
  districtCount: number;
  /** Colis référençant la ville, en origine ou en destination. */
  parcelCount: number;
  /**
   * Le code est entré dans des codes colis déjà émis : le modifier rendrait
   * l'historique illisible. Verrouillé dès le premier colis.
   */
  codeLocked: boolean;
}

export interface ZoneListItem {
  id: Id;
  name: string;
  /** Tarif inter-ville de repli, quand la paire n'est pas dans la matrice. */
  baseRate: AmountInCents;
  sameCityRate: AmountInCents;
  deliveryDays: number;
  isActive: boolean;
  /** Villes rattachées par le pivot `ZoneCity` (dette n°15). */
  cityIds: Id[];
  cityCount: number;
}

/** Tarif d'une zone vers une autre — la maille au-dessus de la paire de villes. */
export interface ZoneRateItem {
  id: Id;
  fromZoneId: Id;
  toZoneId: Id;
  rate: AmountInCents;
  deliveryDays: number;
  isActive: boolean;
}

export interface ZoneRateMatrix {
  zones: Array<{ id: Id; name: string }>;
  rates: ZoneRateItem[];
}

/** DOMAIN.md §1.7 — 10 164 lignes en prod : la maille de livraison réelle. */
export interface DistrictListItem {
  id: Id;
  cityId: Id;
  cityName: string;
  name: string;
  isActive: boolean;
  parcelCount: number;
}

export interface PricingRuleListItem {
  id: Id;
  originCityId: Id;
  originCityName: string;
  destinationCityId: Id;
  destinationCityName: string;
  rateDistrict: AmountInCents;
  rateAgency: AmountInCents;
  rateRelayPoint: AmountInCents;
  isEnabled: boolean;
}

/**
 * UNE ligne de la grille d'UNE origine — chantier « Tarifs par origine ».
 *
 * La ligne est une DESTINATION, tarifée ou non : la grille montre toutes les
 * villes, et celles sans règle affichent en gris ce que le devis leur
 * appliquera (le repli de zone, DOMAIN.md §3.1).
 */
export interface PricingOriginRow {
  cityId: Id;
  name: string;
  nameAr: string | null;
  code: string;
  zoneId: Id;
  zoneName: string;
  /** Tarif de la paire origine → destination. `null` = pas de règle. */
  rate: AmountInCents | null;
  /** Ce que le devis applique SANS règle : le tarif de zone, affiché en gris. */
  zoneRate: AmountInCents;
  /** Délai indicatif de la destination — il appartient à la ville, pas à la paire. */
  deliveryDelay: string | null;
}

export interface PricingOriginPage extends Paginated<PricingOriginRow> {
  origin: { id: Id; name: string; code: string };
  /** Destinations tarifées depuis cette origine, tous filtres ignorés. */
  priced: number;
}

/** Une ligne de fichier RECONNUE : sa ville existe, son tarif se lit. */
export interface PricingOriginImportRow {
  row: number;
  raw: string;
  cityId: Id;
  name: string;
  code: string;
  rate: AmountInCents | null;
  previousRate: AmountInCents | null;
  deliveryDelay: string | null;
  previousDelay: string | null;
  /** La ligne change quelque chose — tarif ou délai. */
  changed: boolean;
}

export interface PricingOriginImportPreview {
  origin: { id: Id; name: string; code: string };
  /** Lignes de données lues, en-tête exclue. */
  rows: number;
  recognized: PricingOriginImportRow[];
  /** Non reconnues : ville inconnue, doublon, tarif illisible. Jamais appliquées. */
  rejected: ImportRowError[];
  /** Lignes reconnues qui changent un tarif ou un délai. */
  changes: number;
}

export interface DeliveryTypeListItem {
  id: Id;
  name: string;
  /**
   * Multiplicateur appliqué au tarif, en MILLIÈMES entiers : 1000 = ×1,
   * 1500 = ×1,5. Entier pour la même raison que les montants — un
   * multiplicateur flottant sur un tarif produit des centimes fantômes.
   */
  multiplierPerMille: number;
  isActive: boolean;
  isDefault: boolean;
  parcelCount: number;
}

/**
 * Erreur d'import, rattachée à SA ligne.
 *
 * DOMAIN.md §4 dette n°12 : le legacy importait sans validation et échouait en
 * bloc. Un import de 400 lignes doit dire laquelle est fausse, et pourquoi.
 */
export interface ImportRowError {
  /** Numéro de ligne dans le fichier, en-tête comprise (1-based). */
  row: number;
  column?: string;
  /** Clé de traduction stable : `unknown-city`, `negative-amount`, … */
  code: string;
  message: string;
}
