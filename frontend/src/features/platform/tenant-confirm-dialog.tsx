'use client';

import type { CompanyStatus, TenantListItem } from '@delivery/shared';
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
import { setTenantStatusAction } from '@/lib/actions/platform';

/**
 * La confirmation avant un geste qui change l'accès d'un client — C8 bloc 4.
 *
 * Les quatre actions qui passent par ici touchent à ce qu'un client peut faire
 * de son espace, parfois séance tenante. Aucune n'est destructive — rien n'est
 * jamais supprimé, et le texte le DIT, parce que c'est exactement la question
 * qu'on se pose la main sur le bouton « Suspendre ».
 *
 * « Se connecter en tant que » y figure aussi, pour une autre raison : l'accès
 * est tracé nominativement, et l'administrateur doit le savoir avant, pas en
 * relisant le journal.
 */

export type TenantAction = 'suspend' | 'reactivate' | 'activate' | 'impersonate';

/** Le statut visé par chaque action. `impersonate` n'en change aucun. */
const TARGET_STATUS: Record<Exclude<TenantAction, 'impersonate'>, CompanyStatus> = {
  suspend: 'suspended',
  reactivate: 'trial',
  activate: 'active',
};

export function TenantConfirmDialog({
  tenant,
  action,
  onImpersonate,
  onDone,
}: {
  tenant: TenantListItem;
  action: TenantAction;
  onImpersonate: (tenant: TenantListItem) => void;
  onDone: () => void;
}) {
  const t = useTranslations('platform');
  const tCommon = useTranslations('common');
  const [pending, startTransition] = useTransition();

  const confirm = () => {
    if (action === 'impersonate') {
      onImpersonate(tenant);
      onDone();
      return;
    }

    startTransition(async () => {
      const result = await setTenantStatusAction(tenant.id, TARGET_STATUS[action]);

      if (!result.ok) {
        toast.error(result.error);
        return;
      }

      toast.success(t(`status.${TARGET_STATUS[action]}`));
      onDone();
    });
  };

  return (
    <Dialog
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
    >
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t(`confirm.${action}Title`, { name: tenant.name })}</DialogTitle>
          <DialogDescription>{t(`confirm.${action}Body`)}</DialogDescription>
        </DialogHeader>

        <DialogFooter>
          <Button variant="outline" onClick={onDone} disabled={pending}>
            {tCommon('cancel')}
          </Button>
          <Button
            variant={action === 'suspend' ? 'destructive' : 'default'}
            onClick={confirm}
            disabled={pending}
          >
            {t('confirm.confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
