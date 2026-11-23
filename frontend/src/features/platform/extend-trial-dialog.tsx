'use client';

import type { TenantListItem } from '@delivery/shared';
import { useTranslations } from 'next-intl';
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
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { extendTrialAction } from '@/lib/actions/platform';

/**
 * Prolonger un essai — C8 bloc 4.
 *
 * Le geste qu'on fait pendant que le client attend au téléphone : il doit tenir
 * en deux clics et un nombre. Les jours s'AJOUTENT à l'échéance en cours, ou à
 * aujourd'hui si elle est passée — c'est la règle du service, et la description
 * la rappelle pour qu'on n'ait pas à la deviner.
 *
 * Prolonger un espace suspendu le rouvre. C'est voulu : quand on accorde du
 * temps à quelqu'un, on ne lui laisse pas la porte fermée.
 */

/** Les durées qu'on accorde réellement. Le champ reste libre pour le reste. */
const PRESETS = [7, 15, 30] as const;

export function ExtendTrialDialog({
  tenant,
  onDone,
}: {
  tenant: TenantListItem;
  onDone: () => void;
}) {
  const t = useTranslations('platform');
  const tCommon = useTranslations('common');
  const [days, setDays] = useState('30');
  const [pending, startTransition] = useTransition();

  const parsed = Number.parseInt(days, 10);
  const valid = Number.isInteger(parsed) && parsed >= 1 && parsed <= 365;

  const submit = () => {
    if (!valid) return;

    startTransition(async () => {
      const result = await extendTrialAction(tenant.id, parsed);

      if (!result.ok) {
        toast.error(result.error);
        return;
      }

      toast.success(t('extend.submit'));
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
          <DialogTitle>{t('extend.title', { name: tenant.name })}</DialogTitle>
          <DialogDescription>{t('extend.description')}</DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-3">
          <Label htmlFor="trial-days">{t('extend.days')}</Label>

          <div className="flex flex-wrap items-center gap-2">
            {PRESETS.map((preset) => (
              <Button
                key={preset}
                type="button"
                size="sm"
                variant={days === String(preset) ? 'default' : 'outline'}
                onClick={() => setDays(String(preset))}
              >
                {preset}
              </Button>
            ))}

            <Input
              id="trial-days"
              type="number"
              min={1}
              max={365}
              inputMode="numeric"
              dir="ltr"
              value={days}
              onChange={(event) => setDays(event.target.value)}
              className="w-24"
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onDone} disabled={pending}>
            {tCommon('cancel')}
          </Button>
          <Button onClick={submit} disabled={pending || !valid}>
            {t('extend.submit')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
