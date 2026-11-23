import type { AmountInCents, IsoDateTime } from './common';

/**
 * Ce que l'application de l'E-COMMERÇANT consomme — M2.
 *
 * Comme pour le livreur (`courier-app.ts`), ces contrats sont **plus pauvres**
 * que ceux du back-office, et volontairement. Un e-commerçant n'a pas à voir la
 * trésorerie interne du transporteur : où en est le cash entre le livreur et
 * l'agence est une conversation qui ne le concerne pas (D-060).
 */

/**
 * LES SEAUX DE STATUT DU VENDEUR — V2 vendeur bloc 2.
 *
 * La spécification d'Anass demande huit compteurs — nouveaux, ramassés, en
 * transit, en livraison, livrés, retournés, annulés, et le total. Le
 * référentiel, lui, porte trente-huit statuts rangés en sept groupes
 * techniques (`initial`, `transit`, `pivot`, `retry`, `failure`, `return`,
 * `terminal`) qui ne répondent pas à la même question : `pivot` mélange
 * « ramassé » et « en cours de livraison », et `failure` mélange « annulé par
 * le vendeur » et « numéro erroné ».
 *
 * **La correspondance vit donc ICI, une fois.** Écrite dans le service, elle
 * aurait été recopiée dans l'écran mobile pour colorer les cartes, puis dans le
 * web, et les trois auraient divergé au premier statut ajouté. Écrite ici, elle
 * est testée, et le seul endroit à corriger.
 *
 * **Les identifiants sont ceux du legacy** (DOMAIN.md §2.3) : ils ne se
 * devinent pas, ils se citent.
 */
export const MERCHANT_PARCEL_BUCKETS = {
  /** Créé, pas encore pris en charge : en attente de ramassage, d'arrivée, planifié. */
  awaiting: [1, 2, 38],
  /** Le transporteur l'a — chez lui ou en entrepôt. */
  pickedUp: [3, 31],
  /** En mouvement entre deux points du réseau. */
  inTransit: [7, 12, 17],
  /** Sorti pour la tournée du jour, ou en attente de distribution. */
  outForDelivery: [13, 16],
  /** Remis au destinataire. */
  delivered: [4],
  /** Tout ce qui repart vers le vendeur, jusqu'à la remise effective. */
  returned: [5, 20, 21, 22, 23, 24],
  /** Annulé — par le vendeur ou par le client. Une décision, pas un échec. */
  cancelled: [19, 27],
} as const satisfies Record<string, readonly number[]>;

export type MerchantParcelBucket = keyof typeof MERCHANT_PARCEL_BUCKETS;

export const MERCHANT_PARCEL_BUCKET_KEYS = Object.keys(
  MERCHANT_PARCEL_BUCKETS,
) as MerchantParcelBucket[];

/** Combien de colis dans chaque seau. Les clés sont exhaustives, jamais partielles. */
export type MerchantParcelCounts = Record<MerchantParcelBucket, number>;

/** Ce qui vient de se passer, tel que l'accueil le raconte — spécification §2. */
export interface MerchantActivityItem {
  /** Le colis concerné, quand il y en a un. */
  parcelCode: string | null;
  /** Statut atteint — c'est lui qui donne le libellé et la couleur. */
  statusId: number;
  at: IsoDateTime;
}

/**
 * Les chiffres de l'accueil.
 *
 * **Quatre nombres à l'origine, et c'était juste** : un écran qu'on regarde
 * debout entre deux commandes répond à « qu'est-ce qui s'est passé aujourd'hui,
 * et combien me doit-on ».
 *
 * Le V2 en demande davantage (spécification §8 et §2), et c'est un autre
 * écran : une page de tableau de bord, pas un bandeau. Les quatre premiers
 * restent en tête — ils n'ont pas changé de sens — et le reste suit, groupé.
 */
export interface MerchantSummary {
  /** Colis créés depuis minuit. */
  createdToday: number;
  /** Colis passés en livré depuis minuit — daté par le STATUT, pas la création. */
  deliveredToday: number;
  /**
   * Colis encore en circulation, tous âges confondus.
   *
   * « En cours » se lit du point de vue du marchand : le colis n'est pas
   * arrivé à son terme. Un colis refusé ou en retour en fait donc partie —
   * il bouge encore, et son sort n'est pas scellé.
   */
  inTransit: number;
  /**
   * Ce que le transporteur doit à l'e-commerçant, en centimes.
   *
   * **Net, et selon son régime** (D-061) : un client `normal` reçoit son COD
   * moins les frais et leur TVA ; un client `professional` reçoit son COD
   * entier par bon de versement, ses frais étant facturés à part.
   *
   * **Ne dépend PAS de `reimbursedByCourier`** : voir la décision du bloc 1.
   * Ce chiffre dit ce qui est dû, pas où l'argent se trouve en interne.
   */
  codToReceive: AmountInCents;
  /** Le régime, parce que l'écran ne dit pas la même phrase dans les deux cas. */
  billingType: 'normal' | 'professional';
  /**
   * CLIENT PRO — point 4. C'est lui qui décide si l'espace vendeur POSE la
   * question « BL à récupérer lors de la livraison ? ». Un client normal ne
   * voit ni le champ, ni le suivi, ni rien du workflow.
   */
  isPro: boolean;

  /* --- V2 vendeur bloc 2 : le tableau de bord ----------------------------- */

  /** Tous ses colis, hors supprimés. Le dénominateur de tous les autres. */
  parcelsTotal: number;
  /** Un compteur par seau. Exhaustif : une clé absente serait un trou muet. */
  parcels: MerchantParcelCounts;
  /**
   * Factures ÉMISES et non réglées, et ce qu'elles totalisent.
   *
   * Le référentiel ne connaît que trois états — brouillon, émise, payée
   * (DOMAIN.md §1.16). « Partiellement payée » et « annulée », que la
   * spécification cite, n'existent pas en base : les inventer ici ferait
   * afficher un chiffre qu'aucune facture ne porte.
   */
  unpaidInvoices: number;
  /**
   * Ce que ces factures totalisent — `totalDue`, l'encaissé moins les frais
   * TTC. Signé en base ; l'écran n'en montre jamais le signe, comme pour
   * `codToReceive`.
   */
  unpaidAmount: AmountInCents;
  /** Réclamations ouvertes ou en cours — celles qui attendent une réponse. */
  openClaims: number;
  /** Lignes de stock sous le seuil de leur agence (`warehouse.lowStockThreshold`). */
  lowStockLines: number;
  /** Les dix derniers mouvements de ses colis, du plus récent au plus ancien. */
  activity: MerchantActivityItem[];
}

/**
 * LA FICHE DE SA SOCIÉTÉ — V2 vendeur bloc 9, spécification §7.
 *
 * « Modifier les informations de son entreprise, son adresse, sa ville. »
 * Jusqu'ici ces champs n'étaient éditables que par l'administration : un
 * vendeur qui déménageait devait téléphoner à son agence, et ses bons de
 * ramassage partaient à l'ancienne adresse en attendant.
 *
 * **Ce qui n'est PAS éditable, et pourquoi.** Le régime de facturation
 * (`billingType`), le compte bancaire et l'agence de rattachement restent à
 * l'administration : ce sont des termes commerciaux négociés, pas des
 * coordonnées. Les ouvrir permettrait à un vendeur de passer seul en
 * « professionnel » — donc de changer la façon dont son argent lui revient
 * (D-061).
 */
export interface MerchantCompany {
  name: string;
  phone: string;
  email: string;
  cityId: string;
  cityName: string;
  address: string | null;
  /**
   * CLIENT PRO — point 4 du chantier « Retours, BL Clients Pro ».
   *
   * EN LECTURE, comme `billingType` : c'est l'administration qui l'accorde. Le
   * vendeur le lit pour savoir si son formulaire colis pose la question
   * « BL à récupérer ? » ; il ne l'écrit pas, sans quoi n'importe quel client
   * s'ouvrirait le workflow tout seul.
   */
  isPro: boolean;
  /**
   * Lot V, V4 (D-234) — le régime de facturation, EN LECTURE comme `isPro`.
   * Seul un client `professional` reçoit des bons de versement (D-233) : la
   * coque vendeur ne montre « Mes versements » qu'à lui.
   */
  billingType: 'normal' | 'professional';
  /**
   * Lot T (D-232) — ses conditions de ramassage EFFECTIVES (celles de sa fiche,
   * sinon les globales), EN LECTURE : frais par bon en centimes, seuil de
   * gratuité en colis. Ni `updateCompany` ni aucune route vendeur ne les écrit.
   */
  pickupTerms: { fee: AmountInCents; freeFrom: number };
}
