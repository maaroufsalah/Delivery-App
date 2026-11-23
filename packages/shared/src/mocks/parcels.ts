import type { BillingState } from '../enums';
import { getParcelStatus } from '../statuses';
import type { Parcel, ParcelEvent, ParcelNote } from '../types/parcel';
import { buildParcelCode } from '../utils/parcel-code';
import { cityCode, districtsOfCity } from './geography';

/**
 * 30 colis mockés. Les chaînes de statuts suivent les transitions réellement
 * observées en production (DOMAIN.md §2.2) :
 *   - nominal même ville   : 2 → 3 → 16 → 13 → 4
 *   - nominal inter-ville  : 2 → 3 → 7 → 13 → 4
 *   - échec                : 13 → {6, 9, 32} → 5 → 23
 * Aucune donnée réelle du dump de production.
 */

interface ParcelSeed {
  clientId: string;
  origin: string;
  destination: string;
  /** Index du quartier dans la liste de la ville de destination. */
  districtIndex: number;
  /** Chaîne de statuts, du plus ancien au plus récent. Le dernier est le statut courant. */
  history: number[];
  courierId: string | null;
  /** Montant COD en dirhams. */
  cod: number;
  /** Frais de livraison en dirhams. */
  fee: number;
  /** Jour et mois de création (année 2026). */
  day: number;
  month: number;
  /** Rang du jour sur le couple de villes. */
  rank: number;
  recipientName: string;
  recipientPhone: string;
  description: string;
  billingState?: BillingState;
  fragile?: boolean;
  openOnDelivery?: boolean;
  externalCode?: string;
  pickupWaived?: boolean;
}

const seeds: ParcelSeed[] = [
  // ---- Livrés (9) -----------------------------------------------------------
  {
    clientId: 'cli-1',
    origin: 'cas',
    destination: 'cas',
    districtIndex: 0,
    history: [2, 3, 16, 13, 4],
    courierId: 'crr-1',
    cod: 349,
    fee: 20,
    day: 1,
    month: 9,
    rank: 1,
    recipientName: 'Salma Bennani',
    recipientPhone: '0661452210',
    description: 'Coffret soin visage argan',
    billingState: 'invoiced',
    externalCode: 'AC-10442',
  },
  {
    clientId: 'cli-4',
    origin: 'cas',
    destination: 'cas',
    districtIndex: 4,
    history: [2, 3, 16, 13, 4],
    courierId: 'crr-2',
    cod: 890,
    fee: 20,
    day: 1,
    month: 9,
    rank: 2,
    recipientName: 'Mehdi Ouazzani',
    recipientPhone: '0662117845',
    description: 'Casque audio sans fil',
    billingState: 'invoiced',
    fragile: true,
    externalCode: 'SD-2291',
  },
  {
    clientId: 'cli-5',
    origin: 'rab',
    destination: 'rab',
    districtIndex: 0,
    history: [2, 3, 16, 13, 4],
    courierId: 'crr-3',
    cod: 520,
    fee: 20,
    day: 2,
    month: 9,
    rank: 1,
    recipientName: 'Nabil Chraibi',
    recipientPhone: '0663998201',
    description: 'Paire de chaussures running',
    billingState: 'invoiced',
  },
  {
    clientId: 'cli-1',
    origin: 'cas',
    destination: 'rab',
    districtIndex: 3,
    history: [2, 3, 7, 13, 4],
    courierId: 'crr-3',
    cod: 275,
    fee: 25,
    day: 2,
    month: 9,
    rank: 2,
    recipientName: 'Imane Fassi',
    recipientPhone: '0664330112',
    description: 'Sérum hydratant x2',
    billingState: 'reimbursed',
  },
  {
    clientId: 'cli-2',
    origin: 'mar',
    destination: 'cas',
    districtIndex: 1,
    history: [2, 3, 7, 13, 4],
    courierId: 'crr-1',
    cod: 1450,
    fee: 40,
    day: 3,
    month: 9,
    rank: 1,
    recipientName: 'Hicham Berrada',
    recipientPhone: '0665220398',
    description: 'Lampe en laiton ciselé',
    billingState: 'invoiced',
    fragile: true,
  },
  {
    clientId: 'cli-3',
    origin: 'fes',
    destination: 'mek',
    districtIndex: 0,
    history: [2, 3, 7, 13, 4],
    courierId: 'crr-7',
    cod: 640,
    fee: 25,
    day: 3,
    month: 9,
    rank: 1,
    recipientName: 'Khadija Alaoui',
    recipientPhone: '0666771203',
    description: 'Service à thé zellige 6 pièces',
    billingState: 'invoiced',
    fragile: true,
  },
  {
    clientId: 'cli-7',
    origin: 'aga',
    destination: 'aga',
    districtIndex: 1,
    history: [2, 3, 16, 13, 4],
    courierId: 'crr-6',
    cod: 380,
    fee: 20,
    day: 4,
    month: 9,
    rank: 1,
    recipientName: 'Rachida Ait Ben Haddou',
    recipientPhone: '0667884501',
    description: "Huile d'argan alimentaire 500 ml",
    billingState: 'invoiced',
    pickupWaived: true,
  },
  {
    clientId: 'cli-6',
    origin: 'tng',
    destination: 'tet',
    districtIndex: 0,
    history: [2, 3, 7, 13, 4],
    courierId: 'crr-5',
    cod: 1200,
    fee: 25,
    day: 4,
    month: 9,
    rank: 1,
    recipientName: 'Amina Zerouali',
    recipientPhone: '0668112934',
    description: 'Caftan brodé sur mesure',
    billingState: 'to-invoice',
    openOnDelivery: true,
  },
  {
    clientId: 'cli-4',
    origin: 'cas',
    destination: 'moh',
    districtIndex: 1,
    history: [2, 3, 16, 13, 4],
    courierId: 'crr-2',
    cod: 199,
    fee: 20,
    day: 5,
    month: 9,
    rank: 1,
    recipientName: 'Ayoub Mansouri',
    recipientPhone: '0669445612',
    description: 'Chargeur rapide 65 W',
    billingState: 'to-invoice',
    externalCode: 'SD-2310',
  },

  // ---- En cours de livraison (3) -------------------------------------------
  {
    clientId: 'cli-1',
    origin: 'cas',
    destination: 'cas',
    districtIndex: 2,
    history: [2, 3, 16, 13],
    courierId: 'crr-1',
    cod: 430,
    fee: 20,
    day: 9,
    month: 9,
    rank: 1,
    recipientName: 'Sofia Lamrani',
    recipientPhone: '0661223344',
    description: 'Palette maquillage',
  },
  {
    clientId: 'cli-5',
    origin: 'rab',
    destination: 'sal',
    districtIndex: 0,
    history: [2, 3, 16, 13],
    courierId: 'crr-3',
    cod: 760,
    fee: 20,
    day: 9,
    month: 9,
    rank: 2,
    recipientName: 'Othmane Kabbaj',
    recipientPhone: '0662556677',
    description: 'Sac de sport + gourde',
  },
  {
    clientId: 'cli-2',
    origin: 'mar',
    destination: 'mar',
    districtIndex: 0,
    history: [2, 3, 16, 13],
    courierId: 'crr-4',
    cod: 2300,
    fee: 20,
    day: 10,
    month: 9,
    rank: 1,
    recipientName: 'Leila Sqalli',
    recipientPhone: '0663889900',
    description: 'Tapis berbère 2 × 3 m',
  },

  // ---- En attente de livraison (3) -----------------------------------------
  {
    clientId: 'cli-4',
    origin: 'cas',
    destination: 'cas',
    districtIndex: 6,
    history: [2, 3, 16],
    courierId: null,
    cod: 549,
    fee: 20,
    day: 10,
    month: 9,
    rank: 2,
    recipientName: 'Zakaria Idrissi',
    recipientPhone: '0664001122',
    description: 'Montre connectée',
    externalCode: 'SD-2338',
  },
  {
    clientId: 'cli-3',
    origin: 'fes',
    destination: 'fes',
    districtIndex: 3,
    history: [2, 3, 16],
    courierId: null,
    cod: 310,
    fee: 20,
    day: 10,
    month: 9,
    rank: 3,
    recipientName: 'Meryem Tahiri',
    recipientPhone: '0665334455',
    description: 'Plateau décoratif céramique',
    fragile: true,
  },
  {
    clientId: 'cli-7',
    origin: 'aga',
    destination: 'aga',
    districtIndex: 3,
    history: [2, 3, 16],
    courierId: null,
    cod: 720,
    fee: 20,
    day: 11,
    month: 9,
    rank: 1,
    recipientName: 'Jamal Ouhbi',
    recipientPhone: '0666667788',
    description: 'Coffret cosmétique bio',
    pickupWaived: true,
  },

  // ---- Ramassés (2) ---------------------------------------------------------
  {
    clientId: 'cli-1',
    origin: 'cas',
    destination: 'aga',
    districtIndex: 0,
    history: [2, 3],
    courierId: null,
    cod: 890,
    fee: 40,
    day: 11,
    month: 9,
    rank: 1,
    recipientName: 'Nadia Belkacem',
    recipientPhone: '0667990011',
    description: 'Lot de 3 crèmes solaires',
  },
  {
    clientId: 'cli-6',
    origin: 'tng',
    destination: 'cas',
    districtIndex: 1,
    history: [2, 3],
    courierId: null,
    cod: 1650,
    fee: 35,
    day: 11,
    month: 9,
    rank: 2,
    recipientName: 'Yasmine Haddad',
    recipientPhone: '0668223344',
    description: 'Takchita deux pièces',
    openOnDelivery: true,
  },

  // ---- Expédiés (2) ---------------------------------------------------------
  {
    clientId: 'cli-2',
    origin: 'mar',
    destination: 'tng',
    districtIndex: 2,
    history: [2, 3, 7],
    courierId: null,
    cod: 980,
    fee: 45,
    day: 8,
    month: 9,
    rank: 1,
    recipientName: 'Fouad Naciri',
    recipientPhone: '0669556677',
    description: 'Miroir artisanal encadré',
    fragile: true,
  },
  {
    clientId: 'cli-7',
    origin: 'aga',
    destination: 'rab',
    districtIndex: 4,
    history: [2, 3, 7],
    courierId: null,
    cod: 460,
    fee: 40,
    day: 8,
    month: 9,
    rank: 2,
    recipientName: 'Hanane Bouchra',
    recipientPhone: '0661778899',
    description: 'Savon noir + gant kessa',
  },

  // ---- Attente de ramassage / arrivée (3) -----------------------------------
  {
    clientId: 'cli-5',
    origin: 'rab',
    destination: 'ken',
    districtIndex: 0,
    history: [1],
    courierId: null,
    cod: 640,
    fee: 25,
    day: 11,
    month: 9,
    rank: 3,
    recipientName: 'Ismail Doukkali',
    recipientPhone: '0662334455',
    description: 'Ballon + maillot officiel',
  },
  {
    clientId: 'cli-4',
    origin: 'cas',
    destination: 'set',
    districtIndex: 1,
    history: [2],
    courierId: null,
    cod: 280,
    fee: 25,
    day: 11,
    month: 9,
    rank: 4,
    recipientName: 'Mounir Saidi',
    recipientPhone: '0663445566',
    description: 'Clavier mécanique compact',
    externalCode: 'SD-2351',
  },
  {
    clientId: 'cli-3',
    origin: 'fes',
    destination: 'ouj',
    districtIndex: 0,
    history: [2],
    courierId: null,
    cod: 1100,
    fee: 45,
    day: 11,
    month: 9,
    rank: 1,
    recipientName: 'Widad Benjelloun',
    recipientPhone: '0664556677',
    description: 'Vasque en zellige émaillé',
    fragile: true,
  },

  // ---- Refusés (2) ----------------------------------------------------------
  {
    clientId: 'cli-1',
    origin: 'cas',
    destination: 'cas',
    districtIndex: 8,
    history: [2, 3, 16, 13, 6],
    courierId: 'crr-1',
    cod: 415,
    fee: 20,
    day: 28,
    month: 8,
    rank: 3,
    recipientName: 'Anouar Rifai',
    recipientPhone: '0665667788',
    description: 'Parfum 100 ml',
    billingState: 'to-invoice',
  },
  {
    clientId: 'cli-4',
    origin: 'cas',
    destination: 'rab',
    districtIndex: 2,
    history: [2, 3, 16, 13, 6],
    courierId: 'crr-3',
    cod: 1890,
    fee: 25,
    day: 29,
    month: 8,
    rank: 1,
    recipientName: 'Soukaina Bahri',
    recipientPhone: '0666778899',
    description: 'Tablette 11 pouces',
    billingState: 'to-invoice',
    externalCode: 'SD-2204',
  },

  // ---- Retournés (2) --------------------------------------------------------
  {
    clientId: 'cli-2',
    origin: 'mar',
    destination: 'aga',
    districtIndex: 0,
    history: [2, 3, 7, 13, 6, 5],
    courierId: 'crr-6',
    cod: 760,
    fee: 40,
    day: 24,
    month: 8,
    rank: 1,
    recipientName: 'Brahim Ouzzine',
    recipientPhone: '0667889900',
    description: 'Pouf en cuir tanné',
    billingState: 'invoiced',
  },
  {
    clientId: 'cli-6',
    origin: 'tng',
    destination: 'tng',
    districtIndex: 2,
    history: [2, 3, 16, 13, 9, 5],
    courierId: 'crr-5',
    cod: 540,
    fee: 20,
    day: 25,
    month: 8,
    rank: 1,
    recipientName: 'Ilham Kettani',
    recipientPhone: '0668990011',
    description: 'Djellaba homme',
    billingState: 'to-invoice',
  },

  // ---- Anomalies en cours (3) -----------------------------------------------
  {
    clientId: 'cli-5',
    origin: 'rab',
    destination: 'rab',
    districtIndex: 4,
    history: [2, 3, 16, 13, 9],
    courierId: 'crr-3',
    cod: 330,
    fee: 20,
    day: 7,
    month: 9,
    rank: 1,
    recipientName: 'Adil Hakimi',
    recipientPhone: '0669001122',
    description: 'Corde à sauter + tapis',
  },
  {
    clientId: 'cli-1',
    origin: 'cas',
    destination: 'cas',
    districtIndex: 5,
    history: [2, 3, 16, 13, 11, 32],
    courierId: 'crr-2',
    cod: 620,
    fee: 20,
    day: 6,
    month: 9,
    rank: 2,
    recipientName: 'Ghita Slaoui',
    recipientPhone: '0661112233',
    description: 'Trousse de soin cheveux',
  },
  {
    clientId: 'cli-3',
    origin: 'fes',
    destination: 'cas',
    districtIndex: 3,
    history: [2, 3, 7, 13, 14],
    courierId: 'crr-1',
    cod: 1340,
    fee: 35,
    day: 5,
    month: 9,
    rank: 3,
    recipientName: 'Reda Benchekroun',
    recipientPhone: '0662223344',
    description: 'Table basse mosaïque',
    fragile: true,
  },

  // ---- Retour clôturé (1) ---------------------------------------------------
  {
    clientId: 'cli-8',
    origin: 'aga',
    destination: 'mar',
    districtIndex: 5,
    history: [2, 3, 7, 13, 6, 20, 22, 23],
    courierId: 'crr-4',
    cod: 2150,
    fee: 40,
    day: 12,
    month: 8,
    rank: 1,
    recipientName: 'Samir Ghazi',
    recipientPhone: '0663334455',
    description: 'Ordinateur portable reconditionné',
    billingState: 'reimbursed',
  },
];

const HOUR = 60 * 60 * 1000;

/** Décalage horaire du Maroc utilisé pour les mocks (UTC+1). */
function isoAt(date: Date): string {
  const shifted = new Date(date.getTime() + HOUR);
  return `${shifted.toISOString().slice(0, 19)}+01:00`;
}

/** Commentaire type attaché à certains statuts, pour donner du corps à la timeline. */
const EVENT_COMMENTS: Record<number, string> = {
  6: 'Le destinataire refuse le colis à l’ouverture.',
  9: 'Trois tentatives d’appel, aucune réponse.',
  11: 'Premier appel sans réponse, nouvelle tentative demain.',
  14: 'Le destinataire demande une livraison en fin de semaine.',
  32: 'Deuxième appel sans réponse.',
  5: 'Colis rendu à l’expéditeur.',
  23: 'Retour remis au client, bon signé.',
};

function buildParcel(seed: ParcelSeed, index: number): { parcel: Parcel; events: ParcelEvent[] } {
  const id = `prc-${String(index + 1).padStart(3, '0')}`;
  const createdDate = new Date(Date.UTC(2026, seed.month - 1, seed.day, 8, 30));
  const districts = districtsOfCity(seed.destination);
  const district = districts[seed.districtIndex % Math.max(districts.length, 1)] ?? null;

  const code = buildParcelCode({
    originCode: cityCode(seed.origin),
    destinationCode: cityCode(seed.destination),
    rank: seed.rank,
    date: createdDate,
  });

  // Un événement toutes les ~9 h à partir de la création.
  const events: ParcelEvent[] = seed.history.map((statusId, step) => ({
    id: `${id}-e${step + 1}`,
    parcelId: id,
    statusId,
    comment: EVENT_COMMENTS[statusId] ?? null,
    authorName: step === 0 ? 'Système' : step === 1 ? 'Agent agence' : 'Livreur',
    cityId: step < 2 ? seed.origin : seed.destination,
    // Preuve de livraison sur les statuts qui en produisent une en tournée.
    attachmentName: statusId === 4 ? 'preuve-livraison.jpg' : statusId === 6 ? 'refus.jpg' : null,
    scheduledDate: statusId === 14 ? isoAt(new Date(createdDate.getTime() + 96 * HOUR)) : null,
    createdAt: isoAt(new Date(createdDate.getTime() + step * 9 * HOUR)),
  }));

  const lastEvent = events[events.length - 1];
  const currentStatusId = seed.history[seed.history.length - 1] ?? 1;
  const status = getParcelStatus(currentStatusId);
  const isFailed = status.group === 'failure' || status.group === 'retry';

  const parcel: Parcel = {
    id,
    code,
    externalCode: seed.externalCode ?? null,
    clientId: seed.clientId,
    courierId: seed.courierId,
    agencyId: `agc-${seed.origin === 'rab' || seed.origin === 'sal' ? 'rab' : seed.origin === 'mar' ? 'mar' : seed.origin === 'tng' ? 'tng' : 'cas'}`,
    statusId: currentStatusId,
    statusAt: lastEvent?.createdAt ?? isoAt(createdDate),
    billingState: seed.billingState ?? 'to-invoice',
    originCityId: seed.origin,
    destinationCityId: seed.destination,
    destinationDistrictId: district?.id ?? null,
    deliveryTarget: 'district',
    currentCityId: seed.history.length > 2 ? seed.destination : seed.origin,
    description: seed.description,
    recipientName: seed.recipientName,
    recipientPhone: seed.recipientPhone,
    pickupAddress: `Entrepôt ${seed.clientId.toUpperCase()}, ${cityCode(seed.origin)}`,
    deliveryAddress: district
      ? `${district.name}, rue 12 n°${20 + index}`
      : `Centre-ville, n°${index}`,
    codAmount: seed.cod * 100,
    deliveryFee: seed.fee * 100,
    failureFee: isFailed || status.id === 5 ? 1000 : null,
    // Aucun autre frais dans le jeu de démonstration : ils se saisissent à la
    // main, colis par colis, et rien ne les rendrait crédibles en masse.
    otherFees: 0,
    feeIncluded: true,
    alreadyPaid: false,
    pickupWaived: seed.pickupWaived ?? false,
    // Le jeu de démonstration ne contient que des colis ordinaires : un retour
    // et un échange naissent d'un GESTE sur un colis livré, et les fabriquer
    // au seed donnerait des dossiers sans histoire.
    kind: 'parcel',
    isReturn: false,
    isFailed,
    referenceParcelId: null,
    openOnDelivery: seed.openOnDelivery ?? false,
    fragile: seed.fragile ?? false,
    /*
     * « BL à récupérer » — chantier « Retours, BL Clients Pro », point 5.
     *
     * Vrai pour les colis du seul CLIENT PRO du jeu de démonstration, et pour
     * eux seuls : c'est ce qui rend la démonstration jouable — un livreur y
     * trouve des bandeaux « BL à récupérer », et les autres vendeurs n'ont
     * aucune question de ce genre sur leurs colis (D-099).
     */
    blRequested: seed.clientId === 'cli-2',
    // Point 3 : le client pro de démonstration paie en espèces par défaut.
    expectedPayment: seed.clientId === 'cli-2' ? 'cash' : null,
    scheduledAt: currentStatusId === 14 ? isoAt(new Date(createdDate.getTime() + 96 * HOUR)) : null,
    createdAt: isoAt(createdDate),
    updatedAt: lastEvent?.createdAt ?? isoAt(createdDate),
  };

  return { parcel, events };
}

/**
 * Jour de référence des mocks. Figé : le rendu du tableau de bord doit être
 * reproductible d'un build à l'autre, ce qu'un `new Date()` interdirait.
 */
export const MOCK_TODAY = '2026-09-11';

/** Générateur pseudo-aléatoire déterministe (mulberry32), semé une fois. */
function seededRandom(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = Math.imul(state ^ (state >>> 15), 1 | state);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/**
 * Volume complémentaire réparti sur les 14 derniers jours : sans lui, les
 * graphiques du tableau de bord n'ont pas assez de points pour être lisibles.
 * Les chaînes de statuts restent celles de DOMAIN.md §2.2.
 */
const CHAINS: number[][] = [
  [2, 3, 16, 13, 4],
  [2, 3, 16, 13, 4],
  [2, 3, 7, 13, 4],
  [2, 3, 16, 13],
  [2, 3, 16],
  [2, 3, 7],
  [2, 3],
  [2],
  [1],
  [2, 3, 16, 13, 6],
  [2, 3, 16, 13, 9],
  [2, 3, 16, 13, 11, 32, 33],
  [2, 3, 7, 13, 14],
  [2, 3, 16, 13, 11, 34, 32],
  // Parcours de retour C3 : à retourner, remis en agence, rendu au client.
  // Trois chaînes plutôt qu'une, pour que les écrans Retours aient de quoi
  // travailler dès le seed — sans elles, `/returns/pending` serait vide.
  [2, 3, 16, 13, 6, 20],
  [2, 3, 16, 13, 9, 20],
  [2, 3, 7, 13, 6, 20, 22],
];

const RECIPIENTS: Array<[string, string]> = [
  ['Yassine Amrani', '0661009911'],
  ['Loubna Cherkaoui', '0662009912'],
  ['Hamid Ouali', '0663009913'],
  ['Siham Bennis', '0664009914'],
  ['Tarik Moussaoui', '0665009915'],
  ['Naima El Ghazi', '0666009916'],
  ['Abdellah Sekkat', '0667009917'],
  ['Fatima Zahra Radi', '0668009918'],
  ['Driss Bekkali', '0669009919'],
  ['Hafsa Loudiyi', '0661009920'],
  ['Younes Filali', '0662009921'],
  ['Btissam Marzouki', '0663009922'],
];

const ITEMS = [
  'Coffret parfum 50 ml',
  'Baskets running taille 42',
  'Théière en inox gravé',
  'Coussin brodé fait main',
  'Batterie externe 20 000 mAh',
  'Crème de nuit à l’argan',
  'Plaid en laine',
  'Enceinte bluetooth',
  'Sac cabas en cuir',
  'Set de 4 verres à thé',
  'Tapis de yoga',
  'Montre bracelet cuir',
];

const ROUTES: Array<[string, string]> = [
  ['cas', 'cas'],
  ['cas', 'rab'],
  ['cas', 'moh'],
  ['cas', 'mar'],
  ['rab', 'rab'],
  ['rab', 'sal'],
  ['rab', 'ken'],
  ['mar', 'mar'],
  ['mar', 'aga'],
  ['tng', 'tng'],
  ['tng', 'tet'],
  ['fes', 'fes'],
  ['fes', 'mek'],
  ['aga', 'aga'],
  ['cas', 'set'],
  ['cas', 'elj'],
];

const CLIENT_IDS = ['cli-1', 'cli-2', 'cli-3', 'cli-4', 'cli-5', 'cli-6', 'cli-7'];
const COURIER_BY_CITY: Record<string, string> = {
  cas: 'crr-1',
  rab: 'crr-3',
  sal: 'crr-3',
  ken: 'crr-3',
  moh: 'crr-2',
  mar: 'crr-4',
  aga: 'crr-6',
  tng: 'crr-5',
  tet: 'crr-5',
  fes: 'crr-7',
  mek: 'crr-7',
};

function pick<T>(list: readonly T[], random: () => number): T {
  return list[Math.floor(random() * list.length)] as T;
}

function generatedSeeds(): ParcelSeed[] {
  const random = seededRandom(20260911);
  const rankByKey = new Map<string, number>();
  const output: ParcelSeed[] = [];

  // 14 jours glissants, 3 à 5 colis par jour.
  for (let dayOffset = 13; dayOffset >= 0; dayOffset -= 1) {
    const date = new Date(Date.UTC(2026, 8, 11 - dayOffset));
    const count = 3 + Math.floor(random() * 3);

    for (let index = 0; index < count; index += 1) {
      const [origin, destination] = pick(ROUTES, random);
      const key = `${origin}-${destination}-${date.getUTCDate()}`;
      const rank = (rankByKey.get(key) ?? 10) + 1;
      rankByKey.set(key, rank);

      // Un colis récent n'a pas eu le temps d'aller au bout de sa chaîne.
      const chain = pick(CHAINS, random);
      const history = dayOffset < 2 ? chain.slice(0, Math.min(chain.length, 3)) : chain;
      const [recipientName, recipientPhone] = pick(RECIPIENTS, random);
      const sameCity = origin === destination;

      output.push({
        clientId: pick(CLIENT_IDS, random),
        origin,
        destination,
        districtIndex: Math.floor(random() * 6),
        history,
        courierId: history.length >= 4 ? (COURIER_BY_CITY[destination] ?? null) : null,
        cod: 120 + Math.floor(random() * 24) * 50,
        fee: sameCity ? 20 : 25 + Math.floor(random() * 3) * 5,
        day: date.getUTCDate(),
        month: date.getUTCMonth() + 1,
        rank,
        recipientName,
        recipientPhone,
        description: pick(ITEMS, random),
      });
    }
  }

  return output;
}

const built = [...seeds, ...generatedSeeds()].map(buildParcel);

export const mockParcels: Parcel[] = built.map((entry) => entry.parcel);
export const mockParcelEvents: ParcelEvent[] = built.flatMap((entry) => entry.events);

/**
 * Notes internes — DOMAIN.md §1.11, la moitié « commentaire libre » de
 * `colis_trakings`. Elles ne sortent jamais vers le suivi public.
 */
export const mockParcelNotes: ParcelNote[] = [
  {
    id: 'nte-1',
    parcelId: 'prc-025',
    body: 'Client rappelé : il confirme être absent jusqu’à jeudi. Replanifier la tournée.',
    authorName: 'Nawal (service client)',
    attachmentName: null,
    createdAt: '2026-09-07T14:20:00+01:00',
  },
  {
    id: 'nte-2',
    parcelId: 'prc-025',
    body: 'Adresse incomplète sur le bon : numéro de rue manquant, obtenu par téléphone.',
    authorName: 'Agent agence Rabat',
    attachmentName: null,
    createdAt: '2026-09-08T09:05:00+01:00',
  },
  {
    id: 'nte-3',
    parcelId: 'prc-026',
    body: 'Deuxième relance sans réponse. Prévenir l’expéditeur avant la troisième.',
    authorName: 'Nawal (service client)',
    attachmentName: null,
    createdAt: '2026-09-06T16:40:00+01:00',
  },
  {
    id: 'nte-4',
    parcelId: 'prc-024',
    body: 'Colis refusé : emballage abîmé au transport. Photo jointe au dossier litige.',
    authorName: 'Karim (qualité)',
    attachmentName: 'emballage-abime.jpg',
    createdAt: '2026-08-29T11:15:00+01:00',
  },
  {
    id: 'nte-5',
    parcelId: 'prc-027',
    body: 'Le destinataire demande une livraison après 18 h.',
    authorName: 'Livreur Rachid',
    attachmentName: null,
    createdAt: '2026-09-07T18:02:00+01:00',
  },
  {
    id: 'nte-6',
    parcelId: 'prc-001',
    body: 'Livraison conforme, COD encaissé en espèces.',
    authorName: 'Livreur Youssef',
    attachmentName: null,
    createdAt: '2026-09-01T17:30:00+01:00',
  },
];
