/**
 * Contrats de l'application mobile — M1, docs/DECISIONS.md D-069 et D-070.
 *
 * Un seul binaire sert tous les clients : l'application demande le sous-domaine
 * à la première ouverture et appelle `https://<slug>.delivery.nocido.com`. Rien
 * ici ne dépend donc d'un tenant — sauf le thème, qui vient de
 * `GET /public/company` comme sur le web.
 */

/**
 * Ce que l'application vérifie AVANT d'afficher quoi que ce soit.
 *
 * Trois réglages de PLATEFORME, pas de société : le binaire est le même pour
 * tout le monde, et un plancher de version posé par société ferait qu'un client
 * oublié laisse ses livreurs sur une version morte.
 */
export interface MobileConfig {
  /**
   * Version minimale acceptée, `x.y.z`. Vide = aucun plancher.
   *
   * L'application compare la sienne et, si elle est plus basse, affiche un
   * écran de mise à jour SANS bouton « continuer » : une version qui parle un
   * contrat disparu casserait plus loin, sur un geste de terrain.
   */
  minVersion: string;
  storeUrls: {
    android: string;
    ios: string;
  };
  /**
   * Coupure annoncée. Vide = service normal.
   *
   * Le message est affiché tel quel : il est écrit par l'exploitant, dans la
   * langue qu'il choisit, et l'application n'a pas à le traduire — elle ne sait
   * pas ce qu'il dit.
   */
  maintenance: string;
}

/**
 * La session rendue à un client MOBILE — `X-Client: mobile` (D-069).
 *
 * Le navigateur reçoit les mêmes jetons en cookies `httpOnly` et ne voit que le
 * profil. L'application n'a pas de navigateur à protéger : elle a un coffre
 * système, mieux gardé qu'un cookie ne l'a jamais été.
 */
export interface MobileSession {
  accessToken: string;
  refreshToken: string;
  /**
   * Durée de vie de l'access token, en SECONDES.
   *
   * Une durée et non une date : l'horloge d'un téléphone peut être fausse de
   * plusieurs heures. Une durée se compte sur son horloge à lui, qui est juste
   * par rapport à elle-même.
   */
  expiresIn: number;
}
