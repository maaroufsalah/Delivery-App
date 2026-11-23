'use client';

import {
  correctionStatusGroups,
  findParcelStatus,
  isCorrectionTarget,
  type StatusCorrectionPreview,
  type StatusCorrectionStep,
} from '@delivery/shared';
import { Loader2, Wrench } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useRef, useState } from 'react';
import { toast } from 'sonner';
import { SearchableSelect } from '@/components/searchable-select';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { ParcelStatusBadge } from '@/features/parcels/status-badge';
import { STATUSES_WITH_DATE } from '@/features/parcels/status-dialogs';
import { useRouter } from '@/i18n/navigation';
import {
  applyStatusCorrectionAction,
  previewStatusCorrectionAction,
} from '@/lib/actions/parcels';

/**
 * « CORRIGER LE STATUT » — D-250.
 *
 * Deux temps, toujours : l'APERÇU (ce que la correction va faire, lu dans le
 * plan du serveur), puis la CONFIRMATION. L'écran ne porte aucune règle : la
 * liste des statuts vient du référentiel partagé, le plan et les refus du
 * serveur, tels qu'ils reviennent. Le livreur et le circuit sont présélectionnés
 * par le serveur (les derniers du colis) ; l'admin peut en choisir d'autres.
 */
export function StatusCorrectionButton({ code, statusId }: { code: string; statusId: number }) {
  const t = useTranslations('parcels.correction');
  const [open, setOpen] = useState(false);

  return (
    <>
      <Button variant="outline" size="sm" className="gap-1.5" onClick={() => setOpen(true)}>
        <Wrench className="size-4" aria-hidden />
        <span>{t('action')}</span>
      </Button>
      {/* Monté à l'ouverture : chaque correction repart d'un formulaire vide. */}
      {open ? <StatusCorrectionDialog code={code} statusId={statusId} onOpenChange={setOpen} /> : null}
    </>
  );
}

/**
 * La fenêtre seule, montée par son appelant : le bouton ci-dessus, et
 * « Changer le statut » quand la machine à états ne suffit pas (lien sous la
 * liste, ou refus du serveur) — vérification du 14/11.
 */
export function StatusCorrectionDialog({
  code,
  statusId,
  onOpenChange,
}: {
  code: string;
  statusId: number;
  onOpenChange: (open: boolean) => void;
}) {
  const t = useTranslations('parcels.correction');
  const tStatus = useTranslations('status');
  const router = useRouter();

  const [target, setTarget] = useState('');
  const [reason, setReason] = useState('');
  const [scheduledDate, setScheduledDate] = useState('');
  const [courierId, setCourierId] = useState('');
  const [routeCode, setRouteCode] = useState('');
  const [preview, setPreview] = useState<StatusCorrectionPreview | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  const needsDate = STATUSES_WITH_DATE.includes(Number(target));
  const payload = {
    statusId: Number(target),
    // L'aperçu se calcule avant le motif : un motif provisoire, jamais écrit.
    reason: reason.trim() || '-',
    ...(courierId ? { courierId } : {}),
    ...(routeCode ? { routeCode } : {}),
    ...(needsDate && scheduledDate ? { scheduledDate } : {}),
  };

  /*
   * L'aperçu se recalcule à chaque choix qui change le plan (cible, livreur,
   * circuit), depuis le geste lui-même. Seule la DERNIÈRE réponse compte : un
   * choix rapide ne doit pas afficher le plan d'avant.
   */
  const request = useRef(0);
  const refresh = (next: { statusId: string; courierId: string; routeCode: string }) => {
    if (!next.statusId) return;
    const ticket = (request.current += 1);
    setLoading(true);
    void previewStatusCorrectionAction(code, {
      statusId: Number(next.statusId),
      reason: '-',
      ...(next.courierId ? { courierId: next.courierId } : {}),
      ...(next.routeCode ? { routeCode: next.routeCode } : {}),
    }).then((result) => {
      if (ticket !== request.current) return;
      setLoading(false);
      if (result.ok) {
        setPreview(result.data);
        setPreviewError(null);
      } else {
        setPreview(null);
        setPreviewError(result.error === 'unexpected' || result.error === 'invalid' ? t('failed') : result.error);
      }
    });
  };

  const options = correctionStatusGroups().flatMap(({ family, statusIds }) =>
    statusIds.map((id) => {
      const status = findParcelStatus(id);
      const label = status ? tStatus(status.key) : `#${id}`;
      return {
        value: String(id),
        label,
        color: status?.color,
        hint: t(`family.${family}`),
        keywords: [t(`family.${family}`)],
        disabled: !isCorrectionTarget(id),
      };
    }),
  );

  const submit = async () => {
    if (!preview) return;
    setSaving(true);
    const result = await applyStatusCorrectionAction(code, {
      ...payload,
      reason,
      fingerprint: preview.fingerprint,
    });
    setSaving(false);
    if (!result.ok) {
      toast.error(result.error === 'unexpected' || result.error === 'invalid' ? t('failed') : result.error);
      return;
    }
    toast.success(t('done'));
    router.refresh();
    onOpenChange(false);
  };

  const ready = preview !== null && !preview.needsCourier && reason.trim().length > 0 && !loading;

  return (
    <Dialog open onOpenChange={(next) => (saving ? undefined : onOpenChange(next))}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{t('title')}</DialogTitle>
          <DialogDescription>{t('description')}</DialogDescription>
        </DialogHeader>

        <div className="flex flex-wrap items-center gap-2 text-sm">
          <span className="bg-muted/60 rounded-md px-2 py-1 font-mono text-xs">{code}</span>
          <span className="text-muted-foreground">{t('current')}</span>
          <ParcelStatusBadge statusId={statusId} />
        </div>

        <div className="flex flex-col gap-2">
          <Label>{t('newStatus')}</Label>
          <SearchableSelect
            options={options}
            value={target}
            onValueChange={(value) => {
              setTarget(value);
              setCourierId('');
              setRouteCode('');
              refresh({ statusId: value, courierId: '', routeCode: '' });
            }}
            placeholder={t('pick')}
            searchPlaceholder={t('search')}
            className="w-full"
          />
          <p className="text-muted-foreground text-xs">{t('routeBornHint')}</p>
        </div>

        {preview && (preview.couriers.length > 0 || preview.needsCourier) ? (
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="flex flex-col gap-2">
              <Label>{t('courier')}</Label>
              <SearchableSelect
                options={preview.couriers.map((courier) => ({
                  value: courier.id,
                  label: courier.name,
                  hint: courier.coversCity ? t('coversCity') : null,
                }))}
                value={courierId || preview.courierId || ''}
                onValueChange={(value) => {
                  setCourierId(value);
                  setRouteCode('');
                  refresh({ statusId: target, courierId: value, routeCode: '' });
                }}
                placeholder={t('pickCourier')}
                className="w-full"
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label>{t('route')}</Label>
              <SearchableSelect
                options={preview.routes.map((route) => ({
                  value: route.code,
                  label: route.code,
                  hint: `${t(`routeStatus.${route.status}` as 'routeStatus.DRAFT')} · ${route.cityName}`,
                }))}
                value={routeCode || preview.routeCode || ''}
                onValueChange={(value) => {
                  setRouteCode(value);
                  refresh({ statusId: target, courierId: courierId || preview.courierId || '', routeCode: value });
                }}
                placeholder={preview.routeCode ? preview.routeCode : t('newRoute')}
                disabled={preview.needsCourier}
                className="w-full"
              />
            </div>
          </div>
        ) : null}

        {needsDate ? (
          <div className="flex flex-col gap-2">
            <Label htmlFor="correction-date">{t('scheduledDate')}</Label>
            <Input
              id="correction-date"
              type="date"
              value={scheduledDate}
              onChange={(event) => setScheduledDate(event.target.value)}
            />
          </div>
        ) : null}

        <div className="flex flex-col gap-2">
          <Label htmlFor="correction-reason">{t('reason')}</Label>
          <Textarea
            id="correction-reason"
            rows={2}
            maxLength={500}
            value={reason}
            onChange={(event) => setReason(event.target.value)}
          />
          <p className="text-muted-foreground text-xs">{t('reasonHint')}</p>
        </div>

        {target ? (
          <div className="flex flex-col gap-2" aria-live="polite">
            <p className="text-sm font-medium">{t('previewTitle')}</p>
            {loading ? (
              <p className="text-muted-foreground flex items-center gap-2 text-sm">
                <Loader2 className="size-4 animate-spin" aria-hidden />
                {t('previewLoading')}
              </p>
            ) : previewError ? (
              <p className="text-destructive text-sm" role="alert">
                {previewError}
              </p>
            ) : preview?.needsCourier ? (
              <p className="text-sm" role="alert">
                {t('needsCourier')}
              </p>
            ) : preview ? (
              <ol className="bg-muted/40 list-decimal space-y-1 rounded-md border py-2 ps-8 pe-3 text-sm">
                {preview.steps.map((step, index) => (
                  <li key={index}>
                    <StepText step={step} />
                  </li>
                ))}
              </ol>
            ) : null}
          </div>
        ) : null}

        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="ghost" onClick={() => onOpenChange(false)} disabled={saving}>
            {t('cancel')}
          </Button>
          <Button onClick={submit} disabled={saving || !ready} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : null}
            {t('confirm')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Une ligne de l'aperçu, traduite. */
function StepText({ step }: { step: StatusCorrectionStep }) {
  const t = useTranslations('parcels.correction.step');
  const tStatus = useTranslations('status');
  const format = useFormatter();
  const status = (id: number) => {
    const found = findParcelStatus(id);
    return found ? tStatus(found.key) : `#${id}`;
  };
  const formatMoney = (cents: number) => format.number(cents / 100, 'currency');
  const money = (cents: number | null) => (cents === null ? t('none') : formatMoney(cents));

  switch (step.kind) {
    case 'cancel-delivery':
      return (
        <>
          {t('cancelDelivery')}
          {step.remittanceCode ? ` ${t('cancelRemittance', { code: step.remittanceCode })}` : ''}
          {step.routeReopened && step.routeCode ? ` ${t('cancelReopen', { code: step.routeCode })}` : ''}
        </>
      );
    case 'leave-note':
      return <>{t('leaveNote', { code: step.code })}</>;
    case 'leave-route':
      return <>{t('leaveRoute', { code: step.code })}</>;
    case 'leave-handover':
      return <>{t('leaveHandover', { code: step.code })}</>;
    case 'release-handover':
      return <>{t('releaseHandover', { code: step.code })}</>;
    case 'mark-route':
      return <>{t('markRoute', { code: step.code })}</>;
    case 'mark-note':
      return <>{t('markNote', { code: step.code })}</>;
    case 'reuse-line':
      return <>{t('reuseLine', { code: step.routeCode, courier: step.courierName })}</>;
    case 'place-on-route':
      return <>{t('placeOnRoute', { code: step.routeCode, courier: step.courierName })}</>;
    case 'open-route':
      return <>{t('openRoute', { courier: step.courierName })}</>;
    case 'set-status':
      return <>{t('setStatus', { from: status(step.from), to: status(step.to) })}</>;
    case 'confirm-outcome':
      return <>{t('confirmOutcome', { code: step.routeCode, status: status(step.statusId) })}</>;
    case 'deliver':
      return <>{t('deliver', { code: step.routeCode, amount: formatMoney(step.amount) })}</>;
    case 'failure-flag':
      return <>{step.after ? t('failureOn') : t('failureOff')}</>;
    case 'failure-fee':
      return <>{t('failureFee', { before: money(step.before), after: money(step.after) })}</>;
    case 'leave-invoice-draft':
      return <>{t('leaveInvoice', { code: step.code })}</>;
    case 'courier-kept':
      return <>{t('courierKept', { courier: step.courierName })}</>;
  }
}
