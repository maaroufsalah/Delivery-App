import type {
  ClaimDetail,
  ClaimListItem,
  DeliveryNoteListItem,
  Paginated,
  ParcelDetail,
  ParcelListItem,
  RemittanceDetail,
  RemittanceListItem,
  StaffAlerts,
} from '@delivery/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';

/**
 * Ce que l'espace du STAFF demande à l'API — M3.
 *
 * Le troisième de la famille, après `courier.ts` et `merchant.ts`, et le plus
 * pauvre des trois : un agent sur un téléphone ne refait pas le back-office. Il
 * traite ce qui cloche, debout, entre deux comptoirs — d'où des alertes, une
 * recherche et des gestes de validation, et rien d'autre.
 *
 * **Une seule route neuve dans tout M3** (`/staff/me/alerts`). Tout le reste
 * passe par les endpoints du back-office, avec leurs permissions existantes :
 * c'est ce qui garantit qu'un agent ne peut pas faire depuis son téléphone ce
 * que la matrice lui refuse sur le web.
 */

export const keys = {
  alerts: (agencyId?: string) => ['staff', 'alerts', agencyId ?? 'mine'] as const,
  dashboard: (agencyId?: string) => ['staff', 'dashboard', agencyId ?? 'mine'] as const,
  search: (query: string) => ['staff', 'search', query] as const,
  parcel: (code: string) => ['staff', 'parcel', code] as const,
  agencies: ['staff', 'agencies'] as const,
};

/** Les cinq familles d'alertes, filtrées par les clés du compte (D-080). */
export function useAlerts(agencyId?: string) {
  const query = agencyId ? `?agencyId=${encodeURIComponent(agencyId)}` : '';

  return useQuery({
    queryKey: keys.alerts(agencyId),
    queryFn: () => api<StaffAlerts>(`/staff/me/alerts${query}`),
    // Une minute : ce sont des alertes, pas un flux. Les rafraîchir à la
    // seconde ferait tourner la radio pour un chiffre qui bouge à l'heure.
    staleTime: 60_000,
  });
}

interface DashboardKpis {
  todayCount: number;
  deliveredCount: number;
  successRate: number;
  codToRemit: number;
}

/**
 * Les KPI du jour.
 *
 * On ne lit que `kpis` de la réponse — le reste (séries journalières,
 * répartition par ville, dix derniers colis) sert des graphiques que cet écran
 * n'affiche pas. C'est du gaspillage assumé : ajouter un paramètre au endpoint
 * du back-office pour un écran mobile aurait touché le web pour rien.
 */
export function useDashboard(agencyId?: string) {
  const query = agencyId ? `?agencyId=${encodeURIComponent(agencyId)}` : '';

  return useQuery({
    queryKey: keys.dashboard(agencyId),
    queryFn: async () => {
      const payload = await api<{ kpis: DashboardKpis }>(`/dashboard${query}`);
      return payload.kpis;
    },
    staleTime: 60_000,
  });
}

/**
 * La recherche UNIVERSELLE — code, téléphone, nom.
 *
 * Le serveur cherche déjà sur les sept champs à la fois (code, code externe,
 * destinataire, téléphone, description, nom du client, ville). Un seul champ de
 * saisie suffit donc, et c'est ce qu'un agent au comptoir a le temps de taper
 * pendant qu'un client lui parle.
 */
export function useSearch(query: string) {
  const needle = query.trim();

  return useQuery({
    queryKey: keys.search(needle),
    queryFn: () =>
      api<Paginated<ParcelListItem>>(`/parcels?search=${encodeURIComponent(needle)}&pageSize=20`),
    // Deux caractères ne cherchent rien d'utile et ramèneraient la moitié de la
    // base : on attend d'avoir de quoi discriminer.
    enabled: needle.length >= 3,
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
 * Changer le statut d'un colis.
 *
 * **Pas de file hors-ligne, contrairement au livreur.** Un agent est à
 * l'agence, sur le wifi ; et surtout son geste dépend de ce qu'il voit à
 * l'écran — la liste des transitions possibles vient du statut courant. Un
 * changement rejoué plus tard partirait d'un statut qui a pu bouger entre
 * temps, et la machine à états le refuserait au pire moment.
 */
export function useChangeStatus(code: string) {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (input: { statusId: number; comment?: string; scheduledDate?: string }) =>
      api(`/parcels/${encodeURIComponent(code)}/events`, { method: 'POST', body: input }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.parcel(code) });
      void client.invalidateQueries({ queryKey: ['staff', 'search'] });
      void client.invalidateQueries({ queryKey: ['staff', 'alerts'] });
    },
  });
}

/** Une note d'agence. Elle ne descend PAS chez l'e-commerçant (M2 bloc 4). */
export function useAddNote(code: string) {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (body: string) =>
      api(`/parcels/${encodeURIComponent(code)}/notes`, { method: 'POST', body: { body } }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.parcel(code) });
    },
  });
}

export interface Agency {
  id: string;
  name: string;
}

/**
 * Les agences joignables.
 *
 * `GET /directory/agencies` ne rend que la sienne à un compte rattaché, et
 * toutes à un super-admin : le sélecteur disparaît donc de lui-même là où il
 * n'aurait qu'une entrée. La règle vit au serveur, pas dans cet écran.
 */
export function useAgencies() {
  return useQuery({
    queryKey: keys.agencies,
    queryFn: () => api<Agency[]>('/directory/agencies'),
    staleTime: 24 * 60 * 60_000,
  });
}

/* -------------------------------------------------------------------------- */
/* Traitement — M3 bloc 8                                                     */
/* -------------------------------------------------------------------------- */

/** Les bons de ramassage que des clients ont demandés. */
export function usePendingNotes() {
  return useQuery({
    queryKey: ['staff', 'pending-notes'] as const,
    queryFn: () =>
      api<Paginated<DeliveryNoteListItem>>(
        '/delivery-notes?kind=pickup&status=awaiting&pageSize=50',
      ),
  });
}

/**
 * Valider un bon : l'agence atteste être passée prendre la marchandise.
 *
 * Le geste qui fait passer les colis en « ramassé ». C'est précisément celui
 * qu'un e-commerçant ne peut pas faire (D-082), et la symétrie est le produit :
 * celui qui remet n'atteste pas la remise.
 */
export function useValidateNote() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (code: string) =>
      api(`/delivery-notes/${encodeURIComponent(code)}/validate`, { method: 'POST' }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['staff'] });
    },
  });
}

/** Les remises de fonds à compter, et celles qui présentent un écart. */
export function useRemittances(status: 'draft' | 'confirmed') {
  return useQuery({
    queryKey: ['staff', 'remittances', status] as const,
    queryFn: () =>
      api<Paginated<RemittanceListItem>>(`/remittances?status=${status}&pageSize=50&order=desc`),
  });
}

export function useRemittance(code: string | undefined) {
  return useQuery({
    queryKey: ['staff', 'remittance', code ?? ''] as const,
    queryFn: () => api<RemittanceDetail>(`/remittances/${encodeURIComponent(code ?? '')}`),
    enabled: Boolean(code),
  });
}

/**
 * Compter la remise, puis la confirmer.
 *
 * **Deux appels, et deux permissions.** `PATCH /count` demande `finance.write`,
 * `POST /confirm` demande `finance.confirm`. Un agent qui compte n'est pas
 * forcément celui qui atteste — la matrice peut les séparer, et l'écran doit
 * suivre plutôt que de supposer.
 *
 * **Le motif est obligatoire dès qu'il y a un écart**, et la règle est dans le
 * schéma partagé : `expectedNet` repart avec le montant déclaré pour que la
 * vérification se fasse au même endroit des deux côtés.
 */
export function useCountRemittance(code: string) {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (input: { declaredAmount: number; expectedNet: number; varianceReason?: string }) =>
      api(`/remittances/${encodeURIComponent(code)}/count`, { method: 'PATCH', body: input }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['staff'] });
    },
  });
}

export function useConfirmRemittance(code: string) {
  const client = useQueryClient();

  return useMutation({
    mutationFn: () => api(`/remittances/${encodeURIComponent(code)}/confirm`, { method: 'POST' }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['staff'] });
    },
  });
}

/** Les réclamations ouvertes, côté agence : tout le fil, notes internes comprises. */
export function useStaffClaims() {
  return useQuery({
    queryKey: ['staff', 'claims'] as const,
    queryFn: () => api<Paginated<ClaimListItem>>('/claims?pageSize=50'),
  });
}

export function useStaffClaim(code: string | undefined) {
  return useQuery({
    queryKey: ['staff', 'claim', code ?? ''] as const,
    queryFn: () => api<ClaimDetail>(`/claims/${encodeURIComponent(code ?? '')}`),
    enabled: Boolean(code),
  });
}

/**
 * Répondre, et éventuellement clôturer — en UN appel.
 *
 * `replyClaimSchema` accepte les deux à la fois, et c'est le geste le plus
 * courant : on répond en clôturant. L'imposer en deux appels ferait deux lignes
 * d'historique pour un seul acte.
 *
 * `isInternal` est exposé ici, et seulement ici : une note d'agence ne descend
 * pas chez l'e-commerçant, et c'est l'API qui la filtre de son fil.
 */
export function useReplyStaffClaim(code: string) {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (input: { body: string; status?: string; isInternal?: boolean }) =>
      api(`/claims/${encodeURIComponent(code)}/events`, { method: 'POST', body: input }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['staff', 'claim', code] });
      void client.invalidateQueries({ queryKey: ['staff', 'claims'] });
      void client.invalidateQueries({ queryKey: ['staff', 'alerts'] });
    },
  });
}
