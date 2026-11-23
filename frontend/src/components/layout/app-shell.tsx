'use client';

import { LogOut, Menu, PanelLeftClose, PanelLeftOpen, Plus, UserRound } from 'lucide-react';
import { useLocale, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { Brand } from '@/components/layout/brand';
import { LocaleSwitcher } from '@/components/layout/locale-switcher';
import { NavLink, SidebarNav } from '@/components/layout/sidebar-nav';
import { ThemeToggle } from '@/components/layout/theme-toggle';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from '@/components/ui/sheet';
import { useMediaQuery } from '@/hooks/use-media-query';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { Link, usePathname, useRouter } from '@/i18n/navigation';
import { localeDirection } from '@/i18n/routing';
import type { UserProfile } from '@/lib/api/auth';
import { logoutAction } from '@/lib/actions/auth';
import {
  BOTTOM_NAV,
  isVisible,
  type NavBadges,
  type NavSectionKey,
  SETTINGS_ITEM,
} from '@/lib/navigation-config';
import { NAV_RAIL_COOKIE, writePreference } from '@/lib/sidebar-preferences';
import { cn } from '@/lib/utils';

/**
 * Trois dispositions, une seule structure :
 *   < 768 px  : barre supérieure + barre inférieure 4 entrées + FAB
 *   768–1023  : barre supérieure + menu en tiroir (Sheet)
 *   ≥ 1024 px : barre latérale fixe
 *   ≥ 1280 px : la barre latérale peut se réduire en rail d'icônes
 */
export function AppShell({
  children,
  user,
  badges,
  banner,
  initialSection = null,
  initialCollapsed = false,
}: {
  children: React.ReactNode;
  user: UserProfile;
  /** Compteurs d'entrées de menu, lus côté serveur par le layout. */
  badges?: NavBadges;
  /**
   * Bandeau d'état de l'espace, rendu SERVEUR par le layout et passé ici en
   * `ReactNode` : ce composant est client, et il n'a pas à savoir ce qu'est un
   * essai — il lui donne seulement sa place, entre l'en-tête et le contenu.
   */
  banner?: React.ReactNode;
  /** Préférences lues au SSR (C10 bloc 4) : le premier pixel est déjà le bon. */
  initialSection?: NavSectionKey | null;
  initialCollapsed?: boolean;
}) {
  const t = useTranslations('nav');
  const tCommon = useTranslations('common');
  const locale = useLocale();
  const pathname = usePathname();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(initialCollapsed);
  const drawerSide = localeDirection(locale) === 'rtl' ? 'right' : 'left';

  /*
   * La réduction n'existe qu'à partir de 1280 px — C10 bloc 5.
   *
   * Sans cette condition, l'état et le CSS divergeraient : le bouton ne
   * s'affiche qu'en `xl`, mais l'état repliée s'appliquerait dès `lg`. Un
   * utilisateur qui replie la barre sur un grand écran puis rétrécit sa
   * fenêtre se retrouverait avec une barre d'icônes et AUCUN moyen de la
   * déplier, le bouton ayant disparu avec le point de rupture.
   *
   * `xl` vaut 80rem chez Tailwind ; la valeur est écrite en rem pour suivre
   * la taille de police de l'utilisateur, comme le fait la classe.
   */
  const wide = useMediaQuery('(min-width: 80rem)', initialCollapsed);
  const rail = collapsed && wide;

  // La barre du bas suit la même règle que la sidebar : pas d'entrée vers un
  // écran que l'API refusera. Elle garde 4 emplacements, d'où le repli.
  const bottomNav = BOTTOM_NAV.filter((item) => isVisible(item, user.permissions));

  const toggleRail = () => {
    const next = !collapsed;
    setCollapsed(next);
    writePreference(NAV_RAIL_COOKIE, next ? '1' : '0');
  };

  return (
    // `data-space` : voir `courier-shell.tsx` — la marque que lit la recette.
    <div data-space="back-office" className="bg-background min-h-dvh">
      {/*
        Barre latérale — trois zones, C10 bloc 1.

        `grid-rows-[auto_minmax(0,1fr)_auto]` plutôt qu'un `flex-col` : c'est le
        `minmax(0, …)` qui autorise la zone du milieu à se COMPRIMER sous la
        hauteur de son contenu. C'est très exactement ce qui manquait — un
        `flex-1` sans `min-h-0` garde `min-height: auto`, la zone débordait
        l'aside `fixed`, et le bas du menu était coupé sans barre de
        défilement.
      */}
      <aside
        className={cn(
          'border-sidebar-border bg-sidebar fixed inset-y-0 start-0 z-30 hidden border-e lg:grid',
          'grid-rows-[auto_minmax(0,1fr)_auto] transition-[width] duration-200',
          // Plus étroit sous 1700 px (chantier « Affichage », point 5) : la place va au tableau.
          rail ? 'w-16' : 'dense:w-56 w-64',
        )}
      >
        <div
          className={cn(
            'border-sidebar-border flex h-16 items-center border-b',
            rail ? 'justify-center px-2' : 'px-5',
          )}
        >
          <Brand variant="sidebar" compact={rail} />
        </div>

        <div className="scrollbar-thin overflow-y-auto overscroll-contain py-4">
          <SidebarNav
            merchant={user.clientId !== null}
            granted={user.permissions}
            badges={badges}
            collapsed={rail}
            initialOpenSection={initialSection}
          />
        </div>

        <SidebarFooter
          user={user}
          collapsed={rail}
          onToggleRail={toggleRail}
          railLabel={rail ? tCommon('expandMenu') : tCommon('collapseMenu')}
        />
      </aside>

      <div className={cn('flex min-h-dvh flex-col', rail ? 'lg:ps-16' : 'dense:ps-56 lg:ps-64')}>
        <header className="bg-header/95 text-header-foreground border-header-border supports-[backdrop-filter]:bg-header/80 sticky top-0 z-20 flex h-16 shrink-0 items-center gap-2 border-b px-4 backdrop-blur sm:px-6">
          {/* Tiroir — tablette et mobile. La réduction ne l'atteint pas. */}
          <Sheet open={drawerOpen} onOpenChange={setDrawerOpen}>
            <SheetTrigger asChild>
              <Button
                variant="ghost"
                size="icon"
                className="lg:hidden"
                aria-label={tCommon('openMenu')}
              >
                <Menu className="size-5" />
              </Button>
            </SheetTrigger>
            {/*
              `SheetContent` est nativement `flex flex-col gap-4` : on garde sa
              disposition et on annule seulement l'écart, qui séparerait les
              trois zones alors qu'elles sont délimitées par des filets. Le
              `min-h-0` est ici ce que le `minmax(0, …)` est à la grille de
              l'aside — sans lui, la zone centrale refuse de se comprimer.
            */}
            <SheetContent
              side={drawerSide}
              className="border-sidebar-border bg-sidebar w-72 gap-0 p-0"
            >
              <SheetTitle className="sr-only">{t('mainMenu')}</SheetTitle>
              <div className="border-sidebar-border flex h-16 shrink-0 items-center border-b px-5">
                <Brand variant="sidebar" />
              </div>
              <div className="min-h-0 flex-1 scrollbar-thin overflow-y-auto overscroll-contain py-4">
                <SidebarNav
                  merchant={user.clientId !== null}
                  granted={user.permissions}
                  badges={badges}
                  initialOpenSection={initialSection}
                  onNavigate={() => setDrawerOpen(false)}
                />
              </div>
              <SidebarFooter user={user} onNavigate={() => setDrawerOpen(false)} />
            </SheetContent>
          </Sheet>

          <div className="lg:hidden">
            <Brand />
          </div>

          <div className="ms-auto flex items-center gap-1">
            <LocaleSwitcher />
            <ThemeToggle />
            {/* Le compte vit dans le pied de la barre dès qu'elle est là. */}
            <div className="lg:hidden">
              <UserMenu user={user} />
            </div>
          </div>
        </header>

        {/* Essai bientôt fini, essai terminé, espace suspendu — C8, D-050. */}
        {banner}

        {/* `min-w-0` : un tableau large défile DANS sa carte, il n'élargit pas la page. */}
        <main className="dense:px-5 dense:pt-4 min-w-0 flex-1 px-4 pt-6 pb-24 sm:px-6 md:pb-10 lg:px-8">
          {children}
        </main>
      </div>

      {/* Barre inférieure + FAB — mobile uniquement */}
      <nav
        className="bg-background/95 fixed inset-x-0 bottom-0 z-30 border-t backdrop-blur md:hidden"
        aria-label={t('mainMenu')}
      >
        <div className="mx-auto grid max-w-md grid-cols-5 items-end px-2 pb-[env(safe-area-inset-bottom)]">
          {bottomNav.slice(0, 2).map((item) => (
            <BottomLink key={item.href} item={item} pathname={pathname} />
          ))}

          <div className="flex justify-center">
            <Button
              asChild
              size="icon"
              className="-mt-5 size-12 rounded-full shadow-lg"
              aria-label={t('newParcel')}
            >
              <Link href="/parcels/new">
                <Plus className="size-5" />
              </Link>
            </Button>
          </div>

          {bottomNav.slice(2, 4).map((item) => (
            <BottomLink key={item.href} item={item} pathname={pathname} />
          ))}
        </div>
      </nav>
    </div>
  );
}

/**
 * Pied FIGÉ de la barre latérale — C10 bloc 1.
 *
 * Paramètres y remonte : il était la dernière entrée du menu, donc la première
 * victime du débordement, alors que c'est une destination fréquente. Le compte
 * le rejoint, parce qu'un pied de barre est l'endroit où on le cherche quand
 * une barre existe — il reste dans l'en-tête en dessous de `lg`, là où il n'y
 * a pas de barre.
 *
 * Le bouton de réduction n'apparaît qu'à partir de 1280 px (`xl`), comme
 * demandé : entre 1024 et 1280 la barre est utile en entier et l'écran n'est
 * pas assez large pour que la réduire soulage vraiment.
 */
function SidebarFooter({
  user,
  collapsed = false,
  onToggleRail,
  railLabel,
  onNavigate,
}: {
  user: UserProfile;
  collapsed?: boolean;
  onToggleRail?: () => void;
  railLabel?: string;
  onNavigate?: () => void;
}) {
  const locale = useLocale();
  const showSettings = isVisible(SETTINGS_ITEM, user.permissions);
  // Radix ne connaît que des côtés physiques : « à côté de la barre » dépend
  // du sens d'écriture.
  const tooltipSide = localeDirection(locale) === 'rtl' ? 'left' : 'right';

  return (
    <div
      className={cn(
        'border-sidebar-border flex shrink-0 flex-col gap-1 border-t py-3',
        collapsed ? 'px-2' : 'px-3',
      )}
    >
      {showSettings ? (
        <NavLink item={SETTINGS_ITEM} collapsed={collapsed} onNavigate={onNavigate} />
      ) : null}

      <SidebarUser user={user} collapsed={collapsed} />

      {onToggleRail && railLabel ? (
        <Tooltip>
          <TooltipTrigger asChild>
            <button
              type="button"
              onClick={onToggleRail}
              aria-label={railLabel}
              aria-pressed={collapsed}
              className={cn(
                'text-sidebar-foreground/60 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
                'focus-visible:ring-sidebar-ring mt-1 hidden items-center gap-3 rounded-md py-2 outline-none focus-visible:ring-2 xl:flex',
                collapsed ? 'justify-center px-2' : 'px-3',
              )}
            >
              {collapsed ? (
                <PanelLeftOpen className="flip-rtl size-4 shrink-0" aria-hidden />
              ) : (
                <PanelLeftClose className="flip-rtl size-4 shrink-0" aria-hidden />
              )}
              {!collapsed ? <span className="truncate text-sm">{railLabel}</span> : null}
            </button>
          </TooltipTrigger>
          {collapsed ? (
            <TooltipContent side={tooltipSide} sideOffset={8}>
              {railLabel}
            </TooltipContent>
          ) : null}
        </Tooltip>
      ) : null}
    </div>
  );
}

/** Le compte, dans le pied de la barre : identité visible, menu au clic. */
function SidebarUser({ user, collapsed }: { user: UserProfile; collapsed: boolean }) {
  const t = useTranslations('nav');
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  const initials = `${user.firstName.at(0) ?? ''}${user.lastName.at(0) ?? ''}`.toUpperCase();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          aria-label={t('account')}
          className={cn(
            'text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
            'focus-visible:ring-sidebar-ring flex items-center gap-3 rounded-md py-2 outline-none focus-visible:ring-2',
            collapsed ? 'justify-center px-2' : 'px-3',
          )}
        >
          <Avatar className="size-7 shrink-0">
            <AvatarFallback className="bg-primary text-primary-foreground text-[11px] font-semibold">
              {initials || '?'}
            </AvatarFallback>
          </Avatar>

          {!collapsed ? (
            <span className="flex min-w-0 flex-col text-start">
              <span className="truncate text-sm font-medium">
                {user.firstName} {user.lastName}
              </span>
              <span className="truncate text-xs opacity-60">{user.email}</span>
            </span>
          ) : null}
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" side="top" className="min-w-56">
        <DropdownMenuLabel className="flex flex-col gap-0.5">
          <span className="truncate text-sm">
            {user.firstName} {user.lastName}
          </span>
          <span className="text-muted-foreground truncate text-xs font-normal">{user.email}</span>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild className="cursor-pointer gap-2">
          {/*
            « Mon profil », pour TOUS les rôles — C13 bloc 4.
            Il remplace le lien « Mot de passe » de C12 : le changement de mot
            de passe y vit toujours, avec le nom, le téléphone et l'adresse. Ni
            l'un ni l'autre ne peut vivre sous Paramètres, dont l'entrée est
            filtrée par `settings.read` — un livreur ne l'aurait jamais trouvé.
          */}
          <Link href="/profile">
            <UserRound className="size-4" aria-hidden />
            {t('profile')}
          </Link>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          className="gap-2"
          disabled={pending}
          onSelect={() =>
            startTransition(async () => {
              await logoutAction();
              router.replace('/login');
              router.refresh();
            })
          }
        >
          <LogOut className="flip-rtl size-4" aria-hidden />
          {t('logout')}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/** Avatar, identité et déconnexion — en-tête, sous `lg` uniquement. */
function UserMenu({ user }: { user: UserProfile }) {
  const t = useTranslations('nav');
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  const initials = `${user.firstName.at(0) ?? ''}${user.lastName.at(0) ?? ''}`.toUpperCase();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className="focus-visible:ring-ring ms-1 rounded-full outline-none focus-visible:ring-2"
          aria-label={t('account')}
        >
          <Avatar className="size-8">
            <AvatarFallback className="bg-primary text-primary-foreground text-xs font-semibold">
              {initials || '?'}
            </AvatarFallback>
          </Avatar>
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="min-w-56">
        <DropdownMenuLabel className="flex flex-col gap-0.5">
          <span className="truncate text-sm">
            {user.firstName} {user.lastName}
          </span>
          <span className="text-muted-foreground truncate text-xs font-normal">{user.email}</span>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild className="cursor-pointer gap-2">
          {/*
            « Mon profil », pour TOUS les rôles — C13 bloc 4.
            Il remplace le lien « Mot de passe » de C12 : le changement de mot
            de passe y vit toujours, avec le nom, le téléphone et l'adresse. Ni
            l'un ni l'autre ne peut vivre sous Paramètres, dont l'entrée est
            filtrée par `settings.read` — un livreur ne l'aurait jamais trouvé.
          */}
          <Link href="/profile">
            <UserRound className="size-4" aria-hidden />
            {t('profile')}
          </Link>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          className="cursor-pointer gap-2"
          disabled={pending}
          onSelect={() =>
            startTransition(async () => {
              await logoutAction();
              router.replace('/login');
              router.refresh();
            })
          }
        >
          <LogOut className="flip-rtl size-4" aria-hidden />
          {t('logout')}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function BottomLink({ item, pathname }: { item: (typeof BOTTOM_NAV)[number]; pathname: string }) {
  const t = useTranslations('nav.items');
  const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
  const Icon = item.icon;

  return (
    <Link
      href={item.href}
      aria-current={active ? 'page' : undefined}
      className={cn(
        'flex flex-col items-center gap-1 rounded-md py-2 text-[11px] transition-colors',
        active ? 'text-primary font-medium' : 'text-muted-foreground',
      )}
    >
      <Icon className="size-5" aria-hidden />
      <span className="max-w-full truncate px-1">{t(item.key)}</span>
    </Link>
  );
}
