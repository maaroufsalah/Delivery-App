'use client';

import {
  type LabelFormat,
  FINANCIAL_STATUS_COLORS,
  LABELS_ASYNC_THRESHOLD,
  adminFieldStatusIds,
  FINANCIAL_STATUSES,
  MERCHANT_FINANCIAL_STATUS_COLORS,
  MERCHANT_FINANCIAL_STATUSES,
  isLockedStatus,
  parcelAddressLine,
  PARCEL_KINDS,
  PARCEL_STATUS_GROUPS,
  PARCEL_STATUSES,
  type City,
  type Client,
  type Courier,
  type Paginated,
  type ParcelListItem,
  type ParcelStats,
  type ResolvedDashboardWindow,
} from '@delivery/shared';
import {
  CircleSlash,
  ClipboardList,
  ArrowRightLeft,
  ListChecks,
  PackageCheck,
  Printer,
  Truck,
  Upload,
  Wallet,
} from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useCallback, useMemo, useState } from 'react';
import { DataTable } from '@/components/data-table/data-table';
import {
  ALL_VALUE,
  DataTableToolbar,
  type FilterSelect,
} from '@/components/data-table/data-table-toolbar';
import { useServerTable, useUrlSearch } from '@/components/data-table/use-server-table';
import { KpiCard, KpiGrid } from '@/components/kpi-card';
import { PeriodFilter } from '@/features/dashboard/period-filter';
import { useParcelColumns } from '@/features/parcels/columns';
import { parcelListFilters } from '@/features/parcels/list-filters';
import { PrintFormatDialog } from '@/components/print-button';
import { downloadBase64 } from '@/lib/download';
import {
  createLabelJobAction,
  parcelLabelAction,
  parcelLabelsAction,
} from '@/lib/actions/documents';
import { LabelJobsPanel, useLabelJobs } from '@/features/parcels/label-jobs-panel';
import { requestDropoffNoteAction, requestPickupNoteAction } from '@/lib/actions/routes';
import { ChangeStatusDialog, DisputeDialog } from '@/features/parcels/status-dialogs';
import { AdminFieldStatusDialog } from '@/features/parcels/admin-field-status-dialog';
import { ParcelTransferDialog } from '@/features/routes/parcel-transfer-dialog';
import { Link, useRouter } from '@/i18n/navigation';
import { Button } from '@/components/ui/button';
import { toast } from 'sonner';

/**
 * Liste des colis.
 *
 * Filtres, tri ET pagination vivent dans l'URL, et c'est le SERVEUR qui
 * découpe (B-5, soldé en C7 bloc 4). La table demandait jusqu'ici cent lignes
 * pour en montrer dix : l'arbitrage se tenait tant que les données étaient
 * mockées, il ne tient plus sur une base qui compte des milliers de colis.
 */
export function ParcelsTable({
  page,
  stats,
  clients,
  cities,
  window,
  isMerchant = false,
  canAdminFieldStatus = false,
  statusCouriers = null,
  canTransfer = false,
}: {
  /**
   * « TRANSFÉRER À UN LIVREUR » (D-246) : l'action de masse sur les cochés,
   * pour qui compose les circuits (`routes.write`). Le serveur juge chaque
   * colis : sur un circuit de livraison ouvert, ou « En cours de livraison »
   * hors circuit ; livré, facturé, sur un bon de retour : refus.
   */
  canTransfer?: boolean;
  /** D-246 : les livreurs du champ « Livreur » de « En cours de livraison ». */
  statusCouriers?: Courier[] | null;
  page: Paginated<ParcelListItem>;
  stats: ParcelStats;
  clients: Client[];
  cities: City[];
  /**
   * LA PÉRIODE RÉSOLUE — chantier « Retours recette 26/09 », point 1. Le même
   * sélecteur que les tableaux de bord, posé dans la barre de filtres ; il
   * borne la liste, les quatre KPI et l'export, et se souvient du dernier
   * choix par utilisateur. Défaut « depuis le début » (résolu par la page).
   */
  window: ResolvedDashboardWindow;
  /**
   * LE COMPTE EST-IL UN VENDEUR ? — V2 vendeur bloc 4.
   *
   * Les deux gestes « demander un ramassage » et « annoncer un dépôt » passent
   * par `/merchant/me/*`, qui lit la portée du compte. Un agent d'agence n'a
   * rien à y faire : il compose les bons depuis les écrans de bons, où il
   * choisit le client. Le bouton ne s'affiche donc que pour qui a une fiche.
   */
  isMerchant?: boolean;
  /**
   * « STATUT LIBRE POUR L'ADMIN » (D-245) : l'action de masse « Changer le
   * statut ». La page la donne au SUPER_ADMIN ; l'API refuse tout autre compte.
   */
  canAdminFieldStatus?: boolean;
}) {
  const t = useTranslations('parcels');
  const tLabelJobs = useTranslations('parcels.labelJobs');
  // Lot D, D3 (D-190) : les grandes planches, préparées en arrière-plan.
  const { jobs: labelJobs, add: addLabelJob } = useLabelJobs();
  const tStatus = useTranslations('status');
  const tFinancial = useTranslations('financialStatus');
  const tMerchantFinancial = useTranslations('merchantFinancialStatus');
  const tGroup = useTranslations('statusGroup');
  /* Le libellé du type — point 2 : « Colis », « Retour », « Échange ». */
  const tKind = useTranslations('parcels.kind');
  const format = useFormatter();

  const router = useRouter();

  const { server, param, setFilter, reset } = useServerTable({
    total: page.total,
    // Date de création, récent d'abord (chantier « Tri par défaut », 11/11/2026).
    defaultSort: 'createdAt',
    defaultOrder: 'desc',
  });

  // La recherche part au serveur : l'écrire à chaque frappe ferait une requête
  // par lettre. `useUrlSearch` garde la saisie locale et ne la commet qu'après
  // une pause.
  const [search, setSearch] = useUrlSearch(setFilter, param('search'));

  // Une seule instance de chaque dialogue pour toute la table : le colis visé
  // est porté par l'état, pas par la ligne — sinon 100 dialogues sont montés.
  const [statusTarget, setStatusTarget] = useState<ParcelListItem | null>(null);
  const [labelTarget, setLabelTarget] = useState<ParcelListItem | null>(null);
  const [labelFormat, setLabelFormat] = useState<LabelFormat>('10x10');
  const [printing, setPrinting] = useState(false);
  /*
   * La sélection est portée ICI et non par la table : c'est cet écran qui sait
   * ce qu'il en fait, et qui doit pouvoir la vider après un téléchargement.
   */
  const [selected, setSelected] = useState<string[]>([]);
  /*
   * « TOUTE LA LISTE » — D-176 (lot B, point B5). Les cochés ne portent que les
   * pages vues ; ce drapeau dit « toute la liste filtrée », et c'est le serveur
   * qui la résout (jusqu'à 5 000 colis, dans l'ordre affiché). Décocher une
   * ligne ou vider la sélection en sort.
   */
  const [allMatching, setAllMatching] = useState(false);
  /*
   * LES CARTONS DES COCHÉS — lot G, D-208. Un colis de 4 cartons imprime 4
   * étiquettes : la boîte d'impression annonce « 12 colis, 19 pages ». La
   * quantité est relevée sur la page affichée au moment où l'on coche — c'est
   * la seule où la case existe. « Toute la liste » se résout côté serveur et
   * n'annonce que le nombre de colis.
   */
  const [cartonsOf, setCartonsOf] = useState<Record<string, number>>({});
  const changeSelection = (next: string[]) => {
    setAllMatching(false);
    setSelected(next);
    setCartonsOf((previous) => {
      const known = { ...previous };
      for (const item of page.items) {
        if (next.includes(item.code)) known[item.code] = item.quantity ?? 1;
      }
      return known;
    });
  };
  const selectedCount = allMatching ? page.total : selected.length;
  const selectedPages = allMatching
    ? null
    : selected.reduce((sum, code) => sum + (cartonsOf[code] ?? 1), 0);
  const [batchOpen, setBatchOpen] = useState(false);
  const [fieldStatusOpen, setFieldStatusOpen] = useState(false);
  // D-246 : « Transférer à un livreur » — les identifiants des cochés de la page.
  const [transferOpen, setTransferOpen] = useState(false);
  const tTransfer = useTranslations('routes.transfer');
  const transferIds = page.items
    .filter((parcel) => selected.includes(parcel.code))
    .map((parcel) => parcel.id);
  const tFieldStatus = useTranslations('parcels.fieldStatus');
  const [noting, setNoting] = useState(false);

  /**
   * Ouvre un bon avec la sélection, puis emmène sur le bon — V2 vendeur bloc 4.
   *
   * On NAVIGUE plutôt que d'afficher un message : le geste suivant du vendeur
   * est toujours le même — vérifier la liste, imprimer le bon, imprimer les
   * étiquettes — et le laisser sur la liste des colis l'obligerait à retrouver
   * lui-même un numéro qu'il vient de voir passer dans une notification.
   */
  const note = async (kind: 'pickup' | 'dropoff') => {
    setNoting(true);
    const result =
      kind === 'pickup'
        ? await requestPickupNoteAction(selected)
        : await requestDropoffNoteAction(selected);
    setNoting(false);

    if (!result.ok) {
      toast.error(result.error);
      return;
    }

    setSelected([]);
    router.push(
      kind === 'pickup'
        ? `/pickup-notes/${result.data.code}`
        : `/dropoff-notes/${result.data.code}`,
    );
  };

  // `useCallback` : la référence entre dans les dépendances du `useMemo` des
  // filtres ; recréée à chaque rendu, elle le recalculerait à chaque frappe.
  const setParam = useCallback(
    (key: string, value: string) => setFilter(key, value === ALL_VALUE ? undefined : value),
    [setFilter],
  );

  const selects: FilterSelect[] = useMemo(
    () => [
      {
        id: 'statusGroup',
        multiple: true,
        label: tGroup('label'),
        value: param('statusGroup') ?? ALL_VALUE,
        options: PARCEL_STATUS_GROUPS.map((group) => ({ value: group, label: tGroup(group) })),
        onChange: (value) => setParam('statusGroup', value),
      },
      {
        id: 'statusId',
        multiple: true,
        label: t('filters.status'),
        value: param('statusId') ?? ALL_VALUE,
        options: PARCEL_STATUSES.map((status) => ({
          value: String(status.id),
          label: tStatus(status.key),
          color: status.color,
        })),
        onChange: (value) => setParam('statusId', value),
      },
      /*
        LE FILTRE « TYPE » — chantier « Étiquette orange », point 2.
        Colis, retour, échange. Un troisième axe : un retour peut être « en
        attente » comme un colis ordinaire, et c'est bien pour ça qu'on veut
        pouvoir demander l'un sans l'autre.
      */
      {
        id: 'kind',
        multiple: true,
        label: t('filters.kind'),
        value: param('kind') ?? ALL_VALUE,
        options: PARCEL_KINDS.map((kind) => ({ value: kind, label: tKind(kind) })),
        onChange: (value) => setParam('kind', value),
      },
      {
        id: 'clientId',
        multiple: true,
        label: t('filters.client'),
        value: param('clientId') ?? ALL_VALUE,
        options: clients.map((client) => ({ value: client.id, label: client.name })),
        onChange: (value) => setParam('clientId', value),
      },
      {
        id: 'destinationCityId',
        multiple: true,
        label: t('filters.city'),
        value: param('destinationCityId') ?? ALL_VALUE,
        options: cities.map((city) => ({ value: city.id, label: city.name })),
        cities,
        onChange: (value) => setParam('destinationCityId', value),
      },
      /*
        LE FILTRE FINANCIER — point 1. Il ne remplace pas le filtre de statut,
        il s'y ajoute : « livré » et « pas encore facturé » se demandent
        ensemble. Pour un e-commerçant, SON axe (lot O, D-227) : Non encaissé,
        Encaissé, Facturé, Payé — jamais l'axe interne (D-060).
      */
      ...(isMerchant
        ? [
            {
              id: 'merchantFinancialStatus',
              multiple: true,
              label: t('filters.financial'),
              value: param('merchantFinancialStatus') ?? ALL_VALUE,
              options: MERCHANT_FINANCIAL_STATUSES.map((status) => ({
                value: status,
                label: tMerchantFinancial(status),
                color: MERCHANT_FINANCIAL_STATUS_COLORS[status],
              })),
              onChange: (value: string) => setParam('merchantFinancialStatus', value),
            },
          ]
        : [
            {
              id: 'financialStatus',
              multiple: true,
              label: t('filters.financial'),
              value: param('financialStatus') ?? ALL_VALUE,
              options: FINANCIAL_STATUSES.map((status) => ({
                value: status,
                label: tFinancial(status),
                color: FINANCIAL_STATUS_COLORS[status],
              })),
              onChange: (value: string) => setParam('financialStatus', value),
            },
          ]),
    ],
    [
      cities,
      clients,
      isMerchant,
      param,
      setParam,
      t,
      tFinancial,
      tMerchantFinancial,
      tGroup,
      tKind,
      tStatus,
    ],
  );

  /**
   * Les filtres de la liste AFFICHÉE, exactement ceux que la page passe à
   * `getParcels` (D-176) : les étiquettes de « toute la liste » sont celles
   * des lignes qu'on voit, ni plus ni moins.
   */
  const listFilters = (): Record<string, string | number | undefined> => ({
    // Lot D, D4 : la même lecture que la page — le statut financier compris.
    ...parcelListFilters(param),
    period: window.period,
    from: window.period === 'custom' ? window.from : undefined,
    to: window.period === 'custom' ? window.to : undefined,
  });

  const resetAll = () => {
    setSearch('');
    reset();
  };

  const columns = useParcelColumns({
    onChangeStatus: setStatusTarget,
    onPrintLabel: setLabelTarget,
  });

  const locked = statusTarget ? isLockedStatus(statusTarget.statusId) : false;

  return (
    <>
      {/* Lot D, D3 : « Préparation de X étiquettes… », puis « Télécharger ». */}
      <LabelJobsPanel jobs={labelJobs} />
      <DataTable
        /*
         * LA SÉLECTION ET LES E-TICKETS EN MASSE — V2 vendeur bloc 3.
         *
         * La sélection ne porte que la page affichée, et c'est dit dans le
         * composant partagé : promettre « les huit cents de la sélection
         * filtrée » demanderait d'aller chercher les identifiants côté
         * serveur, et l'utilisateur téléchargerait sans savoir quoi.
         */
        selection={{
          keyOf: (parcel) => parcel.code,
          selected,
          onChange: changeSelection,
          // « X cochés / Y » (D-176) : Y est la liste entière, toutes pages.
          total: page.total,
          count: selectedCount,
          actions: (
            <>
              {allMatching ? (
                <span className="text-muted-foreground text-xs">
                  {t('bulk.allSelected', { count: String(page.total) })}
                </span>
              ) : page.total > selected.length ? (
                <Button size="sm" variant="ghost" onClick={() => setAllMatching(true)}>
                  {t('bulk.selectAll', { count: String(page.total) })}
                </Button>
              ) : null}
              <Button size="sm" variant="outline" onClick={() => setBatchOpen(true)}>
                <Printer className="size-4" aria-hidden />
                {t('bulk.labels', { count: String(selectedCount) })}
              </Button>

              {/*
                « CHANGER LE STATUT » EN MASSE (D-245). La sélection part telle
                quelle, comme pour les bons : c'est le serveur qui juge chaque
                colis (statut de terrain, facture, argent, bon de retour).
              */}
              {canAdminFieldStatus ? (
                <Button size="sm" variant="outline" onClick={() => setFieldStatusOpen(true)}>
                  <ListChecks className="size-4" aria-hidden />
                  {tFieldStatus('bulkAction', { count: String(selectedCount) })}
                </Button>
              ) : null}

              {/*
                « TRANSFÉRER À UN LIVREUR » (D-246). Sur les COCHÉS de la page
                seulement : le transfert demande les colis un par un, pas un
                filtre. Avec « toute la liste », le bouton se retire.
              */}
              {canTransfer && statusCouriers && !allMatching && transferIds.length > 0 ? (
                <Button size="sm" variant="outline" onClick={() => setTransferOpen(true)}>
                  <ArrowRightLeft className="size-4" aria-hidden />
                  {tTransfer('toCourierCount', { count: String(transferIds.length) })}
                </Button>
              ) : null}

              {/*
                LES DEUX BONS — V2 vendeur bloc 4, spécification §5 et §6.

                La sélection part TELLE QUELLE : c'est le serveur qui retient
                les colis dans l'état attendu du genre de bon et qui écarte
                ceux d'un autre client. Filtrer ici ferait croire à une
                garantie que l'écran ne peut pas donner — et ferait diverger
                les deux règles au premier statut ajouté.
              */}
              {isMerchant ? (
                <>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={noting || allMatching}
                    onClick={() => note('pickup')}
                  >
                    <Truck className="size-4" aria-hidden />
                    {t('bulk.pickup')}
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={noting || allMatching}
                    onClick={() => note('dropoff')}
                  >
                    <ClipboardList className="size-4" aria-hidden />
                    {t('bulk.dropoff')}
                  </Button>
                </>
              ) : null}
            </>
          ),
        }}
        data={page.items}
        columns={columns}
        server={server}
        onRowClick={(parcel) => router.push(`/parcels/${parcel.code}`)}
        // L'adresse complète au survol de la ligne (chantier « Affichage », point 3) : servie avec la liste.
        getRowTitle={parcelAddressLine}
        kpis={
          <KpiGrid>
            <KpiCard
              label={t('kpi.total')}
              value={format.number(stats.total, 'integer')}
              icon={PackageCheck}
            />
            <KpiCard
              label={t('kpi.delivered')}
              value={format.number(stats.delivered, 'integer')}
              hint={t('kpi.deliveryRate', {
                rate: format.number(stats.deliveryRate, 'percent'),
              })}
              icon={PackageCheck}
              tone="var(--chart-4)"
            />
            <KpiCard
              label={t('kpi.inTransit')}
              value={format.number(stats.inTransit, 'integer')}
              icon={Truck}
              tone="var(--chart-3)"
            />
            <KpiCard
              label={t('kpi.collectedCod')}
              value={format.number(stats.collectedCod / 100, 'currency')}
              hint={t('kpi.failed', { count: stats.failed })}
              icon={stats.failed > 0 ? Wallet : CircleSlash}
              tone="var(--chart-2)"
            />
          </KpiGrid>
        }
        toolbar={
          <DataTableToolbar
            search={search}
            onSearchChange={setSearch}
            exportResource="parcels"
            exportQuery={
              window.period === 'custom'
                ? { period: window.period, from: window.from, to: window.to }
                : { period: window.period }
            }
            selects={selects}
            // D-257 : les colis supprimés, consultables par l'administrateur seul.
            highlight={
              canAdminFieldStatus
                ? {
                    label: t('deletedFilter'),
                    active: param('deleted') === 'true',
                    onToggle: (next) => setFilter('deleted', next ? 'true' : undefined),
                  }
                : undefined
            }
            onReset={resetAll}
          >
            <PeriodFilter
              memory="parcels"
              period={window.period}
              from={window.from}
              to={window.to}
            />
            {/* L'import est un ÉCRAN, pas un dialogue : relire quelques
                centaines de lignes et corriger les cellules fausses ne tient
                pas dans une fenêtre modale (C7 bloc 1). */}
            <Button asChild variant="outline" size="sm" className="gap-1.5">
              <Link href="/parcels/import">
                <Upload className="size-4" aria-hidden />
                <span className="hidden sm:inline">{t('import.action')}</span>
              </Link>
            </Button>
          </DataTableToolbar>
        }
      />

      {statusTarget && !locked ? (
        <ChangeStatusDialog
          code={statusTarget.code}
          currentStatusId={statusTarget.statusId}
          couriers={statusCouriers}
          parcelId={statusTarget.id}
          open
          onOpenChange={(next) => {
            if (!next) setStatusTarget(null);
          }}
        />
      ) : null}

      {statusTarget && locked ? (
        <DisputeDialog
          code={statusTarget.code}
          currentStatusId={statusTarget.statusId}
          couriers={statusCouriers}
          parcelId={statusTarget.id}
          open
          onOpenChange={(next) => {
            if (!next) setStatusTarget(null);
          }}
        />
      ) : null}

      {/*
        Monté à l'ouverture : chaque geste repart d'un formulaire vide. Les
        cochés, ou toute la liste avec LES FILTRES DE L'ÉCRAN (D-176), comme les
        étiquettes. La sélection n'est vidée qu'après un succès complet : un
        refus laisse les colis cochés, et le récapitulatif reste à lire.
      */}
      {/* Monté à l'ouverture, comme sur la page circuit : chaque geste repart de zéro. */}
      {transferOpen && statusCouriers ? (
        <ParcelTransferDialog
          parcelIds={transferIds}
          couriers={statusCouriers}
          // Depuis la liste, les colis viennent de partout : aucun livreur n'est écarté.
          currentCourierId={null}
          open
          onOpenChange={setTransferOpen}
          onDone={() => {
            setTransferOpen(false);
            changeSelection([]);
            router.refresh();
          }}
        />
      ) : null}

      {fieldStatusOpen ? (
        <AdminFieldStatusDialog
          open
          onOpenChange={setFieldStatusOpen}
          targets={adminFieldStatusIds()}
          selection={
            allMatching ? { all: true as const, filters: listFilters() } : { codes: selected }
          }
          count={selectedCount}
          onDone={() => changeSelection([])}
        />
      ) : null}

      {/*
        LA MÊME BOÎTE DE FORMAT que pour une étiquette seule : A4 ou 10×10
        (DOMAIN.md §3.5). En écrire une seconde aurait fait deux endroits où
        oublier un format le jour où un troisième arrive.
      */}
      {batchOpen ? (
        <PrintFormatDialog
          open
          onOpenChange={(next) => {
            if (!next) setBatchOpen(false);
          }}
          subject={
            selectedPages !== null && selectedPages > selectedCount
              ? t('bulk.labelsSubjectPages', {
                  count: String(selectedCount),
                  pages: String(selectedPages),
                })
              : t('bulk.labelsSubject', { count: String(selectedCount) })
          }
          pending={printing}
          format={labelFormat}
          onFormatChange={setLabelFormat}
          onConfirm={async () => {
            setPrinting(true);
            /*
             * D-176 : les cochés, ou toute la liste avec LES FILTRES DE L'ÉCRAN
             * — ceux de l'URL et la période — que le serveur rejoue dans la
             * portée du compte, dans l'ordre affiché.
             */
            const selection = allMatching
              ? { all: true as const, filters: listFilters() }
              : { codes: selected };

            /*
             * Lot D, D3 (D-190) : au-delà du seuil, un TRAVAIL. Le PDF se
             * prépare en arrière-plan ; le panneau dit « Préparation de X
             * étiquettes… » puis propose « Télécharger ».
             */
            if (selectedCount > LABELS_ASYNC_THRESHOLD) {
              const job = await createLabelJobAction(selection, labelFormat);
              setPrinting(false);

              if (!job.ok) {
                toast.error(job.error);
                return;
              }

              addLabelJob(job.data);
              toast.success(tLabelJobs('started'));
              setBatchOpen(false);
              changeSelection([]);
              return;
            }

            const result = await parcelLabelsAction(selection, labelFormat);
            setPrinting(false);

            if (!result.ok) {
              toast.error(result.error);
              return;
            }

            downloadBase64(result.data, `etiquettes-${selectedCount}.pdf`, 'application/pdf');
            setBatchOpen(false);
            /*
             * La sélection est vidée APRÈS le téléchargement, jamais avant :
             * un échec réseau ne doit pas obliger à tout recocher.
             */
            changeSelection([]);
          }}
        />
      ) : null}

      {labelTarget ? (
        <PrintFormatDialog
          open
          onOpenChange={(next) => {
            if (!next) setLabelTarget(null);
          }}
          subject={labelTarget.code}
          pending={printing}
          format={labelFormat}
          onFormatChange={setLabelFormat}
          onConfirm={async () => {
            setPrinting(true);
            const result = await parcelLabelAction(labelTarget.code, labelFormat);
            setPrinting(false);

            if (!result.ok) {
              toast.error(result.error);
              return;
            }

            downloadBase64(result.data, `${labelTarget.code}.pdf`, 'application/pdf');
            setLabelTarget(null);
          }}
        />
      ) : null}
    </>
  );
}
