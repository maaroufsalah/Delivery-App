import { USER_ROLES, type UserRole } from './enums';

/**
 * Catalogue des permissions — `module.action`.
 *
 * Source unique, partagée par l'API et le front : le garde `@RequirePermission`
 * s'y réfère pour autoriser, le menu web pour se filtrer. Deux listes auraient
 * divergé au premier ajout de module, et un menu qui propose un écran interdit
 * est pire qu'un menu incomplet.
 *
 * DOMAIN.md §1.4 — le legacy avait 23 « modules » hiérarchiques dans
 * `app_modules`, un pivot `user_modules` peuplé pour 2 utilisateurs sur 165, et
 * un `User::modulesIds()` qui ne le consultait même plus : il renvoyait TOUT si
 * `role_id === 1 || acces_id === 1`, rien sinon. La matrice ci-dessous remplace
 * ce bypass binaire (dette n°13).
 *
 * Les valeurs par défaut sont un POINT DE DÉPART, pas une vérité : la matrice
 * est éditable en base (`RolePermission`), et c'est elle qui fait foi à
 * l'exécution (D-019 — paramétrable avant codé en dur).
 */

export const PERMISSION_MODULES = [
  /*
   * La plateforme n'est pas un module métier : c'est la console qui crée et
   * administre les TENANTS (C8, D-048). Ses permissions ne sont jamais
   * accordées à un rôle de tenant, pas même à `SUPER_ADMIN` — le propriétaire
   * d'une société n'a rien à faire dans la liste des autres.
   */
  'platform',
  'parcels',
  'routes',
  'notes',
  'returns',
  'warehouse',
  'finance',
  // La petite caisse — chantier « Caisse, OV, Petite caisse », point 3. Un
  // module à part de `finance` : aucune de ses clés n'ouvre la facturation.
  'pettycash',
  'claims',
  'announcements',
  'directory',
  'accounts',
  'settings',
  // C11 bloc 5 — le journal des modifications, à part de la configuration.
  'audit',
] as const;

export type PermissionModule = (typeof PERMISSION_MODULES)[number];

interface PermissionShape {
  key: string;
  module: PermissionModule;
  /** Libellé de secours : la matrice affiche la traduction quand elle existe. */
  label: string;
}

export const PERMISSIONS = [
  { key: 'platform.read', module: 'platform', label: 'Consulter les tenants' },
  { key: 'platform.write', module: 'platform', label: 'Créer et administrer les tenants' },
  /*
   * Se connecter en tant qu'un tenant est séparé du reste : c'est le seul geste
   * qui donne accès à de la donnée métier, et il doit pouvoir être retiré sans
   * retirer l'administration.
   */
  { key: 'platform.impersonate', module: 'platform', label: 'Se connecter en tant qu’un tenant' },

  { key: 'parcels.read', module: 'parcels', label: 'Consulter les colis' },
  { key: 'parcels.write', module: 'parcels', label: 'Créer et modifier les colis' },
  { key: 'parcels.status', module: 'parcels', label: 'Changer le statut d’un colis' },
  { key: 'parcels.dispute', module: 'parcels', label: 'Contester un statut terminal' },
  /*
   * D-250 — corriger librement le statut d'un colis (n'importe quelle cible,
   * rattachements alignés). Séparée de `parcels.status` : c'est un geste
   * d'administrateur, et le service exige en plus le rôle SUPER_ADMIN.
   */
  { key: 'parcels.correctStatus', module: 'parcels', label: 'Corriger librement le statut d’un colis' },
  /*
   * Modifier les FRAIS d'un colis est séparé de `parcels.write` — C11 bloc 4,
   * D-063. Corriger une adresse et corriger un montant ne sont pas le même
   * geste : le second change ce qu'un e-commerçant va payer, même après
   * livraison, et il doit pouvoir être retiré sans retirer la saisie.
   */
  { key: 'parcels.editFees', module: 'parcels', label: 'Modifier les frais d’un colis' },

  { key: 'routes.read', module: 'routes', label: 'Consulter les circuits' },
  { key: 'routes.write', module: 'routes', label: 'Créer et modifier les circuits' },
  { key: 'routes.confirm', module: 'routes', label: 'Confirmer un colis en tournée' },
  /*
   * M1, D-073 — ouvrir SA tournée du jour en scannant, depuis l'application.
   *
   * Séparée de `routes.write`, qui compose n'importe quelle tournée pour
   * n'importe quel livreur depuis le back-office. Celle-ci ne peut rien faire
   * d'autre qu'ajouter à la sienne un colis dont on tient le code-barres.
   */
  { key: 'routes.scan', module: 'routes', label: 'Ouvrir sa tournée en scannant' },
  { key: 'routes.close', module: 'routes', label: 'Clôturer un circuit' },
  /*
   * LE STOCK DES LIVREURS — lot M, D-222. Ce que chaque livreur détient, colis
   * et argent, et ses deux PDF. Une clé à part : elle montre la caisse de
   * TOUS les livreurs de l'agence, ce que `routes.read` (que le livreur a) ne
   * doit pas ouvrir. Défaut : SUPER_ADMIN et STAFF, ceux qui ont déjà les
   * circuits ET la caisse.
   */
  {
    key: 'routes.stock',
    module: 'routes',
    label: 'Consulter et télécharger le stock des livreurs',
  },

  { key: 'notes.read', module: 'notes', label: 'Consulter les bons' },
  { key: 'notes.write', module: 'notes', label: 'Valider les bons' },
  /*
   * M2 — l'e-commerçant DEMANDE un ramassage, il ne compose pas de bon.
   *
   * La différence est tout le sujet, et c'est la même qu'entre `routes.scan` et
   * `routes.write` en M1 (D-072). `notes.write` ouvre `POST /delivery-notes`,
   * qui prend un `clientId` DANS LE CORPS sans le confronter à la portée — donc
   * ouvrir un bon pour n'importe quel client — et surtout
   * `POST /:code/validate`, qui fait passer les colis en « ramassé ». Un vendeur
   * pourrait attester lui-même que le transporteur est venu chercher sa
   * marchandise.
   *
   * `notes.request` n'ouvre que `POST /merchant/me/pickups` : SES colis, SON
   * bon, en attente de validation par l'agence.
   */
  { key: 'notes.request', module: 'notes', label: 'Demander un ramassage' },

  /*
   * Les retours ont leur propre module, et `returns.confirm` est séparé de
   * `returns.write` : composer une remise est un geste de saisie, la confirmer
   * fait bouger des colis et verrouille une preuve. C'est la même distinction
   * que `routes.write` / `routes.confirm`.
   */
  { key: 'returns.read', module: 'returns', label: 'Consulter les retours' },
  { key: 'returns.write', module: 'returns', label: 'Composer les remises et les bons de retour' },
  { key: 'returns.confirm', module: 'returns', label: 'Confirmer une remise, clôturer un bon' },

  /*
   * Le warehouse sépare la LECTURE du stock de son MOUVEMENT, et le mouvement
   * de la DÉCISION. Un magasinier saisit des produits et ajuste un inventaire ;
   * valider une demande d'entreposage engage l'entrepôt, c'est un autre geste.
   */
  { key: 'warehouse.read', module: 'warehouse', label: 'Consulter produits, stock et commandes' },
  { key: 'warehouse.write', module: 'warehouse', label: 'Gérer produits, stock et commandes' },
  { key: 'warehouse.validate', module: 'warehouse', label: 'Valider une demande d’entreposage' },

  /*
   * La finance distingue la SAISIE des trois gestes qui engagent de l'argent :
   * émettre une facture fige des montants qu'on ne recalculera plus, enregistrer
   * un règlement la solde, confirmer un retour de fonds atteste que le cash du
   * livreur est bien arrivé à l'agence. Le legacy ouvrait les trois au même
   * `role_id = 1` (DOMAIN.md §3.9 : « aucune automatisation », tout est manuel
   * et tout est admin). C'est la même séparation que `routes.confirm` et
   * `returns.confirm` : composer n'est pas attester.
   */
  { key: 'finance.read', module: 'finance', label: 'Consulter la facturation' },
  { key: 'finance.write', module: 'finance', label: 'Composer factures et retours de fonds' },
  { key: 'finance.validate', module: 'finance', label: 'Émettre une facture' },
  { key: 'finance.pay', module: 'finance', label: 'Enregistrer un règlement' },
  { key: 'finance.confirm', module: 'finance', label: 'Confirmer un retour de fonds' },

  /*
   * Les réclamations séparent la LECTURE, l'OUVERTURE et le TRAITEMENT.
   * L'e-commerçant ouvre et suit les siennes ; il ne change pas leur statut,
   * ne les assigne pas et ne voit pas les notes d'agence. C'est la même
   * distinction que `routes.write` / `routes.confirm` : déposer n'est pas
   * trancher.
   */
  /*
   * LA PETITE CAISSE — chantier « Caisse, OV, Petite caisse », point 3.
   *
   * Les dépenses courantes de la société, sans lien avec les colis ni la
   * facturation : `finance.*` ne l'ouvre pas, et `pettycash.*` n'ouvre rien
   * d'autre. Le défaut ne la donne qu'au SUPER_ADMIN (qui reçoit tout le
   * catalogue) ; une société l'ouvre à un autre rôle depuis la matrice.
   */
  { key: 'pettycash.read', module: 'pettycash', label: 'Consulter la petite caisse' },
  {
    key: 'pettycash.write',
    module: 'pettycash',
    label: 'Saisir, modifier et supprimer les mouvements de petite caisse',
  },
  /*
   * La petite caisse du LIVREUR — D-168. Saisir SES dépenses, qui naissent
   * brouillon, et les corriger tant que l'administration n'a pas tranché.
   * N'ouvre ni la liste de la caisse, ni son solde, ni l'approvisionnement :
   * les routes `courier/me/petty-cash` ne lisent que `scope.userId`.
   */
  {
    key: 'pettycash.courier',
    module: 'pettycash',
    label: 'Saisir ses dépenses de petite caisse (livreur)',
  },
  { key: 'claims.read', module: 'claims', label: 'Consulter les réclamations' },
  { key: 'claims.write', module: 'claims', label: 'Ouvrir une réclamation et y répondre' },
  {
    key: 'claims.handle',
    module: 'claims',
    label: 'Traiter, assigner et clôturer une réclamation',
  },

  /*
   * Les actualités sont une PUBLICATION : tout compte connecté les lit, seul
   * le staff les écrit. `announcements.read` n'existe donc pas — une
   * permission que tout le monde a n'en est pas une, et le bandeau du tableau
   * de bord s'en passerait de toute façon.
   */
  { key: 'announcements.write', module: 'announcements', label: 'Publier des actualités' },

  { key: 'directory.read', module: 'directory', label: 'Consulter le référentiel' },
  { key: 'directory.write', module: 'directory', label: 'Gérer le référentiel' },

  { key: 'accounts.read', module: 'accounts', label: 'Consulter les comptes' },
  { key: 'accounts.write', module: 'accounts', label: 'Gérer les comptes' },

  { key: 'settings.read', module: 'settings', label: 'Consulter la configuration' },
  { key: 'settings.write', module: 'settings', label: 'Modifier la configuration' },

  /*
   * Le journal des modifications — C11 bloc 5, D-064. Une clé à part, dans son
   * propre module : il dit QUI a fait QUOI, et c'est une lecture d'audit, pas
   * de configuration. Le défaut ne la donne qu'au SUPER_ADMIN.
   *
   * Un e-commerçant et un livreur lisent l'onglet « Historique » de LEURS
   * fiches sans elle : la portée serveur borne ce qu'ils voient, et exiger
   * `audit.read` reviendrait à leur refuser l'historique de leurs propres
   * objets pour protéger celui des autres.
   */
  { key: 'audit.read', module: 'audit', label: 'Consulter le journal des modifications' },
] as const satisfies readonly PermissionShape[];

/** Le type dérive de la donnée : `PermissionKey` est l'union des clés réelles. */
export type PermissionDefinition = (typeof PERMISSIONS)[number];
export type PermissionKey = PermissionDefinition['key'];

export const PERMISSION_KEYS: readonly PermissionKey[] = PERMISSIONS.map(
  (permission) => permission.key,
);

export function isPermissionKey(value: unknown): value is PermissionKey {
  return typeof value === 'string' && (PERMISSION_KEYS as readonly string[]).includes(value);
}

export function permissionsOfModule(module: PermissionModule): PermissionDefinition[] {
  return PERMISSIONS.filter((permission) => permission.module === module);
}

/**
 * Matrice par défaut, posée au seed.
 *
 * `SUPER_ADMIN` reçoit tout, et conserve en plus un court-circuit dans le garde :
 * une matrice mal éditée ne doit pas pouvoir verrouiller l'administration hors
 * de son propre outil.
 *
 * `COURIER` a `routes.confirm` sans `routes.write` : un livreur atteste avoir un
 * colis en main, il ne compose pas les tournées. C'est la distinction que le
 * legacy n'avait pas — `MobileMngr` ouvrait 22 routes mobiles sans vérifier le
 * moindre rôle (dette n°3).
 */
export const DEFAULT_ROLE_PERMISSIONS: Record<UserRole, readonly PermissionKey[]> = {
  /*
   * L'administrateur de plateforme n'a QUE `platform.*` (C8, D-048). Il ne
   * reçoit aucune permission métier : la donnée d'un tenant ne se lit qu'en s'y
   * connectant, et cette connexion est tracée dans `PlatformAuditLog`.
   */
  PLATFORM_ADMIN: ['platform.read', 'platform.write', 'platform.impersonate'],
  /*
   * `SUPER_ADMIN` reçoit tout SAUF `platform.*` : il est le propriétaire de SA
   * société, pas de la plateforme. Lui donner `PERMISSION_KEYS` en bloc lui
   * ouvrirait la console des autres tenants.
   */
  SUPER_ADMIN: PERMISSION_KEYS.filter((key) => !key.startsWith('platform.')),
  STAFF: [
    'parcels.read',
    'parcels.write',
    'parcels.status',
    'parcels.dispute',
    // Le cahier des charges C11 désigne le STAFF comme l'acteur : c'est
    // l'agence qui constate qu'un colis a coûté autre chose que son tarif.
    // La clé reste séparable — D-019, la matrice s'édite en base.
    'parcels.editFees',
    'routes.read',
    'routes.write',
    'routes.confirm',
    'routes.close',
    // Lot M, D-222 — le stock des livreurs.
    'routes.stock',
    'notes.read',
    'notes.write',
    'notes.request',
    'returns.read',
    'returns.write',
    'returns.confirm',
    'warehouse.read',
    'warehouse.write',
    'warehouse.validate',
    'finance.read',
    'finance.write',
    'finance.confirm',
    'claims.read',
    'claims.write',
    'claims.handle',
    'announcements.write',
    'directory.read',
    'directory.write',
    'accounts.read',
    'settings.read',
  ],
  /*
   * Le STAFF compose les factures et REÇOIT le cash du livreur — c'est l'agence
   * qui le compte. Il n'émet pas la facture et n'enregistre pas le règlement :
   * ces deux-là arrêtent un montant dû et le soldent, ils restent à
   * l'administration tant que le PO n'a pas tranché l'inverse (D-019 — la
   * matrice est éditable en base, ce défaut n'est qu'un point de départ).
   */
  /*
   * L'e-commerçant CONSULTE ses retours — c'est sa marchandise qui revient —
   * mais ne compose rien : le bon est établi par l'agence qui le lui remet.
   */
  /*
   * L'e-commerçant gère SES produits et SES commandes — c'est sa marchandise —
   * mais ne valide pas ses propres demandes d'entreposage : accepter de la
   * marchandise dans l'entrepôt est une décision du transporteur.
   */
  MERCHANT: [
    'parcels.read',
    'parcels.write',
    'notes.read',
    /*
     * M2 — il demande le ramassage de ses propres colis. Voir le catalogue :
     * c'est délibérément une clé à part de `notes.write`.
     */
    'notes.request',
    'returns.read',
    'warehouse.read',
    'warehouse.write',
    'finance.read',
    'claims.read',
    'claims.write',
    'directory.read',
  ],
  /*
   * Le livreur voit les remises qui le concernent et les compose au fil de sa
   * tournée, mais ne les confirme pas : la confirmation est la RÉCEPTION par
   * l'agence, et celui qui rapporte les colis n'atteste pas lui-même les avoir
   * rendus. C'est ce contrôle croisé que le legacy n'avait pas.
   *
   * `finance.read` porte cette phrase, et manquait — C11-fix bloc 1. L'espace
   * livreur (C11 bloc 7) ouvre « mes remises » et « mes relevés », tous deux
   * gardés par cette clé : les deux écrans rendaient 403 en production. La
   * route `cheque-photo` la réclame explicitement, et son commentaire dit
   * pourquoi — « le livreur ne règle rien, il DOCUMENTE un règlement ».
   *
   * L'ouverture ne montre rien de plus que ce qui le concerne : `courierId`
   * borne remises et relevés, et les portées de `invoices` et `cod-payouts`
   * renvoient `__scope_denies_all__` pour un livreur. `courier-scope.spec.ts`
   * le vérifie, parce que c'est cette étanchéité qui rend la clé donnable.
   */
  COURIER: [
    'parcels.read',
    'parcels.status',
    /*
     * B-19, tranché par la clôture V1 : le livreur VOIT et OUVRE les
     * réclamations **sur ses propres colis**, et rien d'autre.
     *
     * Il n'est pas partie à la relation commerciale — c'était l'argument qui
     * lui fermait la porte — mais il est partie au FAIT : c'est lui qu'on met
     * en cause quand un colis arrive abîmé, et il n'avait aucun moyen de le
     * lire ni d'y répondre. Un livreur qui découvre au relevé qu'une course
     * lui a été retenue pour un motif qu'il n'a jamais vu n'a aucun recours.
     *
     * **La clé n'ouvre pas l'annuaire** : `claimWhere()` joint le colis et
     * exige `parcel.courierId = le sien`, donc une réclamation sans colis —
     * « règlement non reçu », une affaire entre le vendeur et la société —
     * lui reste invisible. À la création, l'e-commerçant est DÉDUIT du colis :
     * le lui faire choisir dans une liste lui montrerait les clients de la
     * société (D-082).
     *
     * *À valider par le PO* : la mission proposait ce défaut, il est retenu
     * tel quel. Une société qui n'en veut pas retire les deux clés dans
     * Paramètres > Rôles, sans déploiement (D-019).
     */
    'claims.read',
    'claims.write',
    'routes.read',
    /*
     * M1, D-073 — le circuit se crée par le SCAN, et c'est le livreur qui
     * scanne. La règle terrain de Speedy lui confie ce geste : le matin,
     * personne ne compose de tournée au bureau, il prend ses colis et part.
     *
     * **`routes.scan` et non `routes.write`**, et la différence est tout le
     * sujet : `routes.write` ouvre `POST /routes` du back-office, c'est-à-dire
     * composer N'IMPORTE QUELLE tournée pour N'IMPORTE QUEL livreur. `scan`
     * n'ouvre que la sienne, du jour, par un code-barres qu'il a en main.
     *
     * Une société qui préfère préparer ses tournées à l'agence retire la clé,
     * et le scan refuse alors d'ouvrir un brouillon — il ne sait plus que
     * compléter celui qu'on lui a préparé (D-019).
     */
    'routes.scan',
    'routes.confirm',
    'returns.read',
    'returns.write',
    'finance.read',
    // D-168 — ses dépenses de petite caisse, en brouillon. Rien d'autre de la caisse.
    'pettycash.courier',
  ],
};

/** Permissions d'un jeu de rôles, dédupliquées. Utilisé par le garde et le menu. */
export function permissionsForRoles(roles: readonly string[]): Set<PermissionKey> {
  const granted = new Set<PermissionKey>();

  for (const role of roles) {
    if (!(USER_ROLES as readonly string[]).includes(role)) continue;
    for (const key of DEFAULT_ROLE_PERMISSIONS[role as UserRole]) {
      granted.add(key);
    }
  }

  return granted;
}
