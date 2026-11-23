'use client';

import { Loader2 } from 'lucide-react';
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
import { Textarea } from '@/components/ui/textarea';
import { savePettyCashCategoriesAction } from '@/lib/actions/petty-cash';

/**
 * La liste des catégories de dépense — « liste simple paramétrable ».
 *
 * Une par ligne, et c'est tout : pas de code, pas de hiérarchie, pas d'écran
 * de paramétrage à part. Elle se règle là où elle sert, sous `pettycash.write`
 * (D-019 : une valeur propre à chaque société va dans un Setting).
 */
export function PettyCashCategoriesDialog({
  categories,
  onDone,
}: {
  categories: string[];
  onDone: () => void;
}) {
  const t = useTranslations('finance.pettyCash');
  const tCommon = useTranslations('common');
  const tError = useTranslations('validation');
  const [text, setText] = useState(categories.join('\n'));
  const [saving, setSaving] = useState(false);

  const save = async () => {
    const list = text
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0);

    setSaving(true);
    const result = await savePettyCashCategoriesAction({ categories: list });
    setSaving(false);

    if (result.ok) {
      toast.success(t('categoriesDialog.saved'));
      onDone();
      return;
    }

    toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
  };

  return (
    <Dialog open onOpenChange={(next) => (next ? null : onDone())}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t('categoriesDialog.title')}</DialogTitle>
          <DialogDescription>{t('categoriesDialog.hint')}</DialogDescription>
        </DialogHeader>

        <Textarea rows={8} value={text} onChange={(event) => setText(event.target.value)} />

        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={onDone} disabled={saving}>
            {tCommon('cancel')}
          </Button>
          <Button onClick={save} disabled={saving} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : null}
            {tCommon('save')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
