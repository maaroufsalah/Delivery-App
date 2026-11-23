'use client';

import type { Paginated, RemittanceDetail, RemittanceListItem } from '@delivery/shared';
import { Download, HandCoins, Loader2, Lock, Package, Wallet } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { ExportExcelButton } from '@/components/export-excel-button';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Separator } from '@/components/ui/separator';
import { RemittanceStageBadge } from '@/features/finance/remittance-stage';
import { useFinanceError } from '@/features/finance/use-finance-error';
import { closeCourierRemittanceAction } from '@/lib/actions/courier';
import { exportListAction } from '@/lib/actions/documents';
import { downloadRemittancePdfAction } from '@/lib/actions/finance';
import { downloadBase64 } from '@/lib/download';

/**
 * MES RETOURS DE FONDS — chantier « Retour de fonds unique », point 7.
 *
 * Deux questions, dans cet ordre, et c'est l'ordre du terrain :
 *
 *  1. **« Qu'est-ce que je porte ? »** — le bon OUVERT, en tête, avec ses
 *     colis, l'encaissé, la commission et ce qu'il devra poser sur la table.
 *     C'est le chiffre qu'il vérifie avant de passer à l'agence, et il ne
 *     l'avait nulle part : la caisse du jour ne lui était pas montrée.
 *  2. **« Qu'est-ce que je dois encore remettre ? »** — ses caisses FERMÉES,
 *     que l'agence n'a pas encore réceptionnées (D-243).
 *  3. **« Est-ce que c'est passé ? »** — l'historique de ses caisses reçues,
 *     avec le reçu à télécharger. Un livreur qui conteste six semaines plus
 *     tard a besoin du papier, pas d'une ligne de liste.
 *
 * **Il clôture, il n'atteste pas (D-243, qui amende D-102 et D-117).**
 * « Clôturer ma caisse » ferme l'enveloppe du jour : sa prochaine livraison en
 * ouvre une nouvelle. Compter l'argent et confirmer la réception restent des
 * gestes d'agence : celui qui rapporte l'argent n'atteste pas lui-même du
 * montant rapporté, et l'API le refuse quelle que soit la matrice de rôles.
 *
 * Les statuts affichés viennent de `stage`, servi par l'API ; l'écran n'en
 * déduit aucun. Après une clôture, la Server Action invalide la page : la
 * caisse quitte le premier bloc pour le second sans état local à tenir.
 */
export function CourierRemittances({
  open,
  closed,
  history,
}: {
  open: RemittanceDetail | null;
  closed: Paginated<RemittanceListItem>;
  history: Paginated<RemittanceListItem>;
}) {
  const t = useTranslations('courier.remittances');
  const tCommon = useTranslations('common');
  // Le libellé des autres boutons « Exporter Excel », déjà traduit dans les trois langues.
  const tExport = useTranslations('routes.detail');
  const format = useFormatter();
  const errorMessage = useFinanceError();
  const [pending, startTransition] = useTransition();
  const [closing, setClosing] = useState(false);

  const money = (cents: number) => format.number(cents / 100, 'currency');

  const close = () => {
    startTransition(async () => {
      const result = await closeCourierRemittanceAction();
      setClosing(false);

      // Caisse vide, déjà clôturée, plus de caisse ouverte : le serveur donne
      // le motif, l'écran le dit dans la langue du livreur.
      if (result.ok) toast.success(t('open.closed'));
      else toast.error(errorMessage(result));
    });
  };

  const download = (code: string) => {
    startTransition(async () => {
      const result = await downloadRemittancePdfAction(code);
      if (result.ok) downloadBase64(result.data, `${code}.pdf`, 'application/pdf');
      else toast.error(errorMessage(result));
    });
  };

  return (
    <div className="flex flex-col gap-6">
      <section className="flex flex-col gap-3">
        <h1 className="text-lg font-semibold tracking-tight">{t('open.title')}</h1>

        {open === null ? (
          <Card>
            <CardContent className="text-muted-foreground p-6 text-center text-sm">
              {t('open.empty')}
            </CardContent>
          </Card>
        ) : (
          <Card>
            <CardContent className="flex flex-col gap-4 p-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span className="font-mono text-sm font-medium">{open.code}</span>
                <RemittanceStageBadge stage={open.stage} />
              </div>

              {/* Les trois chiffres qu'il vient chercher, et rien d'autre. Ils
                  arrivent calculés du serveur : l'écran n'en dérive aucun. */}
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                <Figure
                  icon={Wallet}
                  label={t('open.collected')}
                  value={money(open.expectedCod)}
                />
                <Figure
                  icon={Package}
                  label={t('open.commission')}
                  value={money(open.expectedCommission)}
                />
                <Figure
                  icon={HandCoins}
                  label={t('open.toHandOver')}
                  value={money(open.expectedNet)}
                  strong
                />
              </div>

              {/*
                LES PAPIERS, À PART — chantier « Chèques », point 1. Le livreur
                doit savoir ce qu'il pose en billets ET ce qu'il tend en
                chèques : les additionner lui ferait croire qu'il doit sortir
                de sa poche l'argent du vendeur.
              */}
              {open.cheques.length > 0 ? (
                <div className="bg-muted/40 flex flex-col gap-2 rounded-lg p-3">
                  <div className="flex items-baseline justify-between gap-3">
                    <span className="text-sm font-medium">
                      {t('open.cheques', { count: String(open.cheques.length) })}
                    </span>
                    <span className="text-sm font-semibold tabular-nums">
                      {money(open.totalCheque)}
                    </span>
                  </div>

                  <ul className="flex flex-col gap-1">
                    {open.cheques.map((cheque) => (
                      <li key={cheque.id} className="flex items-center justify-between gap-3">
                        <span className="truncate font-mono text-xs">{cheque.number}</span>
                        <span className="shrink-0 text-xs tabular-nums">
                          {money(cheque.amount)}
                        </span>
                      </li>
                    ))}
                  </ul>
                </div>
              ) : null}

              <Separator />

              <div className="flex flex-col gap-2">
                <p className="text-muted-foreground text-xs">
                  {t('parcels', { count: String(open.parcelCount) })}
                </p>

                <ul className="divide-border divide-y">
                  {open.items.map((item) => (
                    <li
                      key={item.parcelId}
                      className="flex items-center justify-between gap-3 py-2"
                    >
                      <span className="min-w-0 flex-1">
                        <span className="block truncate font-mono text-xs">{item.parcelCode}</span>
                        <span className="text-muted-foreground block truncate text-xs">
                          {item.recipientName}
                        </span>
                      </span>
                      <span className="shrink-0 text-sm font-medium tabular-nums">
                        {money(item.amount)}
                      </span>
                    </li>
                  ))}
                </ul>
              </div>

              {/* Dit à l'écran plutôt que laissé à deviner : il ferme sa
                  caisse, l'agence compte et confirme. Le bouton prend toute la
                  largeur sur un téléphone : c'est le geste de fin de journée. */}
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <p className="text-muted-foreground text-xs">{t('open.courierCloses')}</p>
                <Button
                  className="shrink-0 gap-1.5"
                  disabled={pending}
                  onClick={() => setClosing(true)}
                >
                  <Lock className="size-4" aria-hidden />
                  {t('open.close')}
                </Button>
              </div>
            </CardContent>
          </Card>
        )}
      </section>

      {/*
        CAISSES FERMÉES — D-243. Ce qu'il a clôturé et doit encore remettre à
        l'agence, de la plus ancienne à la plus récente. Le bloc n'apparaît que
        s'il y en a : un livreur à jour n'a pas à lire un titre vide.
      */}
      {closed.items.length > 0 ? (
        <section className="flex flex-col gap-3">
          <div>
            <h2 className="text-base font-semibold tracking-tight">{t('closed.title')}</h2>
            <p className="text-muted-foreground text-sm">{t('closed.hint')}</p>
          </div>

          <ul className="flex flex-col gap-3">
            {closed.items.map((remittance) => (
              <li key={remittance.id}>
                <Card>
                  <CardContent className="flex items-center gap-3 p-4">
                    <span className="min-w-0 flex-1">
                      <span className="block truncate font-mono text-sm font-medium">
                        {remittance.code}
                      </span>
                      <span className="text-muted-foreground block truncate text-xs">
                        {t('parcels', { count: String(remittance.parcelCount) })}
                        {remittance.courierClosedAt
                          ? ` · ${t('closed.closedAt', {
                              date: format.dateTime(new Date(remittance.courierClosedAt), 'stamp'),
                            })}`
                          : ''}
                      </span>
                    </span>

                    <span className="flex shrink-0 flex-col items-end gap-1">
                      <span className="block text-sm font-semibold tabular-nums">
                        {money(remittance.amount)}
                      </span>
                      <RemittanceStageBadge stage={remittance.stage} />
                    </span>
                  </CardContent>
                </Card>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      <section className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-base font-semibold tracking-tight">{t('history.title')}</h2>
          {/*
            TOUTES ses caisses, en classeur — chantier « Affichage web et
            étiquette », point 4. `/remittances/export` existait : la portée
            serveur le borne à lui, sans `courierId` passé par l'écran (D-082).
            Ses colonnes (montant remis, écart, motif, reçu par) sont celles
            du reçu PDF qu'il télécharge déjà : rien de neuf ne lui est montré.
          */}
          <ExportExcelButton
            label={tExport('exportExcel')}
            filename="mes-caisses"
            onExport={() => exportListAction('remittances', { sort: 'createdAt', order: 'desc' })}
          />
        </div>

        {history.items.length === 0 ? (
          <p className="text-muted-foreground py-8 text-center text-sm">{t('history.empty')}</p>
        ) : (
          <ul className="flex flex-col gap-3">
            {history.items.map((remittance) => (
              <li key={remittance.id}>
                <Card>
                  <CardContent className="flex items-center gap-3 p-4">
                    <span className="min-w-0 flex-1">
                      <span className="block truncate font-mono text-sm font-medium">
                        {remittance.code}
                      </span>
                      <span className="text-muted-foreground block truncate text-xs">
                        {remittance.cityName} ·{' '}
                        {t('parcels', { count: String(remittance.parcelCount) })}
                      </span>
                    </span>

                    <span className="flex shrink-0 flex-col items-end gap-1">
                      <span className="block text-sm font-semibold tabular-nums">
                        {money(remittance.amount)}
                      </span>
                      {/* Reçue ou validée : le statut que l'API a déduit. */}
                      <RemittanceStageBadge stage={remittance.stage} />
                    </span>

                    <Button
                      variant="ghost"
                      size="icon"
                      disabled={pending}
                      aria-label={t('history.download')}
                      onClick={() => download(remittance.code)}
                    >
                      <Download className="size-4" aria-hidden />
                    </Button>
                  </CardContent>
                </Card>
              </li>
            ))}
          </ul>
        )}
      </section>

      <Dialog open={closing} onOpenChange={setClosing}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('open.closeTitle')}</DialogTitle>
            <DialogDescription>
              {open
                ? t('open.closeWarning', {
                    count: String(open.parcelCount),
                    amount: money(open.expectedNet),
                  })
                : null}
            </DialogDescription>
          </DialogHeader>

          <DialogFooter>
            <Button variant="outline" onClick={() => setClosing(false)}>
              {tCommon('cancel')}
            </Button>
            <Button disabled={pending} onClick={close} className="gap-1.5">
              {pending ? (
                <Loader2 className="size-4 animate-spin" aria-hidden />
              ) : (
                <Lock className="size-4" aria-hidden />
              )}
              {t('open.close')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Figure({
  icon: Icon,
  label,
  value,
  strong = false,
}: {
  icon: typeof Wallet;
  label: string;
  value: string;
  strong?: boolean;
}) {
  return (
    <div className="bg-muted/40 flex items-center gap-2.5 rounded-lg p-3">
      <Icon className="text-muted-foreground size-4 shrink-0" aria-hidden />
      <span className="min-w-0">
        <span className="text-muted-foreground block truncate text-xs">{label}</span>
        <span
          className={
            strong
              ? 'block text-base font-semibold tabular-nums'
              : 'block text-sm font-medium tabular-nums'
          }
        >
          {value}
        </span>
      </span>
    </div>
  );
}
