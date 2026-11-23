import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { CourierRemittances } from '@/features/courier/courier-remittances';
import { getCourierOpenRemittance } from '@/lib/api/courier';
import { getRemittancePage } from '@/lib/api/finance';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courier.remittances');
  return { title: t('title') };
}

/**
 * Mes retours de fonds — C11 bloc 7 (D-068), étendu par le chantier « Retour
 * de fonds unique », point 7.
 *
 * L'écran répondait à « est-ce que c'est passé ? ». Il répond maintenant
 * d'abord à « qu'est-ce que je porte ? » — le bon OUVERT, avec ses colis et ce
 * qu'il faudra poser sur la table. C'est ce que la caisse du jour savait et ne
 * lui montrait jamais.
 *
 * **Trois lectures, une seule borne.** Le bon ouvert vient de
 * `/courier/me/remittance` qui ne porte aucun identifiant ; les caisses fermées
 * de `/remittances?stage=closed` et l'historique de
 * `/remittances?status=confirmed`, tous deux bornés au livreur par la portée
 * serveur. Aucun n'accepte qu'un écran passe un `courierId` (D-082, D-119).
 *
 * **Les caisses fermées (D-243)** : celles qu'il a clôturées et que l'agence
 * n'a pas encore réceptionnées. De la plus ancienne à la plus récente, l'ordre
 * dans lequel il les remettra. `/courier/me/remittance` ne les rend plus.
 *
 * **Les annulés n'apparaissent pas** : filtrer sur `confirmed` plutôt que
 * « tout sauf brouillon » évite de montrer au livreur un bon qui n'a jamais
 * porté d'argent.
 */
export default async function CourierRemittancesPage() {
  const [open, closed, history] = await Promise.all([
    // Best-effort, comme ses voisins : une lecture qui échoue vide le bloc,
    // elle ne casse pas l'écran.
    getCourierOpenRemittance().catch(() => null),
    getRemittancePage({ stage: 'closed', pageSize: 25, sort: 'createdAt', order: 'asc' }),
    getRemittancePage({ status: 'confirmed', pageSize: 25, sort: 'createdAt', order: 'desc' }),
  ]);

  return <CourierRemittances open={open} closed={closed} history={history} />;
}
