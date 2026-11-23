'use client';

import type { City, Courier, RouteListItem } from '@delivery/shared';
import { Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useTransition } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { RouteForm } from '@/features/routes/route-form';
import { useRouteError } from '@/features/routes/use-route-error';
import { deleteRouteAction } from '@/lib/actions/routes';

type DraftRoute = Pick<RouteListItem, 'code' | 'cityId' | 'courierId' | 'kind' | 'exitDate'>;

/**
 * MODIFIER / SUPPRIMER UN BROUILLON — D-173 (lot B, point B2).
 *
 * Une seule paire de dialogues pour la liste ET la fiche du circuit : deux
 * copies auraient divergé au premier libellé. L'écran ne les propose que sur un
 * brouillon, mais c'est l'API qui tranche (`route-not-draft`, `route-not-empty`)
 * et ses refus sont traduits ici.
 */
export function RouteDraftDialogs({
  route,
  cities,
  couriers,
  locked,
  editing,
  deleting,
  onClose,
  onDeleted,
}: {
  route: DraftRoute;
  cities: City[];
  couriers: Courier[];
  /** Brouillon non vide : ville et type figés. */
  locked: boolean;
  editing: boolean;
  deleting: boolean;
  onClose: () => void;
  onDeleted: () => void;
}) {
  const t = useTranslations('routes.form');
  const tCommon = useTranslations('common');
  const translateError = useRouteError();
  const [pending, startTransition] = useTransition();

  function confirmDelete() {
    startTransition(async () => {
      const result = await deleteRouteAction(route.code);
      if (result.ok) {
        toast.success(t('deleted', { code: route.code }));
        onClose();
        onDeleted();
        return;
      }
      toast.error(translateError(result));
    });
  }

  return (
    <>
      {editing ? (
        <RouteForm
          cities={cities}
          couriers={couriers}
          route={route}
          locked={locked}
          onDone={onClose}
        />
      ) : null}

      <Dialog
        open={deleting}
        onOpenChange={(next) => {
          if (!next) onClose();
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('deleteTitle', { code: route.code })}</DialogTitle>
            <DialogDescription>{t('deleteHint')}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={onClose} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            <Button variant="destructive" onClick={confirmDelete} disabled={pending}>
              {pending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
              {t('deleteConfirm')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
