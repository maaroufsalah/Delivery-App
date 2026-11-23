import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { Card, CardContent } from '@/components/ui/card';
import { CourierParcelList } from '@/features/courier/courier-parcel-list';
import {
  COURIER_PARCELS_STEP,
  type CourierParcelFilters,
  loadCourierParcels,
  toCourierRows,
} from '@/features/courier/courier-parcel-rows';
import { ParcelSearch } from '@/features/courier/parcel-search';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courier.nav');
  return { title: t('parcels') };
}

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

/**
 * MES COLIS — L1 bloc 3.
 *
 * **Aucun endpoint neuf, et aucun filtre de façade.** `GET /parcels` porte
 * `parcels.read`, que le rôle `COURIER` possède, et `ParcelsService.scoped()`
 * borne la lecture à `courierId = le sien`. Passer un `courierId` depuis cet
 * écran donnerait l'illusion d'une garantie que le premier appel direct à
 * l'API contournerait — c'est le serveur qui garantit (**D-082**).
 *
 * **La recherche vit dans l'URL**, pas dans un état de composant : un livreur
 * qui envoie un lien à son agence envoie ce qu'il voit, et le bouton retour du
 * navigateur refait la recherche précédente.
 */
export default async function CourierParcelsPage({ searchParams }: { searchParams: SearchParams }) {
  const params = await searchParams;
  const one = (value: string | string[] | undefined) => (Array.isArray(value) ? value[0] : value);

  const search = one(params.search)?.trim() ?? '';
  const statusId = Number(one(params.statusId)) || undefined;
  const statusGroup = one(params.statusGroup);

  /*
   * TOUS LES COLIS, À LA LONGUEUR CHOISIE — 21/11/2026. `?limit=` vaut 25, 50,
   * 100, `all`, ou ce que « Charger plus » a ajouté (+25). Dans l'URL comme la
   * recherche : rendue au serveur, partageable, retrouvée par le bouton
   * retour. Le livreur affiche ce qu'il veut traiter, puis coche ce qu'il voit.
   */
  const rawLimit = one(params.limit);
  const limit =
    rawLimit === 'all'
      ? Number.POSITIVE_INFINITY
      : Math.max(COURIER_PARCELS_STEP, Math.floor(Number(rawLimit) || COURIER_PARCELS_STEP));
  const filters: CourierParcelFilters = {
    ...(search ? { search } : {}),
    ...(statusId ? { statusId } : {}),
    ...(statusGroup ? { statusGroup } : {}),
  };

  const [t, page] = await Promise.all([
    getTranslations('courier.parcels'),
    loadCourierParcels(filters, limit),
  ]);

  const rows = toCourierRows(page.items);

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold tracking-tight">{t('title')}</h1>

      <ParcelSearch search={search} statusId={statusId} statusGroup={statusGroup} />

      {rows.length === 0 ? (
        <Card>
          <CardContent className="text-muted-foreground p-8 text-center text-sm">
            {t(search || statusId || statusGroup ? 'noMatch' : 'empty')}
          </CardContent>
        </Card>
      ) : (
        <CourierParcelList
          // Un filtre neuf repart d'une sélection vide.
          key={JSON.stringify(filters)}
          rows={rows}
          total={page.total}
          limit={Number.isFinite(limit) ? limit : null}
          filters={filters}
        />
      )}
    </div>
  );
}
