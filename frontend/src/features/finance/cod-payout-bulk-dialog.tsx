'use client';

import type { Agency, CodPayoutBulkCandidate, CodPayoutBulkResult } from '@delivery/shared';
import { Layers, Loader2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useCallback, useEffect, useState } from 'react';
import { toast } from 'sonner';
import { SearchableSelect } from '@/components/searchable-select';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Link } from '@/i18n/navigation';
import { codPayoutBulkCandidatesAction, createCodPayoutsBulkAction } from '@/lib/actions/finance';

/** Les motifs que le serveur peut rendre — liste fermée, traduite. */
const REASONS = [
  'client-unknown',
  'client-not-professional',
  'nothing-to-pay',
  'agency-out-of-scope',
  'error',
] as const;
type Reason = (typeof REASONS)[number];

/** L'agence vide de la liste déroulante (le composant n'accepte pas la chaîne vide). */
const ALL_AGENCIES = '__all__';

/**
 * BONS DE VERSEMENT EN MASSE — lot U (D-233), sur le modèle de la facturation
 * en masse.
 *
 * **La liste ne propose QUE ce qui a un reste à verser** : clients
 * professionnels, colis livrés, cash remis, dans aucun bon. Chaque ligne est
 * un client ET une agence (un bon chacun) et dit ce que son bon versera — le
 * même calcul que le bon unitaire : espèces seulement, chèques pour
 * information (D-229), aucun frais déduit (D-061).
 *
 * **Les refus sont affichés**, client par client, et le résumé remplace la
 * liste : l'opérateur ouvre ce qu'il vient de créer.
 */
export function CodPayoutBulkDialog({
  agencies,
  onDone,
}: {
  agencies: Agency[];
  onDone: () => void;
}) {
  const t = useTranslations('finance.codPayouts.bulk');
  const tReasons = useTranslations('finance.codPayouts.bulk.reasons');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const money = (cents: number) => format.number(cents / 100, 'currency');

  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [agencyId, setAgencyId] = useState(ALL_AGENCIES);
  const [rows, setRows] = useState<CodPayoutBulkCandidate[] | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);
  const [done, setDone] = useState<CodPayoutBulkResult | null>(null);

  const filters = useCallback(
    () => ({
      from: from || undefined,
      to: to || undefined,
      agencyId: agencyId === ALL_AGENCIES ? undefined : agencyId,
    }),
    [from, to, agencyId],
  );

  useEffect(() => {
    let live = true;
    void codPayoutBulkCandidatesAction(filters()).then((result) => {
      if (!live) return;
      if (result.ok) {
        setRows(result.data);
        setSelected([]);
      } else {
        toast.error(result.error);
        setRows([]);
      }
    });
    return () => {
      live = false;
    };
  }, [filters]);

  const list = rows ?? [];
  const clientIds = [...new Set(list.map((row) => row.clientId))];
  const chosen = list.filter((row) => selected.includes(row.clientId));
  const sum = (
    items: CodPayoutBulkCandidate[],
    key: 'cash' | 'chequeAmount' | 'total' | 'parcelCount',
  ) => items.reduce((total, row) => total + row[key], 0);
  const allChecked = clientIds.length > 0 && selected.length === clientIds.length;

  const toggle = (clientId: string, on: boolean) =>
    setSelected((current) =>
      on ? [...new Set([...current, clientId])] : current.filter((id) => id !== clientId),
    );

  const create = async () => {
    setSaving(true);
    const result = await createCodPayoutsBulkAction({ ...filters(), clientIds: selected });
    setSaving(false);

    if (!result.ok) {
      toast.error(result.error);
      return;
    }
    if (result.data.created.length > 0) {
      toast.success(t('created', { count: result.data.created.length }));
    }
    setDone(result.data);
  };

  const reason = (value: string) =>
    REASONS.includes(value as Reason) ? tReasons(value as Reason) : value;

  return (
    <Dialog open onOpenChange={(next) => (next ? null : onDone())}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>{t('title')}</DialogTitle>
          <DialogDescription>{t('hint')}</DialogDescription>
        </DialogHeader>

        {done ? (
          /* LE RÉSUMÉ : ce qui a été créé (liens), le total, et chaque refus nommé. */
          <div className="flex flex-col gap-3 text-sm">
            <p className="font-medium">
              {t('summary', { count: done.created.length, amount: money(done.total) })}
            </p>
            {done.created.length > 0 ? (
              <ul className="divide-border divide-y rounded-md border">
                {done.created.map((row) => (
                  <li
                    key={row.code}
                    className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2"
                  >
                    <Link
                      href={`/cod-payouts/${encodeURIComponent(row.code)}`}
                      className="font-mono underline-offset-4 hover:underline"
                    >
                      {row.code}
                    </Link>
                    <span className="min-w-0 flex-1 truncate">
                      {row.clientName} · {row.agencyName}
                    </span>
                    <span className="text-muted-foreground">
                      {t('parcels', { count: row.parcelCount })}
                    </span>
                    <span className="w-28 text-end font-medium tabular-nums">
                      {money(row.amount)}
                    </span>
                  </li>
                ))}
              </ul>
            ) : null}
            {done.skipped.length > 0 ? (
              <div className="flex flex-col gap-1">
                <p className="text-muted-foreground font-medium">{t('skippedTitle')}</p>
                <ul className="flex flex-col gap-1">
                  {done.skipped.map((row) => (
                    <li key={`${row.clientId}-${row.reason}`} className="text-muted-foreground">
                      {row.clientName} — {reason(row.reason)}
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
          </div>
        ) : (
          <div className="flex flex-col gap-3">
            {/* Filtres : période de LIVRAISON et agence. */}
            <div className="grid gap-2 sm:grid-cols-3">
              <Input
                type="date"
                value={from}
                aria-label={t('from')}
                onChange={(event) => setFrom(event.target.value)}
              />
              <Input
                type="date"
                value={to}
                aria-label={t('to')}
                onChange={(event) => setTo(event.target.value)}
              />
              <SearchableSelect
                options={[
                  { value: ALL_AGENCIES, label: t('allAgencies') },
                  ...agencies.map((agency) => ({ value: agency.id, label: agency.name })),
                ]}
                value={agencyId}
                onValueChange={setAgencyId}
                placeholder={t('agency')}
                className="w-full"
              />
            </div>

            {rows === null ? (
              <p className="text-muted-foreground flex items-center gap-2 py-6 text-sm">
                <Loader2 className="size-4 animate-spin" /> {t('loading')}
              </p>
            ) : list.length === 0 ? (
              <p className="text-muted-foreground py-6 text-center text-sm">{t('empty')}</p>
            ) : (
              <>
                <label className="flex items-center gap-2 text-sm">
                  <Checkbox
                    checked={allChecked}
                    onCheckedChange={(on) => setSelected(on === true ? clientIds : [])}
                  />
                  {t('allWithBalance', { count: String(clientIds.length) })}
                </label>

                <ul className="divide-border divide-y rounded-md border text-sm">
                  {list.map((row) => (
                    <li
                      key={`${row.clientId}:${row.agencyId}`}
                      className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2"
                    >
                      <Checkbox
                        checked={selected.includes(row.clientId)}
                        onCheckedChange={(on) => toggle(row.clientId, on === true)}
                        aria-label={row.clientName}
                      />
                      <span className="min-w-0 flex-1">
                        <span className="block truncate font-medium">{row.clientName}</span>
                        <span className="text-muted-foreground block truncate text-xs">
                          {row.agencyName} · {t('parcels', { count: row.parcelCount })}
                        </span>
                      </span>
                      <span className="text-muted-foreground text-xs">
                        {t('line', {
                          cash: money(row.cash),
                          cheques: String(row.chequeCount),
                          chequeAmount: money(row.chequeAmount),
                          fees: money(row.fees),
                        })}
                      </span>
                      <span className="w-28 text-end font-medium tabular-nums">
                        {money(row.total)}
                      </span>
                    </li>
                  ))}
                </ul>

                {/* LE RÉCAPITULATIF de la sélection, avant validation. */}
                <div className="bg-muted/50 flex flex-col gap-1 rounded-md px-3 py-2 text-sm">
                  <p>
                    {t('recap', {
                      clients: String(selected.length),
                      parcels: String(sum(chosen, 'parcelCount')),
                      cheques: String(chosen.reduce((total, row) => total + row.chequeCount, 0)),
                      chequeAmount: money(sum(chosen, 'chequeAmount')),
                    })}
                  </p>
                  <p className="font-semibold">
                    {t('total', { amount: money(sum(chosen, 'total')) })}
                  </p>
                  <p className="text-muted-foreground text-xs">{t('chequesNote')}</p>
                </div>
              </>
            )}
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onDone}>
            {done ? tCommon('close') : tCommon('cancel')}
          </Button>
          {done ? null : (
            <Button className="gap-1.5" disabled={saving || selected.length === 0} onClick={create}>
              {saving ? <Loader2 className="size-4 animate-spin" /> : <Layers className="size-4" />}
              {t('generate', { count: String(selected.length) })}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
