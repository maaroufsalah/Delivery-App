'use client';

import {
  type Courier,
  findParcelStatus,
  PROVIDER_STATUSES,
  type ProviderCallListItem,
  type ProviderTokenListItem,
  providerStatusTargets,
} from '@delivery/shared';
import {
  Check,
  Copy,
  KeyRound,
  Loader2,
  Plus,
  RotateCw,
  Save,
  ShieldOff,
  TriangleAlert,
} from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellMuted, CellStack } from '@/components/data-table/cells';
import { DataTable } from '@/components/data-table/data-table';
import {
  createDataTableColumnHelper,
  type DataTableColumns,
} from '@/components/data-table/table-features';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { SearchableSelect } from '@/components/searchable-select';
import {
  createProviderAction,
  resolveProviderAlertAction,
  revokeProviderAction,
  rotateProviderAction,
  setProviderModeAction,
  setProviderStatusMapAction,
} from '@/lib/actions/providers';

const helper = createDataTableColumnHelper<ProviderCallListItem>();

/** Les libellés des issues d'un appel, par clé typée du catalogue. */
const OUTCOME_KEYS = {
  APPLIED: 'outcomes.APPLIED',
  WOULD_APPLY: 'outcomes.WOULD_APPLY',
  ALREADY_APPLIED: 'outcomes.ALREADY_APPLIED',
  IGNORED: 'outcomes.IGNORED',
  STALE: 'outcomes.STALE',
  REJECTED: 'outcomes.REJECTED',
  WOULD_REJECT: 'outcomes.WOULD_REJECT',
} as const satisfies Record<ProviderCallListItem['outcome'], string>;

/** La valeur « ignoré » d'un sélecteur : une chaîne, le sélecteur n'accepte pas `null`. */
const IGNORED = 'ignored';

/**
 * PARAMÈTRES > PRESTATAIRES — chantier sync-wimo-speedy.
 *
 * Créer, faire tourner et révoquer un jeton ; basculer journal seul / actif ;
 * surcharger la table de correspondance ; lire les derniers appels. Le jeton
 * en clair s'affiche UNE fois, dans la boîte qui suit la création ou la
 * rotation : il n'existe nulle part ailleurs.
 */
export function ProvidersScreen({
  providers,
  couriers,
  calls,
  canWrite,
}: {
  providers: ProviderTokenListItem[];
  couriers: Courier[];
  calls: Record<string, ProviderCallListItem[]>;
  canWrite: boolean;
}) {
  const t = useTranslations('settings.providers');
  const [secret, setSecret] = useState<{ name: string; token: string } | null>(null);
  const openAlerts = providers.reduce((total, provider) => total + provider.openAlerts, 0);

  return (
    <section className="flex flex-col gap-4">
      <div>
        <h2 className="text-lg font-semibold tracking-tight">{t('title')}</h2>
        <p className="text-muted-foreground mt-1 text-sm">{t('subtitle')}</p>
      </div>

      {openAlerts > 0 ? (
        <div
          role="alert"
          className="border-destructive/40 bg-destructive/10 text-destructive flex items-start gap-2 rounded-lg border p-3 text-sm"
        >
          <TriangleAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
          <span>{t('alertsBanner', { count: openAlerts })}</span>
        </div>
      ) : null}

      {canWrite ? <CreateProvider couriers={couriers} onSecret={setSecret} /> : null}

      {providers.length === 0 ? (
        <p className="text-muted-foreground text-sm">{t('empty')}</p>
      ) : (
        providers.map((provider) => (
          <ProviderCard
            key={provider.id}
            provider={provider}
            calls={calls[provider.id] ?? []}
            canWrite={canWrite}
            onSecret={setSecret}
          />
        ))
      )}

      <SecretDialog secret={secret} onClose={() => setSecret(null)} />
    </section>
  );
}

function CreateProvider({
  couriers,
  onSecret,
}: {
  couriers: Courier[];
  onSecret: (secret: { name: string; token: string }) => void;
}) {
  const t = useTranslations('settings.providers');
  const [name, setName] = useState('');
  const [courierId, setCourierId] = useState('');
  const [pending, startTransition] = useTransition();

  const create = () => {
    startTransition(async () => {
      const result = await createProviderAction({ name, courierId });
      if (!result.ok) {
        toast.error(result.error);
        return;
      }
      setName('');
      setCourierId('');
      onSecret({ name: result.data.item.name, token: result.data.token });
    });
  };

  return (
    <Card>
      <CardContent className="grid gap-3 pt-6 md:grid-cols-[1fr_1fr_auto] md:items-end">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="provider-name">{t('name')}</Label>
          <Input
            id="provider-name"
            value={name}
            maxLength={80}
            onChange={(event) => setName(event.target.value)}
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label>{t('courier')}</Label>
          <SearchableSelect
            options={couriers.map((courier) => ({
              value: courier.id,
              label: `${courier.firstName} ${courier.lastName}`,
              keywords: [courier.phone],
            }))}
            value={courierId}
            onValueChange={setCourierId}
            placeholder={t('courierPlaceholder')}
          />
        </div>
        <Button onClick={create} disabled={pending || !name.trim() || !courierId}>
          {pending ? <Loader2 className="size-4 animate-spin" /> : <Plus className="size-4" />}
          {t('create')}
        </Button>
      </CardContent>
    </Card>
  );
}

function ProviderCard({
  provider,
  calls,
  canWrite,
  onSecret,
}: {
  provider: ProviderTokenListItem;
  calls: ProviderCallListItem[];
  canWrite: boolean;
  onSecret: (secret: { name: string; token: string }) => void;
}) {
  const t = useTranslations('settings.providers');
  const format = useFormatter();
  const [pending, startTransition] = useTransition();
  const revoked = provider.revokedAt !== null;
  const active = provider.mode === 'ACTIVE';

  const run = (work: () => Promise<{ ok: boolean; error?: string }>, done: string) => {
    startTransition(async () => {
      const result = await work();
      if (result.ok) toast.success(done);
      else toast.error(result.error);
    });
  };

  const rotate = () => {
    startTransition(async () => {
      const result = await rotateProviderAction(provider.id);
      if (!result.ok) {
        toast.error(result.error);
        return;
      }
      onSecret({ name: provider.name, token: result.data.token });
    });
  };

  const revoke = () => {
    if (!window.confirm(t('revokeConfirm', { name: provider.name }))) return;
    run(() => revokeProviderAction(provider.id), t('revoked'));
  };

  return (
    <Card>
      <CardContent className="flex flex-col gap-4 pt-6">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex flex-col gap-1">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-medium">{provider.name}</span>
              {revoked ? (
                <Badge variant="destructive">{t('stateRevoked')}</Badge>
              ) : (
                <Badge variant={active ? 'default' : 'secondary'}>
                  {active ? t('modeActive') : t('modeLogOnly')}
                </Badge>
              )}
            </div>
            <span className="text-muted-foreground text-sm">
              {t('linkedCourier', { courier: provider.courierName })}
            </span>
            <span className="text-muted-foreground font-mono text-xs">
              {t('prefix', { prefix: provider.tokenPrefix })}
              {' · '}
              {provider.lastUsedAt
                ? t('lastUsed', { at: format.dateTime(new Date(provider.lastUsedAt), 'stamp') })
                : t('neverUsed')}
              {provider.previousExpiresAt && !revoked
                ? ` · ${t('previousUntil', {
                    at: format.dateTime(new Date(provider.previousExpiresAt), 'stamp'),
                  })}`
                : ''}
            </span>
          </div>

          {canWrite && !revoked ? (
            <div className="flex flex-wrap items-center gap-3">
              <label className="flex items-center gap-2 text-sm">
                <Switch
                  checked={active}
                  disabled={pending}
                  onCheckedChange={(checked) =>
                    run(
                      () =>
                        setProviderModeAction(provider.id, {
                          mode: checked ? 'ACTIVE' : 'LOG_ONLY',
                        }),
                      checked ? t('nowActive') : t('nowLogOnly'),
                    )
                  }
                />
                {t('activeSwitch')}
              </label>
              <Button variant="outline" size="sm" onClick={rotate} disabled={pending}>
                <RotateCw className="size-4" />
                {t('rotate')}
              </Button>
              <Button variant="outline" size="sm" onClick={revoke} disabled={pending}>
                <ShieldOff className="size-4" />
                {t('revoke')}
              </Button>
            </div>
          ) : null}
        </div>

        <p className="text-muted-foreground text-sm">
          {active ? t('activeHint') : t('logOnlyHint')}
        </p>

        <AlertList providerId={provider.id} calls={calls} canWrite={canWrite} />

        <StatusMapEditor provider={provider} canWrite={canWrite && !revoked} />

        <div className="flex flex-col gap-2">
          <h3 className="text-sm font-medium">{t('callsTitle')}</h3>
          <CallsTable calls={calls} />
        </div>
      </CardContent>
    </Card>
  );
}

/**
 * LES CORRECTIONS REFUSÉES — décision de Salah du 21/11, point 3. Le
 * prestataire a corrigé un colis livré, et la correction n'a pas pu s'écrire
 * (caisse reçue, colis facturé…) : elle est à faire à la main, puis marquée
 * traitée. L'alerte reste au journal.
 */
function AlertList({
  providerId,
  calls,
  canWrite,
}: {
  providerId: string;
  calls: ProviderCallListItem[];
  canWrite: boolean;
}) {
  const t = useTranslations('settings.providers');
  const format = useFormatter();
  const [pending, startTransition] = useTransition();
  const open = calls.filter((call) => call.alert && call.alertResolvedAt === null);

  if (open.length === 0) return null;

  const resolve = (callId: string) => {
    startTransition(async () => {
      const result = await resolveProviderAlertAction(providerId, callId);
      if (result.ok) toast.success(t('alertResolved'));
      else toast.error(result.error);
    });
  };

  return (
    <div className="border-destructive/40 flex flex-col gap-2 rounded-lg border p-3">
      <h3 className="text-destructive flex items-center gap-2 text-sm font-medium">
        <TriangleAlert className="size-4" aria-hidden />
        {t('alertsTitle')}
      </h3>
      <p className="text-muted-foreground text-sm">{t('alertsHint')}</p>
      <ul className="flex flex-col gap-2">
        {open.map((call) => (
          <li
            key={call.id}
            className="flex flex-col gap-2 rounded-md border p-2 text-sm sm:flex-row sm:items-center sm:justify-between"
          >
            <span>
              <span className="font-mono">{call.parcelCode}</span>
              {' · '}
              {t('alertLine', {
                status: String(call.providerStatus),
                reason: call.reason ?? '—',
                at: format.dateTime(new Date(call.at), 'stamp'),
              })}
            </span>
            {canWrite ? (
              <Button
                variant="outline"
                size="sm"
                disabled={pending}
                onClick={() => resolve(call.id)}
              >
                <Check className="size-4" />
                {t('alertResolve')}
              </Button>
            ) : null}
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * La table de correspondance : une ligne par statut du prestataire, une cible
 * parmi les issues de terrain du livreur, ou « ignoré ». La liste des cibles
 * vient de `providerStatusTargets()` — l'API refuse toute autre valeur.
 */
function StatusMapEditor({
  provider,
  canWrite,
}: {
  provider: ProviderTokenListItem;
  canWrite: boolean;
}) {
  const t = useTranslations('settings.providers');
  const tStatus = useTranslations('status');
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState<Record<string, number | null>>(provider.statusMap);
  const [pending, startTransition] = useTransition();
  const targets = useMemo(() => providerStatusTargets(), []);

  const label = (id: number) => {
    const definition = findParcelStatus(id);
    return definition ? tStatus(definition.key) : String(id);
  };

  const save = () => {
    startTransition(async () => {
      const result = await setProviderStatusMapAction(provider.id, draft);
      if (result.ok) toast.success(t('mapSaved'));
      else toast.error(result.error);
    });
  };

  return (
    <div className="flex flex-col gap-2">
      <div>
        <Button variant="ghost" size="sm" onClick={() => setOpen((value) => !value)}>
          <KeyRound className="size-4" />
          {open ? t('mapHide') : t('mapShow')}
        </Button>
      </div>
      {open ? (
        <div className="flex flex-col gap-3">
          <p className="text-muted-foreground text-sm">{t('mapHint')}</p>
          <ul className="grid gap-2 md:grid-cols-2">
            {PROVIDER_STATUSES.map((status) => {
              const value = draft[String(status.id)] ?? null;
              return (
                <li
                  key={status.id}
                  className="flex flex-col gap-1.5 rounded-lg border p-2 sm:flex-row sm:items-center sm:justify-between"
                >
                  <span className="text-sm">
                    <span className="text-muted-foreground font-mono">{status.id}</span>{' '}
                    {status.label}
                  </span>
                  <SearchableSelect
                    className="sm:w-56"
                    options={targets.map((id) => ({ value: String(id), label: label(id) }))}
                    emptyOption={{ value: IGNORED, label: t('ignored') }}
                    value={value === null ? IGNORED : String(value)}
                    disabled={!canWrite}
                    onValueChange={(next) =>
                      setDraft((current) => ({
                        ...current,
                        [String(status.id)]: next === IGNORED ? null : Number(next),
                      }))
                    }
                  />
                </li>
              );
            })}
          </ul>
          {canWrite ? (
            <div>
              <Button onClick={save} disabled={pending}>
                {pending ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  <Save className="size-4" />
                )}
                {t('mapSave')}
              </Button>
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

function CallsTable({ calls }: { calls: ProviderCallListItem[] }) {
  const t = useTranslations('settings.providers');
  const tStatus = useTranslations('status');
  const format = useFormatter();

  const columns: DataTableColumns<ProviderCallListItem> = useMemo(
    () =>
      helper.columns([
        helper.accessor('at', {
          header: t('columns.at'),
          meta: { width: 'w-40' },
          cell: (info) => (
            <CellStack
              label={format.dateTime(new Date(info.getValue()), 'stamp')}
              sub={info.row.original.eventKey ?? undefined}
            />
          ),
        }),
        helper.accessor('parcelCode', {
          header: t('columns.parcel'),
          cell: (info) => <CellStack label={info.getValue()} mono />,
        }),
        helper.accessor('providerStatus', {
          header: t('columns.status'),
          cell: (info) => {
            const mapped = info.row.original.mappedStatus;
            const definition = mapped === null ? undefined : findParcelStatus(mapped);
            return (
              <CellStack
                label={`${info.getValue()} → ${definition ? tStatus(definition.key) : t('ignored')}`}
              />
            );
          },
        }),
        helper.accessor('outcome', {
          header: t('columns.outcome'),
          cell: (info) => (
            <CellStack
              label={
                info.row.original.alert
                  ? `${t(OUTCOME_KEYS[info.row.original.outcome])} · ${t('alertTag')}`
                  : t(OUTCOME_KEYS[info.row.original.outcome])
              }
              sub={
                info.row.original.reason
                  ? `${info.row.original.httpStatus} · ${info.row.original.reason}`
                  : String(info.row.original.httpStatus)
              }
            />
          ),
        }),
        helper.accessor('mode', {
          header: t('columns.mode'),
          meta: { hideBelow: 'md' },
          cell: (info) => (
            <CellMuted>
              {info.getValue() === 'ACTIVE' ? t('modeActive') : t('modeLogOnly')}
            </CellMuted>
          ),
        }),
      ]),
    [format, t, tStatus],
  );

  return (
    <DataTable
      data={calls}
      columns={columns}
      compact
      emptyTitle={t('callsEmpty')}
      emptyHint={t('callsEmptyHint')}
    />
  );
}

function SecretDialog({
  secret,
  onClose,
}: {
  secret: { name: string; token: string } | null;
  onClose: () => void;
}) {
  const t = useTranslations('settings.providers');

  const copy = async () => {
    if (!secret) return;
    try {
      await navigator.clipboard.writeText(secret.token);
      toast.success(t('copied'));
    } catch {
      toast.error(t('copyFailed'));
    }
  };

  return (
    <Dialog open={secret !== null} onOpenChange={(open) => (open ? undefined : onClose())}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('secretTitle', { name: secret?.name ?? '' })}</DialogTitle>
          <DialogDescription>{t('secretHint')}</DialogDescription>
        </DialogHeader>
        <code className="bg-muted rounded-md p-3 font-mono text-xs break-all" dir="ltr">
          {secret?.token}
        </code>
        <Button onClick={copy}>
          <Copy className="size-4" />
          {t('copy')}
        </Button>
      </DialogContent>
    </Dialog>
  );
}
