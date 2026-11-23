'use client';

import { useMounted } from '@/hooks/use-mounted';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

/**
 * Cadre de graphique — C7 bloc 4.
 *
 * **La dette.** `ResponsiveContainer` de recharts mesure son parent avec un
 * `ResizeObserver`. Au rendu serveur il n'y a ni parent mesuré ni observateur :
 * le conteneur rend un graphique de zéro pixel, et le tableau de bord arrivait
 * avec deux trous blancs de 256 px qui se remplissaient après hydratation.
 *
 * **Le remède, sans dépendance nouvelle.** Le cadre porte la hauteur, et il
 * rend un `Skeleton` de la MÊME hauteur tant que le client n'a pas pris la
 * main. Il n'y a donc plus de blanc, plus de saut de mise en page, et le
 * graphique se substitue au squelette exactement à sa place.
 *
 * `useMounted` s'appuie sur `useSyncExternalStore` (D-011) : l'alternative
 * `useEffect(() => setMounted(true))` déclencherait un second rendu en cascade
 * sur chaque graphique de la page.
 *
 * La hauteur est passée en CLASSES et non en pixels : elle doit rester
 * responsive, et c'est le même jeu de classes qui habille le squelette et le
 * graphique — impossible qu'ils divergent.
 */
export function ChartFrame({
  className,
  children,
}: {
  /** Classes de hauteur, appliquées à l'identique au squelette et au contenu. */
  className?: string;
  children: React.ReactNode;
}) {
  const mounted = useMounted();

  if (!mounted) {
    return <Skeleton className={cn('w-full', className)} aria-hidden />;
  }

  return <div className={cn('w-full', className)}>{children}</div>;
}
