import { useFocusEffect, useRouter } from 'expo-router';
import { useCallback, useEffect, useRef } from 'react';
import { useI18n } from '@/i18n';
import { useToast } from '@/ui';

/**
 * LE CIRCUIT A DISPARU SOUS LES YEUX DU LIVREUR — chantier « Mobile : Retour +
 * circuits clôturés ».
 *
 * L'API ne rend plus un circuit clôturé (Recette 5, point 2). Quand l'agence le
 * clôt pendant que le livreur le regarde — fiche du circuit, bon, itinéraire —
 * la relecture le retire du cache et l'écran n'a plus rien à montrer. Plutôt
 * qu'un écran vide (ou, pire, un AUTRE circuit pris au hasard, ce que faisait
 * la fiche), on le dit et on revient à la liste d'où l'on vient.
 *
 * `gone` ne doit être vrai qu'après une lecture ABOUTIE : pendant le premier
 * chargement, rien n'a encore disparu.
 *
 * **Seulement l'écran AU PREMIER PLAN.** Les écrans sont des onglets : une fiche
 * quittée reste montée, et verrait le circuit disparaître depuis l'accueil. Y
 * réagir ferait reculer le livreur sans qu'il ait rien touché. Un écran caché
 * attend donc d'être rouvert pour le dire.
 */
export function useRouteGone(gone: boolean): void {
  const router = useRouter();
  const toast = useToast();
  const { t } = useI18n();

  const focused = useRef(false);
  const latest = useRef(gone);
  useEffect(() => {
    latest.current = gone;
  });

  const leave = useCallback(() => {
    toast.show(t('circuits.closedByAgency'), 'warning');
    if (router.canGoBack()) router.back();
    else router.replace('/(courier)/circuits');
    // Le routeur, le toast et `t` sont stables sur la durée de vie de l'écran.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useFocusEffect(
    useCallback(() => {
      focused.current = true;
      if (latest.current) leave();
      return () => {
        focused.current = false;
      };
    }, [leave]),
  );

  useEffect(() => {
    if (gone && focused.current) leave();
  }, [gone, leave]);
}
