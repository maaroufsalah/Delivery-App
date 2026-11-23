'use client';

import type { Agency, ReturnPendingGroup } from '@delivery/shared';
import { Bike, Layers, PackageX, Plus, Search, TriangleAlert } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useCallback, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { useSearchParams } from 'next/navigation';
import { CellBadge } from '@/components/data-table/cells';
import { ExportExcelButton } from '@/components/export-excel-button';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { useRouter } from '@/i18n/navigation';
import { SlipBulkDialog } from '@/features/returns/slip-bulk-dialog';
import { exportListAction } from '@/lib/actions/documents';
import { createHandoverAction } from '@/lib/actions/returns';

/**
 * La pile d'entrée des retours : ce qui est à rendre, et à qui.
 *
 * Ce n'est pas une `DataTable` et c'est délibéré : l'unité de travail n'est pas
 * la ligne mais le LIVREUR — on ouvre une remise pour quelqu'un, pas pour un
 * colis. Une liste plate paginée aurait obligé à recomposer mentalement les
 * groupes à chaque page. Le regroupement est fait par le serveur (D-018).
 */
export function PendingScreen({
  groups,
  agencies,
  search,
}: {
  groups: ReturnPendingGroup[];
  agencies: Agency[];
  search: string;
}) {
  const t = useTranslations('returns.pending');
  const tCommon = useTranslations('common');
  const tError = useTranslations('returns.errors');
  const router = useRouter();
  // Le libellé des autres boutons « Exporter Excel », déjà traduit dans les trois langues.
  const tExport = useTranslations('routes.detail');
  const searchParams = useSearchParams();

  const [value, setValue] = useState(search);

  /**
   * L'export rejoue les filtres de l'URL (recherche, agence), comme la page
   * les passe à `listPending` — chantier « Affichage web et étiquette »,
   * point 4. Un groupe par livreur devient une colonne « Livreur ».
   */
  const exportPending = () => {
    const query: Record<string, string> = {};
    for (const key of ['search', 'agencyId', 'courierId']) {
      const param = searchParams.get(key);
      if (param) query[key] = param;
    }
    return exportListAction('returns/pending', query);
  };
  const [pending, startTransition] = useTransition();
  const tBulk = useTranslations('returns.pending.bulk');
  const [bulk, setBulk] = useState(false);
  const closeBulk = useCallback(() => {
    setBulk(false);
    router.refresh();
  }, [router]);

  const submitSearch = (event: React.FormEvent) => {
    event.preventDefault();
    startTransition(() => {
      router.replace(
        value ? `/returns/pending?search=${encodeURIComponent(value)}` : '/returns/pending',
        { scroll: false },
      );
    });
  };

  /**
   * Ouvre une remise pour ce livreur et y emmène directement.
   *
   * L'agence retenue est celle des colis du groupe : c'est elle qui les
   * réceptionne. Un livreur sans agence ne peut pas ouvrir de remise — c'est
   * une donnée manquante, pas un cas à contourner en silence.
   */
  const openHandover = (group: ReturnPendingGroup) => {
    if (!group.courierId) return;

    const agencyId = group.agencyId ?? agencies[0]?.id;
    if (!agencyId) {
      toast.error(tError('parcel-other-courier'));
      return;
    }

    startTransition(async () => {
      const result = await createHandoverAction({
        courierId: group.courierId,
        agencyId,
        note: '',
      });

      if (result.ok) router.push(`/returns/handovers/${result.data.code}`);
      else toast.error(result.error);
    });
  };

  return (
    <div className="flex flex-col gap-4">
      <form onSubmit={submitSearch} className="flex gap-2">
        <div className="relative flex-1">
          <Search className="text-muted-foreground pointer-events-none absolute inset-y-0 start-3 my-auto size-4" />
          <Input
            value={value}
            onChange={(event) => setValue(event.target.value)}
            placeholder={t('searchPlaceholder')}
            className="ps-9"
          />
        </div>
        <Button type="submit" variant="outline" disabled={pending}>
          {tCommon('search')}
        </Button>
        {/* Lot F, F3 (D-207) : à côté de l’ouverture unitaire, un bon par livreur et par agence. */}
        <Button type="button" variant="outline" className="gap-1.5" onClick={() => setBulk(true)}>
          <Layers className="size-4" />
          <span className="hidden sm:inline">{tBulk('open')}</span>
        </Button>
      </form>

      <div className="flex justify-end">
        <ExportExcelButton
          label={tExport('exportExcel')}
          filename="retours-en-attente"
          disabled={groups.length === 0}
          onExport={exportPending}
        />
      </div>

      {bulk ? <SlipBulkDialog kind="handovers" onDone={closeBulk} /> : null}

      {groups.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-2 py-12 text-center">
            <PackageX className="text-muted-foreground size-8" />
            <p className="font-medium">{t('empty')}</p>
            <p className="text-muted-foreground text-sm">{t('emptyHint')}</p>
          </CardContent>
        </Card>
      ) : (
        groups.map((group) => (
          <Card key={group.courierId ?? 'unassigned'}>
            <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-3">
              <div className="min-w-0">
                <CardTitle className="flex items-center gap-2 text-base">
                  <Bike className="size-4" />
                  {group.courierName ?? t('unassigned')}
                  <CellBadge label={t('count', { count: group.parcels.length })} />
                </CardTitle>

                <p className="text-muted-foreground mt-1 text-sm">
                  {group.courierId ? (group.agencyName ?? '-') : t('unassignedHint')}
                </p>
              </div>

              {/* Sans livreur, pas de remise : une remise atteste QUI rapporte. */}
              {group.courierId ? (
                <Button
                  size="sm"
                  className="gap-1.5"
                  disabled={pending}
                  onClick={() => openHandover(group)}
                >
                  <Plus className="size-4" />
                  <span className="hidden sm:inline">{t('createHandover')}</span>
                </Button>
              ) : (
                <TriangleAlert className="size-4 shrink-0 text-amber-500" />
              )}
            </CardHeader>

            <CardContent className="flex flex-col gap-2">
              {group.parcels.map((parcel) => (
                <ParcelRow key={parcel.id} parcel={parcel} />
              ))}
            </CardContent>
          </Card>
        ))
      )}
    </div>
  );
}

function ParcelRow({ parcel }: { parcel: ReturnPendingGroup['parcels'][number] }) {
  const format = useFormatter();

  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-lg border p-3 text-sm">
      <span className="font-mono text-[13px] font-medium tracking-tight">{parcel.code}</span>

      <span className="min-w-0 flex-1 truncate">
        {parcel.recipientName}
        <span className="text-muted-foreground"> · {parcel.destinationCityName}</span>
      </span>

      <span className="text-muted-foreground text-xs">{parcel.clientName}</span>

      <span className="text-muted-foreground shrink-0 text-xs tabular-nums">
        {format.dateTime(new Date(parcel.statusAt), 'stamp')}
      </span>
    </div>
  );
}
