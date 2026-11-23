'use client';

import {
  type DeliveryTypeListItem,
  type DeliveryTypeInput,
  type DeliveryTypePayload,
  deliveryTypeSchema,
  type Paginated,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Pencil, Plus, Power, Star, Trash2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useMemo, useState, useTransition } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { CellBadge, CellNumber, CellStack, RowActions } from '@/components/data-table/cells';
import { DataTable } from '@/components/data-table/data-table';
import {
  ALL_VALUE,
  DataTableToolbar,
  type FilterSelect,
} from '@/components/data-table/data-table-toolbar';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { useServerTable, useUrlSearch } from '@/components/data-table/use-server-table';
import { Button } from '@/components/ui/button';
import { DropdownMenuItem, DropdownMenuSeparator } from '@/components/ui/dropdown-menu';
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { DeleteDialog, FormSheet } from '@/features/referential/form-sheet';
import {
  deleteDeliveryTypeAction,
  saveDeliveryTypeAction,
  setDeliveryTypeActiveAction,
} from '@/lib/actions/referential';

const helper = createDataTableColumnHelper<DeliveryTypeListItem>();

/** Millièmes entiers → texte lisible : 1500 → « ×1,5 ». */
function multiplierLabel(perMille: number): string {
  return `×${(perMille / 1000).toLocaleString(undefined, { maximumFractionDigits: 3 })}`;
}

/**
 * Types de livraison et leur multiplicateur de tarif.
 *
 * D-019 — paramétrable avant codé en dur : « express ×1,5 » se règle ici, il ne
 * se retape pas colis par colis comme dans le legacy, où toute variation de
 * prix passait par une saisie manuelle (DOMAIN.md §0.3).
 */
export function DeliveryTypesScreen({ page }: { page: Paginated<DeliveryTypeListItem> }) {
  const t = useTranslations('referential.deliveryTypes');
  const tCommon = useTranslations('common');

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'name',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [editing, setEditing] = useState<DeliveryTypeListItem | null | undefined>(undefined);
  const [removing, setRemoving] = useState<DeliveryTypeListItem | null>(null);
  const [, startTransition] = useTransition();

  const toggleActive = (type: DeliveryTypeListItem) => {
    startTransition(async () => {
      const result = await setDeliveryTypeActiveAction(type.id, !type.isActive);
      if (result.ok) toast.success(t('toggled', { name: type.name }));
      else toast.error(result.error);
    });
  };

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        id: 'isActive',
        label: t('filters.state'),
        value: param('isActive') ?? ALL_VALUE,
        options: [
          { value: 'true', label: tCommon('active') },
          { value: 'false', label: tCommon('inactive') },
        ],
        onChange: (value) => setFilter('isActive', value === ALL_VALUE ? undefined : value),
      },
    ],
    [param, setFilter, t, tCommon],
  );

  const columns: DataTableColumns<DeliveryTypeListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('name', {
          header: t('columns.name'),
          cell: (info) => (
            <CellStack
              label={
                <span className="inline-flex items-center gap-1.5">
                  {info.getValue()}
                  {info.row.original.isDefault ? (
                    <Star
                      className="size-3.5 fill-current text-amber-500"
                      aria-label={t('columns.default')}
                    />
                  ) : null}
                </span>
              }
              sub={t('columns.parcelCount', { count: info.row.original.parcelCount })}
            />
          ),
        }),

        helper.accessor('multiplierPerMille', {
          header: t('columns.multiplier'),
          meta: { align: 'end' },
          cell: (info) => <CellNumber value={multiplierLabel(info.getValue())} />,
        }),

        helper.accessor('isActive', {
          header: t('columns.state'),
          meta: { width: 'w-32' },
          cell: (info) =>
            info.getValue() ? (
              <CellBadge label={tCommon('active')} color="var(--chart-4)" />
            ) : (
              <CellBadge label={tCommon('inactive')} />
            ),
        }),

        helper.display({
          id: 'actions',
          header: '',
          meta: { isActions: true, align: 'end', width: 'w-16' },
          cell: (info) => (
            <RowActions>
              <DropdownMenuItem onClick={() => setEditing(info.row.original)}>
                <Pencil className="size-4" />
                {tCommon('edit')}
              </DropdownMenuItem>
              <DropdownMenuItem
                onClick={() => toggleActive(info.row.original)}
                // Le type par défaut ne se désactive pas : la création de colis
                // en exige un, et le retirer laisserait un trou.
                disabled={info.row.original.isDefault}
              >
                <Power className="size-4" />
                {info.row.original.isActive ? tCommon('deactivate') : tCommon('activate')}
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                variant="destructive"
                disabled={info.row.original.isDefault}
                onClick={() => setRemoving(info.row.original)}
              >
                <Trash2 className="size-4" />
                {tCommon('delete')}
              </DropdownMenuItem>
            </RowActions>
          ),
        }),
      ]),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, tCommon],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(type) => setEditing(type)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="delivery-types"
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            onReset={() => {
              setSearch('');
              reset();
            }}
          >
            <Button size="sm" className="gap-1.5" onClick={() => setEditing(null)}>
              <Plus className="size-4" />
              <span className="hidden sm:inline">{t('new')}</span>
            </Button>
          </DataTableToolbar>
        }
      />

      {editing !== undefined ? (
        <DeliveryTypeForm type={editing} onDone={() => setEditing(undefined)} />
      ) : null}

      {removing ? (
        <DeleteDialog
          open
          onOpenChange={(next) => {
            if (!next) setRemoving(null);
          }}
          label={removing.name}
          onConfirm={() => deleteDeliveryTypeAction(removing.id)}
          onDeactivate={() => setDeliveryTypeActiveAction(removing.id, false)}
        />
      ) : null}
    </>
  );
}

function DeliveryTypeForm({
  type,
  onDone,
}: {
  type: DeliveryTypeListItem | null;
  onDone: () => void;
}) {
  const t = useTranslations('referential.deliveryTypes');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);

  const form = useForm<DeliveryTypeInput, unknown, DeliveryTypePayload>({
    resolver: zodResolver(deliveryTypeSchema),
    defaultValues: {
      name: type?.name ?? '',
      multiplierPerMille: type?.multiplierPerMille ?? 1000,
      isActive: type?.isActive ?? true,
      isDefault: type?.isDefault ?? false,
    },
    mode: 'onBlur',
  });

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await saveDeliveryTypeAction(type?.id ?? null, values);
    setSaving(false);

    if (result.ok) {
      toast.success(
        type ? t('updated', { name: values.name }) : t('created', { name: values.name }),
      );
      onDone();
      return;
    }

    if (result.field === 'name') form.setError('name', { message: result.error });
    else toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
  });

  const multiplier = form.watch('multiplierPerMille');

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={type ? t('edit') : t('new')}
      description={t('formHint')}
      onSubmit={submit}
      submitting={saving}
    >
      <Form {...form}>
        <form className="flex flex-col gap-4" onSubmit={submit}>
          <FormField
            control={form.control}
            name="name"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.name')}</FormLabel>
                <FormControl>
                  <Input {...field} autoFocus />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="multiplierPerMille"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.multiplier')}</FormLabel>
                <FormControl>
                  <Input
                    type="number"
                    min={1}
                    max={10000}
                    step={50}
                    value={field.value}
                    onChange={(event) => field.onChange(event.target.valueAsNumber)}
                  />
                </FormControl>
                <FormDescription>
                  {t('fields.multiplierHint', { value: multiplierLabel(multiplier || 1000) })}
                </FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="isDefault"
            render={({ field }) => (
              <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                <div className="flex flex-col gap-0.5">
                  <FormLabel>{t('fields.default')}</FormLabel>
                  <FormDescription>{t('fields.defaultHint')}</FormDescription>
                </div>
                <FormControl>
                  <Switch
                    checked={field.value}
                    // Retirer le défaut sans en désigner un autre est refusé par
                    // l'API : on l'interdit aussi ici, plutôt que d'échouer après.
                    disabled={type?.isDefault ?? false}
                    onCheckedChange={field.onChange}
                  />
                </FormControl>
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="isActive"
            render={({ field }) => (
              <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                <div className="flex flex-col gap-0.5">
                  <FormLabel>{t('fields.active')}</FormLabel>
                  <FormDescription>{t('fields.activeHint')}</FormDescription>
                </div>
                <FormControl>
                  <Switch
                    checked={field.value}
                    disabled={type?.isDefault ?? false}
                    onCheckedChange={field.onChange}
                  />
                </FormControl>
              </FormItem>
            )}
          />
        </form>
      </Form>
    </FormSheet>
  );
}
