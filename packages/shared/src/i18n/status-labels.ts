import type { ParcelStatusKey } from '../statuses';

/**
 * Les libellés de STATUT, dans les trois langues — M1, docs/DECISIONS.md D-076.
 *
 * **Pourquoi ceux-là et pas tout le catalogue.** Le web porte deux mille clés
 * typées sur `messages/fr.json` ; les faire voyager dans un paquet partagé
 * imposerait au téléphone le vocabulaire de l'entrepôt, des tarifs et de la
 * facturation — tout ce que l'espace livreur du web s'était justement employé
 * à ne pas embarquer (D-068).
 *
 * Les statuts, eux, sont du DOMAINE. `statuses.ts` en tient déjà la liste, les
 * couleurs et les familles ; leurs libellés lui appartiennent au même titre.
 * Un colis « Reporté » doit se lire pareil sur le web et sur le téléphone,
 * sinon un livreur et un agent parlent de deux choses en croyant parler de la
 * même.
 *
 * **Le web garde son catalogue**, inchangé : sa typage sur `fr.json` est ce
 * qui fait échouer le build à la première clé manquante, et le démonter pour
 * ce bloc aurait coûté plus qu'il ne rapporte. `status-labels.spec.ts` vérifie
 * que les deux disent la même chose, et rougit sinon.
 */

export const I18N_LOCALES = ['fr', 'ar', 'en'] as const;
export type I18nLocale = (typeof I18N_LOCALES)[number];

export const STATUS_LABELS: Record<I18nLocale, Record<ParcelStatusKey, string>> = {
  fr: {
    'awaiting-pickup': 'Attente de ramassage',
    'awaiting-arrival': "En attente d'arrivée",
    'picked-up': 'Colis ramassé',
    delivered: 'Livré',
    returned: 'Retourné',
    refused: 'Refusé',
    shipped: 'Expédié',
    'out-of-zone': 'Hors zone',
    unreachable: 'Injoignable',
    'wrong-number': 'Numéro erroné',
    'no-answer-1': '1er appel sans réponse',
    'shipping-in-progress': "En cours d'expédition",
    'out-for-delivery': 'En cours de livraison',
    postponed: 'Reporté',
    'awaiting-delivery': 'En attente de livraison',
    'pickup-in-progress': 'En cours de ramassage',
    'picked-up-by-courier': 'Ramassé par le livreur',
    'cancelled-by-seller': 'Annulé par le vendeur',
    'awaiting-return': 'En attente de retour',
    'recovery-in-progress': 'En cours de récupération',
    'recovered-by-courier': 'Récupéré par le livreur',
    'return-collected': 'Retour récupéré',
    'return-in-progress': 'En cours de retour',
    voicemail: 'Boîte vocale',
    'customer-travelling': 'Client en voyage',
    'cancelled-by-customer': 'Annulé par le client',
    'already-delivered': 'Client déjà livré',
    'not-interested': 'Pas intéressé',
    'in-stock': 'En stock',
    'no-answer-2': '2e appel sans réponse',
    'no-answer-3': '3e appel sans réponse',
    'no-answer-1-sms': '1er appel sans réponse + SMS',
    'no-answer-2-sms': '2e appel sans réponse + SMS',
    'wrong-destination': 'Envoi erroné (faute destination)',
    scheduled: 'Programmé',
  },
  ar: {
    'awaiting-pickup': 'في انتظار الاستلام',
    'awaiting-arrival': 'في انتظار الوصول',
    'picked-up': 'تم استلام الطرد',
    delivered: 'تم التسليم',
    returned: 'مُرجَع',
    refused: 'مرفوض',
    shipped: 'تم الشحن',
    'out-of-zone': 'خارج النطاق',
    unreachable: 'تعذّر الاتصال',
    'wrong-number': 'رقم خاطئ',
    'no-answer-1': 'الاتصال الأول بدون رد',
    'shipping-in-progress': 'قيد الشحن',
    'out-for-delivery': 'قيد التسليم',
    postponed: 'مؤجَّل',
    'awaiting-delivery': 'في انتظار التسليم',
    'pickup-in-progress': 'قيد الاستلام',
    'picked-up-by-courier': 'تم الاستلام من طرف الموزّع',
    'cancelled-by-seller': 'ألغاه البائع',
    'awaiting-return': 'في انتظار الإرجاع',
    'recovery-in-progress': 'قيد الاسترجاع',
    'recovered-by-courier': 'استرجعه الموزّع',
    'return-collected': 'تم تسلّم المرتجع',
    'return-in-progress': 'قيد الإرجاع',
    voicemail: 'البريد الصوتي',
    'customer-travelling': 'الزبون مسافر',
    'cancelled-by-customer': 'ألغاه الزبون',
    'already-delivered': 'الزبون تسلّم الطرد سابقاً',
    'not-interested': 'غير مهتم',
    'in-stock': 'في المخزن',
    'no-answer-2': 'الاتصال الثاني بدون رد',
    'no-answer-3': 'الاتصال الثالث بدون رد',
    'no-answer-1-sms': 'الاتصال الأول بدون رد + رسالة',
    'no-answer-2-sms': 'الاتصال الثاني بدون رد + رسالة',
    'wrong-destination': 'وجهة خاطئة',
    scheduled: 'مُبرمَج',
  },
  en: {
    'awaiting-pickup': 'Awaiting pickup',
    'awaiting-arrival': 'Awaiting arrival',
    'picked-up': 'Picked up',
    delivered: 'Delivered',
    returned: 'Returned',
    refused: 'Refused',
    shipped: 'Shipped',
    'out-of-zone': 'Out of zone',
    unreachable: 'Unreachable',
    'wrong-number': 'Wrong number',
    'no-answer-1': '1st call, no answer',
    'shipping-in-progress': 'Shipping in progress',
    'out-for-delivery': 'Out for delivery',
    postponed: 'Postponed',
    'awaiting-delivery': 'Awaiting delivery',
    'pickup-in-progress': 'Pickup in progress',
    'picked-up-by-courier': 'Picked up by courier',
    'cancelled-by-seller': 'Cancelled by seller',
    'awaiting-return': 'Awaiting return',
    'recovery-in-progress': 'Recovery in progress',
    'recovered-by-courier': 'Recovered by courier',
    'return-collected': 'Return collected',
    'return-in-progress': 'Return in progress',
    voicemail: 'Voicemail',
    'customer-travelling': 'Customer travelling',
    'cancelled-by-customer': 'Cancelled by customer',
    'already-delivered': 'Already delivered',
    'not-interested': 'Not interested',
    'in-stock': 'In stock',
    'no-answer-2': '2nd call, no answer',
    'no-answer-3': '3rd call, no answer',
    'no-answer-1-sms': '1st call, no answer + SMS',
    'no-answer-2-sms': '2nd call, no answer + SMS',
    'wrong-destination': 'Wrong destination',
    scheduled: 'Scheduled',
  },
};

/** Le libellé d'un statut, avec repli sur le français. */
export function statusLabel(key: ParcelStatusKey, locale: I18nLocale): string {
  return STATUS_LABELS[locale][key] ?? STATUS_LABELS.fr[key];
}
