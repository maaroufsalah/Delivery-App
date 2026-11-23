'use client';

import { Loader2, PackageSearch } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { ScrollArea } from '@/components/ui/scroll-area';

/** Ce que les deux piles d'entrée ont en commun, vu par le sélecteur. */
export interface PickableParcel {
  id: string;
  code: string;
  recipientName: string;
  /** Deuxième ligne : e-commerçant pour une facture, ville pour une recette. */
  context: string;
  /** Montant de la ligne, en centimes. Peut être négatif sur une facture. */
  amount: number;
  /** Pastille discrète : « cash reçu », « livré le … ». */
  note?: string;
}

/**
 * Sélecteur de colis, partagé par la facture et le retour de fonds.
 *
 * Les deux écrans composent de la même façon : une pile d'entrée que le serveur
 * a déjà filtrée, des cases à cocher, un total qui suit la sélection, et un
 * envoi par LOT. Deux dialogues auraient divergé au premier ajustement.
 *
 * Le filtre est local et immédiat : la pile tient dans l'écran (le serveur la
 * borne), et un aller-retour réseau pour chaque lettre tapée ferait perdre plus
 * de temps qu'il n'en ferait gagner.
 */
export function ParcelPicker({
  open,
  onOpenChange,
  title,
  description,
  parcels,
  pending,
  onConfirm,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: string;
  parcels: PickableParcel[];
  pending: boolean;
  onConfirm: (parcelIds: string[]) => void;
}) {
  const t = useTranslations('finance.picker');
  const tCommon = useTranslations('common');
  const format = useFormatter();

  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [needle, setNeedle] = useState('');

  const visible = useMemo(() => {
    const query = needle.trim().toLowerCase();
    if (query === '') return parcels;

    return parcels.filter(
      (parcel) =>
        parcel.code.toLowerCase().includes(query) ||
        parcel.recipientName.toLowerCase().includes(query) ||
        parcel.context.toLowerCase().includes(query),
    );
  }, [needle, parcels]);

  const total = useMemo(
    () =>
      parcels
        .filter((parcel) => selected.has(parcel.id))
        .reduce((sum, parcel) => sum + parcel.amount, 0),
    [parcels, selected],
  );

  const toggle = (id: string, checked: boolean) => {
    setSelected((current) => {
      const next = new Set(current);
      if (checked) next.add(id);
      else next.delete(id);
      return next;
    });
  };

  /* La case de tête n'agit que sur ce qui est VISIBLE : cocher tout après avoir
     filtré sur une ville ne doit pas embarquer les autres à l'insu de l'œil. */
  const allVisibleSelected =
    visible.length > 0 && visible.every((parcel) => selected.has(parcel.id));

  const toggleAll = (checked: boolean) => {
    setSelected((current) => {
      const next = new Set(current);
      for (const parcel of visible) {
        if (checked) next.add(parcel.id);
        else next.delete(parcel.id);
      }
      return next;
    });
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) {
          setSelected(new Set());
          setNeedle('');
        }
        onOpenChange(next);
      }}
    >
      <DialogContent className="flex max-h-[85vh] flex-col gap-4 sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <Input
          value={needle}
          onChange={(event) => setNeedle(event.target.value)}
          placeholder={t('search')}
        />

        {parcels.length === 0 ? (
          <div className="text-muted-foreground flex flex-col items-center gap-2 py-10 text-sm">
            <PackageSearch className="size-8 opacity-40" />
            {t('empty')}
          </div>
        ) : (
          <>
            <label className="flex items-center gap-2 text-sm font-medium">
              <Checkbox
                checked={allVisibleSelected}
                onCheckedChange={(checked) => toggleAll(checked === true)}
                aria-label={t('selectAll')}
              />
              {t('selectAll')}
            </label>

            <ScrollArea className="min-h-0 flex-1 rounded-md border">
              <ul className="divide-border divide-y">
                {visible.map((parcel) => (
                  <li key={parcel.id}>
                    <label className="hover:bg-muted/50 flex cursor-pointer items-center gap-3 p-3">
                      <Checkbox
                        checked={selected.has(parcel.id)}
                        onCheckedChange={(checked) => toggle(parcel.id, checked === true)}
                        aria-label={parcel.code}
                      />
                      <span className="min-w-0 flex-1">
                        <span className="block truncate font-mono text-sm">{parcel.code}</span>
                        <span className="text-muted-foreground block truncate text-xs">
                          {parcel.recipientName} · {parcel.context}
                        </span>
                      </span>
                      <span className="shrink-0 text-end">
                        <span className="block text-sm font-medium tabular-nums">
                          {format.number(parcel.amount / 100, 'currency')}
                        </span>
                        {parcel.note ? (
                          <span className="text-muted-foreground block text-xs">{parcel.note}</span>
                        ) : null}
                      </span>
                    </label>
                  </li>
                ))}
              </ul>
            </ScrollArea>
          </>
        )}

        <DialogFooter className="items-center gap-3 sm:justify-between">
          <span className="text-muted-foreground text-sm tabular-nums">
            {t('selected', { count: String(selected.size) })} ·{' '}
            <span className="text-foreground font-medium">
              {format.number(total / 100, 'currency')}
            </span>
          </span>

          <span className="flex gap-2">
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              {tCommon('cancel')}
            </Button>
            <Button
              disabled={selected.size === 0 || pending}
              onClick={() => onConfirm([...selected])}
              className="gap-1.5"
            >
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('add')}
            </Button>
          </span>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
