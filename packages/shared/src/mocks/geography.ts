import type { Agency, City, District, Zone } from '../types/directory';

/**
 * Référentiel géographique mocké. Villes et quartiers réels du Maroc.
 * Aucune donnée issue du dump de production.
 */

export const mockZones: Zone[] = [
  {
    id: 'zone-1',
    name: 'Axe Casablanca – Rabat',
    baseRate: 2500,
    sameCityRate: 2000,
    deliveryDays: 1,
  },
  { id: 'zone-2', name: 'Nord', baseRate: 3500, sameCityRate: 2500, deliveryDays: 2 },
  { id: 'zone-3', name: 'Sud & Intérieur', baseRate: 4000, sameCityRate: 2500, deliveryDays: 3 },
];

export const mockCities: City[] = [
  { id: 'cas', name: 'Casablanca', nameAr: 'الدار البيضاء', code: 'CAS', zoneId: 'zone-1', isActive: true },
  { id: 'rab', name: 'Rabat', nameAr: 'الرباط', code: 'RAB', zoneId: 'zone-1', isActive: true },
  { id: 'sal', name: 'Salé', nameAr: 'سلا', code: 'SAL', zoneId: 'zone-1', isActive: true },
  { id: 'moh', name: 'Mohammedia', nameAr: 'المحمدية', code: 'MOH', zoneId: 'zone-1', isActive: true },
  { id: 'ken', name: 'Kénitra', nameAr: 'القنيطرة', code: 'KEN', zoneId: 'zone-1', isActive: true },
  { id: 'set', name: 'Settat', nameAr: 'سطات', code: 'SET', zoneId: 'zone-1', isActive: true },
  { id: 'elj', name: 'El Jadida', nameAr: 'الجديدة', code: 'ELJ', zoneId: 'zone-1', isActive: true },
  { id: 'kho', name: 'Khouribga', nameAr: 'خريبكة', code: 'KHO', zoneId: 'zone-1', isActive: true },
  { id: 'tng', name: 'Tanger', nameAr: 'طنجة', code: 'TNG', zoneId: 'zone-2', isActive: true },
  { id: 'tet', name: 'Tétouan', nameAr: 'تطوان', code: 'TET', zoneId: 'zone-2', isActive: true },
  { id: 'fes', name: 'Fès', nameAr: 'فاس', code: 'FES', zoneId: 'zone-2', isActive: true },
  { id: 'mek', name: 'Meknès', nameAr: 'مكناس', code: 'MEK', zoneId: 'zone-2', isActive: true },
  { id: 'ouj', name: 'Oujda', nameAr: 'وجدة', code: 'OUJ', zoneId: 'zone-2', isActive: true },
  { id: 'nad', name: 'Nador', nameAr: 'الناظور', code: 'NAD', zoneId: 'zone-2', isActive: true },
  { id: 'mar', name: 'Marrakech', nameAr: 'مراكش', code: 'MAR', zoneId: 'zone-3', isActive: true },
  { id: 'aga', name: 'Agadir', nameAr: 'أكادير', code: 'AGA', zoneId: 'zone-3', isActive: true },
  { id: 'saf', name: 'Safi', nameAr: 'آسفي', code: 'SAF', zoneId: 'zone-3', isActive: true },
  { id: 'bem', name: 'Béni Mellal', nameAr: 'بني ملال', code: 'BEM', zoneId: 'zone-3', isActive: true },
  { id: 'ess', name: 'Essaouira', nameAr: 'الصويرة', code: 'ESS', zoneId: 'zone-3', isActive: true },
  { id: 'laa', name: 'Laâyoune', nameAr: 'العيون', code: 'LAA', zoneId: 'zone-3', isActive: false },
];

const districtSeed: Record<string, string[]> = {
  cas: [
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
  rab: ['Agdal', 'Hassan', 'Souissi', 'Yacoub El Mansour', 'Hay Riad', "L'Océan", 'Akkari'],
  sal: ['Tabriquet', 'Hay Karima', 'Bettana', 'Sala Al Jadida'],
  moh: ['Centre-ville', 'Alia', 'Kasbah', 'Hassania'],
  ken: ['Mimosas', 'Bir Rami', 'Ouled Oujih', 'Val Fleuri'],
  set: ['Centre', 'Hay Salam', 'Al Massira'],
  elj: ['Cité Portugaise', 'Hay El Matar', 'Sidi Bouzid'],
  kho: ['Centre', 'Hay Nahda', 'Ouled Abdoune'],
  tng: ['Malabata', 'Iberia', 'Beni Makada', 'Marchan', 'Val Fleuri', 'Branes'],
  tet: ['Médina', 'Touilaâ', 'Sidi Talha', 'Al Wilaya'],
  fes: ['Ville Nouvelle', 'Fès el-Bali', 'Saiss', 'Narjiss', 'Agdal', 'Zouagha'],
  mek: ['Hamria', 'Médina', 'Marjane', 'Toulal'],
  ouj: ['Centre', 'Al Qods', 'Sidi Yahya', 'Lazaret'],
  nad: ['Centre', 'Ariad', 'Hay El Matar'],
  mar: ['Guéliz', 'Médina', 'Hivernage', 'Daoudiate', 'Sidi Youssef Ben Ali', "M'Hamid", 'Targa'],
  aga: ['Talborjt', 'Founty', 'Hay Mohammadi', 'Dakhla', 'Charaf'],
  saf: ['Centre', 'Jerifat', 'Sidi Bouzid'],
  bem: ['Centre', 'Hay El Qods', 'Ouled Hamdane'],
  ess: ['Médina', 'Borj', 'Azlef'],
  laa: ['Centre', 'Hay Essalam'],
};

export const mockDistricts: District[] = Object.entries(districtSeed).flatMap(([cityId, names]) =>
  names.map((name, index) => ({
    id: `${cityId}-d${index + 1}`,
    cityId,
    name,
  })),
);

export const mockAgencies: Agency[] = [
  {
    id: 'agc-cas',
    // Le « — Siège » que portait ce nom était doublement fautif : le statut de
    // siège est DÉDUIT (la plus ancienne agence, cf. `agencies.service.ts`) et
    // l'écran l'affiche déjà en badge, et le tiret cadratin ressortait en « ? »
    // une fois le seed passé par la console du VPS. Un nom de donnée reste en
    // ASCII ; la typographie est affaire d'interface.
    name: 'Agence Casablanca',
    cityId: 'cas',
    phone: '0522445566',
    address: 'Zone industrielle Sidi Bernoussi, Lot 42, Casablanca',
  },
  {
    id: 'agc-rab',
    name: 'Agence Rabat',
    cityId: 'rab',
    phone: '0537778899',
    address: "Avenue Hassan II, Quartier de l'Océan, Rabat",
  },
  {
    id: 'agc-mar',
    name: 'Agence Marrakech',
    cityId: 'mar',
    phone: '0524331122',
    address: 'Route de Safi, Quartier industriel Sidi Ghanem, Marrakech',
  },
  {
    id: 'agc-tng',
    name: 'Agence Tanger',
    cityId: 'tng',
    phone: '0539221144',
    address: 'Zone franche Gzenaya, Tanger',
  },
];

const cityById = new Map(mockCities.map((city) => [city.id, city]));
const agencyById = new Map(mockAgencies.map((agency) => [agency.id, agency]));
const districtById = new Map(mockDistricts.map((district) => [district.id, district]));

export function findCity(id: string): City | undefined {
  return cityById.get(id);
}

export function cityName(id: string): string {
  return cityById.get(id)?.name ?? id;
}

export function cityCode(id: string): string {
  return cityById.get(id)?.code ?? id.toUpperCase().slice(0, 3);
}

export function districtName(id: string | null): string | null {
  if (!id) return null;
  return districtById.get(id)?.name ?? null;
}

export function agencyName(id: string): string {
  return agencyById.get(id)?.name ?? id;
}

/** Agence de rattachement d'une ville, ou l'agence siège à défaut. */
export function agencyOfCity(cityId: string): Agency {
  return mockAgencies.find((agency) => agency.cityId === cityId) ?? (mockAgencies[0] as Agency);
}

export function districtsOfCity(cityId: string): District[] {
  return mockDistricts.filter((district) => district.cityId === cityId);
}
