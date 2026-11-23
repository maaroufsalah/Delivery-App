'use client';

import {
  type CityListItem,
  type CityInput,
  type CityPayload,
  citySchema,
  type Paginated,
  type Zone,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Lock, Pencil, Plus, Power, Trash2 } from 'lucide-react';
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
import { SearchableSelect } from '@/components/searchable-select';
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
import { deleteCityAction, saveCityAction, setCityActiveAction } from '@/lib/actions/referential';

const helper = createDataTableColumnHelper<CityListItem>();

/**
 * Villes du référentiel.
 *
 * La liste est paginée et triée par le serveur : l'URL porte `page`, `pageSize`,
 * `sort`, `order` et les filtres, et le composant serveur de la page relit ces
 * paramètres pour appeler l'API. Aucune ligne inutile ne descend.
 */
export function CitiesScreen({ page, zones }: { page: Paginated<CityListItem>; zones: Zone[] }) {
  const t = useTranslations('referential.cities');
  const tCommon = useTranslations('common');

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'name',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [editing, setEditing] = useState<CityListItem | null | undefined>(undefined);
  const [removing, setRemoving] = useState<CityListItem | null>(null);
  const [, startTransition] = useTransition();

  const toggleActive = (city: CityListItem) => {
    startTransition(async () => {
      const result = await setCityActiveAction(city.id, !city.isActive);
      if (result.ok) toast.success(t('toggled', { name: city.name }));
      else toast.error(result.error);
    });
  };

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        // D-255 : plusieurs zones ; « actif » reste un oui/non unique.
        id: 'zoneId',
        multiple: true,
        label: t('filters.zone'),
        value: param('zoneId') ?? ALL_VALUE,
        options: zones.map((zone) => ({ value: zone.id, label: zone.name })),
        onChange: (value) => setFilter('zoneId', value === ALL_VALUE ? undefined : value),
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
    [param, setFilter, t, tCommon, zones],
  );

  const columns: DataTableColumns<CityListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('name', {
          header: t('columns.name'),
          cell: (info) => (
            <CellStack
              label={info.getValue()}
              sub={t('columns.districtCount', { count: info.row.original.districtCount })}
            />
          ),
        }),

        helper.accessor('code', {
          header: t('columns.code'),
          meta: { width: 'w-28' },
          cell: (info) => (
            <span className="inline-flex items-center gap-1.5 font-mono text-sm font-medium">
              {info.getValue()}
              {/* Le cadenas explique pourquoi le champ sera grisé au formulaire. */}
              {info.row.original.codeLocked ? (
                <Lock className="text-muted-foreground size-3.5" aria-label={t('codeLocked')} />
              ) : null}
            </span>
          ),
        }),

        helper.accessor('zoneName', {
          header: t('columns.zone'),
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
    // `toggleActive` est stable pour la durée de vie de l'écran.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, tCommon],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(city) => setEditing(city)}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="cities"
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
        <CityForm city={editing} zones={zones} onDone={() => setEditing(undefined)} />
      ) : null}

      {removing ? (
        <DeleteDialog
          open
          onOpenChange={(next) => {
            if (!next) setRemoving(null);
          }}
          label={removing.name}
          onConfirm={() => deleteCityAction(removing.id)}
          onDeactivate={() => setCityActiveAction(removing.id, false)}
        />
      ) : null}
    </>
  );
}

function CityForm({
  city,
  zones,
  onDone,
}: {
  city: CityListItem | null;
  zones: Zone[];
  onDone: () => void;
}) {
  const t = useTranslations('referential.cities');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);

  const form = useForm<CityInput, unknown, CityPayload>({
    resolver: zodResolver(citySchema),
    defaultValues: {
      name: city?.name ?? '',
      /* Le nom arabe — chantier « Étiquette orange », point 1. */
      nameAr: city?.nameAr ?? '',
      code: city?.code ?? '',
      zoneId: city?.zoneId ?? zones[0]?.id ?? '',
      isActive: city?.isActive ?? true,
    },
    mode: 'onBlur',
  });

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await saveCityAction(city?.id ?? null, values);
    setSaving(false);

    if (result.ok) {
      toast.success(
        city ? t('updated', { name: values.name }) : t('created', { name: values.name }),
      );
      onDone();
      return;
    }

    // Une erreur qui vise un champ se pose SUR le champ : un message en haut
    // de panneau laisse l'utilisateur chercher lequel des quatre est en cause.
    if (result.field === 'code') form.setError('code', { message: result.error });
    else toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
  });

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={city ? t('edit') : t('new')}
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

          {/*
            LE NOM EN ARABE — chantier « Étiquette orange », point 1.

            « Ville de destination en grand (français + arabe). » C'est le
            destinataire qui le lit, au pas de porte. Facultatif : l'étiquette
            se contente du français tant que la ligne est vide, et aucune
            translittération automatique ne serait juste ici.

            `dir="rtl"` sur le champ : on saisit de l'arabe, quelle que soit la
            langue de l'interface.
          */}
          <FormField
            control={form.control}
            name="nameAr"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.nameAr')}</FormLabel>
                <FormControl>
                  <Input {...field} value={String(field.value ?? '')} dir="rtl" />
                </FormControl>
                <FormDescription>{t('fields.nameArHint')}</FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="code"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.code')}</FormLabel>
                <FormControl>
                  <Input
                    {...field}
                    maxLength={3}
                    disabled={city?.codeLocked ?? false}
                    className="font-mono uppercase"
                    onChange={(event) => field.onChange(event.target.value.toUpperCase())}
                  />
                </FormControl>
                <FormDescription>
                  {city?.codeLocked ? t('fields.codeLocked') : t('fields.codeHint')}
                </FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="zoneId"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.zone')}</FormLabel>
                <FormControl>
                  <SearchableSelect
                    options={zones.map((zone) => ({ value: zone.id, label: zone.name }))}
                    value={field.value}
                    onValueChange={field.onChange}
                    placeholder={t('fields.zone')}
                  />
                </FormControl>
                <FormDescription>{t('fields.zoneHint')}</FormDescription>
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
