import type { Client, Courier } from '../types/directory';

/** E-commerçants et livreurs mockés. Noms inventés, plausibles au Maroc. */

export const mockClients: Client[] = [
  {
    id: 'cli-1',
    name: 'Atlas Cosmetics',
    type: 'merchant',
    taxId: '002345678000019',
    phone: '0661234501',
    email: 'contact@atlascosmetics.ma',
    cityId: 'cas',
    agencyId: 'agc-cas',
    isActive: true,
    billingType: 'normal',
    isPro: false,
    createdAt: '2026-01-14T09:20:00+01:00',
  },
  {
    id: 'cli-2',
    name: 'Riad Décor',
    type: 'company',
    taxId: '001987654000032',
    phone: '0662234502',
    email: 'commandes@riaddecor.ma',
    cityId: 'mar',
    agencyId: 'agc-mar',
    isActive: true,
    billingType: 'professional',
    /*
     * LE CLIENT PRO du jeu de démonstration — chantier « Retours, BL Clients
     * Pro », point 4. Un seul, et c'est voulu : la règle « aucune question BL
     * pour un client normal » ne se vérifie qu'avec des clients normaux à côté.
     *
     * Riad Décor plutôt qu'un autre parce qu'elle vend du mobilier : c'est
     * exactement le commerce où le destinataire signe un bon à la réception.
     */
    isPro: true,
    createdAt: '2026-02-03T11:05:00+01:00',
  },
  {
    id: 'cli-3',
    name: 'Zellige Store',
    type: 'merchant',
    taxId: '003456789000044',
    phone: '0663234503',
    email: 'hello@zelligestore.ma',
    cityId: 'fes',
    agencyId: 'agc-cas',
    isActive: true,
    billingType: 'normal',
    isPro: false,
    createdAt: '2026-02-19T15:40:00+01:00',
  },
  {
    id: 'cli-4',
    name: 'Souk Digital',
    type: 'merchant',
    taxId: '004567891000057',
    phone: '0664234504',
    email: 'ops@soukdigital.ma',
    cityId: 'cas',
    agencyId: 'agc-cas',
    isActive: true,
    billingType: 'normal',
    isPro: false,
    createdAt: '2026-03-08T08:15:00+01:00',
  },
  {
    id: 'cli-5',
    name: 'Medina Sport',
    type: 'merchant',
    taxId: '005678912000061',
    phone: '0665234505',
    email: 'contact@medinasport.ma',
    cityId: 'rab',
    agencyId: 'agc-rab',
    isActive: true,
    billingType: 'normal',
    isPro: false,
    createdAt: '2026-04-01T10:00:00+01:00',
  },
  {
    id: 'cli-6',
    name: 'Kaftan House',
    type: 'individual',
    taxId: null,
    phone: '0666234506',
    email: 'kaftanhouse.ma@gmail.com',
    cityId: 'tng',
    agencyId: 'agc-tng',
    isActive: true,
    billingType: 'normal',
    isPro: false,
    createdAt: '2026-05-12T17:30:00+01:00',
  },
  {
    id: 'cli-7',
    name: 'Argan Bio Maroc',
    type: 'company',
    taxId: '006789123000078',
    phone: '0667234507',
    email: 'export@arganbio.ma',
    cityId: 'aga',
    agencyId: 'agc-cas',
    isActive: true,
    billingType: 'professional',
    isPro: false,
    createdAt: '2026-06-22T13:45:00+01:00',
  },
  {
    id: 'cli-8',
    name: 'TechSouss',
    type: 'merchant',
    taxId: '007891234000083',
    phone: '0668234508',
    email: 'sav@techsouss.ma',
    cityId: 'aga',
    agencyId: 'agc-cas',
    isActive: false,
    billingType: 'normal',
    isPro: false,
    createdAt: '2026-07-05T09:10:00+01:00',
  },
];

export const mockCouriers: Courier[] = [
  {
    id: 'crr-1',
    firstName: 'Youssef',
    lastName: 'Benali',
    phone: '0671000101',
    cityId: 'cas',
    vehicleType: 'motorbike',
    isActive: true,
  },
  {
    id: 'crr-2',
    firstName: 'Karim',
    lastName: 'El Idrissi',
    phone: '0671000102',
    cityId: 'cas',
    vehicleType: 'car',
    isActive: true,
  },
  {
    id: 'crr-3',
    firstName: 'Rachid',
    lastName: 'Amrani',
    phone: '0671000103',
    cityId: 'rab',
    vehicleType: 'motorbike',
    isActive: true,
  },
  {
    id: 'crr-4',
    firstName: 'Hamza',
    lastName: 'Bouzid',
    phone: '0671000104',
    cityId: 'mar',
    vehicleType: 'van',
    isActive: true,
  },
  {
    id: 'crr-5',
    firstName: 'Mehdi',
    lastName: 'Tazi',
    phone: '0671000105',
    cityId: 'tng',
    vehicleType: 'motorbike',
    isActive: true,
  },
  {
    id: 'crr-6',
    firstName: 'Omar',
    lastName: 'Lahlou',
    phone: '0671000106',
    cityId: 'aga',
    vehicleType: 'car',
    isActive: true,
  },
  {
    id: 'crr-7',
    firstName: 'Anas',
    lastName: 'Sbai',
    phone: '0671000107',
    cityId: 'fes',
    vehicleType: 'motorbike',
    isActive: false,
  },
];

const clientById = new Map(mockClients.map((client) => [client.id, client]));
const courierById = new Map(mockCouriers.map((courier) => [courier.id, courier]));

export function clientName(id: string): string {
  return clientById.get(id)?.name ?? id;
}

export function courierName(id: string | null): string | null {
  if (!id) return null;
  const courier = courierById.get(id);
  return courier ? `${courier.firstName} ${courier.lastName}` : null;
}
