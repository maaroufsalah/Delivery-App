'use client';

import {
  findParcelStatus,
  merchantReturnStage,
  RETURN_PENDING,
  RETURNED_TO_CLIENT,
  type ReturnParcelLine,
} from '@delivery/shared';
import { PackageX, Search } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { CellBadge } from '@/components/data-table/cells';
import { ExportExcelButton } from '@/components/export-excel-button';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Link, useRouter } from '@/i18n/navigation';
import { exportListAction } from '@/lib/actions/documents';

/**
 * « MES RETOURS » DU VENDEUR — lot V, V1 (D-234).
 *
 * L'écran de l'agence (`PendingScreen`) parle livreurs, remises et création en
 * masse : c'est la pile du comptoir. Le vendeur lit une liste plate de SES
 * retours, déjà triée par le serveur, et une seule question par ligne : où en
 * est-il ? « En route vers vous » ou « Rendu ». La couleur de l'étape est celle
 * du statut qui la porte, jamais une palette locale.
 */
export function MerchantReturnsScreen({
  parcels,
  search,
}: {
  parcels: ReturnParcelLine[];
  search: string;
}) {
  const t = useTranslations('returns.merchant');
  const tCommon = useTranslations('common');
  // Le libellé des autres boutons « Exporter Excel », déjà traduit dans les trois langues.
  const tExport = useTranslations('routes.detail');
  const router = useRouter();
  const [value, setValue] = useState(search);
  const [pending, startTransition] = useTransition();

  const submitSearch = (event: React.FormEvent) => {
    event.preventDefault();
    startTransition(() => {
      router.replace(
        value ? `/returns/pending?search=${encodeURIComponent(value)}` : '/returns/pending',
        { scroll: false },
      );
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
      </form>

      {/*
        L'export rejoue la recherche AFFICHÉE (celle de l'URL, pas la saisie en
        cours) ; la portée vendeur est posée par le serveur (D-234), et les
        colonnes sont celles de cet écran — chantier « Affichage web et
        étiquette », point 4.
      */}
      <div className="flex justify-end">
        <ExportExcelButton
          label={tExport('exportExcel')}
          filename="mes-retours"
          disabled={parcels.length === 0}
          onExport={() => exportListAction('returns/pending', search ? { search } : {})}
        />
      </div>

      {parcels.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-2 py-12 text-center">
            <PackageX className="text-muted-foreground size-8" />
            <p className="font-medium">{t('empty')}</p>
            <p className="text-muted-foreground text-sm">{t('emptyHint')}</p>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardContent className="flex flex-col gap-2 pt-6">
            <p className="text-muted-foreground text-sm">{t('count', { count: parcels.length })}</p>
            {parcels.map((parcel) => (
              <MerchantReturnRow key={parcel.id} parcel={parcel} />
            ))}
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function MerchantReturnRow({ parcel }: { parcel: ReturnParcelLine }) {
  const t = useTranslations('returns.merchant');
  const format = useFormatter();
  const stage = merchantReturnStage(parcel.statusId);
  const color = findParcelStatus(stage === 'returned' ? RETURNED_TO_CLIENT : RETURN_PENDING)?.color;

  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-lg border p-3 text-sm">
      <Link
        href={`/parcels/${encodeURIComponent(parcel.code)}`}
        className="font-mono text-[13px] font-medium tracking-tight hover:underline"
      >
        {parcel.code}
      </Link>

      <span className="min-w-0 flex-1 truncate">
        {parcel.recipientName}
        <span className="text-muted-foreground"> · {parcel.destinationCityName}</span>
      </span>

      {parcel.code.startsWith('RET-') ? <CellBadge label={t('exchange')} /> : null}
      <CellBadge label={t(`stage.${stage}`)} color={color} />

      <span className="text-muted-foreground shrink-0 text-xs tabular-nums">
        {format.dateTime(new Date(parcel.statusAt), 'stamp')}
      </span>
    </div>
  );
}
