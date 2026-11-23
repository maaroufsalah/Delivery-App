'use client';

import { useTranslations } from 'next-intl';
import { useTransition } from 'react';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useRouter } from '@/i18n/navigation';

export type MerchantReturnsTab = 'on-the-way' | 'slips';

/**
 * Les onglets de l'espace vendeur, en clair — chantier « Onglets vendeur ».
 *
 * Le style shadcn par défaut (gris sur gris) passait inaperçu : le vendeur ne
 * voyait pas qu'il y avait un second onglet. L'actif prend la couleur primaire
 * de SA marque, le texte `primary-foreground` que `readableForeground` garantit
 * lisible (D-046) ; l'inactif un fond et une bordure dérivés du texte, donc
 * visibles en clair comme en sombre. Les classes `dark:` répètent l'actif parce
 * que le composant de base y pose ses propres fonds.
 *
 * Le composant partagé n'est pas touché : les onglets de l'administration
 * gardent leur style.
 */
const LIST =
  'flex h-auto w-full flex-wrap items-stretch gap-2 rounded-none bg-transparent p-0 group-data-[orientation=horizontal]/tabs:h-auto sm:w-fit';

const TRIGGER = [
  'min-h-10 h-auto flex-1 sm:flex-none whitespace-normal px-4 py-2 text-[15px] leading-tight',
  'border-foreground/15 bg-foreground/[0.07] text-foreground font-medium',
  'hover:bg-foreground/[0.13] hover:border-foreground/30 hover:text-foreground',
  // En sombre, 7 % de blanc se fond dans la page : le gris y est plus appuyé.
  'dark:text-foreground dark:hover:text-foreground dark:border-foreground/30 dark:bg-foreground/[0.15] dark:hover:bg-foreground/[0.24]',
  'data-[state=active]:border-primary data-[state=active]:bg-primary data-[state=active]:font-semibold data-[state=active]:text-primary-foreground data-[state=active]:shadow-sm',
  'dark:data-[state=active]:border-primary dark:data-[state=active]:bg-primary dark:data-[state=active]:text-primary-foreground',
  'data-[state=active]:hover:bg-primary/90',
].join(' ');

export function MerchantReturnsTabs({
  tab,
  counts = {},
  children,
}: {
  tab: MerchantReturnsTab;
  /**
   * Les compteurs DÉJÀ lus par la page — elle ne lit que l'onglet ouvert :
   * aucun appel de plus pour remplir l'autre.
   */
  counts?: Partial<Record<MerchantReturnsTab, number>>;
  children: React.ReactNode;
}) {
  const t = useTranslations('returns.merchant.tabs');
  const router = useRouter();
  const [, startTransition] = useTransition();

  const change = (next: string) => {
    startTransition(() => {
      router.replace(next === 'slips' ? '/returns/pending?tab=slips' : '/returns/pending', {
        scroll: false,
      });
    });
  };

  const label = (key: MerchantReturnsTab, text: string) =>
    counts[key] === undefined ? text : `${text} (${counts[key]})`;

  return (
    <Tabs value={tab} onValueChange={change} className="gap-4">
      <TabsList className={LIST}>
        <TabsTrigger value="on-the-way" className={TRIGGER}>
          {label('on-the-way', t('onTheWay'))}
        </TabsTrigger>
        <TabsTrigger value="slips" className={TRIGGER}>
          {label('slips', t('slips'))}
        </TabsTrigger>
      </TabsList>
      {children}
    </Tabs>
  );
}
