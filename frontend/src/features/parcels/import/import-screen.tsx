'use client';

import {
  type Client,
  importDisplayOrder,
  importRowMatchesFilter,
  importSeverityCounts,
  type ParcelImportField,
  type ParcelImportFilter,
  type ParcelImportMode,
  type ParcelImportPreview,
  type ParcelImportResult,
  type ParcelImportRow,
  type ParcelImportRowInput,
} from '@delivery/shared';
import {
  AlertTriangle,
  ArrowLeft,
  CheckCircle2,
  Download,
  FileSpreadsheet,
  Loader2,
  Upload,
} from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useEffect, useMemo, useRef, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { DataTable } from '@/components/data-table/data-table';
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { SearchableSelect } from '@/components/searchable-select';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useImportColumns } from '@/features/parcels/import/import-columns';
import { Link } from '@/i18n/navigation';
import {
  parcelImportAnomaliesAction,
  parcelImportTemplateAction,
  previewParcelImportAction,
  runParcelImportAction,
} from '@/lib/actions/parcels';
import { downloadBase64 } from '@/lib/download';

/**
 * Import Excel des colis — l'écran, en trois temps.
 *
 *  1. **Le fichier.** On télécharge le modèle, on choisit l'e-commerçant, on
 *     dépose le classeur. Rien n'est créé.
 *  2. **La relecture.** Chaque ligne est affichée avec ses erreurs, DANS ses
 *     cellules. On corrige sur place et on revérifie — sans redonner le
 *     fichier : ce sont les lignes corrigées qui repartent à l'API, et elles
 *     passent par le même validateur que la première lecture.
 *  3. **L'import.** Tout ou rien par défaut ; « importer les lignes valides »
 *     doit être demandé.
 *
 * Le classeur n'est lu QUE par le serveur : le navigateur ne fait que l'encoder
 * en base64 et le poster à une Server Action (D-018, D-022). Aucune
 * bibliothèque de lecture Excel n'entre dans le bundle.
 */

const XLSX_MIME = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

type Step = 'upload' | 'review' | 'done';

const FILTERS: readonly ParcelImportFilter[] = ['all', 'errors', 'warnings'];

export function ParcelImportScreen({
  clients,
  lockedClientId,
}: {
  clients: Client[];
  /** Renseigné pour un e-commerçant : il importe pour lui-même, sans choisir. */
  lockedClientId?: string;
}) {
  const t = useTranslations('parcels.import');
  const format = useFormatter();

  const fileRef = useRef<HTMLInputElement>(null);
  const [clientId, setClientId] = useState(lockedClientId ?? '');
  const [filename, setFilename] = useState('');
  const [preview, setPreview] = useState<ParcelImportPreview | null>(null);
  const [result, setResult] = useState<ParcelImportResult | null>(null);
  const [mode, setMode] = useState<ParcelImportMode>('all-or-nothing');
  const [pending, startTransition] = useTransition();
  /*
   * Lot J, J3 : la dernière cellule corrigée, pour y rendre le focus après
   * « Vérifier à nouveau » (désactivée pendant la vérification, elle l'a
   * perdu). `refocus` compte les relectures revenues.
   */
  const lastCell = useRef<string | null>(null);
  const [refocus, setRefocus] = useState(0);
  /*
   * Lot J, J4 : l'ordre d'affichage (erreurs, avertissements, valides) est
   * FIGÉ à chaque relecture du serveur, pas recalculé à chaque frappe : une
   * ligne corrigée garde sa place jusqu'à « Vérifier à nouveau ».
   */
  const [order, setOrder] = useState<number[]>([]);
  const [filter, setFilter] = useState<ParcelImportFilter>('all');
  useEffect(() => {
    if (refocus === 0 || lastCell.current === null) return;
    document.querySelector<HTMLInputElement>(`[data-import-cell="${lastCell.current}"]`)?.focus();
  }, [refocus]);

  const step: Step = result ? 'done' : preview ? 'review' : 'upload';

  /**
   * Les douze cellules d'une ligne, telles que l'API les relit — et son numéro
   * dans le fichier (lot J), que la relecture garde.
   */
  const rowsPayload = (source: ParcelImportPreview): ParcelImportRowInput[] =>
    source.items.map((item) => ({ row: item.row, ...item.values }));

  /** Une relecture revenue du serveur : elle fixe l'ordre d'affichage. */
  const receive = (next: ParcelImportPreview) => {
    setPreview(next);
    setOrder(importDisplayOrder(next.items));
  };

  const counts = useMemo(() => importSeverityCounts(preview?.items ?? []), [preview]);
  const shown = useMemo(() => {
    const byRow = new Map((preview?.items ?? []).map((item) => [item.row, item]));
    return order
      .map((row) => byRow.get(row))
      .filter((item): item is ParcelImportRow => item !== undefined)
      .filter((item) => importRowMatchesFilter(item, filter));
  }, [preview, order, filter]);

  const columns = useImportColumns({
    disabled: pending,
    onEdit: (row, field: ParcelImportField, value) => {
      lastCell.current = `${row}:${field}`;
      setPreview((current) =>
        current === null
          ? current
          : {
              ...current,
              items: current.items.map((item) =>
                item.row === row ? { ...item, values: { ...item.values, [field]: value } } : item,
              ),
            },
      );
    },
  });

  /* --------------------------------------------------------------- actions */

  const downloadTemplate = () => {
    startTransition(async () => {
      const response = await parcelImportTemplateAction();
      if (!response.ok) {
        toast.error(response.error);
        return;
      }
      downloadBase64(response.data, t('template.filename'), XLSX_MIME);
    });
  };

  const readFile = (file: File) => {
    startTransition(async () => {
      const bytes = new Uint8Array(await file.arrayBuffer());
      // Par tranches : `String.fromCharCode(...bytes)` sur un fichier d'un
      // mégaoctet dépasse la taille maximale d'une liste d'arguments.
      let binary = '';
      for (let index = 0; index < bytes.length; index += 8192) {
        binary += String.fromCharCode(...bytes.subarray(index, index + 8192));
      }

      setFilename(file.name);

      const response = await previewParcelImportAction({
        content: btoa(binary),
        filename: file.name,
        clientId: clientId || undefined,
      });

      if (response.ok) receive(response.data);
      else toast.error(response.error);
    });
  };

  /** Revérifie les lignes CORRIGÉES. Le fichier n'est plus nécessaire. */
  const recheck = () => {
    if (!preview) return;

    startTransition(async () => {
      const response = await previewParcelImportAction({
        rows: rowsPayload(preview),
        filename,
        clientId: clientId || undefined,
      });

      if (response.ok) {
        receive(response.data);
        setRefocus((count) => count + 1);
      } else toast.error(response.error);
    });
  };

  /** Lot J, J4 : les anomalies de la relecture EN COURS, corrections comprises. */
  const downloadAnomalies = () => {
    if (!preview) return;

    startTransition(async () => {
      const response = await parcelImportAnomaliesAction({
        rows: rowsPayload(preview),
        filename,
        clientId: clientId || undefined,
      });

      if (response.ok) downloadBase64(response.data, t('anomalies.filename'), XLSX_MIME);
      else toast.error(response.error);
    });
  };

  const apply = () => {
    if (!preview) return;

    startTransition(async () => {
      const response = await runParcelImportAction({
        rows: rowsPayload(preview),
        filename: filename || 'import.xlsx',
        clientId: clientId || undefined,
        mode,
      });

      if (response.ok) {
        setResult(response.data);
        toast.success(t('result.created', { count: response.data.created }));
        return;
      }

      toast.error(response.error);
    });
  };

  const restart = () => {
    setPreview(null);
    setResult(null);
    setFilename('');
    setMode('all-or-nothing');
    setOrder([]);
    setFilter('all');
    if (fileRef.current) fileRef.current.value = '';
  };

  /* ----------------------------------------------------------------- rendu */

  if (step === 'done' && result) {
    return (
      <Card className="flex flex-col items-center gap-4 p-8 text-center">
        <CheckCircle2 className="size-10 text-[var(--chart-4)]" aria-hidden />
        <div>
          <p className="text-lg font-semibold">{t('result.title')}</p>
          <p className="text-muted-foreground mt-1 text-sm">
            {t('result.created', { count: result.created })}
            {result.skipped > 0 ? ` ${t('result.skipped', { count: result.skipped })}` : ''}
            {(result.warnings ?? 0) > 0
              ? ` ${t('result.warnings', { count: result.warnings ?? 0 })}`
              : ''}
          </p>
        </div>
        <div className="flex flex-wrap justify-center gap-2">
          <Button asChild>
            <Link href="/parcels">{t('result.seeParcels')}</Link>
          </Button>
          <Button variant="outline" onClick={restart}>
            {t('result.again')}
          </Button>
        </div>
      </Card>
    );
  }

  if (step === 'upload') {
    return (
      <div className="flex flex-col gap-4">
        <Card className="flex flex-col gap-4 p-4 sm:p-6">
          <div className="flex flex-col gap-1.5">
            {/* E2E-012 (lot W3, D-238) : pas de libellé « E-commerçant » pour le vendeur. */}
            {lockedClientId ? null : <Label htmlFor="import-client">{t('client.label')}</Label>}
            {lockedClientId ? (
              <p className="text-muted-foreground text-sm">{t('client.locked')}</p>
            ) : (
              <SearchableSelect
                options={clients.map((client) => ({ value: client.id, label: client.name }))}
                value={clientId}
                onValueChange={setClientId}
                placeholder={t('client.placeholder')}
                id="import-client"
                className="sm:max-w-sm"
              />
            )}
          </div>

          <div className="border-border flex flex-col items-center gap-3 rounded-lg border border-dashed p-8 text-center">
            <FileSpreadsheet className="text-muted-foreground/60 size-8" aria-hidden />
            <input
              ref={fileRef}
              type="file"
              className="hidden"
              accept={`.xlsx,${XLSX_MIME}`}
              onChange={(event) => {
                const file = event.target.files?.[0];
                if (file) readFile(file);
              }}
            />
            <Button
              onClick={() => {
                if (!lockedClientId && clientId === '') {
                  toast.error(t('errors.noClient'));
                  return;
                }
                fileRef.current?.click();
              }}
              disabled={pending}
              className="gap-1.5"
            >
              {pending ? (
                <Loader2 className="size-4 animate-spin" aria-hidden />
              ) : (
                <Upload className="size-4" aria-hidden />
              )}
              {pending ? t('upload.reading') : t('upload.action')}
            </Button>
            <p className="text-muted-foreground max-w-md text-xs">{t('upload.hint')}</p>
          </div>

          <div className="flex flex-col gap-1.5 border-t pt-4">
            <Button
              variant="outline"
              size="sm"
              onClick={downloadTemplate}
              disabled={pending}
              className="gap-1.5 self-start"
            >
              <Download className="size-4" aria-hidden />
              {t('template.action')}
            </Button>
            <p className="text-muted-foreground text-xs">{t('template.hint')}</p>
          </div>
        </Card>
      </div>
    );
  }

  /* --- étape 2 : relecture ------------------------------------------------- */

  const clean = preview !== null && preview.invalid === 0;
  const applicable = preview !== null && preview.valid > 0 && (clean || mode === 'valid-only');

  return (
    <div className="flex flex-col gap-4">
      <DataTable
        data={shown}
        columns={columns}
        pageSize={25}
        // Lot J, J3 : une ligne = son numéro dans le fichier, et la page tient
        // à travers les corrections et les relectures.
        getRowId={(item) => String(item.row)}
        pageResetKey={filter}
        kpis={
          <KpiGrid>
            <KpiCard
              label={t('kpi.rows')}
              value={String(preview?.rows ?? 0)}
              icon={FileSpreadsheet}
            />
            <KpiCard
              label={t('kpi.valid')}
              value={String(preview?.valid ?? 0)}
              icon={CheckCircle2}
              tone="var(--chart-4)"
            />
            <KpiCard
              label={t('kpi.invalid')}
              value={String(preview?.invalid ?? 0)}
              icon={AlertTriangle}
              tone={preview && preview.invalid > 0 ? 'var(--destructive)' : undefined}
            />
            <KpiCard
              label={t('kpi.fees')}
              value={format.number((preview?.estimatedFees ?? 0) / 100, {
                style: 'currency',
                currency: 'MAD',
              })}
              icon={Download}
            />
          </KpiGrid>
        }
        toolbar={
          <Card className="flex flex-col gap-3 p-3 sm:p-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div className="min-w-0">
                <p className="text-sm font-medium">
                  {preview?.clientName}
                  <span className="text-muted-foreground font-normal">
                    {' · '}
                    {t('review.origin', { city: preview?.originCityName ?? '' })}
                  </span>
                </p>
                <p className="text-muted-foreground mt-0.5 text-xs">
                  {clean ? t('review.allGood') : t('review.hint')}
                </p>
              </div>

              <div className="flex flex-wrap items-center gap-2">
                <Button variant="ghost" size="sm" onClick={restart} disabled={pending}>
                  <ArrowLeft className="flip-rtl size-4" aria-hidden />
                  {t('review.back')}
                </Button>
                <Button variant="outline" size="sm" onClick={recheck} disabled={pending}>
                  {pending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
                  {t('review.recheck')}
                </Button>
                <Button
                  size="sm"
                  onClick={apply}
                  disabled={pending || !applicable}
                  className="gap-1.5"
                >
                  {pending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
                  {pending ? t('applying') : t('apply', { count: preview?.valid ?? 0 })}
                </Button>
              </div>
            </div>

            {/* Lot J, J4 : le bandeau des anomalies, le filtre, l'export. */}
            <div className="flex flex-col gap-2 border-t pt-3 sm:flex-row sm:items-center sm:justify-between">
              <div className="min-w-0">
                <p className="text-sm font-medium tabular-nums">{t('anomalies.summary', counts)}</p>
                <p className="text-muted-foreground mt-0.5 text-xs">{t('anomalies.hint')}</p>
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <div
                  role="group"
                  aria-label={t('anomalies.filter')}
                  className="flex items-center gap-1"
                >
                  {FILTERS.map((value) => (
                    <Button
                      key={value}
                      type="button"
                      size="sm"
                      variant={filter === value ? 'default' : 'outline'}
                      aria-pressed={filter === value}
                      onClick={() => setFilter(value)}
                    >
                      {t(`anomalies.${value}`)}
                    </Button>
                  ))}
                </div>
                {counts.errors + counts.warnings > 0 ? (
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={downloadAnomalies}
                    disabled={pending}
                    className="gap-1.5"
                  >
                    <Download className="size-4" aria-hidden />
                    {t('anomalies.download')}
                  </Button>
                ) : null}
              </div>
            </div>

            {/* Le choix n'apparaît QUE s'il y a des lignes en erreur : proposer
                « importer les lignes valides » sur un fichier propre n'a pas
                de sens, et banaliserait un mode qui doit rester demandé. */}
            {preview && preview.invalid > 0 ? (
              <div className="flex flex-col gap-1.5 border-t pt-3">
                <Label htmlFor="import-mode">{t('mode.label')}</Label>
                <Select value={mode} onValueChange={(value) => setMode(value as ParcelImportMode)}>
                  <SelectTrigger id="import-mode" className="sm:max-w-sm">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all-or-nothing">{t('mode.allOrNothing')}</SelectItem>
                    <SelectItem value="valid-only">{t('mode.validOnly')}</SelectItem>
                  </SelectContent>
                </Select>
                <p className="text-muted-foreground text-xs">{t('mode.hint')}</p>
              </div>
            ) : null}
          </Card>
        }
      />
    </div>
  );
}
