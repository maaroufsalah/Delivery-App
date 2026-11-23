import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { cn } from '@/lib/utils';

/**
 * LES SQUELETTES DU TABLEAU DE BORD — spec § 12.
 *
 * Mêmes hauteurs et mêmes grilles que les widgets qu'ils remplacent : la page
 * ne saute pas quand les données arrivent. Composants SERVEUR, sans état.
 */

export function KpiGridSkeleton({ count = 8 }: { count?: number }) {
  return (
    <div className="flex flex-col gap-2">
      <Skeleton className="h-3 w-24" />
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {Array.from({ length: count }, (_, index) => (
          <Card key={index} className="gap-0 p-4">
            <div className="flex items-start justify-between gap-3">
              <div className="flex min-w-0 flex-1 flex-col gap-2">
                <Skeleton className="h-3 w-20" />
                <Skeleton className="h-7 w-16" />
                <Skeleton className="h-3 w-28" />
              </div>
              <Skeleton className="size-9 rounded-lg" />
            </div>
          </Card>
        ))}
      </div>
    </div>
  );
}

export function CardSkeleton({
  className,
  body = 'h-64 sm:h-72',
}: {
  className?: string;
  /** Classes de hauteur du corps, alignées sur le widget remplacé. */
  body?: string;
}) {
  return (
    <Card className={className}>
      <CardHeader className="gap-2">
        <Skeleton className="h-4 w-40" />
        <Skeleton className="h-3 w-64" />
      </CardHeader>
      <CardContent>
        <Skeleton className={cn('w-full', body)} />
      </CardContent>
    </Card>
  );
}

export function TableSkeleton({ rows = 5, className }: { rows?: number; className?: string }) {
  return (
    <Card className={cn('gap-0 overflow-hidden p-0', className)}>
      <CardHeader className="gap-2 px-4 py-4">
        <Skeleton className="h-4 w-40" />
        <Skeleton className="h-3 w-64" />
      </CardHeader>
      <CardContent className="flex flex-col gap-0 p-0">
        {Array.from({ length: rows }, (_, index) => (
          <div key={index} className="flex items-center gap-4 border-t px-4 py-3">
            <Skeleton className="size-6 rounded-full" />
            <Skeleton className="h-4 flex-1" />
            <Skeleton className="hidden h-4 w-16 sm:block" />
            <Skeleton className="hidden h-4 w-16 md:block" />
            <Skeleton className="h-4 w-12" />
          </div>
        ))}
      </CardContent>
    </Card>
  );
}
