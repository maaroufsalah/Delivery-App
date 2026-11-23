import type {
  AnnouncementListItem,
  CodPayoutListItem,
  DeliveryNoteDetail,
  DeliveryNoteListItem,
  ExpectedPayment,
  InvoiceListItem,
  SellerParcelUpdateInput,
  InvoiceStatus,
  NoteKind,
  ParcelStatusGroup,
  MerchantCompany,
  MerchantCompanyInput,
  MerchantSummary,
  Paginated,
  ParcelDetail,
  ParcelListItem,
  ProductDetail,
  ProductListItem,
  StockDetail,
  StockListItem,
} from '@delivery/shared';
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError, api } from './api';
import { enqueue, newKey } from './queue';
import { useSession } from './session';

/**
 * Ce que l'espace de l'E-COMMERÇANT demande à l'API — M2.
 *
 * Le jumeau de `courier.ts`, et il suit les mêmes règles : une fabrique de clés
 * au lieu de chaînes éparpillées, `enabled` partout où un paramètre peut
 * manquer, et `encodeURIComponent` sur chaque segment dynamique.
 *
 * **Une seule route neuve dans tout M2** (`/merchant/me/summary`). Les colis,
 * les factures, les réclamations et les actualités passent par les endpoints
 * existants : la portée serveur les borne déjà au client de la session, et en
 * ajouter des copies aurait fait deux endroits où se tromper de portée.
 */

export const keys = {
  summary: ['merchant', 'summary'] as const,
  news: ['merchant', 'news'] as const,
  parcels: (filters: ParcelFilters) => ['merchant', 'parcels', filters] as const,
  parcel: (code: string) => ['merchant', 'parcel', code] as const,
  tracking: (code: string) => ['merchant', 'tracking', code] as const,
  cities: ['merchant', 'cities'] as const,
  districts: (cityId: string) => ['merchant', 'districts', cityId] as const,
  quote: (input: unknown) => ['merchant', 'quote', input] as const,
  pickupCandidates: ['merchant', 'pickup-candidates'] as const,
  invoices: (search: string, status: string) => ['merchant', 'invoices', search, status] as const,
  payouts: ['merchant', 'payouts'] as const,
  notes: (kind: NoteKind) => ['merchant', 'notes', kind] as const,
  note: (code: string) => ['merchant', 'note', code] as const,
  products: (search: string) => ['merchant', 'products', search] as const,
  stock: (search: string) => ['merchant', 'stock', search] as const,
  product: (id: string) => ['merchant', 'product', id] as const,
  stockLine: (id: string) => ['merchant', 'stock-line', id] as const,
  company: ['merchant', 'company'] as const,
};

/** Les chiffres de l'accueil. */
export function useSummary() {
  return useQuery({
    queryKey: keys.summary,
    queryFn: () => api<MerchantSummary>('/merchant/me/summary'),
    // Une minute : l'écran se rouvre souvent, et ces chiffres ne bougent pas à
    // la seconde. Le tirer-pour-rafraîchir reste disponible.
    staleTime: 60_000,
  });
}

/** Les actualités de la société. Route existante, ouverte à tout compte. */
export function useNews() {
  return useQuery({
    queryKey: keys.news,
    queryFn: () => api<AnnouncementListItem[]>('/announcements/feed?limit=3'),
    staleTime: 15 * 60_000,
  });
}

export interface ParcelFilters {
  search?: string;
  statusGroup?: string;
  page?: number;
}

/**
 * Mes colis.
 *
 * Le filtre passe par `statusGroup` et non `statusId` : un vendeur pense en
 * « en cours », « livré », « retour », pas en trente-quatre statuts. C'est le
 * même vocabulaire que les familles du référentiel partagé.
 */
export function useParcels(filters: ParcelFilters) {
  const query = new URLSearchParams({ pageSize: '20', page: String(filters.page ?? 1) });
  if (filters.search?.trim()) query.set('search', filters.search.trim());
  if (filters.statusGroup) query.set('statusGroup', filters.statusGroup);

  return useQuery({
    queryKey: keys.parcels(filters),
    queryFn: () => api<Paginated<ParcelListItem>>(`/parcels?${query.toString()}`),
    // Les filtres changent à la frappe : garder l'ancienne page évite un
    // clignotement de squelette à chaque lettre.
    placeholderData: (previous) => previous,
  });
}

export function useParcel(code: string | undefined) {
  return useQuery({
    queryKey: keys.parcel(code ?? ''),
    queryFn: () => api<ParcelDetail>(`/parcels/${encodeURIComponent(code ?? '')}`),
    enabled: Boolean(code),
  });
}

/**
 * La TIMELINE, lue par le suivi PUBLIC — M2, et c'est délibéré.
 *
 * Le détail d'un colis porte ses événements complets : le statut exact, le
 * commentaire de l'agent, son nom. Le suivi public, lui, replie les
 * trente-quatre statuts en sept FAMILLES, retire les commentaires et les
 * auteurs, et fond les relances consécutives en une seule ligne.
 *
 * C'est cette version-là qu'on montre au vendeur, et la mission le demandait
 * nommément. La raison tient en un exemple : « 3e tentative — le client dit
 * qu'il n'a jamais commandé ça » est une note de travail entre l'agence et son
 * livreur. Le vendeur a besoin de savoir que le colis en est aux relances, pas
 * de lire ce qu'on dit de son acheteur.
 *
 * **Réutiliser l'endpoint plutôt que refiltrer** : le filtre du suivi public
 * est une liste blanche au niveau du `select` Prisma — les champs internes ne
 * sont même pas chargés. Le réécrire ici en aurait fait une seconde version, à
 * tenir d'accord avec la première.
 */
export interface PublicStep {
  /** Une FAMILLE, pas un statut : c'est tout l'objet du repli. */
  group: ParcelStatusGroup;
  at: string;
  cityName: string | null;
}

export interface PublicTracking {
  code: string;
  group: ParcelStatusGroup;
  destinationCityName: string;
  steps: PublicStep[];
}

export function useTracking(code: string | undefined) {
  return useQuery({
    queryKey: keys.tracking(code ?? ''),
    queryFn: () => api<PublicTracking>(`/public/track/${encodeURIComponent(code ?? '')}`),
    enabled: Boolean(code),
  });
}

/* -------------------------------------------------------------------------- */
/* Créer un colis — M2 bloc 5                                                 */
/* -------------------------------------------------------------------------- */

/** Une entrée de référentiel : ville, quartier, type de livraison. */
export interface Choice {
  id: string;
  name: string;
}

/**
 * Une ville, avec ce que la recherche lit en plus du nom — point 2 du 26/09 :
 * le nom arabe et le code, que `/directory/cities` renvoie depuis toujours.
 */
export interface CityChoice extends Choice {
  nameAr?: string | null;
  code?: string;
}

/** Les villes de la société. Elles bougent rarement : on les garde longtemps. */
export function useCities() {
  return useQuery({
    queryKey: keys.cities,
    queryFn: () => api<CityChoice[]>('/directory/cities'),
    staleTime: 24 * 60 * 60_000,
  });
}

/** Les quartiers d'une ville. Rechargés quand la ville change. */
export function useDistricts(cityId: string | undefined) {
  return useQuery({
    queryKey: keys.districts(cityId ?? ''),
    queryFn: () => api<Choice[]>(`/directory/districts?cityId=${encodeURIComponent(cityId ?? '')}`),
    enabled: Boolean(cityId),
    staleTime: 24 * 60 * 60_000,
  });
}

export interface QuoteResult {
  fee: number;
  baseFee: number;
}

/**
 * Le TARIF, en direct — `POST /parcels/quote`, qui existait déjà.
 *
 * **Une requête, pas un calcul local.** La grille tarifaire est une matrice de
 * villes avec des remises par client et des replis par zone ; la recopier dans
 * l'application en aurait fait une seconde version, qui aurait divergé au
 * premier changement de grille. Le vendeur verrait alors un prix, et sa facture
 * en dirait un autre.
 *
 * **Silencieux hors ligne.** Sans réseau, on n'affiche pas de tarif plutôt
 * qu'un tarif faux : le colis part quand même en file, et son prix sera celui
 * que le serveur calculera à l'arrivée. C'est aussi ce qui se passe quand un
 * vendeur saisit depuis le web.
 */
export function useQuote(input: {
  originCityId?: string;
  destinationCityId?: string;
  deliveryTarget: string;
}) {
  const ready = Boolean(input.originCityId && input.destinationCityId);

  return useQuery({
    queryKey: keys.quote(input),
    queryFn: () =>
      api<QuoteResult>('/parcels/quote', {
        method: 'POST',
        body: {
          originCityId: input.originCityId,
          destinationCityId: input.destinationCityId,
          deliveryTarget: input.deliveryTarget,
        },
      }),
    enabled: ready,
    // Un tarif refusé ne se retente pas : la paire de villes ne changera pas
    // toute seule, et un second appel dirait la même chose.
    retry: false,
  });
}

/** Ce que l'écran de création envoie. Le serveur applique `createParcelSchema`. */
export interface NewParcel {
  description: string;
  recipientName: string;
  recipientPhone: string;
  originCityId: string;
  pickupAddress: string;
  destinationCityId: string;
  /** Texte libre, facultatif — chantier « Quartier en saisie libre ». */
  destinationDistrictName?: string;
  deliveryAddress: string;
  codAmount: number;
  openOnDelivery: boolean;
  fragile: boolean;
  /** Client Pro seulement — recette 3, point 3. Le serveur l'ignore sinon. */
  expectedPayment?: ExpectedPayment;
}

/**
 * LE VENDEUR CORRIGE OU SUPPRIME SON COLIS — recette SpeedyTrans 3, point 3.
 *
 * En ligne seulement, pas de file hors-ligne : le geste n'a de sens que si le
 * colis n'est pas encore ramassé, et seul le serveur le sait au moment où la
 * requête arrive (422 sinon). Rejouer plus tard une correction sur un colis
 * entre-temps ramassé serait la refuser en silence.
 */
export function useUpdateParcel(code: string) {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (input: SellerParcelUpdateInput) =>
      api<ParcelDetail>(`/parcels/${encodeURIComponent(code)}`, { method: 'PATCH', body: input }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['merchant'] });
    },
  });
}

export function useDeleteParcel(code: string) {
  const client = useQueryClient();

  return useMutation({
    mutationFn: () =>
      api<{ code: string }>(`/parcels/${encodeURIComponent(code)}`, { method: 'DELETE' }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['merchant'] });
    },
  });
}

/**
 * Crée le colis, ou le met en file.
 *
 * **`clientId` vient de la SESSION, pas de l'écran.** Le schéma partagé l'exige,
 * et le service refuse explicitement un identifiant qui n'est pas celui de la
 * portée — « création impossible pour un autre expéditeur ». Le poser ici, une
 * fois, évite de le promener dans l'état du formulaire, où il n'a rien à faire :
 * un vendeur ne choisit pas pour qui il expédie.
 *
 * C'est le correctif du profil qui rend ceci possible : avant lui, `clientId`
 * n'existait pas sur le profil rechargé au démarrage.
 */
export function useCreateParcel() {
  const client = useQueryClient();
  const { profile } = useSession();

  return useMutation({
    mutationFn: async (input: NewParcel): Promise<{ code: string } | Deferred> => {
      const body = { ...input, clientId: profile?.clientId ?? '' };
      /*
       * UNE clé pour cette création, au premier envoi comme au rejeu — audit du
       * 03/10, C2. Un délai dépassé met le geste en file alors que le serveur a
       * pu créer le colis : la même clé lui fait rendre ce colis-là.
       */
      const idempotencyKey = newKey('parcel');

      try {
        return await api<{ code: string }>('/parcels', { method: 'POST', body, idempotencyKey });
      } catch (error) {
        if (!ApiError.isDeferrable(error)) throw error;

        /*
         * Le `clientId` est figé DANS l'entrée de file, pas relu au rejeu : si
         * le téléphone change de main avant que le réseau revienne, le colis
         * doit rester celui de qui l'a saisi. La file se vide de toute façon à
         * la déconnexion (D-077), mais l'écrire ici évite de dépendre de ça.
         */
        await enqueue({
          kind: 'parcel',
          path: '/parcels',
          body,
          label: input.recipientName,
          idempotencyKey,
        });

        return { queued: true };
      }
    },
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['merchant'] });
    },
  });
}

/** Le résultat d'un geste mis en file plutôt qu'envoyé. */
export interface Deferred {
  queued: true;
}

export function isDeferred(result: unknown): result is Deferred {
  return typeof result === 'object' && result !== null && 'queued' in result;
}

/* -------------------------------------------------------------------------- */
/* Ramassage, finances, réclamations — M2 bloc 6                              */
/* -------------------------------------------------------------------------- */

/**
 * Les colis qu'on peut mettre sur un bon — V2 vendeur bloc 4.
 *
 * **La même liste pour les deux genres** — recette SpeedyTrans 4, point 2
 * (D-132). Tout colis pas encore pris en charge et sur aucun bon actif, que la
 * case « ramassage » ait été cochée ou non à la création : c'est le bon qui
 * fixe le mode, et le serveur aligne le statut du colis sur lui.
 */
export function useNoteCandidates(kind: NoteKind) {
  /*
   * PAR PAGES, JUSQU'AU BOUT — chantier « Plafond de 200 ».
   *
   * Une seule page de cent : au-delà, les colis n'existaient pas pour l'écran,
   * et « Demander le ramassage » en oubliait sans le dire. Les pages suivantes
   * sont lues l'une après l'autre (l'écran les enchaîne) : le choix par défaut
   * dépend du statut de CHAQUE colis, il faut donc les avoir tous.
   */
  return useInfiniteQuery({
    queryKey: [...keys.pickupCandidates, kind] as const,
    queryFn: ({ pageParam }) =>
      api<Paginated<ParcelListItem>>(
        `/parcels?noteCandidate=true&pageSize=${NOTE_CANDIDATE_PAGE}&page=${pageParam}`,
      ),
    initialPageParam: 1,
    getNextPageParam: (last) =>
      last.page * last.pageSize < last.total ? last.page + 1 : undefined,
  });
}

/** La plus grande page qu'accepte `GET /parcels`. */
const NOTE_CANDIDATE_PAGE = 200;

/** Demande un bon, du genre demandé. Pas de file hors-ligne : voir le commentaire. */
export function useRequestNote(kind: NoteKind) {
  const client = useQueryClient();

  return useMutation({
    /*
     * **Pas de mise en file.** Un bon de ramassage rend un CODE que l'écran
     * suivant affiche, et une demande partie dans le vide laisserait le vendeur
     * croire que le transporteur va passer. La règle de `queue.ts` s'applique :
     * on ne met en file que les gestes dont l'auteur sait déjà le résultat.
     */
    mutationFn: (parcelIds: string[]) =>
      api<{ code: string }>(kind === 'pickup' ? '/merchant/me/pickups' : '/merchant/me/dropoffs', {
        method: 'POST',
        body: { parcelIds },
      }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['merchant'] });
    },
  });
}

/**
 * Mes factures — V2 vendeur bloc 5, spécification §7.
 *
 * **La recherche et le filtre sont faits par le SERVEUR.** `invoiceListQuerySchema`
 * les porte déjà, et filtrer une page de vingt-cinq lignes côté téléphone ne
 * trouverait jamais la facture de mars. C'est la même règle que le warehouse,
 * et la même raison : la spécification demande que l'écran tienne la charge
 * (§17).
 */
export function useInvoices(filters: { search?: string; status?: InvoiceStatus } = {}) {
  const query = new URLSearchParams({ pageSize: '25', order: 'desc' });
  if (filters.search) query.set('search', filters.search);
  if (filters.status) query.set('status', filters.status);

  return useQuery({
    queryKey: keys.invoices(filters.search ?? '', filters.status ?? ''),
    queryFn: () => api<Paginated<InvoiceListItem>>(`/invoices?${query.toString()}`),
  });
}

/**
 * Les bons de versement — seulement utiles à un client PROFESSIONNEL.
 *
 * Chez lui, le COD ne passe pas par la facture : il est versé à part, et la
 * facture ne réclame que les frais (D-061). Chez un client normal, la liste est
 * vide et l'écran ne l'affiche pas — une section vide poserait une question au
 * lieu d'y répondre.
 */
export function usePayouts(enabled: boolean) {
  return useQuery({
    queryKey: keys.payouts,
    queryFn: () => api<Paginated<CodPayoutListItem>>('/cod-payouts?pageSize=25&order=desc'),
    enabled,
  });
}

/**
 * LES BONS DE RAMASSAGE ET DE DÉPÔT — V2 vendeur bloc 1.
 *
 * `GET /delivery-notes` sert les deux genres et existe depuis C7. Le vendeur
 * porte `notes.read` depuis M2 : **l'écran manquait, pas le droit.** La portée
 * serveur le borne déjà à ses bons (D-082), donc aucune route neuve — en
 * ajouter une aurait fait deux endroits où se tromper de portée.
 */
export function useDeliveryNotes(kind: NoteKind) {
  return useQuery({
    queryKey: keys.notes(kind),
    queryFn: () => api<Paginated<DeliveryNoteListItem>>(`/delivery-notes?kind=${kind}&pageSize=25`),
  });
}

/**
 * LES PRODUITS DU VENDEUR — V2 vendeur bloc 1.
 *
 * La recherche est faite par le SERVEUR, et pas en filtrant une page déjà
 * chargée : la spécification demande que l'écran reste fluide à plusieurs
 * centaines de produits (§17), et vingt-cinq lignes filtrées côté téléphone ne
 * trouveraient jamais la deux-centième.
 */
export function useProducts(search: string) {
  return useQuery({
    queryKey: keys.products(search),
    queryFn: () =>
      api<Paginated<ProductListItem>>(
        `/products?pageSize=25&sort=name&order=asc${search ? `&search=${encodeURIComponent(search)}` : ''}`,
      ),
  });
}

/** Le stock par agence, avec le drapeau « stock faible » posé par le serveur. */
export function useStock(search: string) {
  return useQuery({
    queryKey: keys.stock(search),
    queryFn: () =>
      api<Paginated<StockListItem>>(
        `/stock?pageSize=25&sort=quantity&order=asc${search ? `&search=${encodeURIComponent(search)}` : ''}`,
      ),
  });
}

/**
 * Le détail d'un bon — V2 vendeur bloc 4, spécification §5.
 *
 * `GET /delivery-notes/:code` existe depuis C7 et est borné par `noteWhere()` :
 * un vendeur n'ouvre que ses bons. Aucune route neuve, donc aucun second
 * endroit où se tromper de portée.
 */
export function useDeliveryNote(code: string | undefined) {
  return useQuery({
    queryKey: keys.note(code ?? ''),
    queryFn: () => api<DeliveryNoteDetail>(`/delivery-notes/${encodeURIComponent(code ?? '')}`),
    enabled: Boolean(code),
  });
}

/**
 * LE DÉTAIL D'UN PRODUIT — V2 vendeur bloc 7, spécification §9.
 *
 * `GET /products/:id` existe et est borné par `warehouseWhere()` : un vendeur
 * n'ouvre que ses produits. Il rend aussi le stock PAR AGENCE, avec le drapeau
 * de seuil posé par le serveur — le recalculer ici ferait mentir l'écran dès
 * qu'une agence change sa valeur (D-019).
 */
export function useProduct(id: string | undefined) {
  return useQuery({
    queryKey: keys.product(id ?? ''),
    queryFn: () => api<ProductDetail>(`/products/${encodeURIComponent(id ?? '')}`),
    enabled: Boolean(id),
  });
}

/**
 * LES MOUVEMENTS D'UNE LIGNE DE STOCK — spécification §9, « historique ».
 *
 * Chaque mouvement porte sa quantité SIGNÉE et le solde APRÈS, figé : c'est ce
 * qui permet de relire l'historique sans le rejouer, et de voir tout de suite
 * qu'une sortie de douze a laissé trois.
 */
export function useStockLine(id: string | undefined) {
  return useQuery({
    queryKey: keys.stockLine(id ?? ''),
    queryFn: () => api<StockDetail>(`/stock/${encodeURIComponent(id ?? '')}`),
    enabled: Boolean(id),
  });
}

/**
 * CRÉER UN PRODUIT — V2 vendeur bloc 7, spécification §9.
 *
 * **Le `clientId` envoyé est ignoré par le serveur** pour un compte vendeur :
 * `resolveClient()` rend celui de la portée sans regarder le corps. On le
 * transmet quand même parce que le schéma partagé l'exige — et c'est le
 * serveur, pas cet appel, qui garantit qu'un vendeur ne crée pas le produit
 * d'un autre.
 */
export function useCreateProduct() {
  const client = useQueryClient();
  const { profile } = useSession();

  return useMutation({
    mutationFn: (input: { name: string; reference?: string; price: number | null }) =>
      api<ProductDetail>('/products', {
        method: 'POST',
        body: {
          clientId: profile?.clientId ?? '',
          name: input.name,
          reference: input.reference,
          price: input.price,
          attributeValueIds: [],
          isActive: true,
        },
      }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['merchant'] });
    },
  });
}

/**
 * MA SOCIÉTÉ — V2 vendeur bloc 9, spécification §7.
 *
 * `GET /merchant/me/company` ne prend AUCUN identifiant : la portée donne le
 * client. C'est ce qui la distingue de `/clients/:id`, qui sert
 * l'administration et qu'un vendeur ne doit jamais atteindre.
 */
export function useCompany() {
  return useQuery({
    queryKey: keys.company,
    queryFn: () => api<MerchantCompany>('/merchant/me/company'),
  });
}

/** Quatre champs, et pas un de plus — le serveur refuse le reste. */
export function useUpdateCompany() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (input: MerchantCompanyInput) =>
      api<MerchantCompany>('/merchant/me/company', { method: 'PATCH', body: input }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['merchant'] });
    },
  });
}
