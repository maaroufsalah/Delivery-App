'use client';

import { useSearchParams } from 'next/navigation';
import { usePathname, useRouter } from '@/i18n/navigation';

/**
 * L'onglet ouvert, rangé dans l'URL — C13 bloc 5.
 *
 * Deux raisons, et la seconde est celle qui l'a rendu nécessaire :
 *
 * 1. un onglet partagé par un lien s'ouvre sur le bon onglet ;
 * 2. **une Server Action qui revalide la page ne ramène plus au premier.**
 *    Créer un compte depuis l'onglet « Compte » déclenche un nouveau rendu
 *    serveur ; l'état local d'un `Tabs` non contrôlé n'y survit pas de façon
 *    garantie, et l'agent se retrouvait sur « Profil » sans avoir rien demandé.
 *
 * `replace` et non `push` : l'onglet n'est pas une étape de navigation, et
 * retomber sur le précédent en appuyant sur « retour » serait une surprise.
 * `scroll: false` : changer d'onglet ne doit pas rembobiner la page.
 */
export function useTabParam(fallback: string): [string, (next: string) => void] {
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const router = useRouter();

  const current = searchParams.get('tab') ?? fallback;

  const setTab = (next: string) => {
    const params = new URLSearchParams(searchParams.toString());

    if (next === fallback) params.delete('tab');
    else params.set('tab', next);

    const query = params.toString();
    router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
  };

  return [current, setTab];
}
