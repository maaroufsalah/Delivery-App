import { formatMessage, type I18nLocale, statusLabel, withShortName } from '@delivery/shared';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { createContext, type ReactNode, useCallback, useContext, useEffect, useState } from 'react';
import { I18nManager } from 'react-native';
import { BRAND_SHORT_NAME } from '@/config/brand';
import { ar } from './ar';
import { en } from './en';
import { fr } from './fr';

/**
 * Les trois langues — M1, docs/DECISIONS.md D-076.
 *
 * **Français par défaut** : c'est la langue de saisie du métier au Maroc, et
 * celle des libellés de statut d'origine.
 *
 * **Le catalogue de l'application lui appartient.** Le web en porte deux mille
 * clés typées ; les faire voyager ici imposerait au téléphone le vocabulaire de
 * l'entrepôt et de la facturation. Ce qui est PARTAGÉ, ce sont les libellés de
 * statut (`packages/shared/src/i18n`) — parce qu'un colis « Reporté » doit se
 * lire pareil des deux côtés, et qu'un test le vérifie.
 *
 * **Pas de bibliothèque d'i18n.** Soixante clés et une interpolation `{code}`
 * ne justifient pas une dépendance, et la règle du chantier est de n'en ajouter
 * aucune sans accord.
 */

const LOCALE_KEY = 'app.locale';

const CATALOGUES = { fr, ar, en } as const;

/** Les clés du catalogue français font foi : une clé absente casse le type. */
export type MessageKey = keyof typeof fr;

interface I18n {
  locale: I18nLocale;
  /** `t('scan.added', { code: 'CAS…' })`. */
  t: (key: MessageKey, values?: Record<string, string | number>) => string;
  /** Le libellé d'un statut, depuis le référentiel partagé. */
  status: (key: Parameters<typeof statusLabel>[0]) => string;
  setLocale: (locale: I18nLocale) => Promise<void>;
  /** Vrai en arabe : les écrans s'en servent pour les icônes directionnelles. */
  rtl: boolean;
  /**
   * Lot D, D2 (D-189) : le nom court de la société, posé par le thème du
   * tenant dès que `/public/company` le rend (cache compris).
   */
  setShortName: (shortName: string) => void;
}

const I18nContext = createContext<I18n | null>(null);

export function useI18n(): I18n {
  const value = useContext(I18nContext);
  if (!value) throw new Error('useI18n hors de I18nProvider.');
  return value;
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState<I18nLocale>('fr');
  // Lot D, D2 (D-189) : « Speedy » tant que la société n'a rien dit, hors ligne compris.
  const [shortName, setShortName] = useState(BRAND_SHORT_NAME);

  useEffect(() => {
    void (async () => {
      const saved = await AsyncStorage.getItem(LOCALE_KEY);
      if (saved && saved in CATALOGUES) setLocaleState(saved as I18nLocale);
    })();
  }, []);

  const setLocale = useCallback(async (next: I18nLocale) => {
    setLocaleState(next);
    await AsyncStorage.setItem(LOCALE_KEY, next);

    /*
     * `forceRTL` demande un REDÉMARRAGE pour que la mise en page bascule —
     * React Native lit le sens à l'initialisation des vues natives. On le pose
     * quand même : l'écran qui appelle prévient l'utilisateur, et le prochain
     * lancement sera dans le bon sens. Redémarrer l'application de force au
     * milieu d'une tournée serait pire que d'attendre.
     */
    I18nManager.allowRTL(next === 'ar');
    I18nManager.forceRTL(next === 'ar');
  }, []);

  const t = useCallback(
    (key: MessageKey, values?: Record<string, string | number>) => {
      /*
       * Repli sur le français : une clé manquante en arabe doit afficher du
       * français lisible, jamais `scan.added` à un livreur.
       *
       * Élargi en `string` : les catalogues sont `as const`, donc des unions de
       * littéraux — utile pour que `MessageKey` se type sur les CLÉS, inutile
       * et encombrant sur les VALEURS, qu'on interpole ensuite.
       */
      const template: string = CATALOGUES[locale][key] ?? fr[key] ?? key;

      // `{nom}` comme avant, et `{count, plural, …}` selon la langue (Recette 5,
      // point 8) : la syntaxe ICU que le web lit aussi.
      return formatMessage(withShortName(template, shortName), values, locale);
    },
    [locale, shortName],
  );

  const status = useCallback(
    (key: Parameters<typeof statusLabel>[0]) => statusLabel(key, locale),
    [locale],
  );

  return (
    <I18nContext.Provider
      value={{ locale, t, status, setLocale, rtl: locale === 'ar', setShortName }}
    >
      {children}
    </I18nContext.Provider>
  );
}
