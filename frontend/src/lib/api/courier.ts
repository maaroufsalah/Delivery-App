import type {
  CourierCheque,
  CourierMe,
  CourierPerformance,
  CourierPickupNote,
  CourierRoute,
  CourierBalance,
  CourierOpenRemittance,
  RemittanceDetail,
} from '@delivery/shared';
import { apiFetch } from '@/lib/http';

/**
 * L'ESPACE LIVREUR, côté web — L1 bloc 1.
 *
 * **Les mêmes endpoints que l'application mobile**, et c'est l'essentiel de la
 * décision : `/courier/me/*` sert déjà la tournée avec ses colis, leur rang,
 * le montant à encaisser et la commission figée. Le web aurait pu passer par
 * `GET /routes` — la route du back-office, bornée au livreur par la portée
 * serveur — mais elle rend un circuit SANS ses colis, et il aurait fallu un
 * appel par circuit pour reconstruire ce que l'autre donne d'un coup.
 *
 * Le bénéfice est plus grand qu'un appel économisé : les deux supports lisent
 * la même chose, donc ils affichent le même chiffre. Un livreur qui voit
 * « 3 à livrer » sur son téléphone et « 4 » sur le poste de l'agence ne fait
 * plus confiance à aucun des deux.
 *
 * Une fonction par endpoint, comme partout dans `src/lib/api`.
 */

/** `GET /courier/me` — profil, ville, agence, modèle de paie. */
export async function getCourierMe(): Promise<CourierMe> {
  return apiFetch<CourierMe>('/courier/me');
}

/**
 * `GET /courier/me/routes?status=…` — les circuits du livreur, colis compris.
 *
 * `active` : ce qui est parti, donc la journée en cours. `draft` : ce qui se
 * compose au scan, c'est-à-dire une notion de terrain que le web ne montre pas
 * — mais l'argument reste ouvert, l'API le sert. `closed` : l'onglet
 * HISTORIQUE du chantier « Retours, BL Clients Pro » (point 3), borné par
 * `from` — sans borne, l'historique d'un livreur à l'année ne descend pas sur
 * une 3G d'agence.
 */
export async function getCourierRoutes(
  status: 'draft' | 'active' | 'closed',
  from?: string,
): Promise<CourierRoute[]> {
  return apiFetch<CourierRoute[]>('/courier/me/routes', {
    query: from ? { status, from } : { status },
  });
}

/** `GET /courier/parcels/:code/outcomes` — les issues que le SERVEUR autorise. */
export async function getCourierOutcomes(code: string): Promise<number[]> {
  return apiFetch<number[]>(`/courier/parcels/${encodeURIComponent(code)}/outcomes`);
}

/**
 * `POST /courier/parcels/:code/status` — rapporter une issue.
 *
 * **La route du LIVREUR, pas celle du back-office.** `POST /parcels/:code/events`
 * existe et le rôle `COURIER` porte `parcels.status`, mais elle accepte toutes
 * les transitions du référentiel : le livreur y trouverait des statuts de
 * bureau — « annulé par le vendeur », « en stock » — qu'il n'a aucun moyen de
 * constater sur le pas d'une porte (D-074). La route livreur filtre les issues
 * par ce que `transitions.ts` autorise DEPUIS son état et POUR son rôle.
 */
export async function reportCourierOutcome(
  code: string,
  body: {
    statusId: number;
    comment?: string;
    collectedAmount?: number;
    scheduledAt?: string;
  },
): Promise<void> {
  await apiFetch(`/courier/parcels/${encodeURIComponent(code)}/status`, {
    method: 'POST',
    body,
  });
}

/* --- Les trois lectures ajoutées par L2 ----------------------------------- */

/**
 * `GET /courier/me/performance` — les huit chiffres de l'accueil.
 *
 * **Elle peut ne pas répondre, et c'est prévu.** C'est l'un des trois endpoints
 * du chantier L2, et la production ne les a pas encore. Les appelants la
 * passent à `courierFigures()`, qui rend `null` pour ce qu'on ne peut pas
 * afficher honnêtement — une tuile `null` disparaît, elle ne montre pas zéro.
 */
export async function getCourierPerformance(
  period: 'day' | 'month' = 'day',
): Promise<CourierPerformance> {
  return apiFetch<CourierPerformance>(`/courier/me/performance?period=${period}`);
}

/**
 * `GET /courier/me/notes?kind=…` — bons de ramassage et bons de dépôt.
 *
 * Le livreur y est relié par ses COLIS : un bon ne porte pas de `courierId`, et
 * ce lien est plus étroit qu'une permission — c'est pour cela qu'on n'a pas
 * ouvert `notes.read` au rôle (D-082).
 */
export async function getCourierNotes(kind: 'pickup' | 'dropoff'): Promise<CourierPickupNote[]> {
  return apiFetch<CourierPickupNote[]>(`/courier/me/notes?kind=${kind}`);
}

/** `GET /courier/me/cheques` — les chèques de ses relevés, et leur état. */
export async function getCourierCheques(): Promise<CourierCheque[]> {
  return apiFetch<CourierCheque[]>('/courier/me/cheques');
}

/**
 * SON BON OUVERT — chantier « Retour de fonds unique », point 7.
 *
 * Ce qu'il porte en ce moment. `null` quand il est à jour : une réponse, pas
 * une erreur. Aucun identifiant ne part d'ici — la portée le donne (D-082).
 *
 * Depuis D-243, une caisse qu'il a FERMÉE n'est plus rendue ici : elle se lit
 * par `GET /remittances?stage=closed`, borné au livreur par la portée.
 */
export async function getCourierOpenRemittance(): Promise<RemittanceDetail | null> {
  const { remittance } = await apiFetch<CourierOpenRemittance>('/courier/me/remittance');
  return remittance;
}

/**
 * `POST /courier/me/remittance/close` — IL CLÔTURE SA CAISSE (D-243).
 *
 * Fermer n'est pas attester : la caisse reste à compter et à confirmer par
 * l'agence, et sa prochaine livraison en ouvre une nouvelle. Aucun identifiant
 * ne part d'ici, la portée désigne la caisse. Refus : `no-open-remittance`,
 * `remittance-empty`, `remittance-already-closed`.
 */
export async function closeCourierRemittance(): Promise<RemittanceDetail | null> {
  const { remittance } = await apiFetch<CourierOpenRemittance>('/courier/me/remittance/close', {
    method: 'POST',
  });
  return remittance;
}

/**
 * SON solde — chantier « Finance », point 4.
 *
 * Aucun identifiant : la portée le donne (D-082). Le service ignore de toute
 * façon celui qu'on lui passerait.
 */
export async function getCourierOwnBalance(): Promise<CourierBalance> {
  return apiFetch<CourierBalance>('/courier/me/balance');
}
