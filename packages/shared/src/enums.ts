/**
 * Enums applicatifs. Tous issus de DOMAIN.md — les tables legacy correspondantes
 * étaient soit vides (`roles`), soit dénormalisées en colonnes texte.
 */

/**
 * DOMAIN.md §1.4 — le label de la table legacy `roles` est inversé par rapport
 * à l'usage.
 *
 * `PLATFORM_ADMIN` est d'une autre nature que les quatre autres (C8, D-048) :
 * il n'appartient à AUCUN tenant. Il administre la plateforme — créer un
 * client, prolonger un essai, suspendre — et ne touche jamais la donnée métier
 * d'une société autrement qu'en s'y connectant explicitement, ce qui est tracé.
 */
export const USER_ROLES = [
  'PLATFORM_ADMIN',
  'SUPER_ADMIN',
  'STAFF',
  'MERCHANT',
  'COURIER',
] as const;
export type UserRole = (typeof USER_ROLES)[number];

/** Les rôles qui vivent DANS un tenant. `PLATFORM_ADMIN` en est exclu. */
export const TENANT_ROLES = ['SUPER_ADMIN', 'STAFF', 'MERCHANT', 'COURIER'] as const;
export type TenantRole = (typeof TENANT_ROLES)[number];

/** Cycle de vie d'un tenant — C8. `SUSPENDED` ferme l'accès, ne supprime rien. */
export const COMPANY_STATUSES = ['trial', 'active', 'suspended'] as const;
export type CompanyStatus = (typeof COMPANY_STATUSES)[number];

/** DOMAIN.md §1.5 — `clients.type`, 3 valeurs observées en prod. */
export const CLIENT_TYPES = ['individual', 'merchant', 'company'] as const;
export type ClientType = (typeof CLIENT_TYPES)[number];

/** DOMAIN.md §1.6 — `vehicule_types`, 3 lignes. */
export const VEHICLE_TYPES = ['motorbike', 'car', 'van'] as const;
export type VehicleType = (typeof VEHICLE_TYPES)[number];

/** DOMAIN.md §1.9 — `colis.etat_id`, bien écrit en prod (1 764 colis). */
export const BILLING_STATES = ['to-invoice', 'invoiced', 'reimbursed'] as const;
export type BillingState = (typeof BILLING_STATES)[number];

/** DOMAIN.md §1.13 — `boxe_statuts` filtrés sur `type='circuit'`. */
export const ROUTE_STATUSES = [
  'draft',
  'dispatched',
  'delivering',
  'collecting',
  'closed',
] as const;
export type RouteStatus = (typeof ROUTE_STATUSES)[number];

/**
 * LA NATURE D'UN COLIS — chantier « Étiquette orange », point 2.
 *
 * « Liste Colis : les deux nouveaux colis avec leur code + filtre Type (colis /
 * retour / échange). »
 *
 * Trois natures et pas un booléen : `parcel` est ce qu'on expédie, `return` ce
 * qui revient au vendeur, `exchange` ce qui repart à sa place. Les deux
 * derniers naissent ensemble, d'un même geste sur un colis LIVRÉ.
 */
/**
 * LE GABARIT D'ÉTIQUETTE — chantier « Étiquette orange », point 1.
 *
 * `classic` est celui d'avant, et il reste le DÉFAUT : c'est ce qui garantit la
 * dernière phrase de la mission, « wimo garde son rendu actuel ». `branded`
 * est le gabarit du modèle de référence, peint aux couleurs du tenant.
 */
export const LABEL_TEMPLATES = ['classic', 'branded'] as const;
export type LabelTemplate = (typeof LABEL_TEMPLATES)[number];

export const PARCEL_KINDS = ['parcel', 'return', 'exchange'] as const;
export type ParcelKind = (typeof PARCEL_KINDS)[number];

/** DOMAIN.md §1.13 — `boxes.type_circuit`. */
export const ROUTE_KINDS = ['delivery', 'pickup', 'return'] as const;
export type RouteKind = (typeof ROUTE_KINDS)[number];

/** DOMAIN.md §1.12 — `bon_livraisons.type` discrimine deux objets métier distincts. */
export const NOTE_KINDS = ['pickup', 'dropoff'] as const;
export type NoteKind = (typeof NOTE_KINDS)[number];

/**
 * DOMAIN.md §1.12 — `bon_livraison_statuts`, 2 valeurs, **puis 3**.
 *
 * `picked-up-by-courier` s'intercale — retouches Anass, point 5. Le bon suit
 * désormais la marchandise en deux temps : le livreur atteste l'avoir prise
 * chez le vendeur, l'agence atteste l'avoir reçue à l'entrepôt. Le legacy
 * confondait les deux, et un bon « validé » ne disait pas où était le carton.
 *
 * L'ordre de la liste est celui du parcours, et c'est ce que lisent les filtres.
 */
export const NOTE_STATUSES = ['awaiting-validation', 'picked-up-by-courier', 'validated'] as const;
export type NoteStatus = (typeof NOTE_STATUSES)[number];

/**
 * LE BON DE RETOUR COLIS — chantier « Retours, BL Clients Pro », point 1.
 *
 * DOMAIN.md §1.14, pivot legacy `bon_retour_livreur_colis` (811 lignes). Le
 * livreur rapporte à l'entrepôt les colis qu'il n'a pas pu livrer ; l'admin en
 * confirme la réception, puis clôture.
 *
 * **Deux états sont devenus sept, et l'ordre de la liste est le parcours.**
 * `draft` et `confirmed` existaient déjà et ne changent pas de sens — c'est ce
 * qui permet aux remises en production de survivre à ce chantier sans
 * conversion. Les cinq autres nomment des moments qui se passaient jusqu'ici
 * hors de l'application :
 *
 * | État | Qui le pose | Ce qu'il dit |
 * |---|---|---|
 * | `draft` | celui qui compose | le bon se remplit encore |
 * | `return-requested` | l'admin | « rapportez-moi ces colis » — origine ADMIN seulement |
 * | `picked-up` | le livreur | il a la marchandise en main |
 * | `awaiting-receipt` | le livreur | il l'a déposée à l'entrepôt |
 * | `confirmed` | l'admin | il l'a vue arriver — les colis passent à `22` |
 * | `closed` | l'admin | l'affaire est classée |
 * | `cancelled` | l'un ou l'autre | avant réception, et jamais après |
 *
 * La séparation `picked-up` / `awaiting-receipt` est la même que celle du
 * ramassage (D-094) : le livreur dit « je l'ai », l'entrepôt dit « je l'ai
 * reçue ». Les confondre rendait invisible un carton perdu entre les deux.
 */
export const RETURN_HANDOVER_STATUSES = [
  'draft',
  'return-requested',
  'picked-up',
  'awaiting-receipt',
  'confirmed',
  'closed',
  'cancelled',
] as const;
export type ReturnHandoverStatus = (typeof RETURN_HANDOVER_STATUSES)[number];

/**
 * QUI a ouvert le bon — point 1 du chantier.
 *
 * Ce n'est pas un détail d'affichage : l'origine décide de trois règles. Un bon
 * d'origine ADMIN naît en « retour demandé » et porte la mention « Créé par
 * l'admin » chez le livreur ; un bon d'origine LIVREUR naît en brouillon ; et le
 * livreur ne recompose un bon de l'admin que si celui-ci l'a autorisé
 * (`courierCanEdit`).
 */
export const RETURN_HANDOVER_ORIGINS = ['admin', 'courier'] as const;
export type ReturnHandoverOrigin = (typeof RETURN_HANDOVER_ORIGINS)[number];

/**
 * LES ÉTATS OÙ LE BON SE COMPOSE ENCORE.
 *
 * Au-delà, la marchandise est à l'entrepôt ou l'affaire est close : ajouter une
 * ligne reviendrait à dire qu'un colis est arrivé sans que personne l'ait vu.
 */
export const EDITABLE_RETURN_HANDOVER_STATUSES: readonly ReturnHandoverStatus[] = [
  'draft',
  'return-requested',
  'picked-up',
];

/** Le bon se compose-t-il encore ? */
export function isEditableReturnHandover(status: ReturnHandoverStatus): boolean {
  return EDITABLE_RETURN_HANDOVER_STATUSES.includes(status);
}

/**
 * LES ÉTATS QUI RÉSERVENT LE COLIS.
 *
 * « Un colis non présent dans un bon de retour actif » : tout sauf `cancelled`.
 * Un bon ANNULÉ relâche ses colis — c'est sa raison d'être. Un bon CLÔTURÉ, lui,
 * les garde : ils sont rentrés, et les reproposer ouvrirait un second retour du
 * même colis. C'est précisément le doublon que la contrainte unique empêche.
 */
export function reservesParcels(status: ReturnHandoverStatus): boolean {
  return status !== 'cancelled';
}

/**
 * ANNULER, tant que la marchandise n'est pas reçue.
 *
 * Après la réception, le bon est une preuve : les colis ont bougé, et une
 * erreur se corrige par un mouvement tracé, pas par l'effacement de la preuve.
 * C'est la règle que portait déjà l'ancien couple `draft` / `confirmed`.
 */
export function canCancelReturnHandover(status: ReturnHandoverStatus): boolean {
  return (
    status === 'draft' ||
    status === 'return-requested' ||
    status === 'picked-up' ||
    status === 'awaiting-receipt'
  );
}

/** Bon de retour rendu au client — pivot legacy `bon_retour_colis`. */
export const RETURN_SLIP_STATUSES = ['draft', 'delivered'] as const;
export type ReturnSlipStatus = (typeof RETURN_SLIP_STATUSES)[number];

/**
 * LE BL — chantier « Retours, BL Clients Pro », point 5.
 *
 * « Bon de livraison à récupérer » : le papier que le destinataire signe. C'est
 * un OBJET LIÉ AU COLIS, pas un statut colis — la mission le dit, et c'est ce
 * qui permet tout ce point sans toucher aux 35 statuts (point 7).
 *
 * **Cinq états, là où la mission en énumère six.** Le premier qu'elle cite,
 * « BL à récupérer », est l'état du COLIS (`blRequested`) et non celui d'un BL :
 * le papier n'existe pas encore, il n'a donc pas d'état. La frise de la fiche
 * colis le montre quand même en première marche — déduit du colis — et c'est
 * `blStepLabels` qui le nomme.
 */
export const DELIVERY_SLIP_STATUSES = [
  'collected',
  'handed-over',
  'received',
  'returned',
  'closed',
] as const;
export type DeliverySlipStatus = (typeof DELIVERY_SLIP_STATUSES)[number];

/**
 * Bon de retour BL — code `BRB-`, point 5.
 *
 * « Créé → En attente de réception admin → Reçu par l'admin → Remis au vendeur
 * → Clôturé », dans cet ordre. Pas d'annulation : un bon qui n'aboutit pas est
 * un BROUILLON, et un brouillon se supprime. Passée la remise à l'entrepôt les
 * papiers sont physiquement là — il n'y a plus rien à annuler, seulement à
 * constater.
 */
export const BL_NOTE_STATUSES = [
  'draft',
  'awaiting-receipt',
  'received',
  'returned',
  'closed',
] as const;
export type BlNoteStatus = (typeof BL_NOTE_STATUSES)[number];

/** Le bon BL se compose-t-il encore ? Au-delà, la liasse a quitté la sacoche. */
export function isEditableBlNote(status: BlNoteStatus): boolean {
  return status === 'draft';
}

/**
 * Motif du retour, porté par la LIGNE du bon.
 *
 * Le statut d'échec du colis dit déjà pourquoi la livraison a échoué ; le motif
 * dit pourquoi le colis repart, ce qui n'est pas la même question et ce qui
 * décide des frais. Le legacy ne le stockait nulle part (§1.14).
 */
export const RETURN_REASONS = [
  'refused',
  'unreachable',
  'cancelled',
  'damaged',
  'wrong-address',
  'other',
] as const;
export type ReturnReason = (typeof RETURN_REASONS)[number];

/**
 * DOMAIN.md §0.3 — `livraison_type` vaut `App\Quartier` sur 4 804/4 804 colis.
 * Agence et point relais restent modélisés (question PO n°1) mais jamais utilisés.
 */
/**
 * DOMAIN.md §1.15 — `facture_statuts` : 1 non validée (5 lignes), 2 validée &
 * non payée (4), 3 payée (448). Les trois états legacy, renommés par ce qu'ils
 * FONT : le brouillon se compose, l'émission fige les montants, le règlement
 * solde.
 */
export const INVOICE_STATUSES = ['draft', 'issued', 'paid'] as const;
export type InvoiceStatus = (typeof INVOICE_STATUSES)[number];

/** DOMAIN.md §1.16 — `paiement_modes` : espèce (422), virement (38), chèque (0). */
export const PAYMENT_METHODS = ['cash', 'transfer', 'check'] as const;
export type PaymentMethod = (typeof PAYMENT_METHODS)[number];

/**
 * Retour de fonds — même forme que la remise de retours : on compose, on atteste.
 *
 * `cancelled` est arrivé avec la fusion de la caisse du jour (D-115) : la
 * migration de reprise annule les brouillons vides plutôt que de les
 * supprimer, parce qu'un bon qui a existé laisse une ligne de journal. Il
 * n'est porté par AUCUN geste de l'interface — seule la migration l'écrit —
 * mais il se lit, se filtre, et ne doit donc pas manquer au contrat.
 */
export const CASH_REMITTANCE_STATUSES = ['draft', 'confirmed', 'cancelled'] as const;
export type CashRemittanceStatus = (typeof CASH_REMITTANCE_STATUSES)[number];

/**
 * LES QUATRE STATUTS D'UNE CAISSE, tels qu'ils s'AFFICHENT — D-243.
 *
 * Ce n'est pas l'état en base (`CashRemittanceStatus`, qui n'en a que trois) :
 * c'est ce que l'API en DÉDUIT, pour que les trois clients disent la même
 * chose sans le recalculer chacun de son côté.
 *
 *  - `open` : `draft`, le livreur y verse encore ses colis livrés ;
 *  - `closed` : `draft`, fermée par le livreur — l'argent est À RÉCEPTIONNER,
 *    et sa prochaine livraison ouvre une nouvelle caisse ;
 *  - `received` : `confirmed`, l'agence atteste avoir reçu l'argent ;
 *  - `validated` : reçue, et TOUS ses colis ont été versés à leurs vendeurs
 *    (facture soldée ou bon de versement payé). Calculé en lecture, jamais
 *    stocké ;
 *  - `cancelled` : le bon annulé par la reprise (D-115). Pas un statut de
 *    caisse à proprement parler, mais il se lit.
 */
export const REMITTANCE_STAGES = ['open', 'closed', 'received', 'validated', 'cancelled'] as const;
export type RemittanceStage = (typeof REMITTANCE_STAGES)[number];

/**
 * Relevé de rémunération livreur — D-020.
 *
 * Trois états, comme la facture, et pour la même raison : la validation FIGE
 * les lignes, le règlement solde. Un relevé validé est une promesse de paie ;
 * il ne se recompose pas.
 */
export const COURIER_STATEMENT_STATUSES = ['draft', 'validated', 'paid'] as const;
export type CourierStatementStatus = (typeof COURIER_STATEMENT_STATUSES)[number];

/**
 * Ce qu'une ligne rémunère. Un même colis peut en valoir DEUX pour le même
 * livreur — il l'a ramassé puis livré — et ce sont bien deux courses.
 */
export const COURIER_STATEMENT_LINE_KINDS = ['pickup', 'delivery', 'return'] as const;
export type CourierStatementLineKind = (typeof COURIER_STATEMENT_LINE_KINDS)[number];

/**
 * Régime de facturation d'un e-commerçant — C11 bloc 3, D-061.
 *
 * `normal` : la facture porte le COD encaissé MOINS les frais. Un document, un
 * flux d'argent — le fonctionnement historique.
 *
 * `professional` : les deux flux sont SÉPARÉS. Le COD est versé intégralement
 * par un bon de versement, quand la société le décide ; les frais font l'objet
 * d'une facture mensuelle. Un gros e-commerçant a besoin de sa trésorerie tous
 * les deux jours et de sa comptabilité une fois par mois.
 */
export const BILLING_TYPES = ['normal', 'professional'] as const;
export type BillingType = (typeof BILLING_TYPES)[number];

/** Deux états seulement : verser EST le geste final (C11 bloc 3). */
export const COD_PAYOUT_STATUSES = ['draft', 'paid'] as const;
export type CodPayoutStatus = (typeof COD_PAYOUT_STATUSES)[number];

export const DELIVERY_TARGETS = ['district', 'agency', 'relay-point'] as const;
export type DeliveryTarget = (typeof DELIVERY_TARGETS)[number];

/**
 * « Paiement attendu » d'un colis de client PRO — recette SpeedyTrans 3,
 * point 3. Espèces par défaut ; information pour le livreur, le flux
 * d'encaissement ne change pas.
 */
export const EXPECTED_PAYMENTS = ['cash', 'cheque'] as const;
export type ExpectedPayment = (typeof EXPECTED_PAYMENTS)[number];

/**
 * « Factures clients » / « Factures clients pro » — recette SpeedyTrans 3,
 * point 4. Le critère est `Client.isPro`, le badge « Pro » de la liste des
 * clients, et lui seul (pas `billingType`, D-099).
 */
export const INVOICE_CLIENT_KINDS = ['normal', 'pro'] as const;
export type InvoiceClientKind = (typeof INVOICE_CLIENT_KINDS)[number];

/**
 * docs/DECISIONS.md D-020, mis en oeuvre par C11 bloc 1 (D-058).
 *
 * COMBIEN on doit au livreur — le premier des deux axes. `salary` : fixe,
 * indépendant du volume. `commission` : grille `CourierCity` × courses
 * conclues. `hybrid` : les deux cumulés.
 *
 * `per-delivery` a été renommé `commission` en C11 : c'est le mot du PO, et
 * garder les deux aurait laissé un synonyme mort dans un enum que trois écrans
 * affichent. La valeur d'enum PostgreSQL est renommée sur place, les lignes
 * existantes suivent.
 *
 * QUAND on le règle est un AUTRE axe : voir `SETTLEMENT_MODES` dans
 * `courier-pay.ts`.
 */
export const PAY_MODELS = ['salary', 'commission', 'hybrid'] as const;
export type PayModel = (typeof PAY_MODELS)[number];

export const PAY_PERIODS = ['daily', 'weekly', 'monthly'] as const;
export type PayPeriod = (typeof PAY_PERIODS)[number];

/**
 * Mouvements de stock — C5, DOMAIN.md §1.18.
 *
 * Le legacy n'avait AUCUN mouvement : `produit_stock.quantity` était écrasée,
 * et rien ne disait qui avait bougé quoi ni pourquoi. Les trois causes
 * couvrent tout ce qui fait varier une quantité :
 *  - `in`     : une demande d'entreposage validée fait entrer la marchandise ;
 *  - `out`    : un colis créé depuis une commande la fait sortir ;
 *  - `adjust` : un inventaire, une casse, une erreur de comptage. Motif exigé.
 */
export const STOCK_MOVEMENT_KINDS = ['in', 'out', 'adjust'] as const;
export type StockMovementKind = (typeof STOCK_MOVEMENT_KINDS)[number];

/** DOMAIN.md §1.18 — `entreposages.statut_id` : 1 En attente, 2 Validée, 3 Refusée. */
export const STORAGE_REQUEST_STATUSES = ['pending', 'validated', 'refused'] as const;
export type StorageRequestStatus = (typeof STORAGE_REQUEST_STATUSES)[number];

/**
 * Commandes — modèle NEUF : le legacy n'avait pas de table (§1.18).
 * `parcel-created` est un état distinct de `confirmed` : c'est lui qui empêche
 * de créer deux colis pour la même commande.
 */
export const ORDER_STATUSES = ['draft', 'confirmed', 'parcel-created', 'cancelled'] as const;
export type OrderStatus = (typeof ORDER_STATUSES)[number];

/**
 * Origine d'une commande — l'interface `OrderSource` de C5.
 *
 * DOMAIN.md §4 dette n°19 : l'intégration PrestaShop lisait les commandes en
 * direct, sur une plage de dates CODÉE EN DUR (`2021-01-16` → `2021-09-17`),
 * sans rien persister et sans idempotence ; `client_boutiques` comptait 0 ligne.
 * On ne rejoue pas ça. Deux providers seulement, et tous deux persistent :
 * `manual` (saisie) et `excel` (import). Un connecteur boutique s'ajouterait
 * ici, et devrait respecter la même idempotence `(clientId, externalRef)`.
 */
export const ORDER_SOURCES = ['manual', 'excel'] as const;
export type OrderSource = (typeof ORDER_SOURCES)[number];

/** DOMAIN.md §1.5 — pièces jointes d'une fiche client. */
export const CLIENT_DOCUMENT_KINDS = ['identity', 'tax', 'contract', 'rib', 'other'] as const;
export type ClientDocumentKind = (typeof CLIENT_DOCUMENT_KINDS)[number];

export const LOCALES = ['fr', 'ar', 'en'] as const;
export type AppLocale = (typeof LOCALES)[number];

/**
 * DOMAIN.md §1.19 — `reclamation_statuts` : `1 Non traitée`, `2 Traitement en
 * cours`, `3 Traitée`. `rejected` s'y ajoute : une réclamation infondée se
 * CLÔT, elle ne se « traite » pas, et le legacy n'avait pas de mot pour le dire.
 */
export const CLAIM_STATUSES = ['open', 'in-progress', 'resolved', 'rejected'] as const;
export type ClaimStatus = (typeof CLAIM_STATUSES)[number];

/** Les deux statuts qui ferment une réclamation : le chrono s'y arrête. */
export const CLAIM_CLOSED_STATUSES: readonly ClaimStatus[] = ['resolved', 'rejected'];

/**
 * Parcours autorisé — la même mécanique que `transitions.ts` pour les colis,
 * en beaucoup plus simple.
 *
 * Le legacy portait les statuts atteignables par TYPE de réclamation, dans un
 * `varchar` CSV (`"4,5,6"` — §4 dette n°15). Un parcours écrit dans une chaîne
 * de caractères n'est pas une règle, c'est une donnée qu'on espère bien formée.
 */
export const CLAIM_TRANSITIONS: Record<ClaimStatus, readonly ClaimStatus[]> = {
  open: ['in-progress', 'resolved', 'rejected'],
  'in-progress': ['resolved', 'rejected'],
  // Une réclamation close se ROUVRE — un client qui n'est pas satisfait de la
  // réponse n'a pas à en ouvrir une seconde, qui perdrait le fil de la première.
  resolved: ['in-progress'],
  rejected: ['in-progress'],
};

export function canTransitionClaim(from: ClaimStatus, to: ClaimStatus): boolean {
  return CLAIM_TRANSITIONS[from].includes(to);
}

/** Ce sur quoi porte une réclamation — c'est le TYPE qui le décide. */
export const CLAIM_SUBJECT_TYPES = ['parcel', 'general'] as const;
export type ClaimSubjectType = (typeof CLAIM_SUBJECT_TYPES)[number];

/**
 * À qui s'adresse une actualité — DOMAIN.md §1.20, C7 bloc 3.
 *
 * Le legacy n'avait pas la notion : une seule ligne existait en production, lue
 * par tout le monde. Un message sur la facturation n'intéresse pas un livreur.
 */
export const ANNOUNCEMENT_AUDIENCES = ['all', 'merchants', 'couriers'] as const;
export type AnnouncementAudience = (typeof ANNOUNCEMENT_AUDIENCES)[number];
