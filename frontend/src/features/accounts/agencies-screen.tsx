'use client';

import {
  type AgencyInput,
  type AgencyListItem,
  type AgencyPayload,
  agencySchema,
  type City,
  type Paginated,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Building2, Pencil, Plus, Power, Trash2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useMemo, useState, useTransition } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import {
  CellBadge,
  CellMuted,
  CellNumber,
  CellStack,
  RowActions,
} from '@/components/data-table/cells';
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
import { Switch } from '@/components/ui/switch';
import { CitySelect } from '@/components/city-select';
import { DeleteDialog, FormSheet } from '@/features/referential/form-sheet';
import {
  deleteAgencyAction,
  saveAgencyAction,
  setAgencyActiveAction,
} from '@/lib/actions/accounts';

const helper = createDataTableColumnHelper<AgencyListItem>();

/**
 * Agences — sites d'exploitation.
 *
 * `currency` et `timezone` vides HÉRITENT de la société : la surcharge est
 * l'exception (une agence à Dakar, une à Casablanca), pas la règle. Écrire la
 * valeur de la société dans chaque agence la figerait le jour où elle change.
 */
export function AgenciesScreen({
  page,
  cities,
}: {
  page: Paginated<AgencyListItem>;
  cities: City[];
}) {
  const t = useTranslations('accounts.agencies');
  const tCommon = useTranslations('common');

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'name',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [editing, setEditing] = useState<AgencyListItem | null | undefined>(undefined);
  const [removing, setRemoving] = useState<AgencyListItem | null>(null);
  const [, startTransition] = useTransition();

  const toggleActive = (agency: AgencyListItem) => {
    startTransition(async () => {
      const result = await setAgencyActiveAction(agency.id, !agency.isActive);
      if (result.ok) toast.success(t('toggled', { name: agency.name }));
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

  const columns: DataTableColumns<AgencyListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('name', {
          header: t('columns.name'),
          cell: (info) => (
            <CellStack
              label={
                <span className="inline-flex items-center gap-1.5">
                  {info.getValue()}
                  {info.row.original.isHeadquarters ? (
                    <Building2
                      className="text-muted-foreground size-3.5"
                      aria-label={t('headquarters')}
                    />
                  ) : null}
                </span>
              }
              sub={info.row.original.email ?? info.row.original.phone ?? undefined}
            />
          ),
        }),

        helper.accessor('cityName', {
          header: t('columns.city'),
          meta: { hideBelow: 'sm' },
          cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
        }),

        helper.accessor('userCount', {
          header: t('columns.users'),
          meta: { align: 'end', hideBelow: 'md' },
          cell: (info) => <CellNumber value={info.getValue()} />,
        }),

        helper.accessor('parcelCount', {
          header: t('columns.parcels'),
          meta: { align: 'end', hideBelow: 'lg' },
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
          cell: (info) => {
            const row = info.row.original;

            return (
              <RowActions>
                <DropdownMenuItem onClick={() => setEditing(row)}>
                  <Pencil className="size-4" />
                  {tCommon('edit')}
                </DropdownMenuItem>
                {/* Le siège ne se désactive ni ne se supprime : c'est l'agence
                    de repli de toute la société, l'API le refuse aussi. */}
                <DropdownMenuItem onClick={() => toggleActive(row)} disabled={row.isHeadquarters}>
                  <Power className="size-4" />
                  {row.isActive ? tCommon('deactivate') : tCommon('activate')}
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  variant="destructive"
                  disabled={row.isHeadquarters}
                  onClick={() => setRemoving(row)}
                >
                  <Trash2 className="size-4" />
                  {tCommon('delete')}
                </DropdownMenuItem>
              </RowActions>
            );
          },
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
        onRowClick={(agency) => setEditing(agency)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="agencies"
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
        <AgencyForm agency={editing} cities={cities} onDone={() => setEditing(undefined)} />
      ) : null}

      {removing ? (
        <DeleteDialog
          open
          onOpenChange={(next) => {
            if (!next) setRemoving(null);
          }}
          label={removing.name}
          onConfirm={() => deleteAgencyAction(removing.id)}
          onDeactivate={() => setAgencyActiveAction(removing.id, false)}
        />
      ) : null}
    </>
  );
}

function AgencyForm({
  agency,
  cities,
  onDone,
}: {
  agency: AgencyListItem | null;
  cities: City[];
  onDone: () => void;
}) {
  const t = useTranslations('accounts.agencies');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);

  const form = useForm<AgencyInput, unknown, AgencyPayload>({
    resolver: zodResolver(agencySchema),
    defaultValues: {
      name: agency?.name ?? '',
      cityId: agency?.cityId ?? '',
      phone: agency?.phone ?? '',
      email: agency?.email ?? '',
      address: agency?.address ?? '',
      currency: agency?.currency ?? '',
      timezone: agency?.timezone ?? '',
      coveredCityIds: agency?.coveredCityIds ?? [],
      isActive: agency?.isActive ?? true,
    },
    mode: 'onBlur',
  });

  /*
    LES VILLES DESSERVIES — retouches Anass, point 4.

    `cityId` dit où l'agence SE TROUVE, celles-ci ce qu'elle COUVRE, et c'est
    la couverture qui décide du rattachement d'un nouvel e-commerçant. Sa
    propre ville n'y figure pas : elle est couverte d'office, et l'offrir en
    case à cocher ferait deux endroits pour un même fait.
  */
  const covered = form.watch('coveredCityIds') ?? [];
  const homeCityId = form.watch('cityId');

  const toggleCity = (cityId: string, checked: boolean) => {
    const next = checked
      ? [...new Set([...covered, cityId])]
      : covered.filter((entry) => entry !== cityId);
    form.setValue('coveredCityIds', next, { shouldDirty: true });
  };

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await saveAgencyAction(agency?.id ?? null, values);
    setSaving(false);

    if (result.ok) {
      toast.success(
        agency ? t('updated', { name: values.name }) : t('created', { name: values.name }),
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
      title={agency ? t('edit') : t('new')}
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

          <div className="grid grid-cols-2 gap-3">
            <FormField
              control={form.control}
              name="phone"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.phone')}</FormLabel>
                  <FormControl>
                    <Input {...field} inputMode="tel" />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="email"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.email')}</FormLabel>
                  <FormControl>
                    <Input {...field} type="email" />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <FormField
            control={form.control}
            name="address"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.address')}</FormLabel>
                <FormControl>
                  <Input {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="coveredCityIds"
            render={() => (
              <FormItem>
                <FormLabel>{t('fields.coveredCities')}</FormLabel>
                <FormDescription>{t('fields.coveredCitiesHint')}</FormDescription>
                <div className="flex max-h-56 flex-col gap-1 overflow-y-auto rounded-lg border p-2">
                  {cities
                    .filter((city) => city.id !== homeCityId)
                    .map((city) => (
                      <label
                        key={city.id}
                        className="hover:bg-accent/60 flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm"
                      >
                        <Checkbox
                          checked={covered.includes(city.id)}
                          onCheckedChange={(checked) => toggleCity(city.id, checked === true)}
                        />
                        <span className="truncate">{city.name}</span>
                      </label>
                    ))}
                </div>
                <FormMessage />
              </FormItem>
            )}
          />

          <div className="grid grid-cols-2 gap-3">
            <FormField
              control={form.control}
              name="currency"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.currency')}</FormLabel>
                  <FormControl>
                    <Input {...field} placeholder={t('fields.inherit')} maxLength={3} />
                  </FormControl>
                  <FormDescription>{t('fields.currencyHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="timezone"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.timezone')}</FormLabel>
                  <FormControl>
                    <Input {...field} placeholder={t('fields.inherit')} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

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
                    disabled={agency?.isHeadquarters ?? false}
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
