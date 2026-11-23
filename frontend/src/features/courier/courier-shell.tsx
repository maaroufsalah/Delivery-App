'use client';

import {
  Banknote,
  Home,
  ListChecks,
  LogOut,
  Megaphone,
  Menu,
  MessageSquare,
  Package,
  RotateCcw,
  Route,
  Settings,
  TrendingUp,
  User,
  Wallet,
  Warehouse,
  type LucideIcon,
} from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useRouter } from 'next/navigation';
import { useState, useTransition, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet';
import { Link, usePathname } from '@/i18n/navigation';
import { logoutAction } from '@/lib/actions/auth';
import { cn } from '@/lib/utils';
import { CompanyLogo } from '@/components/layout/company-logo';

/**
 * LA COQUE DE L'ESPACE LIVREUR SUR LE WEB — L1 bloc 1.
 *
 * **Un miroir de la coque mobile M4, pas une seconde application.** Mêmes
 * entrées, mêmes libellés, même ordre : un livreur qui passe du téléphone au
 * navigateur de l'agence doit retrouver « Ma tournée » au même endroit de sa
 * tête. C'est la seule raison pour laquelle cette coque ressemble à celle du
 * mobile plutôt qu'au back-office — et c'est aussi pourquoi la barre basse
 * réapparaît sous 1024 px, jusque dans le choix de ses cinq entrées.
 *
 * **Deux mises en page, un seul jeu de destinations.** Au-dessus de 1024 px,
 * une barre latérale : le navigateur d'agence est sur un écran large, et un
 * livreur qui vient y chercher un relevé n'a pas à naviguer avec son pouce.
 * En dessous, la barre basse du mobile — parce que sous 1024 px, c'est
 * justement un téléphone.
 *
 * **Ce que la coque n'est pas** : le back-office rétréci. `(app)` porte
 * quarante entrées dont un livreur n'en utilise aucune, et son layout est
 * désormais fermé à qui n'y a pas droit (L1 bloc 0).
 */

export interface CourierNavUser {
  name: string;
  agency: string | null;
  company: string;
  logo: string | null;
  /** Le fond du logo (Paramètres > Société) — point 5. */
  logoBackground: string | null;
  /** Permissions effectives : une entrée absente vaut mieux qu'un 403. */
  permissions: readonly string[];
}

/**
 * Les clés de navigation, en union fermée.
 *
 * `string` compilerait, et `t(entry.key)` ne serait plus vérifié : une entrée
 * ajoutée sans sa traduction s'afficherait alors sous son identifiant, en
 * production, sans que rien ne l'ait signalé.
 */
type NavKey =
  | 'home'
  | 'parcels'
  | 'circuits'
  | 'pickups'
  | 'returns'
  | 'billing'
  | 'pettyCash'
  | 'stats'
  | 'statuses'
  | 'claims'
  | 'news'
  | 'profile'
  | 'settings';

interface Entry {
  key: NavKey;
  href: string;
  icon: LucideIcon;
  /** La permission exigée, quand l'entrée en exige une. */
  needs?: string;
}

/**
 * L'ordre de la barre latérale — celui de la mission L2, et celui du mobile.
 *
 * Il a changé au bloc 9 : « Ma tournée » devient « Circuits » — depuis C12 un
 * livreur en a deux le même jour (D-085) et l'écran n'en montrait qu'un —
 * « Ramassages » apparaît, « Mes remises » et « Mes relevés » se rangent sous
 * « Facturation », qui les rassemble avec les quatre tuiles d'Anass, et
 * « Statuts » et « Paramètres » prennent leur place.
 */
const NAV: Entry[] = [
  { key: 'home', href: '/me', icon: Home },
  { key: 'parcels', href: '/me/parcels', icon: Package },
  { key: 'circuits', href: '/me/circuits', icon: Route },
  { key: 'pickups', href: '/me/pickups', icon: Warehouse },
  { key: 'returns', href: '/me/returns', icon: RotateCcw },
  { key: 'billing', href: '/me/billing', icon: Banknote },
  // D-168 — ses dépenses de petite caisse ; la clé se retire depuis la matrice.
  { key: 'pettyCash', href: '/me/petty-cash', icon: Wallet, needs: 'pettycash.courier' },
  { key: 'stats', href: '/me/stats', icon: TrendingUp },
  { key: 'statuses', href: '/me/statuses', icon: ListChecks },
  { key: 'claims', href: '/me/claims', icon: MessageSquare, needs: 'claims.read' },
  { key: 'news', href: '/me/news', icon: Megaphone },
  { key: 'profile', href: '/me/profile', icon: User },
  { key: 'settings', href: '/me/settings', icon: Settings },
];

/**
 * Les QUATRE de la barre basse, plus le menu — exactement celles du mobile
 * (L2 bloc 4) : Accueil, Colis, Circuits, Ramassages, Plus.
 *
 * Elles ne sont plus cinq : le bouton « Plus » occupe la cinquième place, comme
 * sur le téléphone. Un livreur qui passe de l'un à l'autre doit trouver ses
 * destinations au même rang, et le cinquième bouton au même endroit.
 */
const BOTTOM: NavKey[] = ['home', 'parcels', 'circuits', 'pickups'];

export function CourierShell({ user, children }: { user: CourierNavUser; children: ReactNode }) {
  const t = useTranslations('courier.nav');
  const pathname = usePathname();
  const [menu, setMenu] = useState(false);

  const allowed = NAV.filter((entry) => !entry.needs || user.permissions.includes(entry.needs));
  const bottom = BOTTOM.map((key) => allowed.find((entry) => entry.key === key)).filter(
    (entry): entry is Entry => Boolean(entry),
  );

  /*
   * `/me` est le préfixe de tout : l'égalité stricte pour lui, le préfixe pour
   * les autres. Sans cette distinction, « Accueil » resterait actif sur les
   * neuf écrans.
   */
  const isActive = (href: string) =>
    href === '/me' ? pathname === href : pathname.startsWith(href);

  return (
    /*
     * `data-space` : la SEULE marque que la recette de session lit dans le HTML
     * (C17, `scripts/recette-sessions.sh`). Un espace servi se reconnaît à cet
     * attribut, pas à un mot traduit ni à une classe Tailwind — les deux
     * bougent, et une recette qui rougit pour un changement de vocabulaire ne
     * sert plus à rien.
     */
    <div data-space="courier" className="bg-muted/30 flex min-h-dvh">
      {/* --- Desktop : la barre latérale ----------------------------------- */}
      {/* Plus étroite sous 1700 px (chantier « Affichage », point 5). */}
      <aside className="bg-background dense:w-56 hidden w-64 shrink-0 flex-col border-e lg:flex">
        <div className="flex h-16 items-center gap-3 border-b px-5">
          {user.logo ? (
            <CompanyLogo src={user.logo} background={user.logoBackground} className="rounded" />
          ) : null}
          <span className="min-w-0">
            <span className="block truncate text-sm font-semibold">{user.company}</span>
            <span className="text-muted-foreground block truncate text-xs">{t('space')}</span>
          </span>
        </div>

        <nav className="flex flex-1 flex-col gap-0.5 p-3" aria-label={t('label')}>
          {allowed.map(({ key, href, icon: Icon }) => (
            <Link
              key={key}
              href={href}
              aria-current={isActive(href) ? 'page' : undefined}
              className={cn(
                'flex min-h-11 items-center gap-3 rounded-lg px-3 text-sm transition-colors',
                'focus-visible:ring-ring outline-none focus-visible:ring-2',
                isActive(href)
                  ? 'bg-primary/10 text-primary font-medium'
                  : 'text-muted-foreground hover:bg-muted hover:text-foreground',
              )}
            >
              <Icon className="size-4 shrink-0" aria-hidden />
              {t(key)}
            </Link>
          ))}
        </nav>

        <div className="border-t p-3">
          <SignOut label={t('logout')} />
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        {children}

        {/* --- Mobile web : la barre basse, celle du mobile ---------------- */}
        <nav
          className="bg-background fixed inset-x-0 bottom-0 z-20 flex border-t lg:hidden"
          aria-label={t('label')}
        >
          {bottom.map(({ key, href, icon: Icon }) => (
            <Link
              key={key}
              href={href}
              aria-current={isActive(href) ? 'page' : undefined}
              className={cn(
                'flex min-h-14 flex-1 flex-col items-center justify-center gap-1 text-[11px]',
                'focus-visible:ring-ring outline-none focus-visible:ring-2',
                isActive(href) ? 'text-primary font-medium' : 'text-muted-foreground',
              )}
            >
              <Icon className="size-5" aria-hidden />
              {t(key)}
            </Link>
          ))}

          {/* Le menu général : tout ce qui ne tient pas dans les cinq. */}
          <Sheet open={menu} onOpenChange={setMenu}>
            <SheetTrigger asChild>
              <button
                type="button"
                aria-label={t('menu')}
                className="text-muted-foreground focus-visible:ring-ring flex min-h-14 flex-1 flex-col items-center justify-center gap-1 text-[11px] outline-none focus-visible:ring-2"
              >
                <Menu className="size-5" aria-hidden />
                {t('menu')}
              </button>
            </SheetTrigger>

            <SheetContent side="bottom" className="rounded-t-2xl">
              <SheetHeader>
                <SheetTitle>{t('menu')}</SheetTitle>
              </SheetHeader>

              <div className="flex flex-col gap-0.5 p-2">
                {allowed.map(({ key, href, icon: Icon }) => (
                  <Link
                    key={key}
                    href={href}
                    onClick={() => setMenu(false)}
                    className="hover:bg-muted flex min-h-12 items-center gap-3 rounded-lg px-3 text-sm"
                  >
                    <Icon className="text-muted-foreground size-4 shrink-0" aria-hidden />
                    {t(key)}
                  </Link>
                ))}

                <div className="mt-2 border-t pt-2">
                  <SignOut label={t('logout')} />
                </div>

                <p className="text-muted-foreground px-3 pt-3 text-center text-xs">
                  {user.company}
                  {user.agency ? ` · ${user.agency}` : ''}
                </p>
              </div>
            </SheetContent>
          </Sheet>
        </nav>
      </div>
    </div>
  );
}

/**
 * Le bouton de déconnexion, identique dans la barre et dans la feuille.
 *
 * La MÊME Server Action que le back-office (`logoutAction`) : elle révoque le
 * jeton côté serveur et efface le cookie. Une seconde implémentation aurait
 * laissé une session ouverte quelque part le jour où l'une des deux change.
 */
function SignOut({ label }: { label: string }) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  return (
    <Button
      type="button"
      variant="ghost"
      disabled={pending}
      className="w-full justify-start gap-3 text-sm"
      onClick={() =>
        startTransition(async () => {
          await logoutAction();
          router.replace('/login');
          router.refresh();
        })
      }
    >
      <LogOut className="flip-rtl size-4" aria-hidden />
      {label}
    </Button>
  );
}
