'use client';

import type {
  BankAccount,
  CourierStatementDetail,
  CourierStatementListItem,
  PayPeriod,
} from '@delivery/shared';
import {
  CheckCircle2,
  Eye,
  Image as ImageIcon,
  Loader2,
  Plus,
  Printer,
  RefreshCw,
  Trash2,
  Wallet,
} from 'lucide-react';
import { useFormatter, useTimeZone, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge } from '@/components/data-table/cells';
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
import { Label } from '@/components/ui/label';
import { ScrollArea } from '@/components/ui/scroll-area';
import { StatementPayForm } from '@/features/finance/statement-pay-form';
import { useFinanceError } from '@/features/finance/use-finance-error';
import {
  chequePhotoAction,
  createCourierStatementAction,
  deleteCourierStatementAction,
  downloadCourierStatementPdfAction,
  payCourierStatementAction,
  readCourierStatementAction,
  recomputeCourierStatementAction,
  validateCourierStatementAction,
} from '@/lib/actions/finance';
import { downloadBase64 } from '@/lib/download';
import { isoWeekday, localToday, previousMonth, shiftDay } from '@/lib/local-day';

const STATUS_TONE: Record<CourierStatementListItem['status'], string> = {
  draft: 'var(--chart-3)',
  validated: 'var(--chart-1)',
  paid: 'var(--chart-4)',
};

/**
 * La dernière période ÉCOULÉE, selon le rythme de paie du livreur.
 *
 * Écoulée, et pas en cours : proposer la période courante ferait clore une paie
 * avant la fin du travail. Le rythme vient de la fiche du livreur, qui
 * elle-même part du réglage de société (Paramètres > Finance). `today` est le
 * jour de la SOCIÉTÉ, pas de Greenwich (D-135).
 */
function lastPeriod(kind: PayPeriod, today: string): { start: string; end: string } {
  if (kind === 'daily') {
    const yesterday = shiftDay(today, -1);
    return { start: yesterday, end: yesterday };
  }

  if (kind === 'weekly') {
    // Semaine ISO : lundi → dimanche.
    const lastSunday = shiftDay(today, -(isoWeekday(today) + 1));
    return { start: shiftDay(lastSunday, -6), end: lastSunday };
  }

  return previousMonth(today);
}

/**
 * Onglet « Relevés » de la fiche livreur — D-020.
 *
 * Pas de `DataTable` ici : ce n'est pas une liste qu'on filtre et qu'on pagine,
 * c'est l'historique de paie d'UN livreur, lu de haut en bas. La table de
 * l'application sert les écrans de recherche ; celui-ci est une fiche.
 *
 * Le mois écoulé est proposé par défaut, parce que c'est la période qu'on
 * génère neuf fois sur dix — et parce que proposer le mois EN COURS ferait
 * clore une paie avant la fin du travail.
 */
export function CourierStatementsPanel({
  courierId,
  agencyId,
  payPeriod,
  statements,
  accounts,
  canWrite,
  canValidate,
  canPay,
}: {
  courierId: string;
  /** Agence de rattachement du livreur. Sans elle, rien à générer. */
  agencyId: string | null;
  /** Rythme de paie du livreur : il décide de la période proposée. */
  payPeriod: PayPeriod;
  statements: CourierStatementListItem[];
  /** Les comptes d'où peut partir la paie — C11 bloc 7, D-067. */
  accounts: BankAccount[];
  canWrite: boolean;
  canValidate: boolean;
  canPay: boolean;
}) {
  const t = useTranslations('finance.statements');
  const tStatus = useTranslations('finance.statements.status');
  const tKind = useTranslations('finance.statements.kind');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const errorMessage = useFinanceError();

  const suggested = lastPeriod(payPeriod, localToday(useTimeZone()));
  const [generating, setGenerating] = useState(false);
  const [start, setStart] = useState(suggested.start);
  const [end, setEnd] = useState(suggested.end);
  const [opened, setOpened] = useState<CourierStatementDetail | null>(null);
  /** Le relevé qu'on est en train de régler — C11 bloc 7, D-067. */
  const [paying, setPaying] = useState<CourierStatementListItem | null>(null);
  const [pending, startTransition] = useTransition();

  const money = (cents: number) => format.number(cents / 100, 'currency');

  const run = (
    action: () => Promise<
      { ok: true; data: CourierStatementDetail } | { ok: false; error: string; reason?: string }
    >,
    message: string,
  ) => {
    startTransition(async () => {
      const result = await action();

      if (result.ok) {
        // La fiche est revalidée côté serveur ; le dialogue ouvert, lui, doit
        // suivre tout de suite — c'est là que l'opérateur regarde.
        if (opened) setOpened(result.data);
        toast.success(message);
        return;
      }

      toast.error(errorMessage(result));
    });
  };

  const generate = () => {
    startTransition(async () => {
      const result = await createCourierStatementAction({
        courierId,
        agencyId,
        periodStart: start,
        periodEnd: end,
        note: '',
      });

      setGenerating(false);

      if (result.ok) toast.success(t('created', { code: result.data.code }));
      else toast.error(errorMessage(result));
    });
  };

  const print = (code: string) => {
    startTransition(async () => {
      const result = await downloadCourierStatementPdfAction(code);
      if (result.ok) downloadBase64(result.data, `${code}.pdf`, 'application/pdf');
      else toast.error(errorMessage(result));
    });
  };

  /**
   * La photo du chèque, ouverte dans un onglet — M1.
   *
   * Un `blob:` plutôt qu'un téléchargement : une image de chèque se REGARDE,
   * le temps de vérifier un numéro. Un agent qui rapproche une caisse ne veut
   * pas d'un fichier de plus dans son dossier de téléchargements.
   */
  const showCheque = (code: string) => {
    startTransition(async () => {
      const result = await chequePhotoAction(code);

      if (!result.ok) {
        toast.error(errorMessage(result));
        return;
      }

      const bytes = Uint8Array.from(atob(result.data), (char) => char.charCodeAt(0));
      const url = URL.createObjectURL(new Blob([bytes]));
      window.open(url, '_blank', 'noopener');
      // L'onglet a pris sa copie ; garder l'URL retiendrait l'image en mémoire.
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    });
  };

  const remove = (code: string) => {
    startTransition(async () => {
      const result = await deleteCourierStatementAction(code);
      if (result.ok) toast.success(t('deleted'));
      else toast.error(errorMessage(result));
    });
  };

  return (
    <Card>
      <CardHeader className="flex-row items-start justify-between gap-3">
        <div>
          <CardTitle>{t('title')}</CardTitle>
          <CardDescription>{t('subtitle')}</CardDescription>
        </div>

        {canWrite && agencyId ? (
          <Button size="sm" className="gap-1.5" onClick={() => setGenerating(true)}>
            <Plus className="size-4" />
            {t('new')}
          </Button>
        ) : null}
      </CardHeader>

      <CardContent>
        {statements.length === 0 ? (
          <p className="text-muted-foreground py-6 text-center text-sm">
            {agencyId ? t('empty') : t('noAgency')}
          </p>
        ) : (
          <ul className="divide-border divide-y">
            {statements.map((statement) => (
              <li key={statement.id} className="flex flex-wrap items-center gap-3 py-3">
                <span className="min-w-0 flex-1">
                  <span className="flex items-center gap-2">
                    <span className="font-mono text-sm">{statement.code}</span>
                    <CellBadge
                      label={tStatus(statement.status)}
                      color={STATUS_TONE[statement.status]}
                    />
                  </span>
                  <span className="text-muted-foreground block text-xs">
                    {t('period', {
                      start: format.dateTime(new Date(statement.periodStart), 'short'),
                      end: format.dateTime(new Date(statement.periodEnd), 'short'),
                    })}{' '}
                    · {t('courses', { count: String(statement.lineCount) })}
                  </span>
                </span>

                <span className="shrink-0 text-end">
                  <span className="block text-sm font-medium tabular-nums">
                    {money(statement.totalDue)}
                  </span>
                  <span className="text-muted-foreground block text-xs">
                    {t(`payModel.${statement.payModel}`)}
                  </span>
                </span>

                <span className="flex shrink-0 flex-wrap gap-1">
                  <Button
                    size="icon"
                    variant="ghost"
                    aria-label={t('open')}
                    disabled={pending}
                    onClick={() =>
                      startTransition(async () => {
                        // Le détail n'est pas préchargé : une fiche livreur peut
                        // porter douze relevés, et personne ne les ouvre tous.
                        // La lecture ne modifie rien — recalculer est un geste
                        // séparé, avec son propre bouton.
                        const result = await readCourierStatementAction(statement.code);
                        if (result.ok) setOpened(result.data);
                        else toast.error(errorMessage(result));
                      })
                    }
                  >
                    <Eye className="size-4" />
                  </Button>

                  <Button
                    size="icon"
                    variant="ghost"
                    aria-label={t('pdf')}
                    disabled={pending}
                    onClick={() => print(statement.code)}
                  >
                    <Printer className="size-4" />
                  </Button>

                  {canWrite && statement.status === 'draft' ? (
                    <>
                      <Button
                        size="icon"
                        variant="ghost"
                        aria-label={t('recompute')}
                        disabled={pending}
                        onClick={() =>
                          run(
                            () => recomputeCourierStatementAction(statement.code),
                            t('recomputed'),
                          )
                        }
                      >
                        <RefreshCw className="size-4" />
                      </Button>

                      <Button
                        size="icon"
                        variant="ghost"
                        className="text-destructive"
                        aria-label={tCommon('delete')}
                        disabled={pending}
                        onClick={() => remove(statement.code)}
                      >
                        <Trash2 className="size-4" />
                      </Button>
                    </>
                  ) : null}

                  {canValidate && statement.status === 'draft' ? (
                    <Button
                      size="sm"
                      variant="outline"
                      className="gap-1.5"
                      disabled={pending}
                      onClick={() =>
                        run(() => validateCourierStatementAction(statement.code), t('validated'))
                      }
                    >
                      <CheckCircle2 className="size-4" />
                      {t('validate')}
                    </Button>
                  ) : null}

                  {canPay && statement.status === 'validated' ? (
                    <Button
                      size="sm"
                      className="gap-1.5"
                      disabled={pending}
                      // C11 bloc 7 : régler demande maintenant par quel CANAL.
                      onClick={() => setPaying(statement)}
                    >
                      <Wallet className="size-4" />
                      {t('pay')}
                    </Button>
                  ) : null}
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardContent>

      {paying ? (
        <StatementPayForm
          amount={money(paying.totalDue)}
          accounts={accounts}
          pending={pending}
          onCancel={() => setPaying(null)}
          onSubmit={(payload) => {
            const code = paying.code;
            setPaying(null);
            run(() => payCourierStatementAction(code, { ...payload, proof: null }), t('paid'));
          }}
        />
      ) : null}

      <Dialog open={generating} onOpenChange={setGenerating}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('new')}</DialogTitle>
            <DialogDescription>{t('newHint')}</DialogDescription>
          </DialogHeader>

          <div className="grid gap-3 sm:grid-cols-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="periodStart">{t('fields.start')}</Label>
              <Input
                id="periodStart"
                type="date"
                value={start}
                onChange={(event) => setStart(event.target.value)}
              />
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="periodEnd">{t('fields.end')}</Label>
              <Input
                id="periodEnd"
                type="date"
                value={end}
                onChange={(event) => setEnd(event.target.value)}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setGenerating(false)}>
              {tCommon('cancel')}
            </Button>
            <Button disabled={pending || start > end} onClick={generate} className="gap-1.5">
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('generate')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={opened !== null} onOpenChange={(next) => (next ? null : setOpened(null))}>
        <DialogContent className="flex max-h-[85vh] flex-col sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>{opened?.code}</DialogTitle>
            <DialogDescription>
              {opened
                ? t('detailHint', {
                    base: money(opened.baseAmount),
                    variable: money(opened.variableAmount),
                  })
                : ''}
            </DialogDescription>
          </DialogHeader>

          {/*
            LE calcul, en clair — C11 bloc 6, D-058.
            Un livreur « normal » lit « commissions 30, net 0 » et croit à une
            erreur. Les trois lignes disent où sont passées ses 30 : il les a
            gardées à chaque remise. C'est la réclamation qu'elles évitent.
          */}
          {opened ? (
            <dl className="flex flex-col gap-1.5 text-sm">
              <div className="flex items-baseline justify-between gap-3">
                <dt className="text-muted-foreground">{t('codCollected')}</dt>
                <dd className="tabular-nums">{money(opened.codCollected)}</dd>
              </div>

              <div className="flex items-baseline justify-between gap-3">
                <dt className="text-muted-foreground">{t('commissions')}</dt>
                <dd className="tabular-nums">{money(opened.commissionsTotal)}</dd>
              </div>

              {opened.commissionsDeducted > 0 ? (
                <div className="flex items-baseline justify-between gap-3">
                  <dt className="text-muted-foreground">{t('commissionsDeducted')}</dt>
                  <dd className="tabular-nums">-{money(opened.commissionsDeducted)}</dd>
                </div>
              ) : null}

              {opened.baseAmount > 0 ? (
                <div className="flex items-baseline justify-between gap-3">
                  <dt className="text-muted-foreground">{t('fixed')}</dt>
                  <dd className="tabular-nums">{money(opened.baseAmount)}</dd>
                </div>
              ) : null}

              {/*
                Le chèque, quand il y en a un — M1. C11 savait écrire la photo
                et pas la relire : la fiche affichait un nom de fichier sans
                moyen de l'ouvrir, et l'agent ne pouvait pas vérifier le numéro
                qu'il venait de saisir.
              */}
              {opened.chequeNumber ? (
                <div className="flex items-baseline justify-between gap-3">
                  <dt className="text-muted-foreground">{t('fields.chequeNumber')}</dt>
                  <dd className="flex items-center gap-2 font-mono">
                    {opened.chequeNumber}
                    {opened.proofName ? (
                      <Button
                        size="sm"
                        variant="ghost"
                        className="h-7 gap-1.5 px-2"
                        disabled={pending}
                        onClick={() => showCheque(opened.code)}
                      >
                        <ImageIcon className="size-4" />
                        {t('chequePhoto')}
                      </Button>
                    ) : null}
                  </dd>
                </div>
              ) : null}
            </dl>
          ) : null}

          <ScrollArea className="min-h-0 flex-1 rounded-md border">
            <ul className="divide-border divide-y">
              {(opened?.lines ?? []).map((line) => (
                <li key={line.id} className="flex items-center gap-3 p-3">
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-mono text-sm">{line.parcelCode}</span>
                    <span className="text-muted-foreground block truncate text-xs">
                      {format.dateTime(new Date(line.workedAt), 'stamp')} · {tKind(line.kind)} ·{' '}
                      {line.cityName}
                    </span>
                  </span>
                  <span className="shrink-0 text-end">
                    <span className="block text-sm font-medium tabular-nums">
                      {money(line.amount)}
                    </span>
                    {/* Colis par colis : la première chose qu'un livreur conteste. */}
                    {line.deducted ? (
                      <span className="text-muted-foreground block text-xs">
                        {t('atRemittance')}
                      </span>
                    ) : null}
                  </span>
                </li>
              ))}
            </ul>

            {opened && opened.lines.length === 0 ? (
              <p className="text-muted-foreground p-6 text-center text-sm">{t('noCourse')}</p>
            ) : null}
          </ScrollArea>

          <DialogFooter className="items-center sm:justify-between">
            <span className="text-sm">
              {t('total')} :{' '}
              <span className="font-semibold tabular-nums">{money(opened?.totalDue ?? 0)}</span>
            </span>
            <Button variant="outline" onClick={() => setOpened(null)}>
              {tCommon('close')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
