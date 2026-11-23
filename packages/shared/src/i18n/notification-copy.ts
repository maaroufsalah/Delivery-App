/**
 * Ce qu'une notification DIT, dans les trois langues — M2/M3.
 *
 * **PARTAGÉ depuis le V2 vendeur bloc 8**, et il vivait côté serveur avant.
 *
 * La raison d'origine tient toujours : c'est le système d'exploitation qui
 * affiche un push, souvent écran verrouillé, sans que l'application soit
 * lancée — le texte doit donc être écrit au MOMENT DE L'ENVOI, dans la langue
 * que l'appareil a déclarée. Ce chemin-là ne change pas.
 *
 * Ce qui change est le CENTRE de notifications : il stocke le sujet et ses
 * paramètres, jamais la phrase, pour que chaque lecteur voie la ligne dans SA
 * langue et qu'une correction de formulation s'applique rétroactivement. Les
 * écrans doivent donc pouvoir rendre le texte eux-mêmes — d'où le déplacement
 * ici, où le web, le mobile et l'API lisent le même catalogue.
 *
 * Recopier le catalogue dans `apps/mobile/src/i18n` aurait fait deux tables à
 * tenir d'accord, et le push aurait fini par dire autre chose que l'historique
 * du même événement.
 *
 * **Jamais de donnée personnelle dans le titre ni dans le corps.** Une
 * notification s'affiche sur un écran verrouillé, dans un taxi ou sur un
 * comptoir. Le code du colis suffit à savoir de quoi il s'agit ; le nom et le
 * téléphone du destinataire n'ont rien à y faire, et la conformité magasins
 * l'attend aussi (M1 : « pas de logs avec données personnelles »).
 */

import { formatMessage } from './plural';

export type NotificationLocale = 'fr' | 'ar' | 'en';

interface Copy {
  title: string;
  body: string;
}

type Catalogue = Record<string, Record<NotificationLocale, Copy>>;

export const NOTIFICATION_COPY: Catalogue = {
  /* --- Livreur --- */
  'route-assigned': {
    // Le nombre, au pluriel juste (Recette 5, point 8).
    fr: {
      title: 'Nouvelle tournée',
      body: '{count, plural, one {# colis vous est affecté.} other {# colis vous sont affectés.}}',
    },
    ar: {
      title: 'جولة جديدة',
      body: '{count, plural, one {أُسنِد إليك طرد واحد.} two {أُسنِد إليك طردان.} few {أُسنِدت إليك # طرود.} other {أُسنِد إليك # طردًا.}}',
    },
    en: {
      title: 'New run',
      body: '{count, plural, one {# parcel has been assigned to you.} other {# parcels have been assigned to you.}}',
    },
  },
  'route-withdrawn': {
    fr: { title: 'Circuit retiré', body: 'Le circuit {code} ne vous est plus affecté.' },
    ar: { title: 'تم سحب الجولة', body: 'لم تعد الجولة {code} مسندة إليك.' },
    en: { title: 'Run withdrawn', body: 'Run {code} is no longer assigned to you.' },
  },
  'return-assigned': {
    fr: {
      title: 'Retour à reprendre',
      body: 'Reprenez {code} chez le client en livrant l’échange {exchange}.',
    },
    ar: {
      title: 'مرتجع للاسترجاع',
      body: 'استرجع {code} من الزبون عند تسليم الاستبدال {exchange}.',
    },
    en: {
      title: 'Return to pick up',
      body: 'Pick up {code} from the customer when delivering exchange {exchange}.',
    },
  },
  'remittance-confirmed': {
    fr: { title: 'Remise confirmée', body: 'L’agence a compté votre remise {code}.' },
    ar: { title: 'تم تأكيد التسليم', body: 'أحصت الوكالة تسليمك {code}.' },
    en: { title: 'Remittance confirmed', body: 'The agency counted your remittance {code}.' },
  },

  'return-note-requested': {
    fr: {
      title: 'Retour demandé',
      body: 'L’agence vous demande de rapporter {count} colis — bon {code}.',
    },
    ar: {
      title: 'طلب إرجاع',
      body: 'تطلب منك الوكالة إعادة {count} طرد — السند {code}.',
    },
    en: {
      title: 'Return requested',
      body: 'The agency asks you to bring back {count} parcels — note {code}.',
    },
  },
  'return-note-received': {
    fr: { title: 'Retour reçu', body: 'L’agence a compté votre bon de retour {code}.' },
    ar: { title: 'تم استلام الإرجاع', body: 'أحصت الوكالة سند إرجاعك {code}.' },
    en: { title: 'Return received', body: 'The agency counted your return note {code}.' },
  },

  /*
   * AUCUNE ADRESSE DANS LE TEXTE. Une notification voyage en clair sur l'écran
   * de verrouillage d'un téléphone, et une adresse est une donnée personnelle —
   * c'est la règle que `notifications.service.spec.ts` tient depuis M2. Le
   * livreur ouvre le colis pour lire la nouvelle adresse ; ce que la
   * notification doit lui dire, c'est qu'elle a changé.
   */
  'parcel-destination-changed': {
    fr: {
      title: 'Adresse modifiée',
      body: 'La destination du colis {code} a changé. Ouvrez-le avant de partir.',
    },
    ar: {
      title: 'تغيّر العنوان',
      body: 'تغيّرت وجهة الطرد {code}. افتحه قبل الانطلاق.',
    },
    en: {
      title: 'Address changed',
      body: 'Parcel {code} has a new destination. Open it before you set off.',
    },
  },
  /* Lot I, I1 (D-211) : le colis change de livreur — celui qui le perd, celui qui le reçoit. */
  'parcel-transferred-out': {
    fr: {
      title: 'Colis transféré',
      body: '{count, plural, one {Le colis {code} a été transféré à {courier}.} other {# colis ont été transférés à {courier}.}}',
    },
    ar: {
      title: 'تم تحويل طرد',
      body: '{count, plural, one {تم تحويل الطرد {code} إلى {courier}.} two {تم تحويل طردين إلى {courier}.} few {تم تحويل # طرود إلى {courier}.} other {تم تحويل # طردًا إلى {courier}.}}',
    },
    en: {
      title: 'Parcel transferred',
      body: '{count, plural, one {Parcel {code} has been transferred to {courier}.} other {# parcels have been transferred to {courier}.}}',
    },
  },
  'parcel-transferred-in': {
    fr: {
      title: 'Colis reçu',
      body: '{count, plural, one {Le colis {code} vous a été transféré (circuit {route}).} other {# colis vous ont été transférés (circuit {route}).}}',
    },
    ar: {
      title: 'طرد جديد',
      body: '{count, plural, one {تم تحويل الطرد {code} إليك (الجولة {route}).} two {تم تحويل طردين إليك (الجولة {route}).} few {تم تحويل # طرود إليك (الجولة {route}).} other {تم تحويل # طردًا إليك (الجولة {route}).}}',
    },
    en: {
      title: 'Parcel received',
      body: '{count, plural, one {Parcel {code} has been transferred to you (run {route}).} other {# parcels have been transferred to you (run {route}).}}',
    },
  },
  /* Lot I, I2 (D-210) : un « Livré » posé par erreur, annulé par l'administrateur. */
  'delivery-cancelled': {
    fr: {
      title: 'Livraison annulée',
      body: 'La livraison du colis {code} a été annulée par l’agence.',
    },
    ar: { title: 'تم إلغاء التسليم', body: 'ألغت الوكالة تسليم الطرد {code}.' },
    en: {
      title: 'Delivery cancelled',
      body: 'The agency cancelled the delivery of parcel {code}.',
    },
  },

  /* --- E-commerçant --- */
  'parcel-delivery-cancelled': {
    fr: {
      title: 'Livraison annulée',
      body: 'La livraison du colis {code} a été annulée : il sera présenté de nouveau.',
    },
    ar: { title: 'تم إلغاء التسليم', body: 'أُلغي تسليم الطرد {code}: سيُعرض من جديد.' },
    en: {
      title: 'Delivery cancelled',
      body: 'The delivery of parcel {code} was cancelled: it will be presented again.',
    },
  },
  /*
   * Lot L, L1 (D-219) : transféré au livreur d'une autre ville, le colis y part.
   * La ville et non l'adresse (écran verrouillé, M2) ; les frais sont dits
   * recalculés, le montant se lit sur la fiche.
   */
  'parcel-rerouted': {
    fr: {
      title: 'Destination changée',
      body: 'Le colis {code} part désormais vers {city} : ses frais de livraison sont recalculés.',
    },
    ar: {
      title: 'تغيّرت الوجهة',
      body: 'سيتوجّه الطرد {code} الآن إلى {city}: أُعيد حساب رسوم التوصيل.',
    },
    en: {
      title: 'Destination changed',
      body: 'Parcel {code} now goes to {city}: its delivery fee has been recalculated.',
    },
  },
  /*
   * Recette 5, point 7 — le compteur ET le total (point 8). D-240 (10/11/2026) :
   * les non ramassés ne sont plus supprimés, ils restent en attente de ramassage.
   * Le sujet garde sa clé (`note-parcels-removed`) : réglages et historique la portent.
   */
  'note-parcels-removed': {
    fr: {
      title: 'Ramassage partiel',
      body: '{code} : {picked} ramassés sur {total}, {removed, plural, one {l’autre reste} other {les # autres restent}} en attente de ramassage.',
    },
    ar: {
      title: 'استلام جزئي',
      body: '{code}: {picked} مستلمة من أصل {total}، {removed, plural, one {والطرد الآخر يبقى} two {والطردان الآخران يبقيان} few {والطرود الـ# الأخرى تبقى} other {والطرود الـ# الأخرى تبقى}} في انتظار الاستلام.',
    },
    en: {
      title: 'Partial pickup',
      body: '{code}: {picked} picked up out of {total}, {removed, plural, one {the other one stays} other {the # others stay}} awaiting pickup.',
    },
  },
  // D-256 : supprimé par l'agence avant ramassage, motif à l'appui.
  'parcel-deleted': {
    fr: { title: 'Colis supprimé', body: '{code} a été supprimé par l’agence : {reason}' },
    ar: { title: 'تم حذف الطرد', body: 'حذفت الوكالة الطرد {code}: {reason}' },
    en: { title: 'Parcel deleted', body: '{code} was deleted by the agency: {reason}' },
  },
  'parcel-delivered': {
    fr: { title: 'Colis livré', body: '{code} a été livré.' },
    ar: { title: 'تم تسليم الطرد', body: 'تم تسليم {code}.' },
    en: { title: 'Parcel delivered', body: '{code} has been delivered.' },
  },
  'parcel-failed': {
    fr: { title: 'Livraison en échec', body: '{code} n’a pas pu être livré.' },
    ar: { title: 'فشل التسليم', body: 'تعذّر تسليم {code}.' },
    en: { title: 'Delivery failed', body: '{code} could not be delivered.' },
  },
  'parcel-postponed': {
    fr: { title: 'Livraison reportée', body: '{code} est reporté au {date}.' },
    ar: { title: 'تأجيل التسليم', body: 'أُجِّل {code} إلى {date}.' },
    en: { title: 'Delivery postponed', body: '{code} is postponed to {date}.' },
  },
  'invoice-issued': {
    fr: { title: 'Nouvelle facture', body: 'La facture {code} est disponible.' },
    ar: { title: 'فاتورة جديدة', body: 'الفاتورة {code} متاحة.' },
    en: { title: 'New invoice', body: 'Invoice {code} is available.' },
  },
  'claim-answered': {
    fr: { title: 'Réponse à votre réclamation', body: '{code} a reçu une réponse.' },
    ar: { title: 'رد على شكايتك', body: 'تلقّت {code} ردًّا.' },
    en: { title: 'Reply to your claim', body: '{code} has received a reply.' },
  },

  /*
   * Lot E, E1.5 (D-201) : « Réceptionné par Speedy » et « Récupéré par le
   * vendeur ». `{code}` est le bon BRB- quand la liasse en vient, sinon les
   * codes des colis ; `{shortName}`, le nom court de la société (D-189).
   */
  'bl-ready': {
    fr: {
      title: 'BL disponibles',
      body: '{count} BL réceptionnés par {shortName}, prêts à être récupérés — {code}.',
    },
    ar: {
      title: 'سندات جاهزة',
      body: '{count} سند استلمته {shortName}، جاهز للاسترجاع — {code}.',
    },
    en: {
      title: 'Delivery notes ready',
      body: '{count} notes received by {shortName}, ready to be collected — {code}.',
    },
  },
  'bl-returned': {
    fr: { title: 'BL récupérés', body: '{count} BL vous ont été remis par {shortName} — {code}.' },
    ar: { title: 'سندات مُسترجعة', body: '{count} سند سُلِّم إليك من {shortName} — {code}.' },
    en: {
      title: 'Delivery notes collected',
      body: '{count} notes were handed to you by {shortName} — {code}.',
    },
  },

  /* --- Agence --- */
  'cash-variance': {
    fr: { title: 'Écart de caisse', body: 'La remise {code} présente un écart.' },
    ar: { title: 'فرق في الصندوق', body: 'تسجّل التسليم {code} فرقًا.' },
    en: { title: 'Cash variance', body: 'Remittance {code} shows a variance.' },
  },
  'claim-opened': {
    fr: { title: 'Nouvelle réclamation', body: '{code} attend une réponse.' },
    ar: { title: 'شكاية جديدة', body: '{code} في انتظار رد.' },
    en: { title: 'New claim', body: '{code} is waiting for a reply.' },
  },
  'stale-route': {
    fr: { title: 'Circuit non clôturé', body: '{code} est ouvert depuis {hours} h.' },
    ar: { title: 'جولة غير مُقفلة', body: '{code} مفتوحة منذ {hours} ساعة.' },
    en: { title: 'Route still open', body: '{code} has been open for {hours} h.' },
  },
  'return-note-awaiting': {
    fr: {
      title: 'Retour à compter',
      body: 'Le bon {code} attend d’être reçu — {count} colis.',
    },
    ar: {
      title: 'إرجاع بانتظار الإحصاء',
      body: 'السند {code} ينتظر الاستلام — {count} طرد.',
    },
    en: {
      title: 'Return to count',
      body: 'Note {code} is waiting to be received — {count} parcels.',
    },
  },
  'bl-note-awaiting': {
    fr: {
      title: 'Liasse de BL à compter',
      body: 'Le bon BL {code} attend d’être reçu — {count} BL.',
    },
    ar: {
      title: 'حزمة سندات بانتظار الإحصاء',
      body: 'الحزمة {code} تنتظر الاستلام — {count} سند.',
    },
    en: {
      title: 'Delivery-note bundle to count',
      body: 'BL note {code} is waiting to be received — {count} notes.',
    },
  },
};

/**
 * Le texte d'un sujet, dans une langue, avec ses variables remplacées.
 *
 * Repli sur le français si la langue de l'appareil est inconnue : un livreur
 * doit lire du français lisible, jamais `parcel-delivered`.
 */
export function notificationCopy(
  topic: string,
  locale: string,
  params: Record<string, string | number> = {},
): Copy | null {
  const entry = NOTIFICATION_COPY[topic];
  if (!entry) return null;

  const copy = entry[locale as NotificationLocale] ?? entry.fr;

  // `{nom}` comme avant, et `{count, plural, …}` selon la langue — Recette 5,
  // point 8 : « 1 colis », « 50 colis », « طردان ».
  const language = entry[locale as NotificationLocale] ? locale : 'fr';
  return {
    title: formatMessage(copy.title, params, language),
    body: formatMessage(copy.body, params, language),
  };
}
