'use client';

import { ThemeProvider as NextThemesProvider, useTheme } from 'next-themes';
import type { ComponentProps } from 'react';
import { useEffect } from 'react';

/**
 * Rattrape les préférences `system` héritées — C9 bloc 1.
 *
 * Le mode système a été retiré, mais il reste écrit dans le `localStorage` des
 * postes qui l'avaient choisi, et next-themes relit cette valeur au démarrage.
 * Avec `enableSystem={false}`, il ne la traduit plus en clair/sombre : il la
 * pose telle quelle, et `<html>` se retrouve avec `class="system"`. Ni la
 * palette claire ni la sombre ne s'appliquent alors vraiment, et le bouton de
 * bascule n'a pas d'état de départ lisible.
 *
 * Une écriture unique au montage suffit à refermer le sujet : la valeur devient
 * `light`, et le poste repart du défaut commun. Le visiteur qui préférait le
 * sombre le remet d'un clic — c'est le prix, assumé, d'un réglage qui disparaît.
 */
function NormalizeLegacyTheme() {
  const { theme, setTheme } = useTheme();

  useEffect(() => {
    if (theme === 'system') setTheme('light');
  }, [theme, setTheme]);

  return null;
}

export function ThemeProvider({ children, ...props }: ComponentProps<typeof NextThemesProvider>) {
  return (
    <NextThemesProvider {...props}>
      <NormalizeLegacyTheme />
      {children}
    </NextThemesProvider>
  );
}
