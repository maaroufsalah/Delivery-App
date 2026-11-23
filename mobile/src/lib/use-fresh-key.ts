import { useFocusEffect } from 'expo-router';
import { useCallback, useReducer } from 'react';

/**
 * UN ÉCRAN DE SAISIE REPART VIDE — Recette 7, points 1 et 5 (audit MOB-03).
 *
 * Tous les écrans du livreur sont des `Tabs.Screen` cachés : le navigateur les
 * monte UNE fois et les garde. Ouvrir la fiche du colis B après celle du colis
 * A ne remonte rien — c'est la MÊME instance, avec un autre paramètre, et tous
 * ses `useState` gardent leur valeur. C'est ainsi qu'un chèque de 150 saisi
 * sur A s'est retrouvé sur B (« Le chèque dépasse le montant encaissé »), et
 * que le bon BR-2610-0009 affichait « 32 colis sur 6 » : les 32 cochés étaient
 * ceux du bon précédent.
 *
 * Le remède est celui de React : une `key`. L'écran exporté devient une coque
 * qui lit le paramètre et rend le contenu sous `key={useFreshKey(param)}` ; le
 * contenu, lui, ne change pas d'une ligne. La clé change :
 *
 *  - quand le PARAMÈTRE change (colis A → colis B) ;
 *  - quand l'écran est QUITTÉ (`useFocusEffect` → nettoyage au blur) : rouvrir
 *    le même colis demain ne doit pas retrouver le chèque d'hier.
 *
 * Le remontage a lieu pendant que l'écran est caché : le livreur ne le voit
 * pas, et c'est l'écran suivant qui coûte le premier rendu, comme avant.
 *
 * Écarté : passer les écrans de détail dans un `Stack`. C'est ce que fait un
 * navigateur « normal », mais la barre basse et le retour « écran précédent »
 * (`backBehavior="fullHistory"`) tiennent sur les `Tabs`, et le chantier
 * interdit tout changement de navigateur à chaud, en OTA.
 */
export function useFreshKey(param?: string | string[]): string {
  const [generation, bump] = useReducer((count: number) => count + 1, 0);

  useFocusEffect(
    useCallback(() => {
      return () => bump();
    }, []),
  );

  const id = Array.isArray(param) ? param.join('/') : (param ?? '');
  return `${id}#${generation}`;
}
