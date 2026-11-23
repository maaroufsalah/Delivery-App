'use client';

import type { Agency, Client, ProductListItem, StorageRequestDetail } from '@delivery/shared';
import { Loader2, Plus, Trash2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { toast } from 'sonner';
import { SearchableSelect } from '@/components/searchable-select';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { FormSheet } from '@/features/referential/form-sheet';
import { useWarehouseError } from '@/features/warehouse/use-warehouse-error';
import { createStorageRequestAction } from '@/lib/actions/warehouse';

interface Line {
  productId: string;
  quantity: number;
}

/**
 * Dépôt d'une demande d'entreposage.
 *
 * Les lignes sont saisies AVEC l'en-tête, et il en faut au moins une : une
 * demande qui ne dit pas ce qu'elle entrepose est exactement ce que le legacy
 * enregistrait — `entreposages` n'avait aucune table de lignes.
 */
export function StorageRequestForm({
  clients,
  agencies,
  products,
  lockedClientId,
  onDone,
}: {
  clients: Client[];
  agencies: Agency[];
  products: ProductListItem[];
  lockedClientId?: string;
  onDone: (request?: StorageRequestDetail) => void;
}) {
  const t = useTranslations('warehouse.storage');
  const tCommon = useTranslations('common');
  const errorMessage = useWarehouseError();

  const [clientId, setClientId] = useState(lockedClientId ?? '');
  const [agencyId, setAgencyId] = useState(agencies.length === 1 ? (agencies[0]?.id ?? '') : '');
  const [note, setNote] = useState('');
  const [lines, setLines] = useState<Line[]>([]);
  const [draftProduct, setDraftProduct] = useState('');
  const [draftQuantity, setDraftQuantity] = useState('1');
  const [saving, setSaving] = useState(false);

  // Seuls les produits du client retenu : l'API refuse les autres, autant ne
  // pas les proposer.
  const selectable = products.filter((product) => clientId === '' || product.clientId === clientId);
  const listed = new Set(lines.map((line) => line.productId));

  const addLine = () => {
    const quantity = Number(draftQuantity);
    if (!draftProduct || !Number.isInteger(quantity) || quantity < 1) return;

    setLines((current) => [...current, { productId: draftProduct, quantity }]);
    setDraftProduct('');
    setDraftQuantity('1');
  };

  const nameOf = (productId: string) => {
    const product = products.find((entry) => entry.id === productId);
    return product
      ? `${product.reference ? `${product.reference}, ` : ''}${product.name}`
      : productId;
  };

  const submit = async () => {
    setSaving(true);
    const result = await createStorageRequestAction({ clientId, agencyId, note, items: lines });
    setSaving(false);

    if (result.ok) {
      toast.success(t('created', { code: result.data.code }));
      onDone(result.data);
      return;
    }

    toast.error(errorMessage(result));
  };

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={t('new')}
      description={t('formHint')}
      onSubmit={submit}
      submitting={saving || clientId === '' || agencyId === '' || lines.length === 0}
    >
      <div className="flex flex-col gap-4">
        {lockedClientId ? null : (
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="storage-client">{t('fields.client')}</Label>
            <SearchableSelect
              options={clients.map((client) => ({ value: client.id, label: client.name }))}
              value={clientId}
              onValueChange={(value) => {
                setClientId(value);
                // Changer de client invalide les lignes déjà saisies.
                setLines([]);
              }}
              placeholder={t('fields.client')}
              id="storage-client"
            />
          </div>
        )}

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="storage-agency">{t('fields.agency')}</Label>
          <SearchableSelect
            options={agencies.map((agency) => ({ value: agency.id, label: agency.name }))}
            value={agencyId}
            onValueChange={setAgencyId}
            placeholder={t('fields.agency')}
            id="storage-agency"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="storage-note">{t('fields.note')}</Label>
          <Input id="storage-note" value={note} onChange={(event) => setNote(event.target.value)} />
        </div>

        <div className="flex flex-col gap-2 rounded-lg border p-3">
          <p className="text-sm font-medium">{t('addLine')}</p>

          <div className="flex flex-col gap-2 sm:flex-row">
            <SearchableSelect
              options={selectable
                .filter((product) => !listed.has(product.id))
                .map((product) => ({
                  value: product.id,
                  label: `${product.reference ? `${product.reference}, ` : ''}${product.name}`,
                  keywords: [product.reference],
                }))}
              value={draftProduct}
              onValueChange={setDraftProduct}
              placeholder={t('fields.product')}
              className="min-w-0 flex-1"
            />

            <Input
              type="number"
              min={1}
              step={1}
              className="sm:w-24"
              value={draftQuantity}
              onChange={(event) => setDraftQuantity(event.target.value)}
            />

            <Button type="button" variant="outline" onClick={addLine} disabled={!draftProduct}>
              <Plus className="size-4" />
            </Button>
          </div>

          {lines.length === 0 ? (
            <p className="text-muted-foreground text-xs">{t('noLinesHint')}</p>
          ) : (
            lines.map((line) => (
              <div key={line.productId} className="flex items-center gap-2 text-sm">
                <span className="min-w-0 flex-1 truncate">{nameOf(line.productId)}</span>
                <span className="tabular-nums">{line.quantity}</span>
                <Button
                  type="button"
                  size="icon"
                  variant="ghost"
                  aria-label={tCommon('delete')}
                  onClick={() =>
                    setLines((current) =>
                      current.filter((entry) => entry.productId !== line.productId),
                    )
                  }
                >
                  <Trash2 className="text-destructive size-4" />
                </Button>
              </div>
            ))
          )}
        </div>

        {saving ? (
          <p className="text-muted-foreground flex items-center gap-2 text-sm">
            <Loader2 className="size-4 animate-spin" />
            {tCommon('loading')}
          </p>
        ) : null}
      </div>
    </FormSheet>
  );
}
