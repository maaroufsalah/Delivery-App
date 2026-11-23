/**
 * Référentiel géographique du Maroc — jeu de RÉFÉRENCE, C8.
 *
 * **À quoi il sert.** Un tenant créé depuis la console plateforme démarre avec
 * un référentiel utilisable : vingt villes, leurs quartiers, trois zones
 * tarifaires. Sans lui, le premier écran d'un nouveau client serait une liste
 * vide, et sa première action serait de saisir Casablanca à la main.
 *
 * **Ce n'est PAS un mock.** `packages/shared/mocks` sert le jeu de
 * démonstration et n'est lu que par le seed ; ce fichier-ci est de la donnée
 * de PRODUIT, copiée dans chaque nouveau tenant à sa création. Les deux
 * partaient de la même liste, d'où la ressemblance — mais leurs cycles de vie
 * n'ont rien à voir, et les mélanger aurait fait dépendre l'onboarding d'un
 * jeu de démonstration.
 *
 * **Copié, jamais partagé.** Chaque tenant reçoit SES lignes, avec ses propres
 * identifiants : il les renomme, en désactive, en ajoute. Un référentiel
 * commun à toutes les sociétés serait une table sans `companyId`, c'est-à-dire
 * exactement ce que D-035 interdit.
 *
 * Aucune donnée du dump de production.
 */

export interface ReferenceZone {
  key: string;
  name: string;
  /** Tarif de base entre deux villes de la zone, en centimes. */
  baseRate: number;
  sameCityRate: number;
  deliveryDays: number;
}

export interface ReferenceCity {
  /** Code métier à trois lettres — il entre dans le code colis (§3.2). */
  code: string;
  name: string;
  zoneKey: string;
  isActive: boolean;
  districts: string[];
}

export const MOROCCO_ZONES: readonly ReferenceZone[] = [
  {
    key: 'z1',
    name: 'Axe Casablanca – Rabat',
    baseRate: 2500,
    sameCityRate: 2000,
    deliveryDays: 1,
  },
  { key: 'z2', name: 'Nord', baseRate: 3500, sameCityRate: 2500, deliveryDays: 2 },
  { key: 'z3', name: 'Sud & Intérieur', baseRate: 4000, sameCityRate: 2500, deliveryDays: 3 },
];

export const MOROCCO_CITIES: readonly ReferenceCity[] = [
  {
    code: 'CAS',
    name: 'Casablanca',
    zoneKey: 'z1',
    isActive: true,
    districts: [
      'Maârif',
      'Anfa',
      'Sidi Belyout',
      'Aïn Diab',
      'Hay Hassani',
      'Sidi Moumen',
      'Sidi Bernoussi',
      'Oulfa',
      'Derb Ghallef',
      'Aïn Sebaâ',
    ],
  },
  {
    code: 'RAB',
    name: 'Rabat',
    zoneKey: 'z1',
    isActive: true,
    districts: ['Agdal', 'Hassan', 'Souissi', 'Yacoub El Mansour', 'Hay Riad', "L'Océan", 'Akkari'],
  },
  {
    code: 'SAL',
    name: 'Salé',
    zoneKey: 'z1',
    isActive: true,
    districts: ['Tabriquet', 'Hay Karima', 'Bettana', 'Sala Al Jadida'],
  },
  {
    code: 'MOH',
    name: 'Mohammedia',
    zoneKey: 'z1',
    isActive: true,
    districts: ['Centre-ville', 'Alia', 'Kasbah', 'Hassania'],
  },
  {
    code: 'KEN',
    name: 'Kénitra',
    zoneKey: 'z1',
    isActive: true,
    districts: ['Mimosas', 'Bir Rami', 'Ouled Oujih', 'Val Fleuri'],
  },
  {
    code: 'SET',
    name: 'Settat',
    zoneKey: 'z1',
    isActive: true,
    districts: ['Centre', 'Hay Salam', 'Al Massira'],
  },
  {
    code: 'ELJ',
    name: 'El Jadida',
    zoneKey: 'z1',
    isActive: true,
    districts: ['Cité Portugaise', 'Hay El Matar', 'Sidi Bouzid'],
  },
  {
    code: 'KHO',
    name: 'Khouribga',
    zoneKey: 'z1',
    isActive: true,
    districts: ['Centre', 'Hay Nahda', 'Ouled Abdoune'],
  },
  {
    code: 'TNG',
    name: 'Tanger',
    zoneKey: 'z2',
    isActive: true,
    districts: ['Malabata', 'Iberia', 'Beni Makada', 'Marchan', 'Val Fleuri', 'Branes'],
  },
  {
    code: 'TET',
    name: 'Tétouan',
    zoneKey: 'z2',
    isActive: true,
    districts: ['Médina', 'Touilaâ', 'Sidi Talha', 'Al Wilaya'],
  },
  {
    code: 'FES',
    name: 'Fès',
    zoneKey: 'z2',
    isActive: true,
    districts: ['Ville Nouvelle', 'Fès el-Bali', 'Saiss', 'Narjiss', 'Agdal', 'Zouagha'],
  },
  {
    code: 'MEK',
    name: 'Meknès',
    zoneKey: 'z2',
    isActive: true,
    districts: ['Hamria', 'Médina', 'Marjane', 'Toulal'],
  },
  {
    code: 'OUJ',
    name: 'Oujda',
    zoneKey: 'z2',
    isActive: true,
    districts: ['Centre', 'Al Qods', 'Sidi Yahya', 'Lazaret'],
  },
  {
    code: 'NAD',
    name: 'Nador',
    zoneKey: 'z2',
    isActive: true,
    districts: ['Centre', 'Ariad', 'Hay El Matar'],
  },
  {
    code: 'MAR',
    name: 'Marrakech',
    zoneKey: 'z3',
    isActive: true,
    districts: [
      'Guéliz',
      'Médina',
      'Hivernage',
      'Daoudiate',
      'Sidi Youssef Ben Ali',
      "M'Hamid",
      'Targa',
    ],
  },
  {
    code: 'AGA',
    name: 'Agadir',
    zoneKey: 'z3',
    isActive: true,
    districts: ['Talborjt', 'Founty', 'Hay Mohammadi', 'Dakhla', 'Charaf'],
  },
  {
    code: 'SAF',
    name: 'Safi',
    zoneKey: 'z3',
    isActive: true,
    districts: ['Centre', 'Jerifat', 'Sidi Bouzid'],
  },
  {
    code: 'BEM',
    name: 'Béni Mellal',
    zoneKey: 'z3',
    isActive: true,
    districts: ['Centre', 'Hay El Qods', 'Ouled Hamdane'],
  },
  {
    code: 'ESS',
    name: 'Essaouira',
    zoneKey: 'z3',
    isActive: true,
    districts: ['Médina', 'Borj', 'Azlef'],
  },
  {
    code: 'LAA',
    name: 'Laâyoune',
    zoneKey: 'z3',
    isActive: false,
    districts: ['Centre', 'Hay Essalam'],
  },
];

/** Décompte, pour que l'onboarding puisse le rapporter sans recompter. */
export const MOROCCO_DISTRICT_COUNT = MOROCCO_CITIES.reduce(
  (total, city) => total + city.districts.length,
  0,
);
