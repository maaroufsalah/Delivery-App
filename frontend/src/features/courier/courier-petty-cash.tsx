'use client';

import {
  type ClaimAttachmentPayload,
  type PettyCashCourierExpenseInput,
  type PettyCashCourierExpensePayload,
  pettyCashCourierExpenseSchema,
  type PettyCashCourierPage,
  type PettyCashMovement,
  type PettyCashStatus,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { CellBadge } from '@/components/data-table/cells';
import { ExportExcelButton } from '@/components/export-excel-button';
import { SearchableSelect } from '@/components/searchable-select';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
import { AttachmentPicker } from '@/features/claims/attachment-picker';
import { FormSheet } from '@/features/referential/form-sheet';
import { exportListAction } from '@/lib/actions/documents';
import {
  deleteCourierPettyCashExpenseAction,
  saveCourierPettyCashExpenseAction,
} from '@/lib/actions/petty-cash';

/** Brouillon orange, validée verte, refusée rouge — les jetons du thème. */
const STATUS_COLORS: Record<PettyCashStatus, string> = {
  draft: 'var(--warning)',
  validated: 'var(--success)',
  refused: 'var(--destructive)',
};

/**
 * La liste et le formulaire du livreur — D-168.
 *
 * Un brouillon se corrige ou se retire ; une dépense tranchée ne se touche
 * plus (l'API répond 403, l'écran ne propose simplement plus le geste). Le
 * formulaire est REMONTÉ à chaque ouverture (clé), donc vierge à chaque fois.
 */
export function CourierPettyCash({ page }: { page: PettyCashCourierPage }) {
  const t = useTranslations('courier.pettyCash');
  // Le libellé des autres boutons « Exporter Excel », déjà traduit dans les trois langues.
  const tExport = useTranslations('routes.detail');
  const format = useFormatter();
  const [editing, setEditing] = useState<{
    movement: PettyCashMovement | null;
    key: number;
  } | null>(null);
  const [pending, startTransition] = useTransition();

  const open = (movement: PettyCashMovement | null) => setEditing({ movement, key: Date.now() });

  const remove = (row: PettyCashMovement) => {
    if (pending) return;
    startTransition(async () => {
      const result = await deleteCourierPettyCashExpenseAction(row.id);
      if (result.ok) toast.success(t('deleted'));
      else toast.error(result.error);
    });
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-muted-foreground text-sm">{t('pending', { count: page.drafts })}</p>
        <div className="flex flex-wrap items-center gap-2">
          {/*
            SES dépenses, en classeur — chantier « Affichage web et étiquette »,
            point 4. La portée serveur (`mine()`) borne le fichier à lui ; ni
            solde ni approvisionnement, comme à l'écran (D-168).
          */}
          <ExportExcelButton
            label={tExport('exportExcel')}
            filename="mes-depenses"
            disabled={page.items.length === 0}
            onExport={() => exportListAction('courier/me/petty-cash', {})}
          />
          <Button className="gap-1.5" onClick={() => open(null)}>
            <Plus className="size-4" />
            {t('new')}
          </Button>
        </div>
      </div>

      {page.items.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col gap-1 p-8 text-center">
            <span className="text-sm font-medium">{t('empty')}</span>
            <span className="text-muted-foreground text-sm">{t('emptyHint')}</span>
          </CardContent>
        </Card>
      ) : (
        <ul className="flex flex-col gap-2">
          {page.items.map((row) => (
            <li key={row.id}>
              <Card>
                <CardContent className="flex flex-col gap-2 p-4">
                  <div className="flex items-start gap-3">
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium">{row.label}</span>
                      <span className="text-muted-foreground block truncate text-xs">
                        {format.dateTime(new Date(`${row.date}T12:00:00Z`), 'short')} ·{' '}
                        {row.category}
                      </span>
                    </span>
                    <span className="text-sm font-semibold tabular-nums">
                      {format.number(row.amount / 100, 'currency')}
                    </span>
                  </div>

                  <div className="flex flex-wrap items-center gap-2">
                    <CellBadge
                      label={t(`status.${row.status}`)}
                      color={STATUS_COLORS[row.status]}
                    />
                    {row.status === 'refused' && row.refusalReason ? (
                      <span className="text-muted-foreground text-xs">
                        {t('reason', { reason: row.refusalReason })}
                      </span>
                    ) : null}

                    {row.status === 'draft' ? (
                      <span className="ms-auto flex gap-1">
                        <Button
                          size="sm"
                          variant="ghost"
                          className="gap-1.5"
                          onClick={() => open(row)}
                        >
                          <Pencil className="size-4" />
                          {t('edit')}
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          className="text-destructive gap-1.5"
                          disabled={pending}
                          onClick={() => remove(row)}
                        >
                          <Trash2 className="size-4" />
                          {t('delete')}
                        </Button>
                      </span>
                    ) : null}
                  </div>
                </CardContent>
              </Card>
            </li>
          ))}
        </ul>
      )}

      {editing ? (
        <ExpenseForm
          key={editing.key}
          categories={page.categories}
          movement={editing.movement}
          onDone={() => setEditing(null)}
        />
      ) : null}
    </div>
  );
}

function ExpenseForm({
  categories: available,
  movement,
  onDone,
}: {
  categories: string[];
  movement: PettyCashMovement | null;
  onDone: () => void;
}) {
  const t = useTranslations('courier.pettyCash');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);
  const [proof, setProof] = useState<ClaimAttachmentPayload[]>([]);
  const [removeProof, setRemoveProof] = useState(false);

  // Une catégorie retirée depuis reste proposée sur le brouillon qui la porte.
  const categories =
    movement?.category && !available.includes(movement.category)
      ? [...available, movement.category]
      : available;

  const form = useForm<PettyCashCourierExpenseInput, unknown, PettyCashCourierExpensePayload>({
    resolver: zodResolver(pettyCashCourierExpenseSchema),
    defaultValues: {
      label: movement?.label ?? '',
      category: movement?.category ?? '',
      amount: movement ? movement.amount / 100 : undefined,
      proof: null,
    },
    mode: 'onBlur',
  });

  const submit = form.handleSubmit(async (values) => {
    // Un appui, une action : un second Entrée pendant l'envoi ne crée rien.
    if (saving) return;
    const file = proof[0];

    setSaving(true);
    const result = await saveCourierPettyCashExpenseAction(movement?.id ?? null, {
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

    toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
  });

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={movement ? t('form.editTitle') : t('form.newTitle')}
      description={t('form.hint')}
      onSubmit={submit}
      submitting={saving}
    >
      <Form {...form}>
        <form className="flex flex-col gap-4" onSubmit={submit}>
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
                      options={categories.map((category) => ({ value: category, label: category }))}
                      value={field.value}
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
        </form>
      </Form>
    </FormSheet>
  );
}
