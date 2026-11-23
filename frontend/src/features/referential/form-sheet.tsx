'use client';

import type { ReferenceCount } from '@delivery/shared';
import { AlertTriangle, Loader2 } from 'lucide-react';
import { useLocale, useTranslations } from 'next-intl';
import { useState } from 'react';
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
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet';
import { localeDirection } from '@/i18n/routing';
import type { ReferentialResult } from '@/lib/actions/action-result';

/**
 * Panneau d'édition du référentiel.
 *
 * Latéral sur desktop, PLEIN ÉCRAN sur mobile : `w-full` sans contrainte sous
 * `sm`, la feuille occupe alors tout l'écran et se comporte comme une page.
 * Un même composant pour les deux, plutôt qu'une route dédiée en double — le
 * formulaire n'aurait pas été le même des deux côtés bien longtemps.
 *
 * Le côté est choisi par la DIRECTION de la locale : en arabe le panneau entre
 * par la gauche, comme le reste de l'interface (CLAUDE.md §5).
 */
export function FormSheet({
  open,
  onOpenChange,
  title,
  description,
  children,
  onSubmit,
  submitting,
  submitLabel,
  hideFooter = false,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  children: React.ReactNode;
  onSubmit?: () => void;
  submitting?: boolean;
  submitLabel?: string;
  /**
   * Le contenu porte ses PROPRES boutons — C13 bloc 3.
   *
   * Le formulaire de compte est le même dans un tiroir et dans un onglet de
   * fiche ; lui retirer ses boutons dans un cas obligerait à les remonter au
   * conteneur, et donc à écrire deux fois la soumission.
   */
  hideFooter?: boolean;
}) {
  const t = useTranslations('common');
  const locale = useLocale();
  const side = localeDirection(locale) === 'rtl' ? 'left' : 'right';

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side={side} className="flex w-full flex-col gap-0 p-0 sm:max-w-lg">
        <SheetHeader className="border-b">
          <SheetTitle>{title}</SheetTitle>
          {description ? <SheetDescription>{description}</SheetDescription> : null}
        </SheetHeader>

        {/* La zone de saisie défile, l'en-tête et les boutons restent visibles :
            sur un téléphone, un bouton « enregistrer » hors écran n'existe pas. */}
        <div className="flex-1 overflow-y-auto px-4 py-4">{children}</div>

        {hideFooter ? null : (
          <SheetFooter className="flex-row gap-2 border-t">
            <Button
              type="button"
              variant="outline"
              className="flex-1"
              onClick={() => onOpenChange(false)}
              disabled={submitting}
            >
              {t('cancel')}
            </Button>
            <Button
              type="button"
              className="flex-1 gap-1.5"
              onClick={onSubmit}
              disabled={submitting}
            >
              {submitting ? <Loader2 className="size-4 animate-spin" /> : null}
              {submitLabel ?? t('save')}
            </Button>
          </SheetFooter>
        )}
      </SheetContent>
    </Sheet>
  );
}

/**
 * Confirmation de suppression, et REFUS motivé quand la ligne est référencée.
 *
 * DOMAIN.md §4 dette n°8 : le legacy supprimait en cascade. Ici l'API répond
 * 409 avec le décompte, et l'écran le montre — « 412 colis » est une réponse,
 * « échec » n'en est pas une. La désactivation reste proposée, parce que c'est
 * ce que l'utilisateur voulait vraiment neuf fois sur dix.
 */
export function DeleteDialog({
  open,
  onOpenChange,
  label,
  onConfirm,
  onDeactivate,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  label: string;
  onConfirm: () => Promise<ReferentialResult>;
  onDeactivate?: () => Promise<ReferentialResult>;
}) {
  const t = useTranslations('referential.delete');
  const tRef = useTranslations('referential.references');
  const tCommon = useTranslations('common');

  const [pending, setPending] = useState(false);
  const [blocked, setBlocked] = useState<ReferenceCount[] | null>(null);

  const reset = (next: boolean) => {
    if (!next) setBlocked(null);
    onOpenChange(next);
  };

  const confirm = async () => {
    setPending(true);
    const result = await onConfirm();
    setPending(false);

    if (result.ok) {
      toast.success(t('done', { label }));
      reset(false);
      return;
    }

    if (result.references?.length) {
      setBlocked(result.references);
      return;
    }

    toast.error(result.error);
  };

  const deactivate = async () => {
    if (!onDeactivate) return;
    setPending(true);
    const result = await onDeactivate();
    setPending(false);

    if (result.ok) {
      toast.success(t('deactivated', { label }));
      reset(false);
      return;
    }

    toast.error(result.error);
  };

  return (
    <Dialog open={open} onOpenChange={reset}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{blocked ? t('blockedTitle') : t('title')}</DialogTitle>
          <DialogDescription>
            {blocked ? t('blockedHint', { label }) : t('hint', { label })}
          </DialogDescription>
        </DialogHeader>

        {blocked ? (
          <ul className="border-destructive/30 bg-destructive/5 flex flex-col gap-1.5 rounded-lg border p-3">
            {blocked.map((entry) => (
              <li key={entry.entity} className="flex items-center gap-2 text-sm">
                <AlertTriangle className="text-destructive size-4 shrink-0" />
                <span className="tabular-nums">{entry.count}</span>
                {/* La clé vient de l'API, donc elle n'est pas connue à la
                    compilation : `referential.references` est le contrat, et
                    une clé absente retombe sur son propre nom. */}
                <span className="text-muted-foreground">
                  {tRef(entry.entity as Parameters<typeof tRef>[0])}
                </span>
              </li>
            ))}
          </ul>
        ) : null}

        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={() => reset(false)} disabled={pending}>
            {tCommon('cancel')}
          </Button>

          {blocked ? (
            onDeactivate ? (
              <Button onClick={deactivate} disabled={pending} className="gap-1.5">
                {pending ? <Loader2 className="size-4 animate-spin" /> : null}
                {t('deactivateInstead')}
              </Button>
            ) : null
          ) : (
            <Button variant="destructive" onClick={confirm} disabled={pending} className="gap-1.5">
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('confirm')}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
