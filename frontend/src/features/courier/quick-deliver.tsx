'use client';

import { DELIVERED } from '@delivery/shared';
import { Check } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { reportOutcomeAction } from '@/lib/actions/courier';

/**
 * « LIVRER » DEPUIS LA LISTE — vue livreur web, 21/11/2026.
 *
 * **Le même appel que le bouton « Livrer » du mobile** :
 * `POST /courier/parcels/:code/status` avec `statusId = 4` et le dû en
 * `collectedAmount`, sans chèque — donc tout en espèces. Aucune route neuve,
 * aucun calcul neuf : le dû est celui de `codToCollect()` (DOMAIN.md §3.8),
 * passé par la page, et l'API refuse en 422 tout autre montant.
 *
 * **Pas de chèque ici.** Un colis attendu en chèque se livre depuis sa fiche,
 * un par un (règle de `POST /routes/:code/outcomes`, `bulk-cheque-parcels`) :
 * la page n'affiche ce bouton que pour les autres.
 *
 * Après succès, l'action invalide `/me/parcels` : la liste se relit au
 * serveur, sans rechargement ni remontée en haut de page.
 */
export function QuickDeliver({ code, dueInCents }: { code: string; dueInCents: number }) {
  const t = useTranslations('courier.parcels');
  const tCommon = useTranslations('common');
  const format = useFormatter();

  const [open, setOpen] = useState(false);
  const [done, setDone] = useState(false);
  const [pending, startTransition] = useTransition();

  const amount = format.number(dueInCents / 100, { style: 'currency', currency: 'MAD' });

  const confirm = () => {
    startTransition(async () => {
      const result = await reportOutcomeAction(code, {
        statusId: DELIVERED,
        collectedAmount: dueInCents / 100,
      });

      if (!result.ok) {
        toast.error(result.error);
        return;
      }

      setDone(true);
      setOpen(false);
      toast.success(t('deliverDone', { code }));
    });
  };

  if (done) {
    return (
      <span className="text-muted-foreground inline-flex items-center gap-1 text-xs">
        <Check className="size-4" aria-hidden />
        {t('deliverDoneShort')}
      </span>
    );
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm">{t('deliver')}</Button>
      </DialogTrigger>

      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('deliver')}</DialogTitle>
          <DialogDescription>{t('deliverConfirm', { code, amount })}</DialogDescription>
        </DialogHeader>

        <DialogFooter>
          <Button variant="outline" onClick={() => setOpen(false)} disabled={pending}>
            {tCommon('cancel')}
          </Button>
          <Button onClick={confirm} disabled={pending}>
            {t('deliver')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
