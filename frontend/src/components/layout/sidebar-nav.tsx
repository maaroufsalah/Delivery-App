'use client';

import { ChevronDown } from 'lucide-react';
import { useLocale, useTranslations } from 'next-intl';
import { useId, useState } from 'react';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { Link, usePathname } from '@/i18n/navigation';
import { localeDirection } from '@/i18n/routing';
import {
  DASHBOARD_ITEM,
  type NavBadges,
  type NavItem,
  type NavSectionKey,
  visibleSections,
} from '@/lib/navigation-config';
import {
  initialSection,
  NAV_SECTION_COOKIE,
  sectionOfPath,
  writePreference,
} from '@/lib/sidebar-preferences';
import { cn } from '@/lib/utils';

function isActive(pathname: string, href: string) {
  return pathname === href || pathname.startsWith(`${href}/`);
}

/** Pastille de compteur, ou point terne pour un écran pas encore livré. */
function NavMarker({ count, ready, active }: { count?: number; ready?: boolean; active: boolean }) {
  if (count !== undefined && count > 0) {
    return (
      <span
        className={cn(
          'ms-auto min-w-5 rounded-full px-1.5 py-0.5 text-center text-[11px] font-semibold tabular-nums',
          active
            ? 'bg-sidebar-primary-foreground/20 text-sidebar-primary-foreground'
            : 'bg-sidebar-accent text-sidebar-accent-foreground',
        )}
      >
        {count > 99 ? '99+' : count}
      </span>
    );
  }

  if (!ready) {
    return <span className="ms-auto text-[10px] tracking-wide uppercase opacity-50">•</span>;
  }

  return null;
}

export function NavLink({
  item,
  count,
  onNavigate,
  collapsed = false,
}: {
  item: NavItem;
  /** Nombre d'éléments en attente. Zéro ou absent : aucune pastille. */
  count?: number;
  onNavigate?: () => void;
  /** Barre réduite : l'icône seule, le libellé passe en infobulle. */
  collapsed?: boolean;
}) {
  const t = useTranslations('nav.items');
  const locale = useLocale();
  const pathname = usePathname();
  const active = isActive(pathname, item.href);
  const Icon = item.icon;
  const label = t(item.key);

  const link = (
    <Link
      href={item.href}
      onClick={onNavigate}
      aria-current={active ? 'page' : undefined}
      className={cn(
        'group flex items-center gap-3 rounded-md py-2 text-sm transition-colors',
        'focus-visible:ring-sidebar-ring outline-none focus-visible:ring-2',
        collapsed ? 'justify-center px-2' : 'px-3',
        active
          ? 'bg-sidebar-primary text-sidebar-primary-foreground font-medium'
          : 'text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
      )}
    >
      <Icon className="size-4 shrink-0" aria-hidden />

      {collapsed ? (
        // Le libellé reste dans l'accessibilité même quand il quitte l'écran :
        // une barre d'icônes muettes serait illisible au lecteur d'écran.
        <span className="sr-only">{label}</span>
      ) : (
        <>
          <span className="truncate">{label}</span>
          <NavMarker count={count} ready={item.ready} active={active} />
        </>
      )}
    </Link>
  );

  if (!collapsed) return link;

  /*
   * Radix ne connaît que des côtés PHYSIQUES, et aucun `DirectionProvider`
   * n'est monté : « à côté de la barre » vaut donc `right` en latin et `left`
   * en arabe. Le déduire du sens d'écriture évite une infobulle qui sortirait
   * par-dessus la barre elle-même (règle 5 de CLAUDE.md, appliquée à une
   * bibliothèque qui n'a pas de propriétés logiques).
   */
  const side = localeDirection(locale) === 'rtl' ? 'left' : 'right';

  return (
    <Tooltip>
      <TooltipTrigger asChild>{link}</TooltipTrigger>
      <TooltipContent side={side} sideOffset={8}>
        {label}
        {count !== undefined && count > 0 ? ` (${count})` : ''}
      </TooltipContent>
    </Tooltip>
  );
}

/**
 * Une section repliable — C10 bloc 2.
 *
 * L'en-tête est un vrai `<button>` : il est donc atteignable au clavier et
 * actionnable à l'espace comme à l'entrée sans qu'on ait à le réimplémenter.
 *
 * Le panneau reste MONTÉ, masqué par `display: none` quand il est replié.
 * Deux raisons, et la première est la bonne : `aria-controls` doit désigner un
 * élément qui existe, sans quoi la relation annoncée par `aria-expanded` ne
 * mène nulle part. La seconde est que `display: none` retire les liens de
 * l'ordre de tabulation aussi sûrement que les démonter — la tabulation ne
 * traverse donc jamais de lien invisible.
 */
function NavSectionGroup({
  sectionKey,
  items,
  badges,
  open,
  onToggle,
  onNavigate,
}: {
  sectionKey: NavSectionKey;
  items: readonly NavItem[];
  badges?: NavBadges;
  open: boolean;
  onToggle: () => void;
  onNavigate?: () => void;
}) {
  const tSections = useTranslations('nav.sections');
  const panelId = useId();

  return (
    <div className="flex flex-col gap-1">
      <button
        type="button"
        onClick={onToggle}
        aria-expanded={open}
        aria-controls={panelId}
        className={cn(
          'text-sidebar-foreground/45 hover:text-sidebar-foreground/80 flex items-center gap-2 rounded-md px-3 py-1.5',
          'text-[11px] font-semibold tracking-wider uppercase transition-colors',
          'focus-visible:ring-sidebar-ring outline-none focus-visible:ring-2',
        )}
      >
        <span className="truncate text-start">{tSections(sectionKey)}</span>
        <ChevronDown
          className={cn(
            'ms-auto size-3.5 shrink-0 transition-transform duration-200',
            open && 'rotate-180',
          )}
          aria-hidden
        />
      </button>

      {/* `hidden` en CLASSE et non en attribut : l'attribut HTML pose
          `display: none` par la feuille de l'agent, que la classe `flex`
          écraserait aussitôt. */}
      <div id={panelId} className={cn('flex-col gap-1', open ? 'flex' : 'hidden')}>
        {items.map((item) => (
          <NavLink key={item.href} item={item} count={badges?.[item.key]} onNavigate={onNavigate} />
        ))}
      </div>
    </div>
  );
}

/** Corps du menu, partagé par la barre latérale et le tiroir. */
export function SidebarNav({
  granted,
  merchant = false,
  badges,
  onNavigate,
  collapsed = false,
  initialOpenSection = null,
}: {
  /** Permissions effectives du compte — le menu ne montre que l'atteignable. */
  granted: readonly string[];
  /**
   * Espace VENDEUR — chantier « Correctifs avant démo », point 6.
   *
   * Le RATTACHEMENT, pas le rôle, comme partout ailleurs. Il ne change aucune
   * visibilité : seulement le NOM de quelques entrées, là où le même écran ne
   * dit pas la même chose des deux côtés du comptoir.
   */
  merchant?: boolean;
  /** Compteurs en attente, par entrée. Lus côté serveur par le layout. */
  badges?: NavBadges;
  onNavigate?: () => void;
  collapsed?: boolean;
  /** Section retenue au cookie, lue au SSR. `null` = aucune préférence. */
  initialOpenSection?: NavSectionKey | null;
}) {
  const tNav = useTranslations('nav');
  const pathname = usePathname();
  const sections = visibleSections(granted, merchant);

  /*
   * Calculé PENDANT le premier rendu, pas dans un effet : l'initialiseur de
   * `useState` s'exécute identiquement au serveur et au client, donc le HTML
   * envoyé porte déjà la bonne section ouverte. Un `useEffect` produirait le
   * flash que le bloc 4 interdit.
   */
  const [openSection, setOpenSection] = useState<NavSectionKey | null>(() =>
    initialSection(pathname, initialOpenSection),
  );

  /*
   * L'ouverture automatique suit la NAVIGATION, pas le rendu — C10 bloc 3.
   *
   * Ajustement PENDANT le rendu, et non dans un `useEffect`. React le
   * documente pour ce cas précis — un état qui doit changer quand une prop
   * change — et c'est aussi ce que la règle `react-hooks` du compilateur
   * impose : un `setState` dans un effet déclenche un rendu en cascade, donc
   * une peinture avec l'ancienne section ouverte avant la bonne.
   *
   * La mémoire du chemin précédent est ce qui distingue « on a navigué » de
   * « on a re-rendu ». Sans elle, la section de la page courante se rouvrirait
   * à chaque rendu, et l'utilisateur qui vient d'en ouvrir une autre sans
   * changer de page verrait son geste défait aussitôt.
   */
  const [seenPath, setSeenPath] = useState(pathname);

  if (seenPath !== pathname) {
    setSeenPath(pathname);
    const target = sectionOfPath(pathname);
    if (target !== null && target !== openSection) setOpenSection(target);
  }

  const toggle = (key: NavSectionKey) => {
    // Une seule ouverte à la fois : ouvrir ferme l'autre, recliquer replie.
    const next = openSection === key ? null : key;
    setOpenSection(next);
    writePreference(NAV_SECTION_COOKIE, next ?? '');
  };

  return (
    <nav
      className={cn('flex flex-col gap-4', collapsed ? 'px-2' : 'px-3')}
      aria-label={tNav('mainMenu')}
    >
      {/* Tableau de bord reste hors accordéon : il n'appartient à aucune
          section, et c'est le point de départ de la journée. */}
      <NavLink item={DASHBOARD_ITEM} onNavigate={onNavigate} collapsed={collapsed} />

      {collapsed ? (
        /*
         * Réduite, la barre abandonne l'accordéon — C10 bloc 5.
         *
         * Replier une section dont on ne peut pas lire le titre n'aurait aucun
         * sens : il resterait un chevron muet. Les entrées sont donc à plat,
         * séparées par un filet, et chacune garde son libellé au survol.
         */
        sections.map((section) => (
          <div
            key={section.key}
            className="border-sidebar-border flex flex-col gap-1 border-t pt-4 first:border-t-0 first:pt-0"
          >
            {section.items.map((item) => (
              <NavLink
                key={item.href}
                item={item}
                count={badges?.[item.key]}
                onNavigate={onNavigate}
                collapsed
              />
            ))}
          </div>
        ))
      ) : (
        <div className="flex flex-col gap-2">
          {sections.map((section) => (
            <NavSectionGroup
              key={section.key}
              sectionKey={section.key}
              items={section.items}
              badges={badges}
              open={openSection === section.key}
              onToggle={() => toggle(section.key)}
              onNavigate={onNavigate}
            />
          ))}
        </div>
      )}
    </nav>
  );
}
