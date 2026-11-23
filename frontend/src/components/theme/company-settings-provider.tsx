'use client';

import {
  type BrandColors,
  buildThemeTokens,
  type CompanySettings,
  DEFAULT_RADIUS,
  type ThemeOverrides,
  type ThemeTokens,
} from '@delivery/shared';
import { createContext, useCallback, useContext, useMemo, useState } from 'react';

/**
 * Configuration société côté client : nom et logo pour l'en-tête, et aperçu
 * live des couleurs pendant l'édition de Paramètres > Société.
 *
 * Il n'y a pas de sélecteur de marque (docs/DECISIONS.md D-013) : ce provider
 * ne bascule rien, il ne fait que refléter la configuration servie au SSR et,
 * le temps d'une édition, superposer un jeu de tokens provisoire.
 */

const PREVIEW_STYLE_ID = 'company-tokens-preview';

function declarations(tokens: ThemeTokens): string {
  return [
    `--radius:${DEFAULT_RADIUS}`,
    ...Object.entries(tokens).map(([n, v]) => `--${n}:${v}`),
  ].join(';');
}

/**
 * Superpose une feuille de style après celle du SSR.
 *
 * Mêmes sélecteurs, tous deux hors couche : à spécificité égale, c'est l'ordre
 * d'insertion qui tranche, et l'aperçu est inséré après. Il doit donc suivre
 * <CompanyStyle /> à la lettre — un sélecteur différent romprait l'égalité et
 * rendrait le résultat dépendant du hasard.
 */
function applyPreview(brand: BrandColors | null, theme?: ThemeOverrides) {
  const existing = document.getElementById(PREVIEW_STYLE_ID);

  if (!brand) {
    existing?.remove();
    return;
  }

  /*
   * Les surcharges par zone entrent dans l'aperçu (B-10).
   *
   * Sans elles, choisir une palette faisait disparaître la charte avancée le
   * temps de l'aperçu, puis la faisait revenir à l'enregistrement : l'admin
   * voyait un écran qu'il n'allait pas obtenir.
   */
  const tokens = buildThemeTokens(brand, theme);
  const css = `:root{${declarations(tokens.light)}}.dark{${declarations(tokens.dark)}}`;

  if (existing) {
    existing.textContent = css;
    return;
  }

  const style = document.createElement('style');
  style.id = PREVIEW_STYLE_ID;
  style.textContent = css;
  document.head.append(style);
}

interface CompanySettingsContextValue {
  settings: CompanySettings;
  /** Remplace la configuration après enregistrement, et retire l'aperçu. */
  commitSettings: (settings: CompanySettings) => void;
  /**
   * Applique une identité visuelle provisoire. `null` revient à la
   * configuration servie au SSR.
   */
  previewBrand: (brand: BrandColors | null, theme?: ThemeOverrides) => void;
}

const CompanySettingsContext = createContext<CompanySettingsContextValue | null>(null);

export function CompanySettingsProvider({
  initialSettings,
  children,
}: {
  initialSettings: CompanySettings;
  children: React.ReactNode;
}) {
  const [settings, setSettings] = useState<CompanySettings>(initialSettings);

  const previewBrand = useCallback((brand: BrandColors | null, theme?: ThemeOverrides) => {
    applyPreview(brand, theme);
  }, []);

  const commitSettings = useCallback((next: CompanySettings) => {
    setSettings(next);
    // Les nouveaux tokens seront rendus au SSR au prochain rendu serveur ; en
    // attendant, l'aperçu reste la seule source à l'écran.
    applyPreview({ primary: next.primaryColor, secondary: next.secondaryColor }, next.theme);
  }, []);

  const value = useMemo(
    () => ({ settings, commitSettings, previewBrand }),
    [settings, commitSettings, previewBrand],
  );

  return (
    <CompanySettingsContext.Provider value={value}>{children}</CompanySettingsContext.Provider>
  );
}

export function useCompanySettings(): CompanySettingsContextValue {
  const context = useContext(CompanySettingsContext);
  if (!context) {
    throw new Error(
      'useCompanySettings doit être utilisé à l’intérieur de <CompanySettingsProvider>.',
    );
  }
  return context;
}
