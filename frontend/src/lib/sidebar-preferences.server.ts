import { cookies } from 'next/headers';
import type { NavSectionKey } from '@/lib/navigation-config';
import { NAV_RAIL_COOKIE, NAV_SECTION_COOKIE, parseSectionKey } from '@/lib/sidebar-preferences';

/**
 * Lecture serveur des préférences de barre latérale — C10 bloc 4.
 *
 * Séparé de `sidebar-preferences.ts` parce que `next/headers` ne peut pas être
 * importé par un composant client, et que l'autre fichier l'est. La frontière
 * est la même que celle décrite dans `CLAUDE.md` pour les préférences.
 *
 * Appelée par le layout du back-office, qui passe le résultat en props : c'est
 * ce qui permet au premier pixel d'être déjà le bon.
 */
export async function readSidebarPreferences(): Promise<{
  section: NavSectionKey | null;
  collapsed: boolean;
}> {
  const store = await cookies();

  return {
    section: parseSectionKey(store.get(NAV_SECTION_COOKIE)?.value),
    collapsed: store.get(NAV_RAIL_COOKIE)?.value === '1',
  };
}
