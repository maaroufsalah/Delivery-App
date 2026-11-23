/**
 * Les sujets de notification — M2/M3.
 *
 * Un référentiel, comme les statuts et les permissions : c'est lui qui nomme,
 * l'API qui envoie, le mobile qui affiche la liste des préférences. Écrire les
 * sujets en dur des deux côtés aurait donné un écran de réglages qui propose
 * des cases sans effet, ou qui en oublie.
 *
 * **Le sujet dit CE QUI ARRIVE, pas qui le reçoit.** `parcel-delivered` part au
 * marchand ; `remittance-confirmed` au livreur. Le destinataire se déduit de
 * l'événement, il n'est pas un réglage — un livreur ne peut pas s'abonner aux
 * factures d'un client.
 */

/** À qui un sujet s'adresse. Sert à bâtir l'écran de réglages, pas à router. */
export type NotificationAudience = 'courier' | 'merchant' | 'staff';

export interface NotificationTopicDefinition {
  key: string;
  audience: NotificationAudience;
  /**
   * Un sujet CRITIQUE ne se coupe pas.
   *
   * Un livreur qui désactiverait « nouveau colis affecté » ne verrait plus
   * arriver son travail, et croirait l'application cassée. Ces deux-là restent
   * visibles dans les réglages — pour qu'on sache qu'ils existent — mais leur
   * interrupteur est verrouillé.
   */
  critical?: boolean;
}

export const NOTIFICATION_TOPICS: readonly NotificationTopicDefinition[] = [
  /* --- Livreur --- */
  { key: 'route-assigned', audience: 'courier', critical: true },
  /*
   * LE BROUILLON RETIRÉ — D-173 (lot B, point B2). Le livreur voit ses
   * brouillons « À démarrer » sur son téléphone : supprimé ou confié à un
   * autre, il doit le savoir. Critique, comme son pendant `route-assigned`.
   */
  { key: 'route-withdrawn', audience: 'courier', critical: true },
  /*
   * LE RETOUR D'UN ÉCHANGE SUIT SON ECH- — D-178 (lot C, point C4). Quand
   * l'ECH- part avec un livreur, le RET- associé lui est confié : il le
   * reprend chez le client au moment de l'échange. Critique : c'est du travail.
   */
  { key: 'return-assigned', audience: 'courier', critical: true },
  { key: 'remittance-confirmed', audience: 'courier' },
  /*
   * LE RETOUR DEMANDÉ — chantier « Retours, BL Clients Pro », points 2 et 7.
   *
   * « Le bon apparaît immédiatement chez le livreur (web + mobile). »
   * « Immédiatement » ne tient que si on le lui DIT : un bon qui attend qu'il
   * pense à ouvrir un onglet n'est pas une demande, c'est un espoir.
   *
   * CRITIQUE, comme la tournée affectée et pour la même raison : c'est du
   * TRAVAIL qu'on lui confie. Un livreur qui couperait ce sujet ne verrait plus
   * arriver ce qu'on lui demande et croirait l'application cassée.
   */
  { key: 'return-note-requested', audience: 'courier', critical: true },
  /* L'agence a compté ce qu'il a rapporté — la contrepartie de sa remise. */
  { key: 'return-note-received', audience: 'courier' },
  /*
   * L'ADRESSE A CHANGÉ — chantier « Finance », point 7.
   *
   * CRITIQUE : un livreur qui roule vers l'ancienne adresse fait le trajet pour
   * rien, et le destinataire attend chez lui. C'est du travail qui change sous
   * ses roues ; le laisser couper ce sujet reviendrait à le laisser se tromper
   * de porte en silence.
   */
  { key: 'parcel-destination-changed', audience: 'courier', critical: true },
  /*
   * LE COLIS CHANGE DE LIVREUR — lot I, I1 (D-211). Critiques tous deux : l'un
   * perd une course de sa tournée, l'autre en reçoit une. C'est du travail qui
   * change de mains ; le couper ferait chercher un colis qui n'est plus là.
   */
  { key: 'parcel-transferred-out', audience: 'courier', critical: true },
  { key: 'parcel-transferred-in', audience: 'courier', critical: true },
  /* Lot I, I2 (D-210) : sa livraison est annulée — le colis est de nouveau à livrer. */
  { key: 'delivery-cancelled', audience: 'courier', critical: true },

  /* --- E-commerçant --- */
  { key: 'parcel-delivered', audience: 'merchant' },
  { key: 'parcel-delivery-cancelled', audience: 'merchant' },
  /*
   * SON COLIS CHANGE DE VILLE — lot L, L1 (D-219). Un transfert vers le livreur
   * d'une autre ville change sa destination ET ses frais : critique, c'est son
   * argent et la promesse faite à son client.
   */
  { key: 'parcel-rerouted', audience: 'merchant', critical: true },
  { key: 'parcel-failed', audience: 'merchant' },
  { key: 'parcel-postponed', audience: 'merchant' },
  { key: 'invoice-issued', audience: 'merchant' },
  { key: 'claim-answered', audience: 'merchant', critical: true },
  /*
   * SES BL SONT À L'AGENCE — point 5, et le seul sujet BL adressé au vendeur.
   *
   * Il part à la RÉCEPTION et non à la remise : à la remise, le vendeur a les
   * papiers en main, il n'a plus rien à apprendre. C'est le moment où ils sont
   * prêts à être récupérés qui l'intéresse.
   */
  { key: 'bl-ready', audience: 'merchant' },
  // Lot E, E1.5 (D-201) : ses BL lui ont été rendus — « Récupéré par le vendeur ».
  { key: 'bl-returned', audience: 'merchant' },
  /*
   * DES COLIS NON REMIS ONT QUITTÉ SON BON — Recette 5, point 7. Critique : le
   * vendeur doit savoir combien ont été ramassés et que les autres restent en
   * attente de ramassage, hors du bon (« 150 ramassés sur 200, les 50 autres
   * restent en attente de ramassage » — D-240 : plus aucune suppression).
   */
  { key: 'note-parcels-removed', audience: 'merchant', critical: true },
  /*
   * SON COLIS SUPPRIMÉ PAR L'AGENCE — D-256. Critique : un colis qu'il a
   * saisi disparaît de sa liste, il doit savoir pourquoi (le motif suit).
   */
  { key: 'parcel-deleted', audience: 'merchant', critical: true },

  /* --- Agence --- */
  { key: 'cash-variance', audience: 'staff' },
  { key: 'claim-opened', audience: 'staff' },
  { key: 'stale-route', audience: 'staff' },
  /*
   * CE QUI ATTEND SUR LE QUAI — points 1 et 5.
   *
   * Un livreur dépose une pile et repart ; personne ne la compte tant que
   * personne ne sait qu'elle est là. Ces deux sujets-là sont l'état « en
   * attente de réception » rendu visible — sans eux, il ne serait qu'une ligne
   * de plus dans une liste qu'on ouvre quand on y pense.
   *
   * Ils partent sous `returns.confirm` : celui qui ne peut pas confirmer n'a
   * rien à faire d'une alerte qui lui demande de confirmer (D-080).
   */
  { key: 'return-note-awaiting', audience: 'staff' },
  { key: 'bl-note-awaiting', audience: 'staff' },
] as const;

export type NotificationTopic = (typeof NOTIFICATION_TOPICS)[number]['key'];

const BY_KEY = new Map(NOTIFICATION_TOPICS.map((topic) => [topic.key, topic]));

/**
 * Les clés de sujet, en union FERMÉE — L2 bloc 9.
 *
 * Le web les résout en traductions typées : sans l'union, un sujet ajouté sans
 * son libellé s'afficherait sous son identifiant, en production.
 */
export type NotificationTopicKey =
  | 'route-assigned'
  | 'route-withdrawn'
  | 'return-assigned'
  | 'remittance-confirmed'
  | 'return-note-requested'
  | 'return-note-received'
  | 'parcel-destination-changed'
  | 'parcel-transferred-out'
  | 'parcel-transferred-in'
  | 'delivery-cancelled'
  | 'parcel-delivered'
  | 'parcel-delivery-cancelled'
  | 'parcel-rerouted'
  | 'parcel-failed'
  | 'parcel-postponed'
  | 'invoice-issued'
  | 'claim-answered'
  | 'bl-ready'
  | 'bl-returned'
  | 'note-parcels-removed'
  | 'cash-variance'
  | 'claim-opened'
  | 'stale-route'
  | 'return-note-awaiting'
  | 'bl-note-awaiting';

export function findNotificationTopic(key: string): NotificationTopicDefinition | undefined {
  return BY_KEY.get(key);
}

export function isNotificationTopic(key: string): boolean {
  return BY_KEY.has(key);
}

/** Les sujets qu'un compte peut réellement couper. */
export function togglableTopicsFor(audience: NotificationAudience): NotificationTopicDefinition[] {
  return NOTIFICATION_TOPICS.filter((topic) => topic.audience === audience && !topic.critical);
}

/** Ce qu'un appareil enregistre. */
export interface PushTokenInput {
  token: string;
  platform: 'ios' | 'android';
  locale: string;
  appVersion?: string;
}

/** L'état des réglages, tel que l'écran Profil l'affiche. */
export interface NotificationSettings {
  audience: NotificationAudience | null;
  topics: {
    key: string;
    enabled: boolean;
    /** Verrouillé : visible, mais pas coupable. */
    critical: boolean;
  }[];
}

/**
 * Un jeton Expo, reconnu par sa forme.
 *
 * Vérifié AVANT l'écriture : une chaîne quelconque acceptée ici partirait à
 * chaque événement vers l'API d'Expo, qui la rejetterait à chaque fois. Un
 * appareil mal enregistré doit être refusé au moment où on peut encore le dire
 * à l'application.
 */
const EXPO_TOKEN = /^Expo(nent)?PushToken\[[A-Za-z0-9._-]+\]$/;

export function isExpoPushToken(token: string): boolean {
  return EXPO_TOKEN.test(token.trim());
}

/**
 * UNE LIGNE DU CENTRE DE NOTIFICATIONS — V2 vendeur bloc 8, spécification §10.
 *
 * **Le sujet et ses paramètres, jamais le texte rendu.** C'est ce qui permet à
 * chaque lecteur de voir la ligne dans SA langue — et non dans celle de
 * l'appareil qui a reçu le push — et à une correction de formulation de
 * s'appliquer rétroactivement à tout l'historique.
 *
 * `data` porte ce que la ligne OUVRE : un code de colis, un code de
 * réclamation. Sans lui, il faudrait redeviner de quoi la ligne parle pour
 * savoir où l'envoyer.
 */
export interface NotificationItem {
  id: string;
  topic: string;
  params: Record<string, string>;
  data: Record<string, string>;
  /** `null` tant qu'elle n'a pas été lue. C'est la pastille du menu. */
  readAt: string | null;
  createdAt: string;
}

export interface NotificationFeed {
  items: NotificationItem[];
  /** Combien restent non lues, toutes pages confondues. */
  unread: number;
}
