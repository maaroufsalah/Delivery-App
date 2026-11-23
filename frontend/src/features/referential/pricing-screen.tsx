'use client';

import type {
  City,
  ImportRowError,
  PricingOriginImportPreview,
  PricingOriginPage,
  PricingOriginRow,
  PricingOriginSaveInput,
  Zone,
} from '@delivery/shared';
import { AlertTriangle, Check, Download, Loader2, Save, Undo2, Upload } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useRef, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellStack } from '@/components/data-table/cells';
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
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import { CitySelect } from '@/components/city-select';
import {
  exportPricingOriginAction,
  importPricingOriginAction,
  savePricingOriginAction,
} from '@/lib/actions/referential';
import { cn } from '@/lib/utils';

const helper = createDataTableColumnHelper<PricingOriginRow>();

/** Une ligne en cours d'édition : ce qui est saisi, et ce qui était là. */
interface Edit {
  rate: string;
  delay: string;
  original: { rate: string; delay: string };
}

/** Centimes → saisie en dirhams. `null` = case vide, donc tarif de zone. */
function toInput(cents: number | null): string {
  return cents === null ? '' : String(cents / 100);
}

/** Saisie → dirhams. `null` = vide ; `undefined` = illisible. */
function parseRate(value: string): number | null | undefined {
  const text = value.trim().replace(',', '.');
  if (text === '') return null;
  if (!/^\d+(\.\d{1,2})?$/.test(text)) return undefined;
  const amount = Number(text);
  return amount <= 1_000_000 ? amount : undefined;
}

/**
 * Grille tarifaire PAR ORIGINE — chantier « Tarifs par origine ».
 *
 * DOMAIN.md §3.1 : c'est la source de vérité des devis. Le devis lit la paire
 * origine × destination, puis retombe sur la zone ; l'écran montre donc TOUTES
 * les destinations, et celles sans règle affichent en gris le tarif de zone
 * que le devis leur appliquera.
 *
 * Les saisies s'accumulent (elles survivent au changement de page) et partent
 * par ligne ou en bloc. Changer d'origine demande d'abord de les enregistrer ou
 * de les annuler : une modification appartient à UNE grille.
 */
export function PricingScreen({
  page,
  zones,
  cities,
}: {
  page: PricingOriginPage;
  zones: Zone[];
  cities: City[];
}) {
  const t = useTranslations('referential.pricing');
  const format = useFormatter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'name',
    defaultPageSize: 50,
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));
  const [edits, setEdits] = useState<Map<string, Edit>>(new Map());
  const [saving, startSaving] = useTransition();

  const money = (cents: number) => format.number(cents / 100, 'currency');

  const valueOf = (row: PricingOriginRow) => {
    const edit = edits.get(row.cityId);
    return {
      rate: edit?.rate ?? toInput(row.rate),
      delay: edit?.delay ?? row.deliveryDelay ?? '',
    };
  };

  const change = (row: PricingOriginRow, field: 'rate' | 'delay', value: string) => {
    setEdits((current) => {
      const next = new Map(current);
      const original = current.get(row.cityId)?.original ?? {
        rate: toInput(row.rate),
        delay: row.deliveryDelay ?? '',
      };
      const edit = { ...valueOf(row), original, [field]: value };

      // Revenir à la valeur d'origine retire la ligne des modifications : le
      // compteur du bouton doit dire la vérité.
      if (edit.rate.trim() === original.rate && edit.delay.trim() === original.delay) {
        next.delete(row.cityId);
      } else {
        next.set(row.cityId, edit);
      }
      return next;
    });
  };

  const drop = (cityIds: string[]) =>
    setEdits((current) => {
      const next = new Map(current);
      for (const id of cityIds) next.delete(id);
      return next;
    });

  const save = (cityIds: string[]) => {
    const rows: PricingOriginSaveInput['rows'] = [];
    for (const id of cityIds) {
      const edit = edits.get(id);
      if (!edit) continue;

      const rate = parseRate(edit.rate);
      if (rate === undefined) {
        toast.error(t('fixErrors'));
        return;
      }

      const delay = edit.delay.trim();
      rows.push({
        destinationCityId: id,
        rate,
        ...(delay === edit.original.delay ? {} : { deliveryDelay: delay === '' ? null : delay }),
      });
    }

    if (rows.length === 0) {
      toast.info(t('nothingToSave'));
      return;
    }

    startSaving(async () => {
      const result = await savePricingOriginAction(page.origin.id, { rows });
      if (!result.ok) {
        toast.error(result.error);
        return;
      }

      toast.success(
        t('saved', {
          priced: String(result.data.priced),
          cleared: String(result.data.cleared),
          delays: String(result.data.delays),
        }),
      );
      drop(cityIds);
    });
  };

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        // D-255 : plusieurs zones. L'origine, elle, choisit UNE grille : valeur unique.
        id: 'zoneId',
        multiple: true,
        label: t('filters.zone'),
        value: param('zoneId') ?? ALL_VALUE,
        options: zones.map((zone) => ({ value: zone.id, label: zone.name })),
        onChange: (value) => setFilter('zoneId', value === ALL_VALUE ? undefined : value),
      },
    ],
    [param, setFilter, t, zones],
  );

  const columns: DataTableColumns<PricingOriginRow> = useMemo(
    () =>
      helper.columns([
        helper.accessor('name', {
          header: t('columns.city'),
          cell: (info) => (
            <CellStack label={info.getValue()} sub={info.row.original.nameAr ?? undefined} />
          ),
        }),

        helper.accessor('code', {
          header: t('columns.code'),
          meta: { width: 'w-20' },
          cell: (info) => <span className="font-mono text-sm font-medium">{info.getValue()}</span>,
        }),

        helper.accessor('zoneName', {
          header: t('columns.zone'),
          meta: { hideBelow: 'md' },
        }),

        helper.accessor('rate', {
          header: t('columns.rate'),
          meta: { width: 'w-36', align: 'end' },
          cell: (info) => {
            const row = info.row.original;
            const value = valueOf(row).rate;
            const invalid = parseRate(value) === undefined;

            return (
              <Input
                inputMode="decimal"
                value={value}
                disabled={saving}
                aria-label={`${t('columns.rate')} — ${row.name}`}
                aria-invalid={invalid}
                title={invalid ? t('invalidRate') : undefined}
                // Case vide : le tarif de zone, en gris — c'est ce que le devis appliquera.
                placeholder={t('zoneRate', { amount: money(row.zoneRate) })}
                onChange={(event) => change(row, 'rate', event.target.value)}
                className={cn(
                  'placeholder:text-muted-foreground/70 h-8 text-end tabular-nums',
                  edits.has(row.cityId) && 'border-primary',
                  invalid && 'border-destructive',
                )}
              />
            );
          },
        }),

        helper.accessor('deliveryDelay', {
          header: t('columns.delay'),
          meta: { width: 'w-40', hideBelow: 'sm' },
          cell: (info) => {
            const row = info.row.original;
            return (
              <Input
                value={valueOf(row).delay}
                maxLength={40}
                disabled={saving}
                aria-label={`${t('columns.delay')} — ${row.name}`}
                title={t('delayShared')}
                placeholder={t('delayPlaceholder')}
                onChange={(event) => change(row, 'delay', event.target.value)}
                className={cn('h-8', edits.has(row.cityId) && 'border-primary')}
              />
            );
          },
        }),

        helper.display({
          id: 'actions',
          header: '',
          meta: { isActions: true, align: 'end', width: 'w-20' },
          cell: (info) =>
            edits.has(info.row.original.cityId) ? (
              <div className="flex justify-end gap-1">
                <Button
                  size="icon"
                  variant="ghost"
                  className="size-8"
                  disabled={saving}
                  aria-label={t('saveRow')}
                  title={t('saveRow')}
                  onClick={() => save([info.row.original.cityId])}
                >
                  <Check className="size-4" />
                </Button>
                <Button
                  size="icon"
                  variant="ghost"
                  className="size-8"
                  disabled={saving}
                  aria-label={t('resetRow')}
                  title={t('resetRow')}
                  onClick={() => drop([info.row.original.cityId])}
                >
                  <Undo2 className="size-4" />
                </Button>
              </div>
            ) : null,
        }),
      ]),
    // `valueOf`, `change`, `save`, `drop` et `money` ne lisent que `edits`,
    // `saving` et le formateur : les colonnes se refont quand eux changent.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [edits, saving, t, format],
  );

  const dirty = edits.size;

  return (
    <div className="flex flex-col gap-4">
      <Card className="gap-3 p-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex min-w-0 flex-col gap-1.5">
            <span className="text-muted-foreground text-xs font-medium">{t('origin')}</span>
            <OriginPicker
              cities={cities}
              value={page.origin}
              locked={dirty > 0}
              onChange={(id) => setFilter('origin', id)}
            />
          </div>

          <div className="flex flex-wrap items-center gap-2">
            {dirty > 0 ? (
              <Button
                size="sm"
                variant="ghost"
                className="gap-1.5"
                onClick={() => setEdits(new Map())}
                disabled={saving}
              >
                <Undo2 className="size-4" />
                {t('discard')}
              </Button>
            ) : null}
            <Button
              size="sm"
              className="gap-1.5"
              onClick={() => save([...edits.keys()])}
              disabled={saving || dirty === 0}
            >
              {saving ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}
              {t('saveAll', { count: Math.max(dirty, 1) })}
            </Button>
          </div>
        </div>

        <p className="text-muted-foreground text-xs">
          {dirty > 0
            ? t('originLocked')
            : t('summary', {
                priced: String(page.priced),
                total: String(cities.filter((city) => city.isActive).length),
                origin: page.origin.name,
              })}
        </p>
      </Card>

      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        emptyTitle={t('empty')}
        emptyHint={t('emptyHint')}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            onReset={reset}
          >
            <ExportButton origin={page.origin} />
            <ImportDialog origin={page.origin} disabled={dirty > 0} />
          </DataTableToolbar>
        }
      />
    </div>
  );
}

/**
 * Le sélecteur d'origine : le `CitySelect` partagé (point 2 du 26/09), sur
 * les seules villes actives, verrouillé tant que des modifications sont en
 * attente.
 */
function OriginPicker({
  cities,
  value,
  locked,
  onChange,
}: {
  cities: City[];
  value: PricingOriginPage['origin'];
  locked: boolean;
  onChange: (cityId: string) => void;
}) {
  const t = useTranslations('referential.pricing');
  const active = useMemo(() => cities.filter((city) => city.isActive), [cities]);

  return (
    <CitySelect
      cities={active}
      value={value.id}
      onValueChange={onChange}
      disabled={locked}
      title={locked ? t('originLocked') : undefined}
      aria-label={t('origin')}
      className="sm:w-72"
    />
  );
}

function ExportButton({ origin }: { origin: PricingOriginPage['origin'] }) {
  const t = useTranslations('referential.pricing');
  const [pending, startExport] = useTransition();

  const download = () => {
    startExport(async () => {
      const result = await exportPricingOriginAction(origin.id);
      if (!result.ok) {
        toast.error(result.error);
        return;
      }

      // Le base64 revient de l'action ; le fichier est fabriqué ici, côté
      // navigateur, sans route de proxy à maintenir.
      const bytes = Uint8Array.from(atob(result.data), (char) => char.charCodeAt(0));
      const blob = new Blob([bytes], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      });

      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `tarifs-${origin.code}.xlsx`;
      link.click();
      URL.revokeObjectURL(url);
    });
  };

  return (
    <Button size="sm" variant="outline" className="gap-1.5" onClick={download} disabled={pending}>
      {pending ? <Loader2 className="size-4 animate-spin" /> : <Download className="size-4" />}
      <span className="hidden sm:inline">{t('export')}</span>
    </Button>
  );
}

/**
 * Import pour l'origine affichée, en deux temps : aperçu, puis application.
 *
 * L'aperçu ne touche à rien et sépare les lignes reconnues des autres (dette
 * n°12) ; l'application n'écrit que les premières. Aucune ville n'est créée :
 * une ville inconnue est une ligne ignorée, montrée avant de valider.
 */
function ImportDialog({
  origin,
  disabled,
}: {
  origin: PricingOriginPage['origin'];
  disabled: boolean;
}) {
  const t = useTranslations('referential.pricing.import');
  const fileInput = useRef<HTMLInputElement>(null);

  const [open, setOpen] = useState(false);
  const [filename, setFilename] = useState<string | null>(null);
  const [content, setContent] = useState<string | null>(null);
  const [preview, setPreview] = useState<PricingOriginImportPreview | null>(null);
  const [pending, startPending] = useTransition();

  const reset = (next: boolean) => {
    if (!next) {
      setFilename(null);
      setContent(null);
      setPreview(null);
    }
    setOpen(next);
  };

  const onFile = async (file: File | undefined) => {
    if (!file) return;

    const bytes = new Uint8Array(await file.arrayBuffer());
    let binary = '';
    // `String.fromCharCode(...bytes)` dépasse la pile sur un fichier de
    // quelques centaines de kilo-octets : on encode par tranches.
    for (let index = 0; index < bytes.length; index += 8192) {
      binary += String.fromCharCode(...bytes.subarray(index, index + 8192));
    }

    const base64 = btoa(binary);
    setFilename(file.name);
    setContent(base64);
    setPreview(null);

    startPending(async () => {
      const result = await importPricingOriginAction(origin.id, base64, false, file.name);
      if (result.ok) setPreview(result.data);
      else toast.error(result.error);
    });
  };

  const apply = () => {
    if (!content) return;

    startPending(async () => {
      const result = await importPricingOriginAction(
        origin.id,
        content,
        true,
        filename ?? undefined,
      );
      if (result.ok) {
        toast.success(t('applied', { count: result.data.changes }));
        reset(false);
        return;
      }

      toast.error(result.error);
    });
  };

  const changes = preview?.changes ?? 0;

  return (
    <Dialog open={open} onOpenChange={reset}>
      <Button
        size="sm"
        variant="outline"
        className="gap-1.5"
        onClick={() => setOpen(true)}
        disabled={disabled}
      >
        <Upload className="size-4" />
        <span className="hidden sm:inline">{t('trigger')}</span>
      </Button>

      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t('title', { origin: origin.name })}</DialogTitle>
          <DialogDescription>{t('hint')}</DialogDescription>
        </DialogHeader>

        <div className="flex min-w-0 flex-col gap-3">
          <Input
            ref={fileInput}
            type="file"
            accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            disabled={pending}
            onChange={(event) => void onFile(event.target.files?.[0])}
          />

          <p className="text-muted-foreground text-xs">{t('columns')}</p>

          {pending && !preview ? (
            <div className="text-muted-foreground flex items-center gap-2 py-6 text-sm">
              <Loader2 className="size-4 animate-spin" />
              {t('analysing')}
            </div>
          ) : null}

          {preview ? <ImportPreview preview={preview} /> : null}
        </div>

        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={() => reset(false)} disabled={pending}>
            {t('cancel')}
          </Button>
          <Button onClick={apply} disabled={changes === 0 || pending} className="gap-1.5">
            {pending ? <Loader2 className="size-4 animate-spin" /> : null}
            {t('apply', { count: changes })}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function ImportPreview({ preview }: { preview: PricingOriginImportPreview }) {
  const t = useTranslations('referential.pricing.import');
  const format = useFormatter();
  const money = (cents: number | null) =>
    cents === null ? '—' : format.number(cents / 100, 'currency');

  const changed = preview.recognized.filter((row) => row.changed);

  return (
    <div className="flex min-w-0 flex-col gap-3">
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        <Stat label={t('rows')} value={preview.rows} />
        <Stat label={t('recognized')} value={preview.recognized.length} />
        <Stat label={t('rejected')} value={preview.rejected.length} />
        <Stat label={t('changes')} value={preview.changes} />
      </div>

      {changed.length > 0 ? (
        <Card className="gap-0 py-0">
          <CardHeader className="px-3 py-2">
            <CardTitle className="text-sm">{t('recognizedTitle')}</CardTitle>
          </CardHeader>
          <CardContent className="px-0 pb-0">
            <ScrollArea className="max-h-48">
              <ul className="divide-y text-sm">
                {changed.map((row) => (
                  <li key={row.cityId} className="flex items-baseline gap-2 px-3 py-1.5">
                    <span className="text-muted-foreground shrink-0 font-mono text-xs tabular-nums">
                      {t('line', { row: String(row.row) })}
                    </span>
                    <span className="min-w-0 truncate">{row.name}</span>
                    <span className="text-muted-foreground ms-auto shrink-0 text-xs tabular-nums">
                      {row.rate === null || row.rate === row.previousRate
                        ? t('unchanged')
                        : `${money(row.previousRate)} → ${money(row.rate)}`}
                      {row.deliveryDelay !== null && row.deliveryDelay !== row.previousDelay
                        ? ` · ${row.previousDelay ?? '—'} → ${row.deliveryDelay}`
                        : ''}
                    </span>
                  </li>
                ))}
              </ul>
            </ScrollArea>
          </CardContent>
        </Card>
      ) : null}

      {preview.rejected.length > 0 ? (
        <Card className="border-destructive/40 gap-0 py-0">
          <CardHeader className="px-3 py-2">
            <CardTitle className="text-destructive flex items-center gap-2 text-sm">
              <AlertTriangle className="size-4" />
              {t('rejectedTitle')}
            </CardTitle>
            <CardDescription className="text-xs">
              {preview.rejected.length} / {preview.rows}
            </CardDescription>
          </CardHeader>
          <CardContent className="px-0 pb-0">
            <ScrollArea className="max-h-48">
              <ul className="divide-y text-sm">
                {preview.rejected.map((error, index) => (
                  <ErrorRow key={`${error.row}-${index}`} error={error} />
                ))}
              </ul>
            </ScrollArea>
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}

function ErrorRow({ error }: { error: ImportRowError }) {
  const t = useTranslations('referential.pricing.import');

  return (
    <li className="flex items-baseline gap-2 px-3 py-1.5">
      <span className="text-muted-foreground shrink-0 font-mono text-xs tabular-nums">
        {t('line', { row: String(error.row) })}
      </span>
      <span className="min-w-0">{error.message}</span>
    </li>
  );
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-lg border px-3 py-2">
      <p className="text-muted-foreground text-xs">{label}</p>
      <p className="text-lg font-semibold tabular-nums">{value}</p>
    </div>
  );
}
