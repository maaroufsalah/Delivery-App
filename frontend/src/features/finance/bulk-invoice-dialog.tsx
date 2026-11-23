'use client';

import type {
  BulkInvoiceCandidate,
  BulkInvoiceResult,
  InvoiceClientKind,
  TransferOrderDetail,
} from '@delivery/shared';
import { FileSpreadsheet, FileText, Landmark, Layers, Loader2 } from 'lucide-react';
import { useFormatter, useTimeZone, useTranslations } from 'next-intl';
import { useEffect, useState } from 'react';
import { toast } from 'sonner';
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
import { Link } from '@/i18n/navigation';
import {
  bulkInvoiceCandidatesAction,
  createBulkInvoicesAction,
  createTransferOrderAction,
  downloadTransferOrderAction,
} from '@/lib/actions/finance';
import { downloadBase64 } from '@/lib/download';
import { localToday } from '@/lib/local-day';

/** Les motifs de refus que le serveur peut rendre — liste fermée, traduite. */
const REASONS = [
  'client-unknown',
  'no-billable-parcel',
  'no-agency',
  'agency-out-of-scope',
] as const;

type Reason = (typeof REASONS)[number];

/**
 * CRÉER DES FACTURES CLIENTS EN MASSE — chantier « Finance », point 5.
 *
 * **La liste ne propose QUE ce qui est facturable.** Proposer tous les clients
 * aurait obligé l'agent à découvrir après coup que sa sélection n'a rien
 * produit. Chaque ligne dit combien de colis et combien de dirhams : c'est ce
 * qui permet de cocher sans ouvrir neuf fiches.
 *
 * **Les refus sont affichés, pas tus.** Une création en masse qui annonce
 * « 7 factures créées » sur 9 cochées laisse chercher les deux manquantes. Le
 * serveur rend les deux listes ; l'écran les montre toutes les deux.
 *
 * **Un colis ne peut entrer que dans UNE facture**, brouillons compris : le
 * serveur exclut tout colis déjà porté par une facture, et l'index unique de
 * l'émission ferme la porte une seconde fois (D-036).
 *
 * **L'ORDRE DE VIREMENT** — chantier « Caisse, OV, Petite caisse », point 2.
 * Le même dialogue, la même liste, deux usages : `invoices` crée les factures
 * puis propose l'ordre de virement de CES factures ; `transfer` tire l'ordre
 * de virement des clients cochés sans rien facturer. Une seconde liste aurait
 * fini par ne plus proposer les mêmes clients.
 */
export function BulkInvoiceDialog({
  onDone,
  mode = 'invoices',
  kind = 'normal',
}: {
  onDone: () => void;
  mode?: 'invoices' | 'transfer';
  /**
   * Le type de la page — recette 3, point 4. Les candidats (génération en
   * masse ET ordre de virement) ne sont que les clients de ce type.
   */
  kind?: InvoiceClientKind;
}) {
  const t = useTranslations('finance.invoices.bulk');
  const tTransfer = useTranslations('finance.invoices.transfer');
  const tReasons = useTranslations('finance.invoices.bulk.reasons');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  // Le jour du nom de fichier : celui de la SOCIÉTÉ (D-135).
  const timeZone = useTimeZone();

  const [candidates, setCandidates] = useState<BulkInvoiceCandidate[] | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [done, setDone] = useState<BulkInvoiceResult['created'] | null>(null);
  const [order, setOrder] = useState<TransferOrderDetail | null>(null);

  /*
   * ═══ LA BOUCLE INFINIE, ET POURQUOI ELLE TOURNAIT ═══
   *
   * Ce chargement vivait dans le CORPS DE RENDU, gardé par `!loading` :
   *
   *     if (candidates === null && !loading) {
   *       startLoading(async () => { … });     // ← appelé pendant le rendu
   *     }
   *
   * React refuse `startTransition` pendant un rendu. Il logge « Cannot call
   * startTransition while rendering » et **n'enfile pas** la mise à jour
   * optimiste : `loading` ne passait donc JAMAIS à `true`, et la garde était
   * morte. Pire, la transition enfilait quand même sa promesse de fin ; le
   * composant la lisait au rendu suivant, SUSPENDAIT avant d'avoir committé,
   * et perdait tout son état. Au retry, `candidates` revalait `null` — et on
   * repartait. Une requête par aller-retour réseau, sans borne : **238
   * requêtes** observées.
   *
   * Elles apparaissaient comme des `POST /invoices` et non
   * `/invoices/bulk-candidates`, parce qu'une Server Action est un POST vers
   * la page courante — ce qui a longtemps fait chercher au mauvais endroit.
   *
   * **Un effet, avec des dépendances vides.** C'est le seul endroit d'où l'on
   * a le droit de déclencher un effet de bord : il tourne APRÈS le commit, une
   * fois, et le composant est monté quand la réponse arrive.
   */
  useEffect(() => {
    let vivant = true;

    void (async () => {
      const result = await bulkInvoiceCandidatesAction(kind);
      if (!vivant) return;

      if (result.ok) {
        setCandidates(result.data);
        // Tout coché d'emblée : le cas courant est « facture tout ce qui est
        // prêt ». Décocher trois lignes coûte moins que d'en cocher quarante.
        setSelected(result.data.map((row) => row.clientId));
      } else {
        toast.error(result.error);
        setCandidates([]);
      }

      setLoading(false);
    })();

    // Le modal peut se fermer avant la réponse : on ne parle pas à un
    // composant démonté.
    return () => {
      vivant = false;
    };
    // UNE fois (render-side-effects.spec) : `kind` est fixé par la page pour
    // toute la vie du dialogue, il ne change jamais sous lui.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const money = (cents: number) => format.number(cents / 100, 'currency');
  const rows = candidates ?? [];
  const chosen = rows.filter((row) => selected.includes(row.clientId));
  const chosenTotal = chosen.reduce((sum, row) => sum + row.totalDue, 0);

  const download = async (code: string, format: 'pdf' | 'xlsx') => {
    const result = await downloadTransferOrderAction(code, format);

    if (!result.ok) {
      toast.error(result.error);
      return;
    }

    downloadBase64(
      result.data,
      // Le nom que la mission fixe : Ordre-Virement-<date>.
      `Ordre-Virement-${localToday(timeZone)}.${format}`,
      format === 'pdf'
        ? 'application/pdf'
        : 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    );
  };

  /** Génère l'ordre (numéro OV- réservé, journalisé) et en télécharge le PDF. */
  const generateOrder = async (input: { clientIds: string[] } | { invoiceCodes: string[] }) => {
    setSaving(true);
    const result = await createTransferOrderAction(input);

    if (!result.ok) {
      setSaving(false);
      toast.error(result.error);
      return;
    }

    setOrder(result.data);
    await download(result.data.code, 'pdf');
    setSaving(false);
  };

  const create = async () => {
    if (mode === 'transfer') {
      await generateOrder({ clientIds: selected });
      return;
    }

    setSaving(true);
    const result = await createBulkInvoicesAction({ clientIds: selected }, kind);
    setSaving(false);

    if (!result.ok) {
      toast.error(result.error);
      return;
    }

    const { created, skipped } = result.data;

    if (created.length > 0) {
      toast.success(t('created', { count: created.length }));
      // Le résumé REMPLACE la liste : l'opérateur doit pouvoir ouvrir ce qu'il
      // vient de créer, et un toast qui s'efface au bout de trois secondes ne
      // le permet pas.
      setDone(created);
    }
    /*
      Les motifs sont une liste FERMÉE, et le contrat le dit : les traduire par
      une clé calculée aurait laissé passer un motif neuf sans traduction, donc
      une clé brute sous les yeux d'un agent.
    */
    for (const row of skipped) {
      const reason = REASONS.includes(row.reason as Reason)
        ? tReasons(row.reason as Reason)
        : row.reason;

      toast.warning(t('skipped', { client: row.clientName, reason }));
    }

    // Rien créé : on ferme, il n'y a pas de résumé à montrer.
    if (created.length === 0) onDone();
  };

  return (
    <Dialog open onOpenChange={(next) => (next ? null : onDone())}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>{mode === 'transfer' ? tTransfer('title') : t('title')}</DialogTitle>
          <DialogDescription>
            {mode === 'transfer' ? tTransfer('hint') : t('hint')}
          </DialogDescription>
        </DialogHeader>

        {order !== null ? (
          /*
            L'ORDRE EST GÉNÉRÉ : son numéro, son total, et les deux formats. Le
            PDF est déjà parti ; l'Excel relit le MÊME ordre, sans nouveau
            numéro.
          */
          <div className="flex flex-col gap-3 py-2">
            <p className="text-sm">{tTransfer('ready', { code: order.code })}</p>
            <p className="text-muted-foreground text-sm">
              {tTransfer('summary', {
                count: order.lines.length,
                amount: money(order.totalDue),
              })}
            </p>
            <div className="flex flex-wrap gap-2">
              <Button
                variant="outline"
                size="sm"
                className="gap-1.5"
                onClick={() => download(order.code, 'pdf')}
              >
                <FileText className="size-4" />
                {tTransfer('pdf')}
              </Button>
              <Button
                variant="outline"
                size="sm"
                className="gap-1.5"
                onClick={() => download(order.code, 'xlsx')}
              >
                <FileSpreadsheet className="size-4" />
                {tTransfer('excel')}
              </Button>
            </div>
          </div>
        ) : done !== null ? (
          /*
            LE RÉSUMÉ — chantier « Chèques », point 4. Ce qui vient d'être créé,
            avec un lien vers chaque facture : la question qui suit « créer 12
            factures » est toujours « montre-les moi ».
          */
          <ul className="flex max-h-80 flex-col gap-1 overflow-y-auto">
            {done.map((invoice) => (
              <li key={invoice.code}>
                <Link
                  href={`/invoices/${invoice.code}`}
                  className="hover:bg-muted flex items-center gap-3 rounded-lg p-2"
                >
                  <FileText className="text-muted-foreground size-4 shrink-0" aria-hidden />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-mono text-sm">{invoice.code}</span>
                    <span className="text-muted-foreground block truncate text-xs">
                      {invoice.clientName}
                      {/* Lot D, D5 : le brouillon existant a reçu les colis, aucun n'a été ouvert. */}
                      {invoice.reused ? ` · ${t('reused')}` : null}
                    </span>
                  </span>
                  <span className="text-muted-foreground shrink-0 text-xs">
                    {t('parcelCount', { count: invoice.parcelCount })}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        ) : loading ? (
          <p className="text-muted-foreground py-6 text-center text-sm">{tCommon('loading')}</p>
        ) : rows.length === 0 ? (
          <p className="text-muted-foreground py-6 text-center text-sm">{t('empty')}</p>
        ) : (
          <div className="flex flex-col gap-1">
            {/* « Tout cocher » — point 4. Utile pour RECOCHER après avoir
                décoché : sans elle, il faut repasser sur quarante lignes. */}
            <label className="hover:bg-muted flex cursor-pointer items-center gap-3 rounded-lg p-2">
              <Checkbox
                checked={selected.length === rows.length && rows.length > 0}
                onCheckedChange={(checked) =>
                  setSelected(checked ? rows.map((row) => row.clientId) : [])
                }
              />
              <span className="text-sm font-medium">{t('selectAll')}</span>
            </label>

            <ul className="flex max-h-80 flex-col gap-1 overflow-y-auto">
              {rows.map((row) => (
                <li key={row.clientId}>
                  <label className="hover:bg-muted flex cursor-pointer items-center gap-3 rounded-lg p-2">
                    <Checkbox
                      checked={selected.includes(row.clientId)}
                      onCheckedChange={(checked) =>
                        setSelected((current) =>
                          checked
                            ? [...current, row.clientId]
                            : current.filter((id) => id !== row.clientId),
                        )
                      }
                    />
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium">{row.clientName}</span>
                      <span className="text-muted-foreground block truncate text-xs">
                        {t('parcelCount', { count: row.parcelCount })} · {row.agencyName}
                      </span>
                    </span>
                    <span className="text-sm tabular-nums">{money(row.totalDue)}</span>
                  </label>
                </li>
              ))}
            </ul>
          </div>
        )}

        <DialogFooter className="gap-2 sm:gap-2">
          {order !== null ? (
            <Button onClick={onDone}>{tCommon('close')}</Button>
          ) : done !== null ? (
            <>
              {/* Point 2a : l'ordre de virement de CES factures, pas d'autres. */}
              <Button
                variant="outline"
                className="gap-1.5"
                disabled={saving}
                onClick={() => generateOrder({ invoiceCodes: done.map((row) => row.code) })}
              >
                {saving ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  <Landmark className="size-4" />
                )}
                {tTransfer('download')}
              </Button>
              <Button onClick={onDone}>{tCommon('close')}</Button>
            </>
          ) : (
            <>
              <Button variant="outline" onClick={onDone} disabled={saving}>
                {tCommon('cancel')}
              </Button>
              <Button
                onClick={create}
                disabled={saving || selected.length === 0}
                className="gap-1.5"
              >
                {saving ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : mode === 'transfer' ? (
                  <Landmark className="size-4" />
                ) : (
                  <Layers className="size-4" />
                )}
                {mode === 'transfer'
                  ? tTransfer('submit', { amount: money(chosenTotal) })
                  : t('submit', { count: String(chosen.length), amount: money(chosenTotal) })}
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
