'use client';

import {
  CHEQUE_STATUS_COLORS,
  type RemittanceCandidate,
  type RemittanceDetail as Remittance,
} from '@delivery/shared';
import {
  Banknote,
  CheckCircle2,
  FileDown,
  Loader2,
  LockOpen,
  Plus,
  Scale,
  Trash2,
} from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge } from '@/components/data-table/cells';
import { ExportExcelButton } from '@/components/export-excel-button';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { remittanceExportAction } from '@/lib/actions/documents';
import { downloadBase64 } from '@/lib/download';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ParcelPicker } from '@/features/finance/parcel-picker';
import { RemittanceStageBadge } from '@/features/finance/remittance-stage';
import { useFinanceError } from '@/features/finance/use-finance-error';
import {
  addRemittanceParcelsAction,
  confirmRemittanceAction,
  countRemittanceAction,
  downloadRemittancePdfAction,
  removeRemittanceParcelAction,
  reopenRemittanceAction,
} from '@/lib/actions/finance';

/**
 * Fiche d'un retour de fonds — l'écran du comptoir.
 *
 * On compose l'enveloppe colis par colis, la machine calcule ce qui est attendu,
 * on compte l'argent posé sur la table, puis on confirme. La confirmation est le
 * geste qui atteste la RÉCEPTION : elle demande donc un accord explicite et
 * annonce son montant, parce qu'une fois donnée elle ne se reprend pas.
 *
 * C11 bloc 2 : les trois attendus ne se saisissent PAS. Le seul champ de
 * l'écran est le montant remis, et tout écart avec l'attendu exige un motif —
 * une caisse qui ne tombe pas juste et que personne n'explique est exactement
 * le problème que ce module est venu résoudre.
 *
 * D-243 : une caisse FERMÉE par le livreur reste un brouillon. Tout ce que
 * l'agence faisait sur une caisse ouverte (ajouter ou retirer une ligne,
 * compter, confirmer) reste offert ; s'y ajoute « Rouvrir », gardé par la même
 * clé que la confirmation. Le statut affiché est `stage`, servi par l'API.
 */
export function RemittanceDetailScreen({
  remittance,
  candidates,
  canWrite,
  canConfirm,
}: {
  remittance: Remittance;
  candidates: RemittanceCandidate[];
  canWrite: boolean;
  canConfirm: boolean;
}) {
  const t = useTranslations('finance.remittances');
  const tCommon = useTranslations('common');
  const tCheque = useTranslations('finance.cheques.status');
  // Le libellé « Exporter Excel » des fiches, le même que sur le circuit.
  const tExport = useTranslations('routes.detail');
  const format = useFormatter();
  const errorMessage = useFinanceError();

  const [current, setCurrent] = useState(remittance);
  const [picking, setPicking] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [reopening, setReopening] = useState(false);
  const [pending, startTransition] = useTransition();

  // Le comptage se saisit en DIRHAMS et voyage en centimes, comme partout à la
  // frontière. La chaîne vide est un état légitime : « pas encore compté ».
  const [declared, setDeclared] = useState(
    current.declaredAmount === null ? '' : String(current.declaredAmount / 100),
  );
  const [reason, setReason] = useState(current.varianceReason ?? '');

  const draft = current.status === 'draft';
  const money = (cents: number) => format.number(cents / 100, 'currency');
  const deducts = current.settlementMode === 'at-remittance';

  /*
   * L'écart se recalcule À LA SAISIE, sans attendre l'aller-retour serveur :
   * l'opérateur doit voir tout de suite que sa caisse ne tombe pas juste, pas
   * après avoir enregistré. Le serveur reste seul juge — il revérifie la règle
   * du motif, parce qu'un appel direct ne passe pas par cet écran.
   */
  const typed = declared.trim() === '' ? null : Math.round(Number(declared) * 100);
  const liveVariance = typed === null || Number.isNaN(typed) ? 0 : typed - current.expectedNet;
  const needsReason = liveVariance !== 0 && reason.trim() === '';

  const addParcels = (parcelIds: string[]) => {
    startTransition(async () => {
      const result = await addRemittanceParcelsAction(current.code, { parcelIds });

      if (result.ok) {
        setCurrent(result.data);
        setPicking(false);
        toast.success(t('parcelsAdded', { count: parcelIds.length }));
        return;
      }

      toast.error(errorMessage(result));
    });
  };

  const removeParcel = (parcelId: string, parcelCode: string) => {
    startTransition(async () => {
      const result = await removeRemittanceParcelAction(current.code, parcelId);
      if (result.ok) {
        setCurrent(result.data);
        toast.success(t('parcelRemoved', { code: parcelCode }));
      } else toast.error(errorMessage(result));
    });
  };

  const count = () => {
    startTransition(async () => {
      const result = await countRemittanceAction(current.code, {
        declaredAmount: typed === null || Number.isNaN(typed) ? 0 : typed / 100,
        varianceReason: reason,
        expectedNet: current.expectedNet / 100,
      });

      if (result.ok) {
        setCurrent(result.data);
        toast.success(t('counted'));
      } else toast.error(errorMessage(result));
    });
  };

  const downloadPdf = () => {
    startTransition(async () => {
      const result = await downloadRemittancePdfAction(current.code);
      if (result.ok) downloadBase64(result.data, `${current.code}.pdf`, 'application/pdf');
      else toast.error(errorMessage(result));
    });
  };

  const confirm = () => {
    startTransition(async () => {
      const result = await confirmRemittanceAction(current.code);
      setConfirming(false);

      if (result.ok) {
        setCurrent(result.data);
        toast.success(t('confirmed', { code: current.code }));
      } else toast.error(errorMessage(result));
    });
  };

  const reopen = () => {
    startTransition(async () => {
      const result = await reopenRemittanceAction(current.code);
      setReopening(false);

      if (result.ok) {
        setCurrent(result.data);
        toast.success(t('reopened', { code: current.code }));
        return;
      }

      /*
       * Le livreur a livré entre l'affichage et le clic : une nouvelle caisse
       * s'est ouverte. Le bouton se grise tout de suite, avec son explication,
       * plutôt que de laisser l'agence recliquer sur un refus certain.
       */
      if (result.reason === 'courier-has-newer-remittance') {
        setCurrent((previous) => ({ ...previous, reopenable: false }));
      }

      toast.error(errorMessage(result));
    });
  };

  const closedByCourier = current.stage === 'closed';
  const closedAt = current.courierClosedAt
    ? format.dateTime(new Date(current.courierClosedAt), 'stamp')
    : null;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <RemittanceStageBadge
          stage={current.stage}
          icon={
            current.stage === 'received' || current.stage === 'validated' ? CheckCircle2 : undefined
          }
        />

        <span className="text-muted-foreground text-sm">
          {current.courierName} · {current.cityName} · {current.agencyName}
        </span>

        <span className="ms-auto flex flex-wrap gap-2">
          {canWrite && draft ? (
            <Button
              size="sm"
              variant="outline"
              className="gap-1.5"
              onClick={() => setPicking(true)}
            >
              <Plus className="size-4" />
              {t('addParcels')}
            </Button>
          ) : null}

          <Button
            size="sm"
            variant="outline"
            className="gap-1.5"
            disabled={pending}
            onClick={downloadPdf}
          >
            <FileDown className="size-4" />
            {t('pdf')}
          </Button>

          {/* Une ligne par colis ; pour tout compte qui lit la caisse, livreur compris. */}
          <ExportExcelButton
            label={tExport('exportExcel')}
            filename={current.code}
            disabled={pending || current.items.length === 0}
            onExport={() => remittanceExportAction(current.code)}
          />

          {/*
            « Rouvrir » — D-243. La même clé que la confirmation : c'est le
            geste de la personne qui réceptionne l'argent. Quand le livreur a
            déjà une nouvelle caisse ouverte, le bouton reste VISIBLE mais
            grisé, et la raison est écrite juste en dessous : le voir grisé dit
            la règle mieux que son absence.
          */}
          {canConfirm && closedByCourier ? (
            <Button
              size="sm"
              variant="outline"
              className="gap-1.5"
              disabled={pending || !current.reopenable}
              onClick={() => setReopening(true)}
            >
              <LockOpen className="size-4" />
              {t('reopen')}
            </Button>
          ) : null}

          {canConfirm && draft ? (
            <Button
              size="sm"
              className="gap-1.5"
              disabled={!current.confirmable}
              onClick={() => setConfirming(true)}
            >
              <CheckCircle2 className="size-4" />
              {t('confirm')}
            </Button>
          ) : null}
        </span>
      </div>

      {/* Qui a fermé la caisse, et quand. La date reste lisible après la
          réception : elle dit depuis quand l'argent attendait. */}
      {closedAt ? (
        <p className="text-muted-foreground text-sm">
          {current.courierClosedByName
            ? t('closedByCourier', { date: closedAt, name: current.courierClosedByName })
            : t('closedByCourierNoName', { date: closedAt })}
        </p>
      ) : null}

      {canConfirm && closedByCourier && !current.reopenable ? (
        <p className="border-border bg-muted/40 rounded-lg border p-3 text-sm">
          {t('reopenBlocked')}
        </p>
      ) : null}

      <Card>
        <CardHeader className="flex-row items-center justify-between gap-3">
          <div>
            <CardTitle>{t('parcels')}</CardTitle>
            <CardDescription>
              {draft ? t('parcelsDraftHint') : t('parcelsFrozenHint')}
            </CardDescription>
          </div>
          <div className="text-end">
            <p className="text-muted-foreground text-xs">{t('expectedNet')}</p>
            <p className="text-2xl font-semibold tabular-nums">{money(current.expectedNet)}</p>
          </div>
        </CardHeader>
        <CardContent>
          {current.items.length === 0 ? (
            <p className="text-muted-foreground py-6 text-center text-sm">{t('noParcel')}</p>
          ) : (
            <ul className="divide-border divide-y">
              {current.items.map((item) => (
                <li key={item.parcelId} className="flex items-center gap-3 py-2.5">
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-mono text-sm">{item.parcelCode}</span>
                    <span className="text-muted-foreground block truncate text-xs">
                      {item.recipientName} · {item.clientName}
                    </span>
                  </span>

                  <span className="shrink-0 text-end text-sm tabular-nums">
                    <span className="block font-medium">{money(item.amount)}</span>
                    {deducts && item.commission > 0 ? (
                      <span className="text-muted-foreground block text-xs">
                        − {money(item.commission)}
                      </span>
                    ) : null}
                  </span>

                  {canWrite && draft ? (
                    <Button
                      size="icon"
                      variant="ghost"
                      className="text-destructive shrink-0"
                      disabled={pending}
                      aria-label={tCommon('delete')}
                      onClick={() => removeParcel(item.parcelId, item.parcelCode)}
                    >
                      <Trash2 className="size-4" />
                    </Button>
                  ) : null}
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      {/*
        Le calcul et le comptage — C11 bloc 2.

        Les trois premières lignes sont CALCULÉES et ne se touchent pas ; la
        quatrième est le seul champ saisi de tout le module. Les afficher
        ensemble est le point : un opérateur doit voir d'où sort le net attendu
        avant de compter les billets, sinon il compare un total à rien.
      */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Scale className="size-4" />
            {t('calculation')}
          </CardTitle>
          <CardDescription>
            {deducts ? t('calculationHintDeducted') : t('calculationHintMonthly')}
          </CardDescription>
        </CardHeader>

        <CardContent className="flex flex-col gap-4">
          <dl className="flex flex-col gap-2 text-sm">
            <div className="flex items-baseline justify-between gap-3">
              <dt className="text-muted-foreground">{t('expectedCod')}</dt>
              <dd className="tabular-nums">{money(current.expectedCod)}</dd>
            </div>

            {/*
              LE CHÈQUE SORT D'ABORD — chantier « Chèques », point 1.

              La ligne n'apparaît que s'il y a un papier : sur l'immense
              majorité des bons, tout est en billets, et une ligne « − 0,00 »
              n'aurait fait qu'épaissir le calcul qu'on vient vérifier.
            */}
            {current.totalCheque > 0 ? (
              <>
                <div className="flex items-baseline justify-between gap-3">
                  <dt className="text-muted-foreground">
                    {t('chequesDeducted', { count: String(current.chequeCount) })}
                  </dt>
                  <dd className="tabular-nums">{`− ${money(current.totalCheque)}`}</dd>
                </div>

                <div className="flex items-baseline justify-between gap-3">
                  <dt className="text-muted-foreground">{t('cashCollected')}</dt>
                  <dd className="tabular-nums">{money(current.totalCash)}</dd>
                </div>
              </>
            ) : null}

            <div className="flex items-baseline justify-between gap-3">
              <dt className="text-muted-foreground">
                {deducts ? t('expectedCommission') : t('commissionOnStatement')}
              </dt>
              <dd className="tabular-nums">
                {deducts ? `− ${money(current.expectedCommission)}` : '—'}
              </dd>
            </div>

            <div className="border-border flex items-baseline justify-between gap-3 border-t pt-2">
              <dt className="font-medium">{t('expectedNet')}</dt>
              <dd className="text-base font-semibold tabular-nums">{money(current.expectedNet)}</dd>
            </div>
          </dl>

          {/*
            LES PAPIERS, À CÔTÉ DES BILLETS — point 1.

            Ils ne s'additionnent PAS au net : le « montant remis » que
            l'agence saisit plus bas ne concerne que les espèces. Un chèque se
            reçoit en le prenant en main, un par un, et son état le dit.
          */}
          {current.cheques.length > 0 ? (
            <div className="border-border flex flex-col gap-2 rounded-lg border p-3">
              <div className="flex items-baseline justify-between gap-3">
                <p className="text-sm font-medium">
                  {t('chequesToHandOver', { count: String(current.cheques.length) })}
                </p>
                <p className="text-sm font-semibold tabular-nums">{money(current.totalCheque)}</p>
              </div>

              <ul className="divide-border divide-y">
                {current.cheques.map((cheque) => (
                  <li key={cheque.id} className="flex items-center gap-3 py-2">
                    <span className="min-w-0 flex-1">
                      <span className="block truncate font-mono text-sm">{cheque.number}</span>
                      <span className="text-muted-foreground block truncate text-xs">
                        {cheque.bank ?? t('noBank')} · {cheque.parcelCode}
                      </span>
                    </span>
                    <CellBadge
                      label={tCheque(cheque.status)}
                      color={CHEQUE_STATUS_COLORS[cheque.status]}
                    />
                    <span className="shrink-0 text-sm font-medium tabular-nums">
                      {money(cheque.amount)}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}

          {canWrite && draft ? (
            <div className="flex flex-col gap-3">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="declaredAmount">{t('declaredAmount')}</Label>
                <Input
                  id="declaredAmount"
                  type="number"
                  min={0}
                  step="0.01"
                  inputMode="decimal"
                  value={declared}
                  onChange={(event) => setDeclared(event.target.value)}
                />
              </div>

              {liveVariance === 0 ? null : (
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="varianceReason">
                    {t('variance', { amount: money(liveVariance) })}
                  </Label>
                  <Textarea
                    id="varianceReason"
                    rows={2}
                    value={reason}
                    onChange={(event) => setReason(event.target.value)}
                    placeholder={t('varianceReasonPlaceholder')}
                  />
                  <p className="text-muted-foreground text-xs">{t('varianceReasonHint')}</p>
                </div>
              )}

              <Button
                size="sm"
                variant="outline"
                className="self-start"
                disabled={pending || needsReason || declared.trim() === ''}
                onClick={count}
              >
                {pending ? <Loader2 className="size-4 animate-spin" /> : null}
                {t('saveCount')}
              </Button>
            </div>
          ) : current.declaredAmount === null ? null : (
            <dl className="flex flex-col gap-2 text-sm">
              <div className="flex items-baseline justify-between gap-3">
                <dt className="text-muted-foreground">{t('declaredAmount')}</dt>
                <dd className="tabular-nums">{money(current.declaredAmount)}</dd>
              </div>

              {current.variance === 0 ? null : (
                <div className="flex items-baseline justify-between gap-3">
                  <dt className="font-medium">{t('varianceShort')}</dt>
                  <dd className="font-semibold tabular-nums">{money(current.variance)}</dd>
                </div>
              )}

              {current.varianceReason ? (
                <p className="text-muted-foreground text-xs">{current.varianceReason}</p>
              ) : null}
            </dl>
          )}
        </CardContent>
      </Card>

      {current.note ? <p className="text-muted-foreground text-sm">{current.note}</p> : null}

      <ParcelPicker
        open={picking}
        onOpenChange={setPicking}
        title={t('addParcels')}
        description={t('candidatesHint')}
        pending={pending}
        onConfirm={addParcels}
        parcels={candidates.map((parcel) => ({
          id: parcel.id,
          code: parcel.code,
          recipientName: parcel.recipientName,
          context: deducts
            ? t('candidateWithCommission', {
                city: parcel.destinationCityName,
                client: parcel.clientName,
                commission: money(parcel.courierCommission),
              })
            : `${parcel.destinationCityName} · ${parcel.clientName}`,
          amount: parcel.codCollected,
        }))}
      />

      <Dialog open={confirming} onOpenChange={setConfirming}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('confirm')}</DialogTitle>
            <DialogDescription>
              {t('confirmWarning', {
                count: String(current.items.length),
                amount: money(current.declaredAmount ?? current.expectedNet),
                courier: current.courierName,
              })}
            </DialogDescription>
          </DialogHeader>

          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirming(false)}>
              {tCommon('cancel')}
            </Button>
            <Button disabled={pending} onClick={confirm} className="gap-1.5">
              {pending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <Banknote className="size-4" />
              )}
              {t('confirm')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={reopening} onOpenChange={setReopening}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('reopenTitle')}</DialogTitle>
            <DialogDescription>
              {t('reopenWarning', { courier: current.courierName })}
            </DialogDescription>
          </DialogHeader>

          <DialogFooter>
            <Button variant="outline" onClick={() => setReopening(false)}>
              {tCommon('cancel')}
            </Button>
            <Button disabled={pending} onClick={reopen} className="gap-1.5">
              {pending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <LockOpen className="size-4" />
              )}
              {t('reopen')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
