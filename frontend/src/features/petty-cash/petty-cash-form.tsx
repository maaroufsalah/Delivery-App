'use client';

import {
  type ClaimAttachmentPayload,
  type PettyCashKind,
  type PettyCashMeta,
  type PettyCashMovement,
  type PettyCashMovementInput,
  type PettyCashMovementPayload,
  pettyCashMovementSchema,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { SearchableSelect } from '@/components/searchable-select';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { AttachmentPicker } from '@/features/claims/attachment-picker';
import { FormSheet } from '@/features/referential/form-sheet';
import { savePettyCashMovementAction } from '@/lib/actions/petty-cash';

/** `AAAA-MM-JJ` du jour, dans le fuseau du navigateur — celui de l'opérateur. */
function today(): string {
  const now = new Date();
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/**
 * Un mouvement de petite caisse — chantier « Caisse, OV, Petite caisse »,
 * point 3.
 *
 * Le TYPE est fixé par le bouton qui ouvre le formulaire (« Approvisionner »
 * ou « Nouvelle dépense ») et ne se change pas ensuite : un approvisionnement
 * n'a ni libellé, ni catégorie, ni acheteur, et basculer l'un en l'autre
 * reviendrait à saisir un autre mouvement.
 *
 * Le justificatif réutilise le sélecteur des réclamations, limité à un fichier.
 */
export function PettyCashForm({
  meta,
  kind,
  movement,
  defaultDate,
  defaultAgencyId,
  onDone,
}: {
  meta: PettyCashMeta;
  kind: PettyCashKind;
  /** Présent : modification. Absent : saisie d'un nouveau mouvement. */
  movement: PettyCashMovement | null;
  /** La journée choisie à l'écran, `AAAA-MM-JJ` — D-249. */
  defaultDate?: string;
  /** L'agence filtrée à l'écran : celle de la journée visée. */
  defaultAgencyId?: string;
  onDone: () => void;
}) {
  const t = useTranslations('finance.pettyCash');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);
  const [proof, setProof] = useState<ClaimAttachmentPayload[]>([]);
  const [removeProof, setRemoveProof] = useState(false);

  const expense = kind === 'expense';

  /*
   * Une catégorie retirée de la liste depuis la saisie reste proposée pour CE
   * mouvement : le modifier ne doit pas obliger à la changer.
   */
  const categories =
    movement?.category && !meta.categories.includes(movement.category)
      ? [...meta.categories, movement.category]
      : meta.categories;

  const form = useForm<PettyCashMovementInput, unknown, PettyCashMovementPayload>({
    resolver: zodResolver(pettyCashMovementSchema),
    defaultValues: {
      kind,
      // D-249 — sur une journée choisie (correction), le mouvement neuf en porte la date.
      date: movement?.date ?? defaultDate ?? today(),
      amount: movement ? movement.amount / 100 : undefined,
      agencyId:
        movement?.agencyId ??
        (defaultAgencyId && meta.agencies.some((agency) => agency.id === defaultAgencyId)
          ? defaultAgencyId
          : meta.agencies.length === 1
            ? (meta.agencies[0]?.id ?? '')
            : ''),
      label: movement?.label ?? '',
      category: movement?.category ?? '',
      buyerId: movement?.buyerId ?? '',
      note: movement?.note ?? '',
      proof: null,
    },
    mode: 'onBlur',
  });

  const submit = form.handleSubmit(async (values) => {
    const file = proof[0];

    setSaving(true);
    const result = await savePettyCashMovementAction(movement?.id ?? null, {
      ...values,
      proof: file
        ? { name: file.filename.slice(0, 120), mimeType: file.mimeType, content: file.content }
        : null,
      removeProof: removeProof && !file,
    });
    setSaving(false);

    if (result.ok) {
      toast.success(t('saved'));
      onDone();
      return;
    }

    toast.error(
      result.error === 'invalid'
        ? tError('invalid')
        : // D-249 — une journée clôturée se rouvre (« Corriger ») avant d'être modifiée.
          result.reason === 'petty-cash-day-closed'
          ? t('days.closedError')
          : result.error,
    );
  });

  const title = movement
    ? t('form.editTitle')
    : expense
      ? t('form.expenseTitle')
      : t('form.supplyTitle');

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={title}
      description={t('form.hint')}
      onSubmit={submit}
      submitting={saving}
    >
      <Form {...form}>
        <form className="flex flex-col gap-4" onSubmit={submit}>
          <div className="grid grid-cols-2 gap-3">
            <FormField
              control={form.control}
              name="date"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('form.date')}</FormLabel>
                  <FormControl>
                    <Input type="date" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="amount"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('form.amount')}</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      inputMode="decimal"
                      step="0.01"
                      min="0"
                      value={field.value === undefined ? '' : String(field.value)}
                      onChange={(event) =>
                        field.onChange(
                          event.target.value === '' ? undefined : Number(event.target.value),
                        )
                      }
                      onBlur={field.onBlur}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          {expense ? (
            <>
              <FormField
                control={form.control}
                name="label"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('form.label')}</FormLabel>
                    <FormControl>
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <FormField
                  control={form.control}
                  name="category"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('form.category')}</FormLabel>
                      <FormControl>
                        <SearchableSelect
                          options={categories.map((category) => ({
                            value: category,
                            label: category,
                          }))}
                          value={field.value ?? ''}
                          onValueChange={field.onChange}
                          placeholder={t('form.category')}
                          className="w-full"
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={form.control}
                  name="buyerId"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('form.buyer')}</FormLabel>
                      <FormControl>
                        <SearchableSelect
                          options={[
                            ...meta.buyers.map((buyer) => ({ value: buyer.id, label: buyer.name })),
                            /* D-168 — le livreur d'une dépense saisie depuis son espace. */
                            ...(movement?.buyerId &&
                            !meta.buyers.some((buyer) => buyer.id === movement.buyerId)
                              ? [{ value: movement.buyerId, label: movement.buyerName ?? '' }]
                              : []),
                          ]}
                          value={field.value ?? ''}
                          onValueChange={field.onChange}
                          placeholder={t('form.buyer')}
                          className="w-full"
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
            </>
          ) : null}

          <FormField
            control={form.control}
            name="agencyId"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('form.agency')}</FormLabel>
                <FormControl>
                  <SearchableSelect
                    options={meta.agencies.map((agency) => ({
                      value: agency.id,
                      label: agency.name,
                    }))}
                    value={field.value}
                    onValueChange={field.onChange}
                    placeholder={t('form.agency')}
                    className="w-full"
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="note"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('form.note')}</FormLabel>
                <FormControl>
                  <Textarea rows={2} {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          {expense ? (
            <div className="flex flex-col gap-2">
              <Label>{t('form.proof')}</Label>
              {movement?.proofName && proof.length === 0 ? (
                <div className="flex flex-col gap-1.5">
                  <p className="text-muted-foreground text-sm">
                    {t('form.currentProof', { name: movement.proofName })}
                  </p>
                  <Label className="flex items-center gap-2 text-sm font-normal">
                    <Checkbox
                      checked={removeProof}
                      onCheckedChange={(checked) => setRemoveProof(checked === true)}
                    />
                    {t('form.removeProof')}
                  </Label>
                </div>
              ) : null}
              <AttachmentPicker
                value={proof}
                onChange={setProof}
                disabled={saving}
                max={1}
                hint={t('form.proofHint')}
              />
            </div>
          ) : null}
        </form>
      </Form>
    </FormSheet>
  );
}
