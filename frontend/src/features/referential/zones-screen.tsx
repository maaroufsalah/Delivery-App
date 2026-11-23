'use client';

import {
  type City,
  type Paginated,
  type ZoneListItem,
  type ZoneInput,
  type ZonePayload,
  zoneSchema,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Pencil, Plus, Power, Trash2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
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
import { Checkbox } from '@/components/ui/checkbox';
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
import { ScrollArea } from '@/components/ui/scroll-area';
import { Switch } from '@/components/ui/switch';
import { DeleteDialog, FormSheet } from '@/features/referential/form-sheet';
import { deleteZoneAction, saveZoneAction, setZoneActiveAction } from '@/lib/actions/referential';

const helper = createDataTableColumnHelper<ZoneListItem>();

/**
 * Zones tarifaires.
 *
 * Le rattachement des villes se fait ICI, par cases à cocher, et il écrit le
 * pivot `ZoneCity`. DOMAIN.md §4 dette n°15 : le legacy stockait cette liste
 * en CSV dans une colonne `text`, ce qui interdisait toute jointure et rendait
 * « quelles zones contiennent Casablanca ? » impossible à poser en SQL.
 */
export function ZonesScreen({ page, cities }: { page: Paginated<ZoneListItem>; cities: City[] }) {
  const t = useTranslations('referential.zones');
  const tCommon = useTranslations('common');
  const format = useFormatter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'name',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [editing, setEditing] = useState<ZoneListItem | null | undefined>(undefined);
  const [removing, setRemoving] = useState<ZoneListItem | null>(null);
  const [, startTransition] = useTransition();

  const toggleActive = (zone: ZoneListItem) => {
    startTransition(async () => {
      const result = await setZoneActiveAction(zone.id, !zone.isActive);
      if (result.ok) toast.success(t('toggled', { name: zone.name }));
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

  const columns: DataTableColumns<ZoneListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('name', {
          header: t('columns.name'),
          cell: (info) => (
            <CellStack
              label={info.getValue()}
              sub={t('columns.cityCount', { count: info.row.original.cityCount })}
            />
          ),
        }),

        helper.accessor('baseRate', {
          header: t('columns.baseRate'),
          meta: { align: 'end' },
          cell: (info) => <CellNumber value={format.number(info.getValue() / 100, 'currency')} />,
        }),

        helper.accessor('sameCityRate', {
          header: t('columns.sameCityRate'),
          meta: { align: 'end', hideBelow: 'sm' },
          cell: (info) => <CellNumber value={format.number(info.getValue() / 100, 'currency')} />,
        }),

        helper.accessor('deliveryDays', {
          header: t('columns.deliveryDays'),
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => <CellNumber value={t('days', { count: info.getValue() })} />,
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
              <DropdownMenuItem onClick={() => toggleActive(info.row.original)}>
                <Power className="size-4" />
                {info.row.original.isActive ? tCommon('deactivate') : tCommon('activate')}
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                variant="destructive"
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
    [format, t, tCommon],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(zone) => setEditing(zone)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="zones"
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
        <ZoneForm zone={editing} cities={cities} onDone={() => setEditing(undefined)} />
      ) : null}

      {removing ? (
        <DeleteDialog
          open
          onOpenChange={(next) => {
            if (!next) setRemoving(null);
          }}
          label={removing.name}
          onConfirm={() => deleteZoneAction(removing.id)}
          onDeactivate={() => setZoneActiveAction(removing.id, false)}
        />
      ) : null}
    </>
  );
}

function ZoneForm({
  zone,
  cities,
  onDone,
}: {
  zone: ZoneListItem | null;
  cities: City[];
  onDone: () => void;
}) {
  const t = useTranslations('referential.zones');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);

  const form = useForm<ZoneInput, unknown, ZonePayload>({
    resolver: zodResolver(zoneSchema),
    defaultValues: {
      name: zone?.name ?? '',
      // Les montants se SAISISSENT en dirhams et se lisent en centimes : la
      // conversion vit à la frontière, jamais dans un composant (CLAUDE.md).
      baseRate: (zone?.baseRate ?? 0) / 100,
      sameCityRate: (zone?.sameCityRate ?? 0) / 100,
      deliveryDays: zone?.deliveryDays ?? 2,
      isActive: zone?.isActive ?? true,
      cityIds: zone?.cityIds ?? [],
    },
    mode: 'onBlur',
  });

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await saveZoneAction(zone?.id ?? null, values);
    setSaving(false);

    if (result.ok) {
      toast.success(
        zone ? t('updated', { name: values.name }) : t('created', { name: values.name }),
      );
      onDone();
      return;
    }

    toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
  });

  // `watch` rend le type d'ENTRÉE, où `cityIds` est facultatif (`.default([])`).
  const selected = form.watch('cityIds') ?? [];

  const toggleCity = (cityId: string, checked: boolean) => {
    const next = checked
      ? [...new Set([...selected, cityId])]
      : selected.filter((id) => id !== cityId);
    form.setValue('cityIds', next, { shouldDirty: true });
  };

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={zone ? t('edit') : t('new')}
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

          <div className="grid grid-cols-2 gap-3">
            <FormField
              control={form.control}
              name="baseRate"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.baseRate')}</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      min={0}
                      step="0.01"
                      value={field.value}
                      onChange={(event) => field.onChange(event.target.valueAsNumber)}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="sameCityRate"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.sameCityRate')}</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      min={0}
                      step="0.01"
                      value={field.value}
                      onChange={(event) => field.onChange(event.target.valueAsNumber)}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <FormField
            control={form.control}
            name="deliveryDays"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.deliveryDays')}</FormLabel>
                <FormControl>
                  <Input
                    type="number"
                    min={0}
                    max={30}
                    value={field.value}
                    onChange={(event) => field.onChange(event.target.valueAsNumber)}
                  />
                </FormControl>
                <FormDescription>{t('fields.deliveryDaysHint')}</FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="cityIds"
            render={() => (
              <FormItem>
                <FormLabel>{t('fields.cities')}</FormLabel>
                <FormDescription>{t('fields.citiesHint')}</FormDescription>
                <ScrollArea className="h-56 rounded-lg border">
                  <div className="flex flex-col gap-1 p-2">
                    {cities.map((city) => (
                      <label
                        key={city.id}
                        className="hover:bg-accent/60 flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm"
                      >
                        <Checkbox
                          checked={selected.includes(city.id)}
                          onCheckedChange={(checked) => toggleCity(city.id, checked === true)}
                        />
                        <span className="truncate">{city.name}</span>
                        <span className="text-muted-foreground ms-auto font-mono text-xs">
                          {city.code}
                        </span>
                      </label>
                    ))}
                  </div>
                </ScrollArea>
                <FormMessage />
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
                  <Switch checked={field.value} onCheckedChange={field.onChange} />
                </FormControl>
              </FormItem>
            )}
          />
        </form>
      </Form>
    </FormSheet>
  );
}
