'use client';

import {
  type City,
  type DistrictListItem,
  type DistrictInput,
  type DistrictPayload,
  districtSchema,
  type Paginated,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Pencil, Plus, Power, Trash2 } from 'lucide-react';
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
import { CitySelect } from '@/components/city-select';
import { DeleteDialog, FormSheet } from '@/features/referential/form-sheet';
import {
  deleteDistrictAction,
  saveDistrictAction,
  setDistrictActiveAction,
} from '@/lib/actions/referential';

const helper = createDataTableColumnHelper<DistrictListItem>();

/**
 * Quartiers — 10 164 lignes en production (DOMAIN.md §1.7).
 *
 * C'est l'écran qui justifie à lui seul la pagination serveur : charger la
 * table entière pour en montrer vingt-cinq lignes transférait dix mille
 * enregistrements à chaque visite.
 */
export function DistrictsScreen({
  page,
  cities,
}: {
  page: Paginated<DistrictListItem>;
  cities: City[];
}) {
  const t = useTranslations('referential.districts');
  const tCommon = useTranslations('common');

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'name',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [editing, setEditing] = useState<DistrictListItem | null | undefined>(undefined);
  const [removing, setRemoving] = useState<DistrictListItem | null>(null);
  const [, startTransition] = useTransition();

  const toggleActive = (district: DistrictListItem) => {
    startTransition(async () => {
      const result = await setDistrictActiveAction(district.id, !district.isActive);
      if (result.ok) toast.success(t('toggled', { name: district.name }));
      else toast.error(result.error);
    });
  };

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        // D-255 : plusieurs villes ; « actif » reste un oui/non unique.
        id: 'cityId',
        multiple: true,
        label: t('filters.city'),
        value: param('cityId') ?? ALL_VALUE,
        options: cities.map((city) => ({ value: city.id, label: city.name })),
        cities,
        onChange: (value) => setFilter('cityId', value === ALL_VALUE ? undefined : value),
      },
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
    [cities, param, setFilter, t, tCommon],
  );

  const columns: DataTableColumns<DistrictListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('name', {
          header: t('columns.name'),
          cell: (info) => <CellStack label={info.getValue()} sub={info.row.original.cityName} />,
        }),

        helper.accessor('cityName', {
          header: t('columns.city'),
          meta: { hideBelow: 'sm' },
        }),

        helper.accessor('parcelCount', {
          header: t('columns.parcels'),
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => <CellNumber value={info.getValue()} />,
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
    [t, tCommon],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(district) => setEditing(district)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="districts"
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
        <DistrictForm
          district={editing}
          cities={cities}
          defaultCityId={param('cityId')}
          onDone={() => setEditing(undefined)}
        />
      ) : null}

      {removing ? (
        <DeleteDialog
          open
          onOpenChange={(next) => {
            if (!next) setRemoving(null);
          }}
          label={removing.name}
          onConfirm={() => deleteDistrictAction(removing.id)}
          onDeactivate={() => setDistrictActiveAction(removing.id, false)}
        />
      ) : null}
    </>
  );
}

function DistrictForm({
  district,
  cities,
  defaultCityId,
  onDone,
}: {
  district: DistrictListItem | null;
  cities: City[];
  defaultCityId?: string;
  onDone: () => void;
}) {
  const t = useTranslations('referential.districts');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);

  const form = useForm<DistrictInput, unknown, DistrictPayload>({
    resolver: zodResolver(districtSchema),
    defaultValues: {
      // La ville filtrée est pré-remplie : on crée presque toujours un quartier
      // dans la ville qu'on est en train de regarder.
      cityId: district?.cityId ?? defaultCityId ?? cities[0]?.id ?? '',
      name: district?.name ?? '',
      isActive: district?.isActive ?? true,
    },
    mode: 'onBlur',
  });

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await saveDistrictAction(district?.id ?? null, values);
    setSaving(false);

    if (result.ok) {
      toast.success(
        district ? t('updated', { name: values.name }) : t('created', { name: values.name }),
      );
      onDone();
      return;
    }

    if (result.field === 'name') form.setError('name', { message: result.error });
    else toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
  });

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={district ? t('edit') : t('new')}
      description={t('formHint')}
      onSubmit={submit}
      submitting={saving}
    >
      <Form {...form}>
        <form className="flex flex-col gap-4" onSubmit={submit}>
          <FormField
            control={form.control}
            name="cityId"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.city')}</FormLabel>
                <FormControl>
                  <CitySelect
                    cities={cities}
                    value={field.value}
                    onValueChange={field.onChange}
                    placeholder={t('fields.city')}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="name"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.name')}</FormLabel>
                <FormControl>
                  <Input {...field} autoFocus />
                </FormControl>
                <FormDescription>{t('fields.nameHint')}</FormDescription>
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
