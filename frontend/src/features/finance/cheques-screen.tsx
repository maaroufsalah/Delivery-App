'use client';

import {
  CHEQUE_PENDING_FILTER,
  CHEQUE_STATUS_COLORS,
  CHEQUE_STATUSES,
  type ChequeListItem,
  type ChequePage,
  type ChequeStatus,
  type Courier,
  nextChequeStatus,
} from '@delivery/shared';
import { Ban, ChevronRight, FileText, XCircle } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useState, useTransition } from 'react';
import { toast } from 'sonner';
import {
  CellBadge,
  CellMuted,
  CellNumber,
  CellStack,
  RowActions,
} from '@/components/data-table/cells';
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { DropdownMenuItem, DropdownMenuSeparator } from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Link } from '@/i18n/navigation';
import { advanceChequeAction, bulkAdvanceChequesAction } from '@/lib/actions/finance';
import { useChequeGroupSlipDownload, useChequeSlipDownload } from './cheque-slip-button';

const helper = createDataTableColumnHelper<ChequeListItem>();

/**
 * LES CHÈQUES REÇUS — chantier « Finance », point 9.
 *
 * **Aucun bouton « Nouveau chèque »**, et c'est le sujet : le chèque naît à
 * l'encaissement, avec le montant que le livreur a saisi au pas de porte. En
 * proposer un à la main aurait ouvert la porte au chèque saisi deux fois,
 * c'est-à-dire encaissé deux fois.
 *
 * **Une seule action : la marche SUIVANTE.** Le cycle est un chemin, pas un
 * menu ; proposer « déposé en banque » sur un chèque qu'on n'a pas encore reçu
 * aurait permis de sauter une constatation.
 *
 * **Le livreur lit le même écran**, borné par sa portée, et n'y voit qu'un
 * geste : « je l'ai remis ». La règle vit dans le service, pas ici.
 */
export function ChequesScreen({
  page,
  couriers,
  clients,
  readOnly = false,
}: {
  page: ChequePage;
  /** Les vendeurs, pour le filtre du point 2. Vide dans l'espace livreur. */
  clients: Array<{ id: string; name: string }>;
  couriers: Courier[];
  /**
   * LECTURE SEULE — chantier « Correctifs avant démo », point 6.
   *
   * L'écran est le même pour l'agence, le livreur et le VENDEUR : c'est la
   * portée du serveur qui borne les lignes (chantier « Chèques », point 2). Les
   * trois gestes du cycle, eux, lui sont refusés par le service — mais l'écran
   * les lui PROPOSAIT quand même, menu déroulant et bouton de pile compris. Un
   * bouton qui ne rend qu'une erreur est pire qu'un bouton absent.
   *
   * La colonne d'actions et la sélection disparaissent donc ensemble : cocher
   * vingt lignes n'a plus d'objet quand aucun geste ne s'y applique.
   */
  readOnly?: boolean;
}) {
  const t = useTranslations('finance.cheques');
  const tStatus = useTranslations('finance.cheques.status');
  const tCommon = useTranslations('common');
  const format = useFormatter();

  const [pending, startTransition] = useTransition();
  const { download: downloadSlip, pending: slipPending } = useChequeSlipDownload();
  const { download: downloadGroupSlip, pending: groupSlipPending } = useChequeGroupSlipDownload();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    defaultSort: 'receivedAt',
    defaultOrder: 'desc',
  });

  const [search, setSearch] = useUrlSearch(setFilter, param('search'));

  /*
   * LA SÉLECTION — chantier « Chèques », point 2. Elle est portée ICI, comme
   * le veut `DataTableSelection` : c'est l'appelant qui sait ce qu'il en fait
   * et qui doit la vider après un geste réussi.
   */
  const [selected, setSelected] = useState<string[]>([]);
  /** Le chèque qu'on s'apprête à rejeter, et le motif qu'on doit écrire. */
  const [rejecting, setRejecting] = useState<ChequeListItem | null>(null);
  const [reason, setReason] = useState('');

  const setParam = (key: string, value: string) =>
    setFilter(key, value === ALL_VALUE ? undefined : value);

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        // D-255 : statut, livreur et vendeur à choix multiple (`a,b` dans l'URL).
        id: 'status',
        multiple: true,
        label: t('filters.status'),
        value: param('status') ?? ALL_VALUE,
        options: [
          /*
           * « En cours » — lot S (D-231) : ce que compte la tuile de l'accueil
           * vendeur, qui ouvre cette liste ainsi filtrée. Espace vendeur
           * seulement : l'écran de l'agence ne change pas.
           */
          ...(readOnly ? [{ value: CHEQUE_PENDING_FILTER, label: t('filters.pending') }] : []),
          ...CHEQUE_STATUSES.map((status) => ({
            value: status,
            label: tStatus(status),
            color: CHEQUE_STATUS_COLORS[status],
          })),
        ],
        onChange: (value) => setParam('status', value),
      },
      {
        id: 'courierId',
        multiple: true,
        label: t('filters.courier'),
        value: param('courierId') ?? ALL_VALUE,
        options: couriers.map((courier) => ({
          value: courier.id,
          label: `${courier.firstName} ${courier.lastName}`,
        })),
        onChange: (value) => setParam('courierId', value),
      },
      /*
       * Le VENDEUR — point 2. C'est à lui que le chèque appartient, et c'est
       * la question qu'on pose le plus souvent à cet écran : « où en sont les
       * chèques d'Atlas Store ? ». Masqué quand la portée n'en donne qu'un.
       */
      ...(clients.length > 1
        ? [
            {
              id: 'clientId',
              multiple: true,
              label: t('filters.client'),
              value: param('clientId') ?? ALL_VALUE,
              options: clients.map((client) => ({ value: client.id, label: client.name })),
              onChange: (value: string) => setParam('clientId', value),
            },
          ]
        : []),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [clients, couriers, param, readOnly, t, tStatus],
  );

  const act = (id: string, step: 'advance' | 'reject' | 'cancel', label: string, comment = '') => {
    startTransition(async () => {
      const result = await advanceChequeAction(id, step, comment);

      if (result.ok) {
        toast.success(label);
        setSelected((current) => current.filter((key) => key !== id));
        return;
      }

      toast.error(result.error);
    });
  };

  /*
   * LE REJET DEMANDE UN MOTIF — point 2.
   *
   * L'écran envoyait la chaîne vide, et `rejectionReason` n'a donc JAMAIS été
   * rempli en production alors que le service le stockait depuis le début. Un
   * chèque rendu sans raison est une ligne que personne ne saura expliquer au
   * vendeur qui réclame son argent.
   */
  const confirmReject = () => {
    const cheque = rejecting;
    if (!cheque || reason.trim() === '') return;

    act(cheque.id, 'reject', t('rejected'), reason.trim());
    setRejecting(null);
    setReason('');
  };

  /**
   * LE MÊME GESTE, SUR UNE PILE.
   *
   * La cible est celle de la marche suivante du PREMIER chèque coché : une
   * pile se constitue en filtrant sur un état, et l'opérateur qui coche vingt
   * lignes les a devant lui, toutes au même stade. Les intrus sont refusés par
   * le serveur et NOMMÉS dans le retour, plutôt que d'avancer en silence vers
   * un état que personne n'a demandé.
   */
  const bulkTarget = useMemo(() => {
    const first = page.items.find((row) => selected.includes(row.id));
    return first ? nextChequeStatus(first.status) : null;
  }, [page.items, selected]);

  const runBulk = () => {
    if (!bulkTarget) return;

    startTransition(async () => {
      const result = await bulkAdvanceChequesAction({ ids: selected, status: bulkTarget });

      if (!result.ok) {
        toast.error(result.error);
        return;
      }

      const { done, skipped } = result.data;
      setSelected([]);

      if (done.length > 0) toast.success(t('bulkDone', { count: String(done.length) }));

      // Les refus sont NOMMÉS : un « 17 sur 20 » sans dire lesquels oblige à
      // comparer la liste à l'œil, et personne ne le fait.
      if (skipped.length > 0) {
        toast.warning(
          t('bulkSkipped', {
            count: String(skipped.length),
            numbers: skipped.map((row) => row.number ?? '?').join(', '),
          }),
        );
      }
    });
  };

  const columns: DataTableColumns<ChequeListItem> = useMemo(
    () =>
      helper.columns([
        /*
         * LE NUMÉRO OUVRE LA FICHE — point 6. C'est là que vit l'historique
         * daté, et c'est la seule chose que la liste ne peut pas montrer.
         */
        helper.accessor('number', {
          header: t('columns.number'),
          meta: { width: 'w-44' },
          cell: (info) => (
            <Link
              href={`/cheques/${encodeURIComponent(info.row.original.id)}`}
              className="underline-offset-4 hover:underline"
            >
              <CellStack mono label={info.getValue()} sub={info.row.original.bank ?? '—'} />
            </Link>
          ),
        }),

        helper.accessor('amount', {
          header: t('columns.amount'),
          sortFn: 'basic',
          meta: { align: 'end' },
          cell: (info) => <CellNumber value={format.number(info.getValue() / 100, 'currency')} />,
        }),

        helper.accessor('status', {
          header: t('columns.status'),
          sortFn: 'basic',
          cell: (info) => {
            const { status } = info.row.original;

            return <CellBadge label={tStatus(status)} color={CHEQUE_STATUS_COLORS[status]} />;
          },
        }),

        helper.accessor('parcelCode', {
          header: t('columns.parcel'),
          meta: { hideBelow: 'md' },
          cell: (info) => (
            <CellStack mono label={info.getValue()} sub={info.row.original.clientName} />
          ),
        }),

        /*
         * L'ESPACE VENDEUR — lot S (D-231) : le tireur et la date de
         * récupération ont leur colonne ; le livreur arrive déjà masqué en
         * « Livreur Speedy » par le serveur (D-228). La vue de l'agence garde
         * ses colonnes d'avant.
         */
        ...(readOnly
          ? [
              helper.accessor('payerName', {
                header: t('columns.payer'),
                meta: { hideBelow: 'md' },
                cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
              }),
              helper.accessor('receivedAt', {
                header: t('columns.pickedUpAt'),
                sortFn: 'datetime',
                meta: { hideBelow: 'lg' },
                cell: (info) => (
                  <CellMuted>{format.dateTime(new Date(info.getValue()), 'stamp')}</CellMuted>
                ),
              }),
              helper.accessor('courierName', {
                header: t('columns.courier'),
                meta: { hideBelow: 'xl' },
                cell: (info) => <CellMuted>{info.getValue()}</CellMuted>,
              }),
            ]
          : [
              helper.accessor('courierName', {
                header: t('columns.courier'),
                meta: { hideBelow: 'lg' },
                cell: (info) => (
                  <CellStack label={info.getValue()} sub={info.row.original.payerName} />
                ),
              }),
              helper.accessor('receivedAt', {
                header: t('columns.receivedAt'),
                sortFn: 'datetime',
                meta: { hideBelow: 'xl' },
                cell: (info) => (
                  <CellMuted>{format.dateTime(new Date(info.getValue()), 'stamp')}</CellMuted>
                ),
              }),
            ]),

        // En lecture seule, la colonne n'est pas désactivée : elle n'existe pas.
        ...(readOnly ? [] : [helper.display({
          id: 'actions',
          header: () => <span className="sr-only">{tCommon('actions')}</span>,
          enableSorting: false,
          meta: { align: 'end', isActions: true, width: 'w-12' },
          cell: (info) => {
            const row = info.row.original;
            const next = nextChequeStatus(row.status);
            /* Clos : le vendeur a confirmé, ou le papier est sorti du circuit. */
            const closed =
              row.status === 'recu-vendeur' || row.status === 'annule' || row.status === 'rejete';

            return (
              <RowActions>
                {/* D-265 : le papier qui accompagne le chèque rendu au vendeur. */}
                <DropdownMenuItem
                  disabled={slipPending}
                  onClick={() => downloadSlip(row.id, row.parcelCode)}
                >
                  <FileText className="size-4" />
                  {t('slip')}
                </DropdownMenuItem>

                {next ? <DropdownMenuSeparator /> : null}

                {next ? (
                  <DropdownMenuItem
                    disabled={pending}
                    onClick={() => act(row.id, 'advance', t('advanced', { status: tStatus(next) }))}
                  >
                    <ChevronRight className="flip-rtl size-4" />
                    {t('advanceTo', { status: tStatus(next as ChequeStatus) })}
                  </DropdownMenuItem>
                ) : null}

                {closed ? null : <DropdownMenuSeparator />}

                {closed ? null : (
                  <DropdownMenuItem
                    disabled={pending}
                    onClick={() => {
                      // On OUVRE le dialogue : le motif est obligatoire, et il
                      // ne se saisit pas dans un menu déroulant.
                      setReason('');
                      setRejecting(row);
                    }}
                  >
                    <XCircle className="size-4" />
                    {t('reject')}
                  </DropdownMenuItem>
                )}

                {closed ? null : (
                  <DropdownMenuItem
                    disabled={pending}
                    onClick={() => act(row.id, 'cancel', t('cancelled'))}
                  >
                    <Ban className="size-4" />
                    {t('cancel')}
                  </DropdownMenuItem>
                )}
              </RowActions>
            );
          },
        })]),
      ]),
    /*
      La dérogation `exhaustive-deps` qui vivait ici ne servait plus — le
      linteur la signalait comme inutile depuis que la colonne d'actions est
      conditionnelle (chantier « Correctifs avant démo », point 6). Un
      commentaire qui fait taire un linteur qui n'a rien à dire finit par
      couvrir un vrai avertissement.
    */
    [downloadSlip, format, pending, readOnly, slipPending, t, tCommon, tStatus],
  );

  return (
    <>
      <DataTable
        data={page.items}
        columns={columns}
        server={server}
        /*
         * LA SÉLECTION EST CELLE DU SOCLE — `DataTableSelection`, déjà servie
         * par la liste des colis. En écrire une seconde ici aurait donné deux
         * comportements pour « tout cocher » sur une liste paginée, et les
         * deux auraient divergé au premier correctif.
         */
        selection={readOnly ? undefined : {
          keyOf: (row) => row.id,
          selected,
          onChange: setSelected,
          actions: (
            <div className="flex flex-wrap items-center gap-2">
              {/*
                Le bon de remise GROUPÉ : un bon par vendeur de la sélection,
                un PDF. Il ne fait avancer aucune marche, donc il s'offre quel
                que soit l'état des chèques cochés.
              */}
              <Button
                size="sm"
                variant="outline"
                disabled={groupSlipPending}
                onClick={() => downloadGroupSlip(selected)}
              >
                <FileText className="size-4" />
                {t('groupSlip')}
              </Button>

              {bulkTarget ? (
                <Button size="sm" disabled={pending} onClick={runBulk}>
                  <ChevronRight className="flip-rtl size-4" />
                  {t('bulkAdvanceTo', { status: tStatus(bulkTarget) })}
                </Button>
              ) : (
                <span className="text-muted-foreground text-xs">{t('bulkNothing')}</span>
              )}
            </div>
          ),
        }}
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            searchPlaceholder={t('searchPlaceholder')}
            selects={selects}
            // Mêmes filtres et même portée que la liste (agence, livreur, vendeur).
            exportResource="cheques"
            onReset={() => {
              setSearch('');
              setSelected([]);
              reset();
            }}
          >
            {/*
              LA PÉRIODE — chantier « Correctifs avant démo », point 6.

              Deux dates crues, et non le `PeriodFilter` des tableaux de bord :
              celui-là travaille sur des périodes NOMMÉES que le serveur résout
              (`period=this-month` → bornes), et il écrit un cookie de
              préférence partagé avec le tableau de bord. Une liste filtrée n'a
              besoin ni de l'un ni de l'autre : `from` et `to` partent tels
              quels à l'API, qui les attend déjà, et ils vivent dans l'URL comme
              tous les autres filtres de cette barre.

              `setFilter` remet en page 1 : changer la fenêtre change le jeu de
              lignes, et rester page 7 montrerait du vide.
            */}
            <div className="col-span-2 grid grid-cols-2 gap-2 md:col-span-1 md:flex md:items-center">
              <Input
                type="date"
                value={param('from') ?? ''}
                aria-label={t('filters.from')}
                className="h-9 md:w-40"
                onChange={(event) => setFilter('from', event.target.value || undefined)}
              />
              <Input
                type="date"
                value={param('to') ?? ''}
                aria-label={t('filters.to')}
                className="h-9 md:w-40"
                onChange={(event) => setFilter('to', event.target.value || undefined)}
              />
            </div>
          </DataTableToolbar>
        }
      />

      {/*
        LE TOTAL DU FILTRE — lot S (D-231). Calculé par le serveur sur toutes
        les pages : le vendeur lit combien ses chèques « en cours » pèsent, pas
        combien la page 1 en montre. Espace vendeur seulement.
      */}
      {readOnly && page.total > 0 ? (
        <p className="text-muted-foreground text-end text-sm">
          {t('totalAmount', {
            count: page.total,
            amount: format.number(page.totalAmount / 100, 'currency'),
          })}
        </p>
      ) : null}

      {/*
        LE MOTIF DU REJET — point 2. Un chèque rendu sans raison est une ligne
        que personne ne saura expliquer au vendeur qui réclame son argent.

        Jamais monté en lecture seule : le geste qui l'ouvre n'existe pas.
      */}
      <Dialog open={!readOnly && rejecting !== null} onOpenChange={(open) => !open && setRejecting(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('rejectTitle')}</DialogTitle>
            <DialogDescription>
              {rejecting ? t('rejectHint', { number: rejecting.number }) : null}
            </DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="cheque-reason">{t('rejectReason')}</Label>
            <Textarea
              id="cheque-reason"
              value={reason}
              rows={3}
              placeholder={t('rejectPlaceholder')}
              onChange={(event) => setReason(event.target.value)}
            />
          </div>

          <DialogFooter>
            <Button variant="ghost" onClick={() => setRejecting(null)}>
              {tCommon('cancel')}
            </Button>
            <Button variant="destructive" disabled={pending || reason.trim() === ''} onClick={confirmReject}>
              {t('reject')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
