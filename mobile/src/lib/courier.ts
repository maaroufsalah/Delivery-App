import type {
  AnnouncementListItem,
  CourierDeliveries,
  CourierMe,
  CourierPerformance,
  CourierCheque,
  CourierPickupNote,
  CourierRoute,
  ParcelDetail,
  ParcelListItem,
  CourierOpenRemittance,
  RemittanceListItem,
  BlNoteDetail,
  BlNoteListItem,
  DeliverySlipListItem,
  ParcelBlTracking,
  ReturnHandoverDetail,
  ReturnHandoverListItem,
  ReturnParcelLine,
  ReturnPendingGroup,
  CourierStatementListItem,
  Paginated,
  ScanResult,
  CourierBalance,
} from '@delivery/shared';
import {
  BULK_JOBS_HEADER,
  type BulkJob,
  type BulkJobAccepted,
  courierOutcomesFor,
} from '@delivery/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, ApiError, UPLOAD_TIMEOUT_MS } from './api';
import { enqueue, newKey, pending as pendingQueue } from './queue';
import { assertCanCloseRemittance } from './remittance-close';

/**
 * Les appels de l'espace livreur — M1.
 *
 * **Une clé de cache par ressource**, et les invalidations sont explicites : un
 * scan change la tournée, un rapport d'issue change la tournée ET le colis. Les
 * laisser au `refetchOnWindowFocus` ferait payer un aller-retour à chaque
 * ouverture de clavier, sur une 3G d'agence.
 */

export const keys = {
  me: ['courier', 'me'] as const,
  routes: (status: 'draft' | 'active' | 'closed') => ['courier', 'routes', status] as const,
  /* Chantier « Retours, BL Clients Pro », point 3. */
  handovers: ['courier', 'handovers'] as const,
  handover: (code: string) => ['courier', 'handover', code] as const,
  route: (code: string) => ['courier', 'route', code] as const,
  outcomes: (code: string) => ['courier', 'outcomes', code] as const,
  /* La fiche d'un colis (`GET /parcels/:code`) — invalidée avec les issues. */
  parcel: (code: string) => ['courier', 'parcel', code] as const,
  remittances: ['courier', 'remittances'] as const,
  /* Chantier « Retour de fonds unique », point 7. */
  openRemittance: ['courier', 'remittance', 'open'] as const,
  /* D-243 — sous `remittances` : une invalidation de l'historique la relit aussi. */
  closedRemittances: ['courier', 'remittances', 'closed'] as const,
  statements: ['courier', 'statements'] as const,
};

export function useMe() {
  return useQuery({
    queryKey: keys.me,
    queryFn: () => api<CourierMe>('/courier/me'),
    // Le profil ne change pas d'une tournée à l'autre : une heure évite de le
    // redemander à chaque passage sur l'écran Profil.
    staleTime: 60 * 60 * 1000,
  });
}

export function useRoutes(status: 'draft' | 'active' | 'closed', from?: string) {
  return useQuery({
    queryKey: [...keys.routes(status), from ?? ''] as const,
    queryFn: () =>
      api<CourierRoute[]>(
        `/courier/me/routes?status=${status}${from ? `&from=${encodeURIComponent(from)}` : ''}`,
      ),
  });
}

/**
 * LES CIRCUITS DE RETOUR du livreur — lot C, point C2 (D-179). Demandés à part
 * (`kind=return`) : la liste ordinaire n'en porte pas, et c'est voulu.
 *
 * Sous `['courier', 'routes', …]`, comme les autres : chaque issue rapportée
 * invalide ce préfixe, et le compteur « X sur Y traités » suit. Le filtre sur
 * le type protège d'une API plus ancienne qui ignorerait le paramètre.
 */
export function useReturnRoutes() {
  return useQuery({
    queryKey: ['courier', 'routes', 'return'] as const,
    queryFn: () => api<CourierRoute[]>('/courier/me/routes?status=active&kind=return'),
    select: (routes) => routes.filter((route) => route.kind === 'return'),
  });
}

/**
 * LA LISTE UNIQUE DU MENU LIVRAISON — `GET /courier/me/deliveries`.
 *
 * Tous les colis à livrer, circuits de livraison partis confondus, et le
 * compteur « X livrés / Y ». C'est le SERVEUR qui décide de ce qui y figure
 * (non livré, sur aucun bon de retour actif) : l'écran n'en retire rien.
 *
 * Sous `['courier', 'routes', …]` : chaque issue rapportée, chaque scan et
 * chaque départ invalident ce préfixe, et la liste suit sans y penser.
 */
export function useDeliveries() {
  return useQuery({
    queryKey: ['courier', 'routes', 'deliveries'] as const,
    queryFn: () => api<CourierDeliveries>('/courier/me/deliveries'),
  });
}

export function useRoute(code: string | undefined) {
  return useQuery({
    queryKey: keys.route(code ?? ''),
    queryFn: () => api<CourierRoute>(`/courier/me/routes/${encodeURIComponent(code ?? '')}`),
    enabled: Boolean(code),
  });
}

/**
 * Les issues permises d'un colis — `GET /courier/parcels/:code/outcomes`.
 *
 * `statusId` (lot C, point C3) : le statut que l'écran connaît déjà (ligne du
 * circuit, carte du scan, fiche). Le serveur rend `courierOutcomesFor(statut)`,
 * la même règle partagée : le téléphone l'applique en PROVISOIRE pendant que
 * la requête part. Sans cela, le panneau s'ouvrait vide, grandissait à
 * l'arrivée de la réponse, et le bouton glissait sous le doigt — le « deux
 * appuis ». La réponse du serveur remplace le provisoire dès qu'elle arrive.
 */
export function useOutcomes(code: string | undefined, statusId?: number, kind?: string) {
  return useQuery({
    queryKey: keys.outcomes(code ?? ''),
    queryFn: () => api<number[]>(`/courier/parcels/${encodeURIComponent(code ?? '')}/outcomes`),
    enabled: Boolean(code),
    // Le TYPE du circuit ouvre la révision d'une livraison (liste unique) :
    // sans lui le provisoire montrerait moins que ce que le serveur rendra.
    placeholderData: statusId === undefined ? undefined : courierOutcomesFor(statusId, kind),
  });
}

export function useRemittances() {
  return useQuery({
    queryKey: keys.remittances,
    // Les caisses REÇUES par l'agence (« reçue » ou « validée », D-243) : la
    // caisse ouverte et les fermées ont leurs propres blocs, et une annulée n'a
    // jamais porté d'argent — la montrer au livreur n'apprendrait rien.
    queryFn: () =>
      api<Paginated<RemittanceListItem>>('/remittances?status=confirmed&pageSize=25&order=desc'),
  });
}

/**
 * SON BON OUVERT — chantier « Retour de fonds unique », point 7.
 *
 * Ce qu'il porte en ce moment : les colis, l'encaissé, et ce qu'il devra poser
 * sur la table. `null` quand il est à jour — une réponse, pas une erreur.
 *
 * Aucun identifiant ne part d'ici : la portée le donne (D-082).
 */
export function useOpenRemittance() {
  return useQuery({
    queryKey: keys.openRemittance,
    queryFn: async () => (await api<CourierOpenRemittance>('/courier/me/remittance')).remittance,
  });
}

/**
 * SES CAISSES FERMÉES — D-243. Clôturées par lui, pas encore reçues par
 * l'agence : l'argent qu'il doit encore poser sur la table. La plus ancienne en
 * premier, dans l'ordre où l'agence les réceptionnera.
 */
export function useClosedRemittances() {
  return useQuery({
    queryKey: keys.closedRemittances,
    queryFn: () =>
      api<Paginated<RemittanceListItem>>('/remittances?stage=closed&pageSize=25&order=asc'),
  });
}

/**
 * CLÔTURER SA CAISSE — D-243.
 *
 * **Un geste EN LIGNE, sans repli hors ligne** : contrairement aux gestes de
 * terrain plus bas, un échec réseau remonte tel quel à l'écran. Rien n'est mis
 * en file : une clôture rejouée plus tard fermerait la caisse d'un autre
 * moment de la journée.
 *
 * **Refusée tant que la file hors ligne n'est pas vide** : la règle
 * (`remittance-close.ts`) est vérifiée ICI, juste avant l'appel, pour qu'aucun
 * écran ne puisse l'oublier. La file est relue à cet instant, pas devinée
 * depuis un compteur affiché.
 */
export function useCloseRemittance() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: async () => {
      assertCanCloseRemittance((await pendingQueue()).length);

      return (await api<CourierOpenRemittance>('/courier/me/remittance/close', { method: 'POST' }))
        .remittance;
    },
    onSuccess: () => {
      // La carte « ce que je porte » se vide tout de suite, sans attendre la
      // relecture ; la caisse rejoint la liste des fermées.
      client.setQueryData(keys.openRemittance, null);
      void client.invalidateQueries({ queryKey: keys.openRemittance });
      void client.invalidateQueries({ queryKey: keys.remittances });
    },
  });
}

export function useStatements() {
  return useQuery({
    queryKey: keys.statements,
    queryFn: () => api<Paginated<CourierStatementListItem>>('/courier-statements?pageSize=25'),
  });
}

/* --- Les gestes, avec repli HORS LIGNE ------------------------------------ */

/** Ce qu'un geste rend quand il n'a pas pu partir : mis en file, pas perdu. */
export interface Deferred {
  queued: true;
}

export function isDeferred(result: unknown): result is Deferred {
  return typeof result === 'object' && result !== null && 'queued' in result;
}

/**
 * Le SCAN — M1, D-073 et D-077.
 *
 * Hors ligne, le geste part en file et l'écran avance quand même : un livreur
 * qui charge sa moto dans un parking souterrain ne doit pas attendre le réseau
 * pour poser un colis dans son sac.
 *
 * **Un refus MÉTIER remonte tel quel.** « Déjà pris par Youssef » est une
 * information dont le livreur a besoin TOUT DE SUITE, debout dans l'agence —
 * la mettre en file la lui livrerait le soir, quand le colis est déjà parti.
 */
export function useScan() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: async (code: string): Promise<ScanResult | Deferred> => {
      // La clé part dès le PREMIER envoi et suit le geste en file (point 7).
      const idempotencyKey = newKey('scan');
      try {
        return await api<ScanResult>('/courier/me/scan', {
          method: 'POST',
          body: { code },
          idempotencyKey,
        });
      } catch (error) {
        if (!ApiError.isDeferrable(error)) throw error;

        await enqueue({
          kind: 'scan',
          path: '/courier/me/scan',
          body: { code },
          label: code,
          idempotencyKey,
        });
        return { queued: true };
      }
    },
    onSuccess: (outcome) => {
      /*
       * LA LIGNE APPARAÎT TOUT DE SUITE — lot C, point C1. La réponse porte le
       * brouillon à jour : il remplace celui du cache avant la relecture, et le
       * colis s'ajoute au bas de la liste du scanner sans attendre le réseau.
       */
      if (!isDeferred(outcome)) {
        client.setQueriesData<CourierRoute[]>({ queryKey: keys.routes('draft') }, (routes) =>
          routes
            ? [outcome.route, ...routes.filter((route) => route.code !== outcome.route.code)]
            : routes,
        );
      }
      void client.invalidateQueries({ queryKey: keys.routes('draft') });
      invalidateParcelScreens(client);
    },
  });
}

/**
 * « LE PRENDRE DANS MA TOURNÉE » — lot I, I1 (D-211). Le scan a répondu que le
 * colis est sur la tournée d'un autre livreur, et qu'il est transférable ; le
 * livreur a confirmé. Le même `POST /courier/me/scan`, avec `transfer: true`
 * et une clé d'idempotence (D-152) : un double appui ne transfère qu'une fois.
 *
 * **En ligne seulement**, comme le ramassage d'un bon : prendre le colis d'un
 * collègue est une décision qui se confirme tout de suite, debout devant lui —
 * pas un geste qu'on rejoue le soir.
 */
export function useTransferScan() {
  const client = useQueryClient();

  return useMutation({
    // Lot L (D-219) : la ville choisie quand le colis va hors de ses villes.
    mutationFn: ({
      code,
      destinationCityId,
    }: {
      code: string;
      destinationCityId?: string;
    }): Promise<ScanResult> =>
      api<ScanResult>('/courier/me/scan', {
        method: 'POST',
        body: { code, transfer: true, ...(destinationCityId ? { destinationCityId } : {}) },
        idempotencyKey: newKey('transfer'),
      }),
    onSuccess: () => {
      // Le colis peut rejoindre la tournée EN COURS comme le brouillon.
      void client.invalidateQueries({ queryKey: keys.routes('draft') });
      void client.invalidateQueries({ queryKey: keys.routes('active') });
      invalidateParcelScreens(client);
    },
  });
}

/**
 * CE QUE TOUT GESTE CHANGE SUR LA FICHE — retours recette 26/09, point 6.
 *
 * Un scan, un départ de circuit, un ramassage de bon ou une issue changent le
 * statut du colis, donc les ISSUES qu'un livreur peut rapporter. La fiche
 * gardait pourtant ses issues 30 s en cache, sans jamais les redemander : un
 * colis ouvert avant le départ montrait encore « pas en main » après. Une
 * seule fonction, appelée par les quatre mutations, pour que la cinquième ne
 * l'oublie pas.
 */
function invalidateParcelScreens(client: ReturnType<typeof useQueryClient>) {
  void client.invalidateQueries({ queryKey: ['courier', 'outcomes'] });
  void client.invalidateQueries({ queryKey: ['courier', 'parcel'] });
}

export function useDispatch() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (code: string) =>
      api<CourierRoute>(`/courier/me/routes/${encodeURIComponent(code)}/dispatch`, {
        method: 'POST',
      }),
    onSuccess: () => {
      // Le brouillon devient une tournée active : les deux listes changent.
      void client.invalidateQueries({ queryKey: ['courier', 'routes'] });
      invalidateParcelScreens(client);
    },
  });
}

/**
 * LE MÊME RÉSULTAT POUR LES COLIS COCHÉS — chantier « Sélection en masse sur
 * les circuits » (D-146). `POST /routes/:code/outcomes` : l'API rejoue l'action
 * unitaire colis par colis, en une transaction, et borne le livreur à SES
 * circuits et à SES issues.
 *
 * **Pas de file hors-ligne** : un geste sur deux cents colis dont on ne connaît
 * pas l'issue ne se rejoue pas à l'aveugle. Sans réseau, l'écran le dit, et le
 * livreur recommence au retour du réseau.
 */
export function useBulkOutcome(code: string) {
  const client = useQueryClient();

  return useMutation({
    /*
     * Lot W2 (D-239), E2E-001 : au-delà de 300 colis, l'API répond 202 et une
     * TÂCHE par paquets (l'en-tête dit que l'application sait la suivre) ; en
     * dessous, le résultat d'avant. L'écran suit la tâche (`useBulkJob`).
     */
    mutationFn: (input: { parcelIds: string[]; statusId: number }) =>
      api<{ count: number; cashExpected: number } | BulkJobAccepted>(
        `/routes/${encodeURIComponent(code)}/outcomes`,
        {
          method: 'POST',
          body: input,
          timeoutMs: 60_000,
          headers: { [BULK_JOBS_HEADER]: '1' },
        },
      ),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['courier', 'routes'] });
      invalidateParcelScreens(client);
    },
  });
}

/**
 * UNE TÂCHE DE MASSE EN COURS — lot W2 (D-239). Relue toutes les 2 s tant
 * qu'elle travaille ; terminée, les écrans du livreur se relisent. `null` :
 * rien à suivre.
 */
export function useBulkJob(id: string | null) {
  const client = useQueryClient();

  return useQuery({
    queryKey: ['courier', 'bulk-job', id],
    enabled: id !== null,
    queryFn: async () => {
      const job = await api<BulkJob>(`/bulk-jobs/${encodeURIComponent(id ?? '')}`);
      if (job.status === 'done') {
        void client.invalidateQueries({ queryKey: ['courier', 'routes'] });
        invalidateParcelScreens(client);
      }
      return job;
    },
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === 'done' || status === 'failed' ? false : 2_000;
    },
  });
}

/**
 * LE LIVREUR VALIDE UN BON — retouches Anass, point 5.
 *
 * « Validation au choix : par scan (contrôle que les colis appartiennent au
 * bon, alerte sinon) ou manuellement (cocher les colis). »
 *
 * **Une seule mutation pour les deux gestes.** Le scan et la case à cocher
 * produisent la même chose — une liste de colis qu'on a en main — et la
 * distinction vit dans l'écran, pas ici. En faire deux aurait dédoublé le
 * contrôle d'appartenance, qui est précisément ce qui compte.
 *
 * **Pas de file hors ligne** (`queue.ts`), à la différence du scan de tournée :
 * la réponse du serveur EST le contrôle — « ce colis n'appartient pas à ce
 * bon » — et un livreur qui apprendrait deux heures plus tard qu'il a chargé le
 * carton du voisin serait déjà reparti.
 */
export function usePickupNote(routeCode: string) {
  const client = useQueryClient();

  return useMutation({
    mutationFn: ({
      noteId,
      parcelIds,
      removeUnpicked,
    }: {
      noteId: string;
      parcelIds: string[];
      /** Recette 5, point 5 : les non cochés sont retirés du bon (après confirmation). */
      removeUnpicked?: boolean;
    }) =>
      api<unknown>(
        `/courier/me/routes/${encodeURIComponent(routeCode)}/notes/${encodeURIComponent(noteId)}/pickup`,
        { method: 'POST', body: { parcelIds, removeUnpicked } },
      ),
    onSuccess: () => {
      // Le bon ET les colis changent d'état : les deux listes de tournées
      // portent les deux.
      void client.invalidateQueries({ queryKey: ['courier', 'routes'] });
      void client.invalidateQueries({ queryKey: ['courier', 'route'] });
      invalidateParcelScreens(client);
    },
  });
}

export function useRemoveParcel(routeCode: string) {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (parcelId: string) =>
      api<CourierRoute>(`/courier/me/routes/${encodeURIComponent(routeCode)}/parcels/${parcelId}`, {
        method: 'DELETE',
      }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.routes('draft') });
    },
  });
}

export interface ReportInput {
  statusId: number;
  comment?: string;
  collectedAmount?: number;
  /**
   * LE PAIEMENT MIXTE — chantier « Finance », point 8.
   *
   * Seul le CHÈQUE voyage : les espèces sont la différence, et le serveur la
   * calcule. Envoyer les deux aurait laissé deux sources s'écarter sur le
   * réseau d'un sous-sol, et c'est le montant encaissé qui en aurait souffert.
   */
  chequeAmount?: number;
  chequeNumber?: string;
  chequeBank?: string;
  scheduledAt?: string;
  latitude?: number;
  longitude?: number;
  /** Lot E, E1.1 (D-201) : « BL récupéré » coché avec « Livrer ». */
  blCollected?: boolean;
  blReference?: string;
}

/**
 * Rapporter une issue — M1, D-074 et D-077.
 *
 * Hors ligne, le geste part en file. C'est LE cas d'usage de la file : un
 * livreur devant une porte, dans une cage d'escalier, n'a pas de réseau et ne
 * peut pas attendre d'en avoir pour dire « livré ».
 */
export function useReport(parcelCode: string) {
  const client = useQueryClient();

  return useMutation({
    mutationFn: async (input: ReportInput): Promise<CourierRoute | Deferred> => {
      const path = `/courier/parcels/${encodeURIComponent(parcelCode)}/status`;
      const idempotencyKey = newKey('status');

      try {
        return await api<CourierRoute>(path, { method: 'POST', body: input, idempotencyKey });
      } catch (error) {
        if (!ApiError.isDeferrable(error)) throw error;

        await enqueue({ kind: 'status', path, body: input, label: parcelCode, idempotencyKey });
        return { queued: true };
      }
    },
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['courier', 'routes'] });
      void client.invalidateQueries({ queryKey: keys.outcomes(parcelCode) });
      void client.invalidateQueries({ queryKey: keys.parcel(parcelCode) });
      // Lot E, E1.1 : un BL a pu naître avec la livraison.
      void client.invalidateQueries({ queryKey: ['courier', 'bl'] });
    },
  });
}

/**
 * La photo de preuve — M1, D-074.
 *
 * Elle suit le rapport dans la file, et donc dans l'ordre : l'API la refuse si
 * elle arrive avant l'événement qu'elle documente. C'est pour cela que la file
 * rejoue une entrée à la fois (D-077).
 */
export function useProof(parcelCode: string) {
  return useMutation({
    mutationFn: async (proof: { name: string; mimeType: string; content: string }) => {
      const path = `/courier/parcels/${encodeURIComponent(parcelCode)}/proof`;
      const idempotencyKey = newKey('proof');

      try {
        return await api<{ attached: true }>(path, {
          method: 'POST',
          body: { proof },
          timeoutMs: UPLOAD_TIMEOUT_MS,
          idempotencyKey,
        });
      } catch (error) {
        if (!ApiError.isDeferrable(error)) throw error;

        await enqueue({ kind: 'proof', path, body: { proof }, label: parcelCode, idempotencyKey });
        return { queued: true } as const;
      }
    },
  });
}

export function useCheque() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (body: Record<string, unknown>) =>
      api<CourierStatementListItem>('/courier/me/cheques', {
        method: 'POST',
        body,
        timeoutMs: UPLOAD_TIMEOUT_MS,
      }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.statements });
    },
  });
}

/* --- Ce que M4 ajoute : l'accueil, la recherche, la fiche ----------------- */

/**
 * LES ACTUALITÉS — M4 bloc 2, sur un endpoint qui existait déjà.
 *
 * `GET /announcements/feed` ne porte AUCUNE permission : il rend « les
 * actualités qui concernent ce compte », bornées par la portée serveur. Le
 * livreur y a donc droit sans qu'on ouvre quoi que ce soit — c'est le seul des
 * trois libellés de menu demandés (Retours, Réclamations, Actualités) qui
 * trouve un endpoint ouvert au rôle.
 *
 * **L'échec est SILENCIEUX et la section disparaît.** Si une installation plus
 * ancienne ne servait pas cette route, un livreur verrait une erreur rouge sur
 * son accueil pour une information d'agrément. `retry: false` : une route
 * absente ne devient pas présente à la deuxième tentative.
 */
export function useAnnouncements(limit = 3) {
  return useQuery({
    queryKey: ['courier', 'announcements', limit] as const,
    queryFn: () => api<AnnouncementListItem[]>(`/announcements/feed?limit=${limit}`),
    retry: false,
    staleTime: 10 * 60 * 1000,
  });
}

/**
 * LA RECHERCHE DE COLIS — M4 bloc 3, sans endpoint neuf.
 *
 * `GET /parcels` porte `parcels.read`, que le rôle `COURIER` possède, et
 * `ParcelsService.scoped()` applique `scopeWhere()` : pour un livreur, le
 * `where` porte `courierId = le sien`. Un livreur ne peut donc chercher que
 * dans SES colis — **D-082 est tenu par le serveur**, pas par cet écran.
 *
 * C'est aussi ce qui rend l'OTA sûr : aucune fonctionnalité de ce chantier
 * n'attend un déploiement d'API.
 */
export function useParcelSearch(query: {
  search?: string;
  statusId?: number;
  /**
   * Une FAMILLE de statuts — L2 bloc 2.
   *
   * La puce « En retour » de la maquette 3 n'est pas un statut : le référentiel
   * en compte plusieurs dans le groupe `return` (DOMAIN.md §2.1), et le livreur
   * les vit tous comme « ça repart ». Filtrer sur un seul en cacherait les
   * autres. `GET /parcels` sait déjà filtrer par groupe ; rien de neuf côté API.
   */
  statusGroup?: string;
}) {
  const params = new URLSearchParams({ pageSize: '25' });
  if (query.search) params.set('search', query.search);
  if (query.statusId) params.set('statusId', String(query.statusId));
  if (query.statusGroup) params.set('statusGroup', query.statusGroup);

  return useQuery({
    queryKey: [
      'courier',
      'parcels',
      query.search ?? '',
      query.statusId ?? 0,
      query.statusGroup ?? '',
    ] as const,
    queryFn: () => api<Paginated<ParcelListItem>>(`/parcels?${params.toString()}`),
    // La recherche se retape : garder l'ancien résultat pendant la frappe
    // évite le clignotement d'une liste qui se vide entre deux lettres.
    placeholderData: (previous) => previous,
  });
}

/**
 * LES HUIT CHIFFRES DE L'ACCUEIL — L2 bloc 1, maquette 2.
 *
 * **La seule lecture de ce chantier qui attend un déploiement d'API**, et elle
 * est écrite pour ne pas l'attendre : `retry: false` sur un 404, et l'accueil
 * retombe sur ce qu'il calculait déjà depuis la tournée (`dayStats`). Tant que
 * la production n'a pas la route, le livreur voit ses cinq compteurs et son
 * encaissé comme avant ; il ne voit ni « à remettre » ni le taux, et rien ne
 * casse. C'est la règle de la mission : **404 → section masquée, jamais de
 * plantage**.
 *
 * Une minute de fraîcheur : le taux bouge à chaque issue rapportée, et une
 * valeur d'il y a un quart d'heure ferait douter le livreur de son écran.
 */
export function usePerformance(period: 'day' | 'month' = 'day') {
  return useQuery({
    queryKey: ['courier', 'performance', period] as const,
    queryFn: () => api<CourierPerformance>(`/courier/me/performance?period=${period}`),
    retry: false,
    staleTime: 60 * 1000,
  });
}

/**
 * LES COLIS À RETOURNER — L2 bloc 5, maquette 8, premier volet.
 *
 * `GET /returns/pending` porte `returns.read`, que le rôle `COURIER` possède, et
 * la portée serveur le borne à SES colis. **Aucun endpoint neuf** : ce volet-là
 * fonctionne sur l'API qui tourne aujourd'hui, contrairement aux deux autres.
 *
 * La route rend des GROUPES par livreur. Pour un livreur il n'y en a qu'un —
 * le sien — mais on aplatit plutôt que de prendre `[0]` : une réponse vide
 * n'est pas une erreur, et un groupe d'un autre livreur ne peut pas arriver.
 */
export function useReturnsPending() {
  return useQuery({
    queryKey: ['courier', 'returns', 'pending'] as const,
    queryFn: () => api<ReturnPendingGroup[]>('/returns/pending'),
  });
}

/**
 * LES CHÈQUES — L2 bloc 5, maquette 9, deuxième volet.
 *
 * Troisième et dernier endpoint du chantier, non déployé : `retry: false`, et
 * le volet affiche un état vide qui le dit.
 */
export function useCheques() {
  return useQuery({
    queryKey: ['courier', 'cheques'] as const,
    queryFn: () => api<CourierCheque[]>('/courier/me/cheques'),
    retry: false,
  });
}

/**
 * LES BONS DE RAMASSAGE ET DE DÉPÔT — L2 blocs 4 et 5.
 *
 * `GET /courier/me/notes` est le deuxième des trois endpoints du chantier, et
 * la production ne l'a pas encore : `retry: false`, et l'écran affiche un état
 * vide qui le dit plutôt qu'une erreur. Un livreur ne doit jamais voir un
 * message technique parce qu'une agence n'a pas encore déployé.
 *
 * `kind` discrimine : `pickup` pour la maquette 7, `dropoff` pour le troisième
 * volet des retours (maquette 8). Le même endpoint, parce que ce sont les mêmes
 * objets — des `DeliveryNote` (DOMAIN.md §1.12).
 */
export function usePickups(kind: 'pickup' | 'dropoff') {
  return useQuery({
    queryKey: ['courier', 'notes', kind] as const,
    queryFn: () => api<CourierPickupNote[]>(`/courier/me/notes?kind=${kind}`),
    retry: false,
  });
}

/**
 * LA FICHE d'un colis, avec son historique — M4 bloc 3.
 *
 * `GET /parcels/:code` rend `events` déjà trié du plus récent au plus ancien :
 * la timeline n'a rien à réordonner, et un tri côté application aurait été une
 * seconde vérité à maintenir.
 */
export function useParcelDetail(code: string | undefined) {
  return useQuery({
    queryKey: keys.parcel(code ?? ''),
    queryFn: () => api<ParcelDetail>(`/parcels/${encodeURIComponent(code ?? '')}`),
    enabled: Boolean(code),
    retry: false,
  });
}

/* -------------------------------------------------------------------------- */
/* Bons de retour colis — chantier « Retours, BL Clients Pro », points 1 à 3   */
/* -------------------------------------------------------------------------- */

/**
 * SES bons de retour — ceux qu'il a ouverts ET ceux que l'admin lui demande.
 *
 * **Aucun `courierId` n'est passé**, et c'est la même règle que partout dans
 * cet espace : la portée du jeton borne `/return-handovers` au livreur de la
 * session. Le passer laisserait croire que le téléphone choisit ce qu'il a le
 * droit de voir.
 */
export function useHandovers(status?: string) {
  return useQuery({
    queryKey: [...keys.handovers, status ?? ''] as const,
    queryFn: () =>
      api<Paginated<ReturnHandoverListItem>>(
        `/return-handovers?page=1&pageSize=50&order=desc${status ? `&status=${status}` : ''}`,
      ),
    retry: false,
  });
}

export function useHandover(code: string | undefined) {
  return useQuery({
    queryKey: keys.handover(code ?? ''),
    queryFn: () => api<ReturnHandoverDetail>(`/return-handovers/${encodeURIComponent(code ?? '')}`),
    enabled: Boolean(code),
    retry: false,
  });
}

/**
 * Les colis que CE livreur peut rapporter — la liste de composition.
 *
 * **Aucun `courierId` n'est passé** : la portée du jeton le donne. Le passer
 * laisserait croire qu'on peut en passer un AUTRE — et l'API l'ignore désormais
 * pour un livreur, précisément pour que la question ne se pose plus (D-082).
 */
export function useHandoverCandidates() {
  return useQuery({
    queryKey: ['courier', 'handover-candidates'] as const,
    queryFn: () => api<ReturnParcelLine[]>('/return-handovers/candidates'),
    retry: false,
  });
}

/**
 * LES QUATRE GESTES DU LIVREUR, sous une seule mutation.
 *
 * Ouvrir un bon, y ajouter des colis, le prendre en charge, le remettre. Ils
 * partagent leur invalidation — la liste et le détail — et la séparer en quatre
 * hooks aurait multiplié par quatre les endroits où l'oublier.
 *
 * **La prise en charge et la remise ne passent PAS par la file hors ligne.**
 * Elles engagent la marchandise de quelqu'un d'autre : les rejouer plus tard,
 * après que l'agence a compté, produirait une attestation datée d'un moment où
 * le livreur n'avait plus rien en main. La file sert le rapport d'issue, où
 * l'inverse est vrai — le livreur EST devant le client (D-074).
 */
export function useHandoverActions() {
  const client = useQueryClient();

  const invalidate = (code?: string) => {
    void client.invalidateQueries({ queryKey: keys.handovers });
    if (code) void client.invalidateQueries({ queryKey: keys.handover(code) });
    void client.invalidateQueries({ queryKey: ['courier', 'returns', 'pending'] });
    void client.invalidateQueries({ queryKey: ['courier', 'handover-candidates'] });
  };

  /*
   * L'OUVERTURE D'UN BON PAR LE LIVREUR — chantier « Bon de retour livreur »,
   * point 1.
   *
   * **Le corps ne porte QUE des colis.** Le livreur et son agence viennent de
   * sa fiche, côté serveur : même forme que `POST /merchant/me/pickups`, et
   * même raison (D-082). `parcelIds` n'est pas facultatif : l'API refuse un bon
   * de livreur sans colis (point 3), et le téléphone n'a aucune raison d'en
   * proposer un vide.
   */
  const create = useMutation({
    mutationFn: (input: { parcelIds: string[] }) =>
      api<ReturnHandoverDetail>('/return-handovers', { method: 'POST', body: input }),
    onSuccess: (handover) => invalidate(handover.code),
  });

  const addParcels = useMutation({
    mutationFn: (input: { code: string; parcelIds: string[] }) =>
      api<ReturnHandoverDetail>(
        `/return-handovers/${encodeURIComponent(input.code)}/parcels/batch`,
        { method: 'POST', body: { parcelIds: input.parcelIds } },
      ),
    onSuccess: (handover) => invalidate(handover.code),
  });

  const pickup = useMutation({
    mutationFn: (input: { code: string; parcelCodes?: string[] }) =>
      api<ReturnHandoverDetail>(`/return-handovers/${encodeURIComponent(input.code)}/pickup`, {
        method: 'POST',
        body: input.parcelCodes ? { parcelCodes: input.parcelCodes } : {},
      }),
    onSuccess: (handover) => invalidate(handover.code),
  });

  const handOver = useMutation({
    mutationFn: (code: string) =>
      api<ReturnHandoverDetail>(`/return-handovers/${encodeURIComponent(code)}/handover`, {
        method: 'POST',
      }),
    onSuccess: (handover) => invalidate(handover.code),
  });

  const cancel = useMutation({
    mutationFn: (code: string) =>
      api<ReturnHandoverDetail>(`/return-handovers/${encodeURIComponent(code)}/cancel`, {
        method: 'POST',
        body: {},
      }),
    onSuccess: (handover) => invalidate(handover.code),
  });

  return { create, addParcels, pickup, handOver, cancel };
}

/* -------------------------------------------------------------------------- */
/* BL — chantier « Retours, BL Clients Pro », point 5                          */
/* -------------------------------------------------------------------------- */

/**
 * LE BL D'UN COLIS — la demande, et le papier s'il est revenu.
 *
 * `retry: false` : un colis sans BL rend une réponse valide (`requested:
 * false`), donc un échec ici est un vrai échec — inutile de le rejouer trois
 * fois sur une 3G d'agence.
 */
export function useParcelBl(code: string | undefined) {
  return useQuery({
    queryKey: ['courier', 'bl', 'parcel', code ?? ''] as const,
    queryFn: () => api<ParcelBlTracking>(`/bl/parcels/${encodeURIComponent(code ?? '')}`),
    enabled: Boolean(code),
    retry: false,
  });
}

/**
 * « BL RÉCUPÉRÉ ? » — la question posée à la livraison.
 *
 * **Pas de file hors ligne**, contrairement au rapport d'issue (D-074). Le
 * rapport dit ce que le livreur a VU, et le rejouer plus tard reste vrai ; un
 * BL, lui, est un papier qui change de main, et l'admin le comptera. Une
 * attestation rejouée après le comptage dirait que la feuille était là quand
 * elle n'y était pas.
 */
export function useCollectBl(parcelCode: string) {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (input: { collected: boolean; reference?: string }) =>
      api<ParcelBlTracking>(`/bl/parcels/${encodeURIComponent(parcelCode)}/collect`, {
        method: 'POST',
        body: input,
      }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['courier', 'bl'] });
      void client.invalidateQueries({ queryKey: ['courier', 'parcel', parcelCode] });
    },
  });
}

/** Les BL en sacoche, groupables dans un bon — bornés au livreur par la portée. */
export function useCollectableSlips(clientId?: string) {
  return useQuery({
    queryKey: ['courier', 'bl', 'slips', clientId ?? ''] as const,
    queryFn: () =>
      api<DeliverySlipListItem[]>(
        `/bl/slips${clientId ? `?clientId=${encodeURIComponent(clientId)}` : ''}`,
      ),
    retry: false,
  });
}

/**
 * SES BL, tous états — lot E, E1 (D-201) : le volet Retours > « Bons de
 * livraison ». La même liste que l'admin et le vendeur (`GET /bl/delivery-slips`),
 * bornée au livreur par la portée du jeton. Les cent plus récents : c'est ce
 * qu'il a en sacoche et ce qui vient d'en sortir, pas une archive.
 */
export function useDeliverySlips() {
  return useQuery({
    queryKey: ['courier', 'bl', 'delivery-slips'] as const,
    queryFn: () => api<Paginated<DeliverySlipListItem>>('/bl/delivery-slips?pageSize=100'),
    retry: false,
  });
}

/** Ses bons BL — ceux qu'il a ouverts, bornés par la portée du jeton. */
export function useBlNotes() {
  return useQuery({
    queryKey: ['courier', 'bl', 'notes'] as const,
    queryFn: () => api<Paginated<BlNoteListItem>>('/bl/notes?page=1&pageSize=50&order=desc'),
    retry: false,
  });
}

export function useBlNote(code: string | undefined) {
  return useQuery({
    queryKey: ['courier', 'bl', 'note', code ?? ''] as const,
    queryFn: () => api<BlNoteDetail>(`/bl/notes/${encodeURIComponent(code ?? '')}`),
    enabled: Boolean(code),
    retry: false,
  });
}

/**
 * LES DEUX GESTES DU LIVREUR sur un bon BL : l'ouvrir avec sa liasse, la
 * remettre. Recevoir et rendre appartiennent à l'agence — le rôle `COURIER` ne
 * porte pas `returns.confirm`, et un bouton qui échoue toujours est une
 * promesse fausse.
 */
export function useBlNoteActions() {
  const client = useQueryClient();

  const invalidate = () => {
    void client.invalidateQueries({ queryKey: ['courier', 'bl'] });
  };

  const create = useMutation({
    mutationFn: (input: { clientId: string; agencyId: string; slipIds: string[] }) =>
      api<BlNoteDetail>('/bl/notes', { method: 'POST', body: input }),
    onSuccess: invalidate,
  });

  const handOver = useMutation({
    mutationFn: (code: string) =>
      api<BlNoteDetail>(`/bl/notes/${encodeURIComponent(code)}/handover`, { method: 'POST' }),
    onSuccess: invalidate,
  });

  return { create, handOver };
}

/**
 * SON SOLDE — chantier « Finance », point 4.
 *
 * Aucun identifiant n'est passé : la portée du jeton le donne (D-082). Une
 * dette qu'on ne voit pas est une dette qu'on conteste le jour où elle est
 * retenue sur une paie.
 */
export function useCourierBalance() {
  return useQuery({
    queryKey: ['courier', 'balance'] as const,
    queryFn: () => api<CourierBalance>('/courier/me/balance'),
    retry: false,
  });
}
