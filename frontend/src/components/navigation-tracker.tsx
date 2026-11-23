'use client';

import { useEffect } from 'react';
import { usePathname } from '@/i18n/navigation';
import { markNavigation } from '@/lib/navigation-depth';

/**
 * LE TÉMOIN DE NAVIGATION — chantier « Bon de retour livreur », point 4.
 *
 * Il ne rend rien. Il note qu'on a changé de page, pour que le bouton
 * « Retour » sache s'il peut reculer ou s'il doit se rabattre sur la liste
 * (`lib/navigation-depth.ts` explique pourquoi la question se pose).
 *
 * Il est monté UNE fois, au plus haut de l'arbre client, parce qu'il doit voir
 * les pas faits AVANT que le bouton n'existe : c'est précisément la liste qu'on
 * quitte qui compte.
 */
export function NavigationTracker() {
  const pathname = usePathname();

  useEffect(() => {
    markNavigation(pathname);
  }, [pathname]);

  return null;
}
