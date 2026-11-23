import type { ClaimDetail, ClaimListItem, Paginated } from '@delivery/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, UPLOAD_TIMEOUT_MS } from './api';

/**
 * LES RÉCLAMATIONS, POUR LES TROIS ESPACES — extrait de `merchant.ts` par le
 * complément de L2.
 *
 * Elles vivaient chez le marchand parce qu'il était seul à les ouvrir (M2
 * bloc 6). Depuis **B-19**, le livreur voit et ouvre celles qui portent sur
 * SES colis : garder les crochets sous `@/lib/merchant` aurait fait importer
 * « marchand » depuis un écran livreur, ce qui se lit comme une erreur même
 * quand c'en est une de rangement.
 *
 * **Aucun des crochets ne passe d'identité.** L'API borne seule :
 * `claimWhere()` rend au marchand les siennes, au livreur celles de ses colis,
 * au staff toute la société. Un paramètre ici donnerait l'illusion d'un
 * filtre, que le premier appel direct contournerait (D-082).
 */

const keys = {
  claims: ['claims'] as const,
  claim: (code: string) => ['claims', code] as const,
};

export function useClaims() {
  return useQuery({
    queryKey: keys.claims,
    queryFn: () => api<Paginated<ClaimListItem>>('/claims?pageSize=25'),
  });
}

export function useClaim(code: string | undefined) {
  return useQuery({
    queryKey: keys.claim(code ?? ''),
    queryFn: () => api<ClaimDetail>(`/claims/${encodeURIComponent(code ?? '')}`),
    enabled: Boolean(code),
  });
}

/** Répondre dans le fil d'une réclamation. */
export function useReplyClaim(code: string) {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (body: string) =>
      api<ClaimDetail>(`/claims/${encodeURIComponent(code)}/events`, {
        method: 'POST',
        // `isInternal` n'est même pas envoyé : un e-commerçant ne peut pas
        // écrire une note d'agence, et le lui offrir serait un piège.
        body: { body },
      }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.claim(code) });
      void client.invalidateQueries({ queryKey: keys.claims });
    },
  });
}

/** Ouvrir une réclamation, éventuellement sur un colis et avec une photo. */
export function useOpenClaim() {
  const client = useQueryClient();

  return useMutation({
    mutationFn: (input: {
      typeId: string;
      subject: string;
      message: string;
      parcelCode?: string;
      /*
       * `filename`, et non `name` : c'est le nom du champ dans
       * `claimAttachmentSchema`. Les types du serveur ne descendent pas
       * jusqu'ici, et une pièce jointe mal nommée aurait été refusée à
       * l'envoi, après que le vendeur a écrit son message.
       */
      attachments?: { filename: string; mimeType: string; content: string }[];
    }) =>
      api<ClaimDetail>('/claims', { method: 'POST', body: input, timeoutMs: UPLOAD_TIMEOUT_MS }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.claims });
    },
  });
}

/**
 * Les motifs de réclamation, tels que la société les a définis.
 *
 * Les motifs DÉSACTIVÉS sont retirés : ils restent en base pour que les
 * anciennes réclamations gardent leur libellé, mais en proposer un ouvrirait
 * une réclamation sur un motif que la société a retiré exprès.
 */
export interface ClaimType {
  id: string;
  label: string;
  /** `parcel` : le motif EXIGE un colis. Le service le vérifie. */
  subjectType: 'parcel' | 'general';
  isActive: boolean;
}

export function useClaimTypes() {
  return useQuery({
    queryKey: ['claims', 'types'] as const,
    queryFn: async () => {
      const rows = await api<ClaimType[]>('/claims/types');
      return rows.filter((row) => row.isActive);
    },
    staleTime: 60 * 60_000,
  });
}
