'use client';

import type { CourierRouteNote, CourierRouteParcel } from '@delivery/shared';
import { ClipboardList, Loader2, PackageCheck, Store } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { useRouteError } from '@/features/routes/use-route-error';
import { useRouter } from '@/i18n/navigation';
import { pickupRouteNoteAction } from '@/lib/actions/routes';

/**
 * LE LIVREUR VALIDE UN BON, DEPUIS LE WEB — retouches Anass, point 5.
 *
 * L'espace livreur du web double l'application mobile (L2) : un téléphone tombe
 * en panne, et la tournée continue. Ce qu'il ne double PAS, c'est le scan — il
 * n'y a pas de caméra devant un écran d'agence, et en simuler une n'ajouterait
 * rien qu'un champ de saisie de plus.
 *
 * Restent les cases à cocher, c'est-à-dire « Valider manuellement », qui est
 * l'une des deux façons demandées. L'autre vit sur le téléphone, avec la
 * caméra, là où elle sert.
 *
 * **Le contrôle d'appartenance n'est pas ici.** La liste proposée EST celle du
 * bon — le composant ne reçoit que ses colis — et le serveur refait la
 * vérification de toute façon. C'est lui qui écrit, donc lui qui décide.
 */
export function CourierNotePickup({
  routeCode,
  note,
  parcels,
}: {
  routeCode: string;
  note: CourierRouteNote;
  /** Les colis DE CE BON, déjà filtrés par la page. */
  parcels: CourierRouteParcel[];
}) {
  const t = useTranslations('courier.notes');
  const tDetail = useTranslations('notes.detail');
  const router = useRouter();
  const translateError = useRouteError();

  const [pending, startTransition] = useTransition();
  const [picked, setPicked] = useState<string[]>([]);

  const done = note.status !== 'awaiting-validation';

  /*
   * VALIDATION PARTIELLE — Recette 5, points 5 et 7, validé par Anass, comme
   * sur le téléphone : les non cochés sont retirés du bon, et le premier appui
   * le DIT avec les nombres ; le second confirme.
   */
  const [confirming, setConfirming] = useState(false);
  const missing = parcels.length - picked.length;

  function submit() {
    if (missing > 0 && !confirming) {
      setConfirming(true);
      return;
    }
    startTransition(async () => {
      const result = await pickupRouteNoteAction(routeCode, note.id, picked, true);

      if (result.ok) {
        toast.success(t('validated', { count: String(picked.length) }));
        setPicked([]);
        setConfirming(false);
        router.refresh();
        return;
      }

      toast.error(translateError(result));
    });
  }

  return (
    <Card className={done ? 'opacity-70' : undefined}>
      <CardContent className="flex flex-col gap-4 p-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex min-w-0 items-start gap-3">
            {done ? (
              <PackageCheck className="text-muted-foreground mt-0.5 size-5 shrink-0" aria-hidden />
            ) : (
              <ClipboardList className="text-primary mt-0.5 size-5 shrink-0" aria-hidden />
            )}
            <div className="min-w-0">
              {/* LE NOM DU VENDEUR d'abord : c'est chez lui que le livreur va. */}
              <p className="flex items-center gap-1.5 text-sm font-medium">
                <Store className="text-muted-foreground size-3.5 shrink-0" aria-hidden />
                <span className="truncate">{note.clientName}</span>
              </p>
              <p className="text-muted-foreground truncate text-xs">
                <span className="font-mono">{note.code}</span>
                {' · '}
                {note.pickupAddress || note.cityName}
              </p>
            </div>
          </div>

          {done ? (
            <Badge variant="secondary" className="rounded-full font-normal">
              {t('pickedUp')}
            </Badge>
          ) : null}
        </div>

        {done ? null : (
          <>
            <ul className="flex flex-col gap-1.5">
              {parcels.map((parcel) => (
                <li key={parcel.id}>
                  <label className="hover:bg-muted/40 flex cursor-pointer items-center gap-3 rounded-md border p-2.5">
                    <Checkbox
                      checked={picked.includes(parcel.id)}
                      onCheckedChange={(value) => {
                        setConfirming(false);
                        setPicked((current) =>
                          value === true
                            ? [...new Set([...current, parcel.id])]
                            : current.filter((entry) => entry !== parcel.id),
                        );
                      }}
                      aria-label={parcel.code}
                    />
                    <span className="min-w-0 flex-1">
                      <span className="block font-mono text-xs">{parcel.code}</span>
                      <span className="text-muted-foreground block truncate text-xs">
                        {parcel.recipientName}
                      </span>
                    </span>
                  </label>
                </li>
              ))}
            </ul>

            <div className="flex flex-wrap items-center justify-between gap-2">
              <p className="text-muted-foreground text-xs">
                {t('counter', { done: picked.length, total: String(parcels.length) })}
              </p>

              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={pending || parcels.length === 0}
                  onClick={() => {
                    setConfirming(false);
                    setPicked(parcels.map((parcel) => parcel.id));
                  }}
                >
                  {t('selectAll')}
                </Button>

                <Button
                  size="sm"
                  className="gap-1.5"
                  disabled={pending || picked.length === 0}
                  onClick={submit}
                >
                  {pending ? (
                    <Loader2 className="size-4 animate-spin" />
                  ) : (
                    <PackageCheck className="size-4" aria-hidden />
                  )}
                  {confirming
                    ? tDetail('partialTitle', {
                        count: picked.length,
                        total: String(parcels.length),
                      })
                    : t('validate', { count: String(picked.length) })}
                </Button>
              </div>
            </div>
            {confirming && missing > 0 ? (
              <p className="text-destructive text-xs">
                {tDetail('partialBody', {
                  missing,
                  count: String(picked.length),
                  total: String(parcels.length),
                })}
              </p>
            ) : null}
          </>
        )}
      </CardContent>
    </Card>
  );
}
