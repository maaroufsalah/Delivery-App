import type { LucideIcon } from 'lucide-react-native';
import { BottomBar, type BottomBarItem } from '@/ui';

/**
 * LA BARRE BASSE, branchée sur les onglets `expo-router` — M4 bloc 5.
 *
 * Les trois espaces la partagent. Écrire trois fois le même adaptateur aurait
 * garanti que le prochain correctif — un badge, une teinte, une hauteur —
 * n'atteigne qu'un rôle sur trois, et c'est exactement ce qui s'est produit
 * avec `'var(--primary)'` : la teinte active était absente dans les trois
 * layouts, et il a fallu les corriger tous les trois.
 *
 * Chaque espace ne fournit que ce qui lui appartient : ses destinations, leurs
 * libellés traduits, et le badge quand il en a un.
 */
export interface TabEntry {
  /** Le nom de la route `expo-router`, qui sert aussi de clé d'état. */
  name: string;
  label: string;
  icon: LucideIcon;
  badge?: number;
  /** L'entrée mise en avant, au centre : le scan pour qui scanne. */
  raised?: boolean;
}

interface TabBarProps {
  state: { index: number; routes: { name: string }[] };
  navigation: { navigate: (name: string) => void };
}

/**
 * Rend la propriété `tabBar` de `<Tabs>`.
 *
 * `roleTabBar(entries)` s'écrit en une ligne dans un layout, et la barre reste
 * un composant ordinaire — donc testable, et remplaçable sans toucher aux
 * routes.
 */
export function roleTabBar(entries: TabEntry[], options: { raisedSize?: number } = {}) {
  return function TabBar({ state, navigation }: TabBarProps) {
    const current = state.routes[state.index]?.name ?? entries[0]?.name ?? '';

    const items: BottomBarItem[] = entries.map((entry) => ({
      key: entry.name,
      label: entry.label,
      icon: entry.icon,
      badge: entry.badge,
      raised: entry.raised,
      onPress: () => navigation.navigate(entry.name),
    }));

    return <BottomBar active={current} items={items} raisedSize={options.raisedSize} />;
  };
}
