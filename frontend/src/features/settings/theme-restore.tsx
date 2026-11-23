'use client';

import type { CompanySettings, ThemeSnapshot } from '@delivery/shared';
import { Loader2, RotateCcw, Undo2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { useCompanySettings } from '@/components/theme/company-settings-provider';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { restoreThemeAction } from '@/lib/actions/settings';

/**
 * « Revenir au standard » et « Annuler la dernière modification » — B-10.
 *
 * Les deux boutons vivent sur Société ET sur Thème : ils portent sur
 * l'identité visuelle ENTIÈRE — les deux couleurs et les surcharges par zone —
 * et l'admin qui veut tout remettre d'aplomb ne doit pas avoir à deviner
 * lequel des deux écrans détient le geste global. Ils s'ajoutent aux remises à
 * zéro par section livrées en C0, ils ne les remplacent pas.
 *
 * Le dialogue ne demande pas seulement « êtes-vous sûr ? » : pendant qu'il est
 * ouvert, l'application ENTIÈRE est repeinte avec l'instantané visé. On voit ce
 * qu'on va obtenir avant de le vouloir, et refuser remet tout en place.
 */
export function ThemeRestoreButtons({
  standard,
  previous,
  onRestored,
}: {
  standard: ThemeSnapshot | null;
  previous: ThemeSnapshot | null;
  onRestored: (settings: CompanySettings) => void;
}) {
  const t = useTranslations('settings.theme.restore');
  const tCommon = useTranslations('common');
  const { previewBrand } = useCompanySettings();

  const [target, setTarget] = useState<'standard' | 'previous' | null>(null);
  const [pending, startTransition] = useTransition();

  const snapshot = target === 'standard' ? standard : target === 'previous' ? previous : null;

  const open = (which: 'standard' | 'previous', value: ThemeSnapshot | null) => {
    if (!value) {
      toast.error(t(which === 'standard' ? 'noStandard' : 'nothingToUndo'));
      return;
    }

    setTarget(which);
    previewBrand({ primary: value.primaryColor, secondary: value.secondaryColor }, value.theme);
  };

  const close = () => {
    setTarget(null);
    // Refuser doit rendre l'écran d'avant, au pixel près.
    previewBrand(null);
  };

  const confirm = () => {
    if (!target) return;

    startTransition(async () => {
      const result = await restoreThemeAction(target);

      if (!result.ok) {
        previewBrand(null);
        setTarget(null);
        toast.error(
          result.code === 'FORBIDDEN'
            ? t('forbidden')
            : result.reason === 'nothing-to-restore'
              ? t('nothingToUndo')
              : (result.error ?? t('failed')),
        );
        return;
      }

      onRestored(result.data);
      setTarget(null);
      toast.success(t('done'));
    });
  };

  return (
    <>
      <Button
        type="button"
        variant="ghost"
        size="sm"
        className="gap-1.5"
        disabled={pending}
        onClick={() => open('standard', standard)}
      >
        <RotateCcw className="size-4" aria-hidden />
        {t('standard')}
      </Button>

      <Button
        type="button"
        variant="ghost"
        size="sm"
        className="gap-1.5"
        // Désactivé plutôt que masqué : le bouton doit exister à la même place
        // avant et après une modification, sinon la barre d'actions danse.
        disabled={pending || !previous}
        onClick={() => open('previous', previous)}
      >
        <Undo2 className="flip-rtl size-4" aria-hidden />
        {t('undo')}
      </Button>

      <Dialog
        open={target !== null}
        onOpenChange={(next) => {
          if (!next) close();
        }}
      >
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>
              {target === 'standard' ? t('confirmStandardTitle') : t('confirmUndoTitle')}
            </DialogTitle>
            <DialogDescription>
              {target === 'standard' ? t('confirmStandard') : t('confirmUndo')}
            </DialogDescription>
          </DialogHeader>

          {snapshot ? (
            <div className="flex items-center gap-3 rounded-lg border p-3">
              <span
                className="size-8 shrink-0 rounded-md border"
                style={{ backgroundColor: snapshot.primaryColor }}
                aria-hidden
              />
              <span
                className="size-8 shrink-0 rounded-md border"
                style={{ backgroundColor: snapshot.secondaryColor }}
                aria-hidden
              />
              <p className="text-muted-foreground text-xs">{t('livePreview')}</p>
            </div>
          ) : null}

          <DialogFooter className="gap-2 sm:gap-2">
            <Button type="button" variant="outline" onClick={close} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            <Button type="button" onClick={confirm} disabled={pending} className="gap-1.5">
              {pending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
              {t('apply')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
