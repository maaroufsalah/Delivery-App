'use client';

import { LABELS_ASYNC_THRESHOLD, type LabelFormat, type LabelJob } from '@delivery/shared';
import { Loader2, Printer } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { toast } from 'sonner';
import { PrintFormatDialog } from '@/components/print-button';
import { Button } from '@/components/ui/button';
import { createLabelJobAction, parcelLabelsAction } from '@/lib/actions/documents';
import { downloadBase64 } from '@/lib/download';

/**
 * LES ÉTIQUETTES DE TOUT LE BON — remarques de Badr du 12/11/2026, point a.
 *
 * Le même geste que « Étiquettes » sur la liste des colis : le sélecteur A4 /
 * 10×10 (10×10 par défaut, DOMAIN.md §3.5), la même route `POST /parcels/labels`
 * dans la portée du compte, et le même travail d'arrière-plan au-delà de
 * `LABELS_ASYNC_THRESHOLD`. Rien de propre au bon : seulement ses codes, dans
 * l'ordre où la fiche les montre.
 *
 * Le panneau des planches préparées vit dans la fiche (`LabelJobsPanel`), pas
 * ici : un bouton ne porte pas une liste. `onJob` lui remet le travail lancé.
 */
export function NoteLabelsButton({
  noteCode,
  parcelCodes,
  label,
  onJob,
  disabled = false,
}: {
  noteCode: string;
  parcelCodes: string[];
  label: string;
  onJob: (job: LabelJob) => void;
  disabled?: boolean;
}) {
  const tJobs = useTranslations('parcels.labelJobs');
  const [open, setOpen] = useState(false);
  const [format, setFormat] = useState<LabelFormat>('10x10');
  const [printing, setPrinting] = useState(false);

  const count = parcelCodes.length;

  const print = async () => {
    setPrinting(true);
    const selection = { codes: parcelCodes };

    if (count > LABELS_ASYNC_THRESHOLD) {
      const job = await createLabelJobAction(selection, format);
      setPrinting(false);

      if (!job.ok) {
        toast.error(job.error);
        return;
      }

      onJob(job.data);
      toast.success(tJobs('started'));
      setOpen(false);
      return;
    }

    const result = await parcelLabelsAction(selection, format);
    setPrinting(false);

    if (!result.ok) {
      toast.error(result.error);
      return;
    }

    downloadBase64(result.data, `etiquettes-${noteCode}.pdf`, 'application/pdf');
    setOpen(false);
  };

  return (
    <>
      <Button
        type="button"
        variant="outline"
        size="sm"
        className="gap-1.5"
        disabled={disabled || printing || count === 0}
        onClick={() => setOpen(true)}
      >
        {printing ? (
          <Loader2 className="size-4 animate-spin" />
        ) : (
          <Printer className="size-4" aria-hidden />
        )}
        <span className="hidden sm:inline">{label}</span>
      </Button>

      <PrintFormatDialog
        open={open}
        onOpenChange={setOpen}
        subject={noteCode}
        count={count}
        pending={printing}
        format={format}
        onFormatChange={setFormat}
        onConfirm={print}
      />
    </>
  );
}
