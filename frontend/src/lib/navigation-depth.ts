/**
 * COMBIEN DE PAS AVONS-NOUS FAITS DANS L'APPLICATION ? — chantier « Bon de
 * retour livreur », point 4.
 *
 * Un bouton « Retour » n'a que deux comportements justes, et le choix entre
 * les deux tient à une seule question : **est-on arrivé ici depuis une page à
 * nous ?**
 *
 *  - Oui → `router.back()`. On retrouve la liste EXACTEMENT comme on l'avait
 *    laissée : ses filtres, sa recherche, sa page, son tri. Ils vivent dans
 *    l'URL (`useServerTable`), et l'entrée d'historique les porte déjà. Aucun
 *    état à recopier, aucun état à perdre.
 *  - Non → `router.push(repli)`. Un lien collé dans un chat, un signet, un
 *    onglet neuf : reculer sortirait du site, ou ne ferait rien du tout.
 *
 * **Pourquoi pas `window.history.length` ?** Il compte les entrées de l'ONGLET,
 * pas les nôtres. Un agent qui arrive depuis un moteur de recherche a un
 * historique long et aucune page à nous derrière lui : le bouton l'expulserait
 * du site. Et `document.referrer` ne bouge pas d'un iota pendant une navigation
 * client, donc il ment dans l'autre sens.
 *
 * On compte donc NOS pas, et nous seuls. Le compteur vit au niveau du module :
 * il survit aux navigations client — c'est le propre d'un module — et repart à
 * zéro au rechargement complet, ce qui est exactement la frontière cherchée.
 */

let depth = 0;
let lastPath: string | null = null;

/**
 * Enregistre un passage sur une page.
 *
 * Le PREMIER appel ne compte pas : c'est la page d'arrivée, pas un pas. Les
 * suivants comptent, un par chemin distinct — un remplacement d'URL qui ne
 * change que la query string (un filtre qu'on coche) n'est pas un pas en
 * arrière possible, et il ne doit donc pas en créer un.
 */
export function markNavigation(pathname: string): void {
  if (lastPath !== null && lastPath !== pathname) depth += 1;
  lastPath = pathname;
}

/** Y a-t-il une page À NOUS derrière celle-ci ? */
export function hasInAppHistory(): boolean {
  return depth > 0;
}

/** Remise à zéro — pour les tests, qui rejouent plusieurs parcours. */
export function resetNavigationDepth(): void {
  depth = 0;
  lastPath = null;
}
