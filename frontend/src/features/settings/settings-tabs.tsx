'use client';

import {
  Building2,
  History,
  KeyRound,
  MessageSquareWarning,
  PlugZap,
  Paintbrush,
  Palette,
  SlidersHorizontal,
  Wallet,
} from 'lucide-react';
import { useTranslations } from 'next-intl';
import { Link, usePathname } from '@/i18n/navigation';
import { cn } from '@/lib/utils';

/**
 * Onglets de la section Paramètres.
 *
 * `Société`, `Métier`, `Finance` et `Thème` configurent l'ESPACE (admin) ;
 * `Apparence` et `Mot de passe` sont les préférences de l'UTILISATEUR. `Thème`
 * se range du bon côté : il repeint l'application pour tout le monde, pas pour
 * celui qui le règle.
 *
 * Depuis C8 (D-048), « l'espace » est le TENANT de la session et non
 * l'installation : deux sociétés cohabitent, chacune avec ses réglages.
 */
const TABS = [
  /*
   * Les onglets d'ESPACE sont sous `settings.read` — C12 bloc 2c.
   *
   * Ils l'ont toujours été côté API ; ils ne l'étaient pas ici, parce que la
   * page n'était atteignable que depuis une entrée de menu déjà filtrée sur
   * cette clé. Le menu « Mot de passe » du compte y mène désormais pour TOUS
   * les rôles, et un livreur qui y arrive n'a rien à faire dans la fiche de sa
   * société : sans le filtre, il verrait cinq onglets qui rendent 403.
   */
  { key: 'company', href: '/settings/company', icon: Building2, permission: 'settings.read' },
  {
    key: 'business',
    href: '/settings/business',
    icon: SlidersHorizontal,
    permission: 'settings.read',
  },
  { key: 'finance', href: '/settings/finance', icon: Wallet, permission: 'settings.read' },
  {
    key: 'claims',
    href: '/settings/claims',
    icon: MessageSquareWarning,
    permission: 'settings.read',
  },
  /* sync-wimo-speedy : les jetons des prestataires qui poussent leurs statuts. */
  {
    key: 'providers',
    href: '/settings/providers',
    icon: PlugZap,
    permission: 'settings.read',
  },
  /*
   * Le journal des modifications — C11 bloc 5, D-064. `audit.read`, que le
   * défaut ne donne qu'au SUPER_ADMIN : il dit qui a fait quoi dans toute la
   * société, y compris ce que fait le staff.
   */
  { key: 'journal', href: '/settings/journal', icon: History, permission: 'audit.read' },
  { key: 'theme', href: '/settings/theme', icon: Paintbrush, permission: 'settings.read' },
  /* Les deux préférences de l'UTILISATEUR : ouvertes à tout le monde. */
  { key: 'appearance', href: '/settings/appearance', icon: Palette },
  { key: 'password', href: '/settings/password', icon: KeyRound },
] as const;

export function SettingsTabs({ permissions = [] }: { permissions?: readonly string[] }) {
  const t = useTranslations('settings.tabs');
  const pathname = usePathname();

  // Cacher plutôt que laisser échouer : c'est la règle du menu, appliquée ici.
  const visible = TABS.filter(
    (tab) => !('permission' in tab) || permissions.includes(tab.permission),
  );

  return (
    <div className="mb-6 flex gap-1 overflow-x-auto border-b" role="tablist">
      {visible.map(({ key, href, icon: Icon }) => {
        const active = pathname === href;
        return (
          <Link
            key={key}
            href={href}
            role="tab"
            aria-selected={active}
            className={cn(
              '-mb-px flex items-center gap-2 border-b-2 px-4 py-2.5 text-sm whitespace-nowrap transition-colors',
              'focus-visible:ring-ring rounded-t-md outline-none focus-visible:ring-2',
              active
                ? 'border-primary text-foreground font-medium'
                : 'text-muted-foreground hover:text-foreground border-transparent',
            )}
          >
            <Icon className="size-4" aria-hidden />
            {t(key)}
          </Link>
        );
      })}
    </div>
  );
}
