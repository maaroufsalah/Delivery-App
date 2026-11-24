import { brandGradient, buildThemeTokens, resolveTokens, type ThemeTokens } from '@delivery/shared';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { useColorScheme as useNativeWindScheme, vars } from 'nativewind';
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { AppState, useColorScheme as useSystemScheme, View } from 'react-native';
import { useI18n } from '@/i18n';
import { api } from '@/lib/api';

/**
 * Le thème du TENANT, sur le téléphone — M1 (D-075), refondu par M4.
 *
 * **La même fonction que le web.** `buildThemeTokens()` vit dans
 * `packages/shared` depuis B-10 : deux couleurs de marque en entrée, tous les
 * jetons en sortie, clair et sombre, avec un contraste AA garanti et non estimé
 * (`readableForeground`, D-046). L'application ne recalcule rien et ne devine
 * rien.
 *
 * **Aucune couleur n'est écrite dans l'application.** `vars()` de NativeWind
 * pose les jetons en variables CSS sur une vue racine, exactement comme
 * `<CompanyStyle />` les pose sur `<html>` côté web. Un client qui change sa
 * marque dans Paramètres > Société la voit changer au prochain lancement, sans
 * soumission au magasin.
 *
 * **Ce que M4 ajoute, et pourquoi :**
 *
 *  1. **Les jetons sont RÉSOLUS** (`resolveTokens`). Quelques-uns sortaient de
 *     la dérivation en `color-mix(in oklab, …)`, que le navigateur sait peindre
 *     et que React Native ne sait pas : un dégradé SVG à qui on donne cette
 *     chaîne rend du transparent. La résolution exécute le mélange au lieu de
 *     le décrire.
 *  2. **Les jetons sont LISIBLES EN JS** (`tokens`). Une barre de statut, un
 *     dégradé SVG, une icône `lucide` ou un `tintColor` n'acceptent pas de
 *     classe Tailwind. Sans cet objet, chaque écran écrirait ses couleurs à la
 *     main — et le thème du tenant s'arrêterait à la frontière des classes.
 *  3. **La marque est mise en CACHE.** Un lancement hors réseau affichait la
 *     marque de la plateforme : un livreur qui ouvre l'application dans un
 *     sous-sol voyait l'orange Nocido à la place de celui de son employeur.
 *     Le cache est relu d'abord, le réseau ensuite — et à chaque retour au
 *     premier plan, pour que le changement de palette d'un admin arrive sans
 *     redémarrage.
 *  4. **Trois réglages** : Système (défaut), Clair, Sombre.
 *
 * **Le repli est la marque de la PLATEFORME**, pas une couleur inventée : tant
 * que ni le cache ni le réseau n'ont répondu, on affiche du Nocido. Afficher la
 * marque d'un autre transporteur pendant deux secondes serait le seul défaut
 * visuel qu'on ne pourrait pas expliquer.
 */

interface TenantBrand {
  name: string;
  /** Lot D, D2 (D-189) : absent d'une API d'avant le lot. */
  shortName?: string;
  /**
   * « RECETTE » / « UAT », `null` en production — D-199. Une propriété de la
   * PILE servie par l'API, pas de la société ; absente d'une API plus ancienne.
   */
  environmentLabel?: string | null;
  logo: string;
  primaryColor: string;
  secondaryColor: string;
  /**
   * Le contact de la société — L2 bloc 8, « Aide & Support ».
   *
   * `/public/company` les rend déjà : ce sont des informations de contact,
   * elles figurent sur les factures et les colis, et l'endpoint est public.
   * Facultatifs parce qu'une société peut ne pas les avoir renseignés, et
   * l'écran se tait alors plutôt que d'afficher un bouton qui n'appelle
   * personne.
   */
  phone?: string | null;
  email?: string | null;
}

/** Le mode effectivement peint. */
export type Scheme = 'light' | 'dark';

/**
 * Ce que l'utilisateur a CHOISI — DEUX valeurs, C15 bloc 5.
 *
 * M4 avait ajouté « Système », c'est-à-dire l'absence de choix, et en avait
 * fait le défaut. Le PO revient dessus : un livreur ne sait pas ce que son
 * téléphone est « réglé » à faire, et ne fait pas le lien entre un réglage
 * Android et une application qui devient noire. Il voyait donc le thème
 * changer sans avoir rien demandé.
 *
 * **Clair par défaut**, et deux boutons. Le sombre reste à un appui pour qui le
 * veut — c'est un choix, plus une conséquence.
 */
export type ThemePreference = Scheme;

/** Là où la préférence survit au redémarrage. */
const PREFERENCE_KEY = 'app.theme';
/**
 * Le clair, et c'est un choix — C15 bloc 5.
 *
 * Le premier écran d'une application de livraison se lit dehors, au soleil, sur
 * un téléphone dont la luminosité n'est jamais au maximum. C'était déjà le
 * raisonnement de M1.
 */
const DEFAULT_PREFERENCE: ThemePreference = 'light';
/** La clé de M1, relue une fois pour ne pas perdre le choix des installés. */
const LEGACY_SCHEME_KEY = 'app.scheme';
/** La marque du tenant, pour que le premier écran ne soit jamais générique. */
const BRAND_KEY = 'app.brand';

interface TenantTheme {
  brand: TenantBrand | null;
  /** Les jetons du mode courant, résolus : des couleurs, pas des expressions. */
  tokens: ThemeTokens;
  /** Les deux bornes du dégradé de marque, dérivées (jamais choisies). */
  gradient: { from: string; to: string };
  loading: boolean;
  /** Le mode peint, une fois « système » résolu. */
  scheme: Scheme;
  /** Ce que l'utilisateur a choisi : clair ou sombre, et rien d'autre. */
  preference: ThemePreference;
  setPreference: (next: ThemePreference) => void;
}

/** La marque de la plateforme — `PLATFORM_BRAND` du web, en deux couleurs. */
const PLATFORM_FALLBACK = { primary: '#0F62FE', secondary: '#0A1F44' };

const FALLBACK_TOKENS = buildThemeTokens(PLATFORM_FALLBACK);
const FALLBACK_LIGHT = resolveTokens(FALLBACK_TOKENS.light);

const ThemeContext = createContext<TenantTheme>({
  brand: null,
  tokens: FALLBACK_LIGHT,
  gradient: brandGradient(FALLBACK_LIGHT),
  loading: true,
  scheme: 'light',
  preference: 'light',
  setPreference: () => undefined,
});

export function useTenantTheme(): TenantTheme {
  return useContext(ThemeContext);
}

/**
 * Les jetons seuls — le raccourci que 90 % des composants utilisent.
 *
 * `const t = useTokens()` puis `t.primary`. Passer par `useTenantTheme()` pour
 * la même chose obligerait à déstructurer partout, et l'habitude finirait par
 * produire un `'#fff'` un jour de fatigue.
 */
export function useTokens(): ThemeTokens {
  return useContext(ThemeContext).tokens;
}

export function TenantThemeProvider({ children }: { children: ReactNode }) {
  const [brand, setBrand] = useState<TenantBrand | null>(null);
  const [loading, setLoading] = useState(true);

  /*
   * LE NOM COURT — lot D, D2 (D-189). « Reçu par … », « Remis à … » le lisent
   * dans `t()`. Une réponse d'avant le lot D n'en porte pas : le repli du
   * binaire (« Speedy ») reste alors en place.
   */
  const { setShortName } = useI18n();
  useEffect(() => {
    if (brand?.shortName) setShortName(brand.shortName);
  }, [brand?.shortName, setShortName]);
  const [preference, setPreferenceState] = useState<ThemePreference>(DEFAULT_PREFERENCE);

  const system = useSystemScheme();
  const { setColorScheme } = useNativeWindScheme();

  /* Plus de résolution : la préférence EST le mode peint (C15 bloc 5). */
  const scheme: Scheme = preference;

  useEffect(() => {
    /*
     * La préférence survit au redémarrage. Tant qu'elle n'est pas lue, c'est le
     * clair — le défaut assumé, et jamais un éclair de noir au lancement.
     */
    void (async () => {
      const saved = await AsyncStorage.getItem(PREFERENCE_KEY);

      if (saved === 'light' || saved === 'dark') {
        setPreferenceState(saved);
        return;
      }

      /*
       * MIGRATION DE « SYSTÈME » — C15 bloc 5.
       *
       * Quelqu'un qui avait choisi « Système » voit aujourd'hui ce que son
       * téléphone affiche. On fige CE mode-là plutôt que de le ramener au
       * clair : la valeur disparaît, mais l'écran ne change pas sous ses yeux
       * au premier lancement de la mise à jour — et il peut en changer d'un
       * appui, comme avant.
       */
      if (saved === 'system') {
        const resolved: Scheme = system === 'dark' ? 'dark' : 'light';
        setPreferenceState(resolved);
        await AsyncStorage.setItem(PREFERENCE_KEY, resolved);
        return;
      }

      // Migration silencieuse de M1 : `app.scheme` ne portait que light/dark.
      const legacy = await AsyncStorage.getItem(LEGACY_SCHEME_KEY);
      if (legacy === 'dark' || legacy === 'light') setPreferenceState(legacy);
    })();
    // `system` n'est lu qu'une fois, à la migration : le remettre en dépendance
    // ferait rejouer l'effet à chaque bascule du téléphone, donc réécrire la
    // préférence de quelqu'un qui vient d'en choisir une.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  /**
   * Change la préférence, et le dit AUSSI à NativeWind.
   *
   * L'application n'utilise aucune classe `dark:` — tout passe par les
   * variables CSS projetées plus bas. Mais NativeWind garde son propre état, et
   * le laisser diverger ferait mentir la première classe `dark:` qu'on écrira.
   */
  const setPreference = useCallback((next: ThemePreference) => {
    setPreferenceState(next);
    void AsyncStorage.setItem(PREFERENCE_KEY, next);
  }, []);

  useEffect(() => {
    setColorScheme(preference);
  }, [preference, setColorScheme]);

  /* --- La marque : le cache d'abord, le réseau ensuite -------------------- */

  const load = useCallback(async () => {
    try {
      // Publique : elle sert AVANT la connexion, sur l'écran de login, pour
      // que le livreur voie la marque de son employeur en tapant son mot de
      // passe.
      const company = await api<TenantBrand>('/public/company', { anonymous: true });
      setBrand(company);
      await AsyncStorage.setItem(BRAND_KEY, JSON.stringify(company));
    } catch {
      // Silencieux : un thème qui ne charge pas n'est pas une panne. Le cache
      // tient le relais, et à défaut la marque de la plateforme. Une alerte
      // ici s'afficherait par-dessus l'écran de connexion, sans rien à y faire.
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void (async () => {
      const cached = await AsyncStorage.getItem(BRAND_KEY);
      if (cached) {
        try {
          setBrand(JSON.parse(cached) as TenantBrand);
          // Le cache suffit à peindre : l'écran n'attend plus le réseau.
          setLoading(false);
        } catch {
          // Cache illisible — une version antérieure, un écrit interrompu. Il
          // sera réécrit par l'appel qui suit ; rien à réparer ici.
        }
      }

      await load();
    })();
  }, [load]);

  /*
   * À CHAQUE RETOUR AU PREMIER PLAN. « Le mobile suit au prochain lancement »
   * se lit « lancement » par un développeur et « ouverture » par un client :
   * sur Android, l'application n'est presque jamais tuée, et sans ceci un
   * changement de palette attendrait le prochain redémarrage du téléphone.
   */
  // `0` et non `Date.now()` : lire l'horloge pendant le rendu est impur, et le
  // compilateur React le refuse. La valeur est posée au premier passage utile.
  const lastRefresh = useRef(0);

  useEffect(() => {
    const subscription = AppState.addEventListener('change', (state) => {
      if (state !== 'active') return;

      // Une minute de garde : revenir de l'appareil photo ne doit pas relancer
      // un appel réseau à chaque photo de preuve. Le premier retour passe
      // toujours (`lastRefresh` vaut zéro), ce qui est le cas qu'on veut.
      if (Date.now() - lastRefresh.current < 60_000) return;

      lastRefresh.current = Date.now();
      void load();
    });

    return () => subscription.remove();
  }, [load]);

  const both = useMemo(
    () =>
      buildThemeTokens(
        brand
          ? { primary: brand.primaryColor, secondary: brand.secondaryColor }
          : PLATFORM_FALLBACK,
      ),
    [brand],
  );

  const tokens = useMemo(() => resolveTokens(both[scheme]), [both, scheme]);
  const gradient = useMemo(() => brandGradient(tokens), [tokens]);

  const value = useMemo(
    () => ({ brand, tokens, gradient, loading, scheme, preference, setPreference }),
    [brand, tokens, gradient, loading, scheme, preference, setPreference],
  );

  return (
    <ThemeContext.Provider value={value}>
      {/*
        `vars()` sur une vue qui remplit l'écran : tout ce qui est en dessous
        lit `var(--primary)` par ses classes Tailwind. C'est le pendant natif de
        la balise `<style>` que le web pose au SSR. Le fond est posé ici aussi —
        sans lui, le blanc de la fenêtre traverse sous les écrans en sombre.
      */}
      <View style={[{ flex: 1, backgroundColor: tokens.background }, vars(toCssVars(tokens))]}>
        {children}
      </View>
    </ThemeContext.Provider>
  );
}

/**
 * `{ background: '#FFF' }` → `{ '--background': '#FFF' }`.
 *
 * `buildThemeTokens` rend les jetons sous leur nom nu, parce que le web les
 * écrit dans une feuille de style où le préfixe est ajouté à l'écriture.
 * NativeWind, lui, attend des noms de variables déjà préfixés.
 */
function toCssVars(tokens: ThemeTokens): Record<string, string> {
  const cssVars: Record<string, string> = {};

  for (const [name, value] of Object.entries(tokens)) {
    cssVars[`--${name}`] = value;
  }

  return cssVars;
}
