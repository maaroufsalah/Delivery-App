import { Skeleton } from '@/components/ui/skeleton';
import { CardSkeleton, KpiGridSkeleton, TableSkeleton } from '@/features/dashboard/skeletons';

/**
 * LE SQUELETTE DE LA PAGE — spec § 12. Rendu par Next pendant la navigation
 * vers le tableau de bord, avant même que la page serveur ait commencé : la
 * même composition que la page, aux mêmes hauteurs.
 */
export default function DashboardLoading() {
  return (
    <>
      <div className="mb-6 flex flex-col gap-2">
        <Skeleton className="h-7 w-56" />
        <Skeleton className="h-4 w-72" />
      </div>
      <div className="flex flex-col gap-6">
        <Skeleton className="h-20 w-full rounded-xl" />
        <KpiGridSkeleton count={8} />
        <KpiGridSkeleton count={7} />
        <div className="grid gap-6 lg:grid-cols-3">
          <CardSkeleton className="lg:col-span-2" />
          <CardSkeleton />
        </div>
        <div className="grid gap-6 lg:grid-cols-3">
          <TableSkeleton className="lg:col-span-2" />
          <CardSkeleton />
        </div>
        <TableSkeleton rows={10} />
      </div>
    </>
  );
}
