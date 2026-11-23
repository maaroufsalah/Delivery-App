import { type Href, useRouter } from 'expo-router';

/**
 * RECULER, ET SAVOIR OÙ ALLER QUAND IL N'Y A RIEN DERRIÈRE — chantier « Bon de
 * retour livreur », point 4.
 *
 * `router.back()` ne fait **rien** quand la pile est vide, et c'est le cas le
 * plus fréquent de tous sur un téléphone : une notification ouvre
 * l'application directement sur la fiche. Le livreur voit alors une flèche qui
 * ne répond pas — pas un message, pas un retour, rien.
 *
 * `fallback` dit où aller dans ce cas : l'onglet d'où cette fiche SERAIT venue.
 * `replace` et non `push`, pour que la fiche ne reste pas derrière l'onglet et
 * que le geste suivant ne l'y ramène pas.
 *
 * Quand il y a une pile — le cas ordinaire, on vient de la liste — on recule,
 * et la liste retrouve son état : sa recherche, son filtre, sa position de
 * défilement. Y aller par un `push` la reconstruirait à zéro.
 */
export function useGoBack(fallback: Href): () => void {
  const router = useRouter();

  return () => {
    if (router.canGoBack()) {
      router.back();
      return;
    }

    router.replace(fallback);
  };
}
