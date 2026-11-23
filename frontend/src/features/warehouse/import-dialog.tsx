'use client';

import type { Client, OrderImportPreview, ProductImportPreview } from '@delivery/shared';
import { AlertTriangle, CheckCircle2, Loader2, Upload } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useRef, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { SearchableSelect } from '@/components/searchable-select';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { useWarehouseError } from '@/features/warehouse/use-warehouse-error';
import type { ReferentialResult } from '@/lib/actions/action-result';

type Preview = ProductImportPreview | OrderImportPreview;

/**
 * Import d'un classeur — catalogue ou commandes.
 *
 * Deux temps, comme l'import tarifaire (D-022) : un **aperçu** qui ne touche à
 * rien et rend les erreurs ligne par ligne, puis une application explicite. Un
 * fichier qui contient une erreur n'est jamais appliqué à moitié, et le bouton
 * d'application reste fermé tant qu'il en reste une.
 *
 * Pour les commandes, l'aperçu montre aussi les lignes IGNORÉES : ce sont les
 * références déjà connues, et c'est le cas normal d'un réimport — pas une
 * erreur. Le dire évite qu'un opérateur croie l'import cassé.
 */
export function WarehouseImportDialog({
  kind,
  clients,
  lockedClientId,
  onClose,
  onRun,
}: {
  kind: 'products' | 'orders';
  clients: Client[];
  lockedClientId?: string;
  onClose: () => void;
  onRun: (
    content: string,
    apply: boolean,
    clientId?: string,
  ) => Promise<ReferentialResult<Preview>>;
}) {
  const t = useTranslations(kind === 'orders' ? 'warehouse.orders' : 'warehouse.products');
  const tOrders = useTranslations('warehouse.orders');
  const tCommon = useTranslations('common');
  const errorMessage = useWarehouseError();

  const inputRef = useRef<HTMLInputElement>(null);
  const [clientId, setClientId] = useState(lockedClientId ?? '');
  const [content, setContent] = useState<string | null>(null);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [pending, startTransition] = useTransition();

  const skipped = preview && 'skipped' in preview ? preview.skipped : 0;
  const applicable = preview !== null && preview.errors.length === 0 && preview.valid > 0;

  const read = (file: File) => {
    startTransition(async () => {
      const bytes = new Uint8Array(await file.arrayBuffer());
      let binary = '';
      for (let index = 0; index < bytes.length; index += 8192) {
        binary += String.fromCharCode(...bytes.subarray(index, index + 8192));
      }

      const base64 = btoa(binary);
      setContent(base64);
      setPreview(null);

      const result = await onRun(base64, false, clientId || undefined);
      if (result.ok) setPreview(result.data);
      else toast.error(errorMessage(result));
    });
  };

  const apply = () => {
    if (!content) return;

    startTransition(async () => {
      const result = await onRun(content, true, clientId || undefined);

      if (result.ok) {
        setPreview(result.data);
        toast.success(tOrders('importDone', { count: result.data.created }));
        onClose();
        return;
      }

      toast.error(errorMessage(result));
    });
  };

  return (
    <Dialog
      open
      onOpenChange={(next) => {
        if (!next) onClose();
      }}
    >
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{kind === 'orders' ? tOrders('importTitle') : t('title')}</DialogTitle>
          <DialogDescription>{tOrders('importHint')}</DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-3">
          {lockedClientId ? null : (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="import-client">{t('fields.client')}</Label>
              <SearchableSelect
                options={clients.map((client) => ({ value: client.id, label: client.name }))}
                value={clientId}
                onValueChange={setClientId}
                placeholder={t('fields.client')}
                id="import-client"
              />
            </div>
          )}

          <input
            ref={inputRef}
            type="file"
            className="hidden"
            accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            onChange={(event) => {
              const file = event.target.files?.[0];
              if (file) read(file);
            }}
          />

          <Button
            variant="outline"
            className="gap-1.5"
            disabled={pending || (!lockedClientId && clientId === '')}
            onClick={() => inputRef.current?.click()}
          >
            {pending ? <Loader2 className="size-4 animate-spin" /> : <Upload className="size-4" />}
            {tCommon('more')}
          </Button>

          {kind === 'orders' ? (
            <p className="text-muted-foreground text-xs">{tOrders('importIdempotent')}</p>
          ) : null}

          {preview ? (
            <div className="flex flex-col gap-2 rounded-lg border p-3 text-sm">
              <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
                <span className="inline-flex items-center gap-1.5">
                  <CheckCircle2 className="size-4 text-[var(--chart-4)]" />
                  {preview.valid} / {preview.rows}
                </span>

                {skipped > 0 ? (
                  <span className="text-muted-foreground">
                    {tOrders('importSkipped', { count: skipped })}
                  </span>
                ) : null}

                {preview.errors.length > 0 ? (
                  <span className="text-destructive inline-flex items-center gap-1.5">
                    <AlertTriangle className="size-4" />
                    {preview.errors.length}
                  </span>
                ) : null}
              </div>

              {/* Les erreurs sont rendues LIGNE PAR LIGNE : un fichier refusé
                  en bloc n'apprend pas laquelle corriger (dette n°12). */}
              {preview.errors.length > 0 ? (
                <ul className="max-h-40 overflow-y-auto text-xs">
                  {preview.errors.slice(0, 30).map((error) => (
                    <li key={`${error.row}-${error.code}`} className="text-muted-foreground">
                      <span className="text-foreground tabular-nums">L{error.row}</span>{' '}
                      {error.message}
                    </li>
                  ))}
                </ul>
              ) : null}
            </div>
          ) : null}
        </div>

        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={onClose} disabled={pending}>
            {tCommon('cancel')}
          </Button>
          <Button onClick={apply} disabled={pending || !applicable} className="gap-1.5">
            {pending ? <Loader2 className="size-4 animate-spin" /> : null}
            {tOrders('importApply')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
