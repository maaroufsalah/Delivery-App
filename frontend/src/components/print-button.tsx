'use client';

import { LABEL_FORMATS, type LabelFormat } from '@delivery/shared';
import { Download, FileText, Loader2, Printer } from 'lucide-react';
import { useTranslations } from 'next-intl';
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
import type { ReferentialResult } from '@/lib/actions/action-result';
import { downloadBase64 } from '@/lib/download';
import { cn } from '@/lib/utils';

/**
 * Le SEUL bouton d'impression de l'application.
 *
 * Deux natures de document, un seul composant :
 *
 *  - `kind="label"` — étiquettes et planches. Un sélecteur A4 / 10×10 s'ouvre,
 *    **défaut 10×10** : c'est la règle permanente de DOMAIN.md §3.5, et c'est
 *    le rouleau thermique des agences. La liste des formats est celle du
 *    schéma partagé, jamais une valeur libre — le legacy passait le `$request`
 *    brut à mPDF sur certaines routes.
 *  - `kind="document"` — bon, facture, relevé, reçu. Un seul format possible,
 *    donc aucune question posée : le clic imprime.
 *
 * Tout PDF vient du service `@react-pdf/renderer` de l'API (C0, D-034) et
 * revient en base64 par une Server Action : le cookie de session est `httpOnly`
 * sur l'origine du web, un lien direct vers l'API partirait sans lui (D-018).
 */
export function PrintButton({
  kind,
  subject,
  count = 1,
  onPrint,
  label,
  icon,
  variant = 'outline',
  disabled = false,
  iconOnly = false,
  className,
}: {
  kind: 'label' | 'document';
  /** Ce qu'on imprime — sert au nom de fichier et au texte du dialogue. */
  subject: string;
  /** Nombre d'exemplaires concernés, pour le libellé du dialogue. */
  count?: number;
  /** Rend le PDF encodé en base64. */
  onPrint: (format: LabelFormat) => Promise<ReferentialResult<string>>;
  label?: string;
  icon?: 'printer' | 'file' | 'download';
  variant?: 'outline' | 'default' | 'ghost';
  disabled?: boolean;
  /**
   * Icône seule, libellé en `aria-label` — l'action de LIGNE d'une liste
   * (colis d'un bon, recette 3 point 1), posée à côté d'une corbeille.
   */
  iconOnly?: boolean;
  className?: string;
}) {
  const t = useTranslations('labels');
  const [open, setOpen] = useState(false);
  const [format, setFormat] = useState<LabelFormat>('10x10');
  const [pending, setPending] = useState(false);

  const Icon = icon === 'file' ? FileText : icon === 'download' ? Download : Printer;

  const run = async (chosen: LabelFormat) => {
    setPending(true);
    const result = await onPrint(chosen);
    setPending(false);

    if (!result.ok) {
      toast.error(result.error);
      return;
    }

    downloadBase64(result.data, `${subject}.pdf`, 'application/pdf');
    setOpen(false);
  };

  return (
    <>
      <Button
        type="button"
        variant={variant}
        size={iconOnly ? 'icon' : 'sm'}
        disabled={disabled || pending}
        // Un document n'a qu'un format : il n'y a rien à demander.
        onClick={() => (kind === 'label' ? setOpen(true) : run('a4'))}
        aria-label={iconOnly ? (label ?? t('title')) : undefined}
        title={iconOnly ? (label ?? t('title')) : undefined}
        className={cn(iconOnly ? 'size-8' : 'gap-1.5', className)}
      >
        {pending ? (
          <Loader2 className="size-4 animate-spin" />
        ) : (
          <Icon className="size-4" aria-hidden />
        )}
        {iconOnly ? null : <span className="hidden sm:inline">{label ?? t('title')}</span>}
      </Button>

      {kind === 'label' ? (
        <PrintFormatDialog
          open={open}
          onOpenChange={setOpen}
          subject={subject}
          count={count}
          pending={pending}
          format={format}
          onFormatChange={setFormat}
          onConfirm={() => run(format)}
        />
      ) : null}
    </>
  );
}

/**
 * Le sélecteur de format, sans bouton.
 *
 * La liste des colis ouvre l'impression depuis une action de LIGNE : il lui
 * faut le dialogue seul. C'est le même, pas une copie — `PrintButton` s'en sert
 * aussi.
 */
export function PrintFormatDialog({
  open,
  onOpenChange,
  subject,
  count = 1,
  pending = false,
  format,
  onFormatChange,
  onConfirm,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  subject: string;
  count?: number;
  pending?: boolean;
  format: LabelFormat;
  onFormatChange: (format: LabelFormat) => void;
  onConfirm: () => void;
}) {
  const t = useTranslations('labels');

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Printer className="size-4" aria-hidden />
            {t('title')}
          </DialogTitle>
          <DialogDescription>{t('description', { subject, count })}</DialogDescription>
        </DialogHeader>

        <fieldset className="grid grid-cols-2 gap-3">
          <legend className="sr-only">{t('format')}</legend>
          {LABEL_FORMATS.map((value) => {
            const selected = value === format;
            return (
              <button
                key={value}
                type="button"
                role="radio"
                aria-checked={selected}
                onClick={() => onFormatChange(value)}
                className={cn(
                  'flex flex-col items-start gap-1 rounded-lg border p-4 text-start transition-colors',
                  'focus-visible:ring-ring outline-none focus-visible:ring-2',
                  selected ? 'border-primary bg-accent' : 'hover:bg-muted/60',
                )}
              >
                <span className="text-sm font-medium">{t(`formats.${value}.label`)}</span>
                <span className="text-muted-foreground text-xs">{t(`formats.${value}.hint`)}</span>
              </button>
            );
          })}
        </fieldset>

        <DialogFooter className="gap-2 sm:gap-2">
          <Button type="button" variant="ghost" onClick={() => onOpenChange(false)}>
            {t('cancel')}
          </Button>
          <Button type="button" disabled={pending} onClick={onConfirm} className="gap-1.5">
            {pending ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <Printer className="size-4" aria-hidden />
            )}
            {t('generate')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
