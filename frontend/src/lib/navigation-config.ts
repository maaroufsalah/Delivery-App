import {
  ReceiptText,
  Coins,
  Banknote,
  Backpack,
  Bike,
  Boxes,
  Building2,
  ClipboardCheck,
  ClipboardList,
  CreditCard,
  FileText,
  HandCoins,
  Landmark,
  Inbox,
  Layers,
  LayoutDashboard,
  type LucideIcon,
  Map,
  MapPin,
  Megaphone,
  MessageSquareWarning,
  Package,
  FileSignature,
  PackageCheck,
  PackagePlus,
  Route,
  Settings,
  ShoppingCart,
  Signpost,
  ShieldCheck,
  Store,
  Tags,
  Truck,
  Undo2,
  UserCog,
  Users,
  Wallet,
  Warehouse,
} from 'lucide-react';
import type { PermissionKey } from '@delivery/shared';
import type fr from '../../messages/fr.json';

/**
 * Menu du back-office : les 6 sections héritées de Wimo (DOMAIN.md §1.4 —
 * COLIS, FACTURATION ET RÈGLEMENT, WAREHOUSE, COMPTES, DEMANDES, RÉFÉRENTIEL),
 * plus le tableau de bord à la racine.
 *
 * Les clés sont typées sur le fichier de messages de référence : renommer une
 * entrée sans la traduire casse la compilation.
 */
export type NavItemKey = keyof typeof fr.nav.items;
export type NavSectionKey = keyof typeof fr.nav.sections;

export interface NavItem {
  key: NavItemKey;
  href: string;
  icon: LucideIcon;
  /** Écran déjà livré. Les autres pointent vers la page « Bientôt ». */
  ready?: boolean;
  /**
   * Permission qui ouvre l'écran. Absente = visible par tout compte connecté.
   *
   * C'est la MÊME clé que celle du `@RequirePermission` de l'endpoint : un
   * menu qui propose un écran répondant 403 est pire qu'un menu incomplet.
   */
  permission?: PermissionKey;
  /**
   * Permissions TOUTES requises, quand l'endpoint seul ne suffit pas à décider.
   *
   * Le cas est celui des retours de fonds : l'API les ouvre en lecture à qui a
   * `finance.read`, et c'est correct — un livreur doit pouvoir consulter les
   * siens. Mais l'e-commerçant, qui a la même clé, n'a RIEN à voir dans ce
   * flux : sa portée serveur lui rendrait une liste vide, et une entrée de menu
   * qui ne mène qu'à du vide est un mensonge. On exige donc aussi le droit de
   * composer.
   */
  permissions?: PermissionKey[];
}

export interface NavSection {
  key: NavSectionKey;
  items: NavItem[];
}

/**
 * Compteurs affichés en pastille sur une entrée de menu.
 *
 * Lus par le layout, côté serveur, et passés au menu : une entrée qui porte un
 * chiffre dit qu'il y a quelque chose à faire, et c'est la seule raison d'en
 * afficher un. Le jour où d'autres compteurs arrivent, ils s'ajoutent ici —
 * le menu n'a pas à savoir d'où ils viennent.
 */
export type NavBadges = Partial<Record<NavItemKey, number>>;

/**
 * Tableau de bord — hors accordéon, toujours visible (C10 bloc 2).
 *
 * Sa clé valait `'parcels'` : le libellé venait d'ailleurs (`nav.dashboard`),
 * donc personne ne s'en apercevait tant que l'entrée était rendue à la main.
 * Dès qu'elle passe par le composant commun, qui lit `nav.items[key]`, le
 * Tableau de bord s'annonce « Colis ». La clé dit désormais ce qu'elle
 * désigne.
 */
export const DASHBOARD_ITEM: NavItem = {
  key: 'dashboard',
  href: '/dashboard',
  icon: LayoutDashboard,
  ready: true,
};

export const NAV_SECTIONS: NavSection[] = [
  {
    key: 'parcels',
    items: [
      { key: 'parcels', href: '/parcels', icon: Package, ready: true, permission: 'parcels.read' },
      /*
       * L'ENTRÉE « ÉCHANGES » A ÉTÉ RETIRÉE — chantier « Étiquette orange »,
       * point 2 : « Supprimer l'entrée de menu “Échanges”. »
       *
       * **Les écrans restent** (`/exchanges` et `/exchanges/[code]`), et c'est
       * délibéré : la fiche colis y renvoie, le dossier est la pièce qui porte
       * le journal, et un lien mort dans une carte serait pire qu'une entrée de
       * menu en trop. Ce qui disparaît, c'est la PORTE D'ENTRÉE par le menu.
       *
       * Le geste ne commence plus par « je vais dans Échanges » : il commence
       * par « je regarde ce colis livré », et le bouton est là. Une liste
       * d'échanges ne répond à aucune question qu'on se pose le matin — celle
       * qu'on se pose, c'est « où en est le colis de M. Amrani ».
       */
      {
        key: 'pickupNotes',
        href: '/pickup-notes',
        icon: ClipboardList,
        ready: true,
        permission: 'notes.read',
      },
      {
        key: 'dropoffNotes',
        href: '/dropoff-notes',
        icon: PackagePlus,
        ready: true,
        permission: 'notes.read',
      },
      { key: 'routes', href: '/routes', icon: Route, ready: true, permission: 'routes.read' },
      /*
       * STOCK LIVREUR — lot M (D-222) : ce que chaque livreur détient, colis et
       * argent. Dans COLIS, à côté des circuits dont il se déduit.
       */
      {
        key: 'courierStock',
        href: '/courier-stock',
        icon: Backpack,
        ready: true,
        permission: 'routes.stock',
      },
    ],
  },
  /*
   * Les retours ont leur propre section, comme dans le legacy (« Gestion des
   * retours »). Les loger dans COLIS, qui porte déjà cinq entrées, aurait noyé
   * un parcours qui a ses propres acteurs et ses propres permissions.
   */
  {
    key: 'returns',
    items: [
      {
        key: 'returnsPending',
        href: '/returns/pending',
        icon: Undo2,
        ready: true,
        permission: 'returns.read',
      },
      {
        key: 'returnHandovers',
        href: '/returns/handovers',
        icon: ClipboardCheck,
        ready: true,
        permission: 'returns.read',
      },
      {
        key: 'returnSlips',
        href: '/returns/slips',
        icon: PackageCheck,
        ready: true,
        permission: 'returns.read',
      },
      /*
       * L'ONGLET BL — chantier « Retours, BL Clients Pro », point 5.
       *
       * Dans RETOURS et pas dans COLIS : un bon de livraison signé fait le
       * même voyage qu'un colis qui rentre — le livreur le rapporte, l'agence
       * le reçoit, le vendeur le récupère. Il a les mêmes acteurs et la même
       * permission (`returns.read`).
       *
       * L'entrée reste visible même sans Client Pro : la liste se dira vide,
       * ce qui apprend son existence à l'agent le jour où un client la
       * demande. La cacher aurait rendu la fonctionnalité introuvable.
       */
      {
        key: 'returnBl',
        href: '/returns/bl',
        icon: FileSignature,
        ready: true,
        permission: 'returns.read',
      },
    ],
  },
  {
    key: 'billing',
    items: [
      /*
       * Le tableau de bord ouvre la section : c'est un outil d'EXPLOITATION —
       * où est le cash, qui doit quoi — et il n'a pas d'intérêt pour un
       * e-commerçant, d'où la double permission.
       */
      {
        key: 'financeDashboard',
        href: '/finance',
        icon: Wallet,
        ready: true,
        permissions: ['finance.read', 'finance.write'],
      },
      /*
       * Factures et règlements sont ouverts à l'E-COMMERÇANT : ce sont ses
       * montants, et la portée serveur ne lui montre que les siens.
       */
      {
        key: 'invoices',
        href: '/invoices',
        icon: FileText,
        ready: true,
        permission: 'finance.read',
      },
      /*
       * « FACTURES CLIENTS PRO » — recette SpeedyTrans 3, point 4. Les Clients
       * Pro (`isPro`) ont leur page ; `/invoices` garde les clients normaux et
       * ses liens. Back-office seulement : `finance.write` en plus, comme la
       * caisse, pour que l'e-commerçant (qui a `finance.read`) ne la voie pas.
       */
      {
        key: 'invoicesPro',
        href: '/invoices-pro',
        icon: FileText,
        ready: true,
        permissions: ['finance.read', 'finance.write'],
      },
      {
        key: 'payments',
        href: '/payments',
        icon: CreditCard,
        ready: true,
        permission: 'finance.read',
      },
      {
        key: 'remittances',
        href: '/remittances',
        icon: Banknote,
        ready: true,
        permissions: ['finance.read', 'finance.write'],
      },
      /*
       * LES CHÈQUES REÇUS — chantier « Finance », point 9. À côté de la caisse :
       * ce sont les deux moitiés de ce qu'un livreur rapporte, les espèces et
       * le papier. Le LIVREUR voit la même entrée, bornée par sa portée.
       */
      {
        key: 'cheques',
        href: '/cheques',
        icon: ReceiptText,
        ready: true,
        permission: 'finance.read',
      },
      /*
       * Les bons de versement sont ouverts à l'E-COMMERÇANT, comme les
       * factures : c'est son argent, et le bon est la pièce qu'il rapproche de
       * son relevé bancaire. La portée serveur ne lui montre que les siens.
       */
      {
        key: 'codPayouts',
        href: '/cod-payouts',
        icon: HandCoins,
        ready: true,
        permission: 'finance.read',
      },
      /*
       * LES ORDRES DE VIREMENT déjà générés — consultation seule. Même clé que
       * les routes `/transfer-orders` (`finance.write`) : un OV liste les
       * montants de TOUS les clients, l'e-commerçant ne le voit pas.
       */
      {
        key: 'transferOrders',
        href: '/transfer-orders',
        icon: Landmark,
        ready: true,
        permission: 'finance.write',
      },
      /*
       * La petite caisse — chantier « Caisse, OV, Petite caisse », point 3.
       * Rangée ici parce que c'est de l'argent, mais indépendante : sa propre
       * clé, que `finance.*` n'accorde pas. Administrateur seul par défaut.
       */
      {
        key: 'pettyCash',
        href: '/petty-cash',
        // Petite caisse : des pièces. `Wallet` sert déjà à un autre menu, et la
        // tirelire en forme de cochon est proscrite (inadaptée pour nos utilisateurs).
        icon: Coins,
        ready: true,
        permission: 'pettycash.read',
      },
    ],
  },
  {
    key: 'warehouse',
    items: [
      {
        key: 'products',
        href: '/products',
        icon: Boxes,
        ready: true,
        permission: 'warehouse.read',
      },
      { key: 'stock', href: '/stock', icon: Layers, ready: true, permission: 'warehouse.read' },
      {
        key: 'storage',
        href: '/storage',
        icon: Warehouse,
        ready: true,
        permission: 'warehouse.read',
      },
      /*
       * Les commandes sont visibles du STAFF comme de l'E-COMMERÇANT : c'est
       * lui qui les saisit et les importe, et la portée serveur les lui borne.
       */
      {
        key: 'orders',
        href: '/orders',
        icon: ShoppingCart,
        ready: true,
        permission: 'warehouse.read',
      },
    ],
  },
  {
    key: 'accounts',
    items: [
      { key: 'clients', href: '/clients', icon: Store, ready: true, permission: 'directory.read' },
      { key: 'couriers', href: '/couriers', icon: Bike, ready: true, permission: 'directory.read' },
      { key: 'team', href: '/team', icon: Users, ready: true, permission: 'accounts.read' },
      { key: 'users', href: '/users', icon: UserCog, ready: true, permission: 'accounts.read' },
      { key: 'roles', href: '/roles', icon: ShieldCheck, ready: true, permission: 'accounts.read' },
      {
        key: 'agencies',
        href: '/agencies',
        icon: Building2,
        ready: true,
        permission: 'accounts.read',
      },
    ],
  },
  {
    key: 'requests',
    items: [
      {
        key: 'claims',
        href: '/claims',
        icon: MessageSquareWarning,
        ready: true,
        permission: 'claims.read',
      },
      /*
       * Même liste que `/storage`, vue du CLIENT : il y dépose ses demandes et
       * suit leur sort. Un seul endpoint, deux écrans — c'est la portée qui
       * change, pas la route.
       */
      {
        key: 'storageRequests',
        href: '/storage-requests',
        icon: Inbox,
        ready: true,
        permission: 'warehouse.read',
      },
    ],
  },
  {
    key: 'directory',
    items: [
      { key: 'cities', href: '/cities', icon: MapPin, ready: true, permission: 'directory.read' },
      { key: 'zones', href: '/zones', icon: Map, ready: true, permission: 'directory.read' },
      {
        key: 'districts',
        href: '/districts',
        icon: Signpost,
        ready: true,
        permission: 'directory.read',
      },
      { key: 'pricing', href: '/pricing', icon: Tags, ready: true, permission: 'directory.read' },
      /*
       * Les actualités sont lisibles par TOUT compte connecté — c'est une
       * publication, pas une donnée de référentiel. Elles n'ont donc pas de
       * permission de lecture : seule l'écriture en demande une.
       */
      { key: 'news', href: '/news', icon: Megaphone, ready: true },
      {
        key: 'deliveryTypes',
        href: '/delivery-types',
        icon: Truck,
        ready: true,
        permission: 'directory.read',
      },
    ],
  },
];

export const SETTINGS_ITEM: NavItem = {
  key: 'settings',
  href: '/settings/company',
  icon: Settings,
  ready: true,
  permission: 'settings.read',
};

/**
 * LE MÊME ÉCRAN NE PORTE PAS LE MÊME NOM DANS LES DEUX ESPACES — chantier
 * « Correctifs avant démo », point 6.
 *
 * La mission demande une entrée « Mes chèques » dans l'espace vendeur. C'est le
 * même écran que « Chèques reçus » du back-office — même route, même
 * permission, même table — et ce n'est pas la même PHRASE : l'agence REÇOIT
 * des chèques qui ne lui appartiennent pas, le vendeur lit les SIENS.
 *
 * Une substitution de clé de traduction, et non une entrée de menu de plus :
 * deux entrées sur la même route auraient donné deux lignes actives en même
 * temps le jour où quelqu'un porte les deux rattachements.
 */
const MERCHANT_LABELS: Partial<Record<NavItemKey, NavItemKey>> = {
  cheques: 'myCheques',
};

/**
 * Filtre du menu par les permissions EFFECTIVES du compte.
 *
 * DOMAIN.md §4 dette n°13 : le legacy avait un `User::modulesIds()` qui
 * renvoyait tout ou rien selon `role_id === 1`, et un pivot `user_modules`
 * peuplé pour 2 utilisateurs sur 165. Ici le menu et le garde de l'API lisent
 * la MÊME matrice — un écran visible est un écran atteignable.
 *
 * Une section vidée de ses entrées disparaît : un titre seul ne dit rien.
 */
export function visibleSections(
  granted: readonly string[],
  /** Espace VENDEUR : quelques entrées s'y nomment autrement (point 6). */
  merchant = false,
): NavSection[] {
  const allowed = new Set(granted);

  return NAV_SECTIONS.map((section) => ({
    ...section,
    items: section.items
      .filter((item) => !item.permission || allowed.has(item.permission))
      .map((item) => {
        const relabelled = merchant ? MERCHANT_LABELS[item.key] : undefined;
        return relabelled ? { ...item, key: relabelled } : item;
      }),
  })).filter((section) => section.items.length > 0);
}

export function isVisible(item: NavItem, granted: readonly string[]): boolean {
  if (item.permissions && !item.permissions.every((key) => granted.includes(key))) return false;
  return !item.permission || granted.includes(item.permission);
}

/** Barre inférieure mobile — 4 entrées, le FAB « nouveau colis » occupe le centre. */
export const BOTTOM_NAV: NavItem[] = [
  { key: 'parcels', href: '/parcels', icon: Package, ready: true },
  { key: 'routes', href: '/routes', icon: Route, ready: true },
  { key: 'pickupNotes', href: '/pickup-notes', icon: ClipboardList, ready: true },
  { key: 'clients', href: '/clients', icon: Store },
];

export const ALL_NAV_ITEMS: NavItem[] = [
  DASHBOARD_ITEM,
  ...NAV_SECTIONS.flatMap((section) => section.items),
  SETTINGS_ITEM,
];
