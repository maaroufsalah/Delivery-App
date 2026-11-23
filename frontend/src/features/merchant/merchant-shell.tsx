'use client';

import {
  Banknote,
  Bell,
  Boxes,
  ClipboardList,
  FileText,
  Home,
  Layers,
  LogOut,
  Megaphone,
  Menu,
  MessageSquare,
  Package,
  PackagePlus,
  ReceiptText,
  RotateCcw,
  Truck,
  Upload,
  User,
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
 * LA COQUE DE L'E-COMMERÇANT — L1 bloc 7.
 *
 * **Elle enveloppe les écrans qui existent, elle ne les réécrit pas.** Un
 * vendeur travaille déjà sur `/parcels`, `/parcels/new`, `/invoices`,
 * `/claims` : ces écrans sont bornés à ses colis par la portée serveur, ils
 * sont testés, et ils marchent. Ce qui n'allait pas était la BARRE qui les
 * entourait — celle du back-office, quarante entrées dont il n'en utilise
 * neuf, avec des sections « Référentiel » et « Entrepôt » qu'il ouvre pour
 * découvrir qu'elles sont vides ou interdites.
 *
 * En dupliquer des copies sous `/shop/*` aurait coûté deux formulaires de
 * création de colis à maintenir — et le second aurait divergé au premier champ
 * ajouté, sur l'écran qui fait entrer l'argent.
 *
 * **Neuf destinations, dans l'ordre d'une journée de vendeur** : ce qui s'est
 * passé, mes colis, en créer un, les retours, l'argent, les réclamations, les
 * actualités, mon profil.
 *
 * **Même grammaire que le livreur (L1 bloc 1) et que le mobile (M4, D-088)** :
 * barre latérale au-dessus de 1024 px, barre basse et menu en feuille en
 * dessous.
 */

export interface MerchantNavUser {
  name: string;
  company: string;
  logo: string | null;
  /** Le fond du logo (Paramètres > Société) — point 5. */
  logoBackground: string | null;
  permissions: readonly string[];
  /**
   * Lot V, V4 (D-234) : le régime de sa fiche. `normal` retire « Mes
   * versements » (il est réglé par facture) ; `null`, lecture ratée, ne retire rien.
   */
  billingType: 'normal' | 'professional' | null;
}

type NavKey =
  | 'home'
  | 'parcels'
  | 'new'
  | 'import'
  | 'pickupNotes'
  | 'dropoffNotes'
  | 'products'
  | 'stock'
  | 'returns'
  | 'invoices'
  | 'payouts'
  | 'cheques'
  | 'claims'
  | 'news'
  | 'notifications'
  | 'profile';

interface Entry {
  key: NavKey;
  href: string;
  icon: LucideIcon;
  needs?: string;
}

/**
 * Les destinations, et les routes EXISTANTES qu'elles visent.
 *
 * Aucune n'est neuve sauf `/feed` : le flux d'actualités n'avait pas d'écran
 * hors du bandeau du tableau de bord, et l'entrée « Actualités » du menu
 * mobile n'avait donc rien à ouvrir ici.
 */
const NAV: Entry[] = [
  { key: 'home', href: '/dashboard', icon: Home },
  { key: 'parcels', href: '/parcels', icon: Package, needs: 'parcels.read' },
  { key: 'new', href: '/parcels/new', icon: PackagePlus, needs: 'parcels.write' },
  /*
   * L'IMPORT EXCEL — V2 vendeur bloc 3, spécification §3.
   *
   * L'écran existait entièrement (modèle, aperçu, erreurs ligne à ligne,
   * rapport), sous `parcels.write` que le vendeur porte. Il n'avait aucune
   * entrée de menu : personne ne pouvait l'atteindre autrement qu'en tapant
   * l'URL.
   */
  { key: 'import', href: '/parcels/import', icon: Upload, needs: 'parcels.write' },
  /*
   * LES BONS ET L'ENTREPÔT — V2 vendeur bloc 1.
   *
   * Quatre écrans qui existaient, que le vendeur avait le droit de lire, et
   * qu'aucun chemin ne lui ouvrait : ni entrée de menu, ni liste blanche de
   * routes. La spécification d'Anass les demande tous les quatre, et ils n'ont
   * pas eu besoin d'être écrits — seulement d'être montrés.
   */
  { key: 'pickupNotes', href: '/pickup-notes', icon: Truck, needs: 'notes.read' },
  { key: 'dropoffNotes', href: '/dropoff-notes', icon: ClipboardList, needs: 'notes.read' },
  { key: 'products', href: '/products', icon: Boxes, needs: 'warehouse.read' },
  { key: 'stock', href: '/stock', icon: Layers, needs: 'warehouse.read' },
  { key: 'returns', href: '/returns/pending', icon: RotateCcw, needs: 'returns.read' },
  { key: 'invoices', href: '/invoices', icon: FileText, needs: 'finance.read' },
  { key: 'payouts', href: '/cod-payouts', icon: Banknote, needs: 'finance.read' },
  /*
   * « MES CHÈQUES » — lot S2, correctif du lot S (D-231).
   *
   * Le lot S avait ajouté l'entrée au menu du back-office
   * (`navigation-config.ts`, libellé `myCheques`) — que le vendeur n'affiche
   * jamais : il vit dans cette coque. Seule la tuile de l'accueil ouvrait la
   * page. Même route, même permission et même icône que l'entrée back-office ;
   * pas de pastille : le compteur reste sur la tuile (RAPPORT-lot-S §2, point 7).
   */
  { key: 'cheques', href: '/cheques', icon: ReceiptText, needs: 'finance.read' },
  { key: 'claims', href: '/claims', icon: MessageSquare, needs: 'claims.read' },
  { key: 'news', href: '/feed', icon: Megaphone },
  { key: 'notifications', href: '/notifications', icon: Bell },
  { key: 'profile', href: '/profile', icon: User },
];

/**
 * Les quatre du bas, puis « Plus » — V2 vendeur bloc 1.
 *
 * **La même barre que l'application mobile**, et c'est volontaire : la
 * spécification d'Anass fixe `Accueil · Colis · Bons · Warehouse · Plus`, et un
 * vendeur qui passe du téléphone au navigateur dans la même journée ne doit pas
 * avoir à réapprendre où sont les choses (D-088).
 *
 * « Créer un colis » quitte la barre basse : il reste dans le menu et sur
 * l'écran Colis, là où on l'utilise. Lui garder un cinquième des pouces aurait
 * coûté la place des bons, que le vendeur ouvre plusieurs fois par jour.
 */
const BOTTOM: NavKey[] = ['home', 'parcels', 'pickupNotes', 'products'];

export function MerchantShell({ user, children }: { user: MerchantNavUser; children: ReactNode }) {
  const t = useTranslations('merchantNav');
  const pathname = usePathname();
  const [menu, setMenu] = useState(false);

  const permitted = NAV.filter((entry) => !entry.needs || user.permissions.includes(entry.needs));
  // Lot V, V4 (D-234) : un client normal est réglé par facture, pas par bon de versement.
  const allowed = permitted.filter(
    (entry) => !(entry.key === 'payouts' && user.billingType === 'normal'),
  );
  const bottom = BOTTOM.map((key) => allowed.find((entry) => entry.key === key)).filter(
    (entry): entry is Entry => Boolean(entry),
  );

  /*
   * `/parcels/new` est un préfixe de `/parcels` : sans l'égalité stricte pour
   * le second, les deux entrées seraient actives en même temps sur l'écran de
   * création.
   */
  const isActive = (href: string) =>
    href === '/parcels' || href === '/dashboard' ? pathname === href : pathname.startsWith(href);

  return (
    // `data-space` : voir `courier-shell.tsx` — la marque que lit la recette.
    <div data-space="merchant" className="bg-muted/30 flex min-h-dvh">
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
                </p>
              </div>
            </SheetContent>
          </Sheet>
        </nav>
      </div>
    </div>
  );
}

/** La MÊME Server Action que partout : une seule façon de fermer une session. */
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
