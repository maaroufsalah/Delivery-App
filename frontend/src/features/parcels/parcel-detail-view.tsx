'use client';

import {
  type Agency,
  type City,
  type Courier,
  type District,
  DELIVERED,
  findParcelStatus,
  isDestinationLocked,
  isEditableBySeller,
  type ParcelDetail,
} from '@delivery/shared';
import { ExpectedPaymentRow } from '@/features/parcels/expected-payment-row';
import { SellerParcelActions } from '@/features/parcels/seller-parcel-actions';
import {
  Banknote,
  Box,
  Building2,
  FlameKindling,
  MapPin,
  MessageSquareWarning,
  Package,
  PackageOpen,
  Phone,
  Store,
  Truck,
  User,
} from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { BackButton } from '@/components/back-button';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { PrintButton } from '@/components/print-button';
import { parcelLabelAction } from '@/lib/actions/documents';
import { EntityHistory } from '@/features/audit/entity-history';
import { ParcelDestinationEditor } from '@/features/parcels/parcel-destination';
import { ParcelExchangeDialog } from '@/features/parcels/parcel-exchange-dialog';
import { ParcelExchangesCard } from '@/features/parcels/parcel-exchanges-card';
import { ParcelFeeHistory, ParcelFeesEditor } from '@/features/parcels/parcel-fees';
import { ParcelFinanceCard, ParcelRoutesCard } from '@/features/parcels/parcel-finance-card';
import { ParcelNotes } from '@/features/parcels/parcel-notes';
import { ParcelTimeline } from '@/features/parcels/parcel-timeline';
import { FinancialStatusBadge } from '@/features/parcels/financial-status-badge';
import { ParcelStatusBadge } from '@/features/parcels/status-badge';
import { StatusActionButtons } from '@/features/parcels/status-dialogs';
import { CancelDeliveryDialog } from '@/features/parcels/cancel-delivery-dialog';
import { AdminFieldStatusButton } from '@/features/parcels/admin-field-status-dialog';
import { StatusCorrectionButton } from '@/features/parcels/status-correction-dialog';
import {
  AdminDeleteParcelButton,
  ChangeClientButton,
} from '@/features/parcels/parcel-admin-actions';
import { ParcelTraceCard } from '@/features/parcels/parcel-trace-card';
import { ParcelTransferButton } from '@/features/routes/parcel-transfer-dialog';
import { Link } from '@/i18n/navigation';

/**
 * Détail d'un colis.
 *
 * Deux colonnes sur grand écran, empilé sous `lg`. À gauche l'état du colis
 * (destinataire, adresses, COD, contenu), à droite son histoire.
 */
export function ParcelDetailView({
  parcel,
  canEditFees,
  canChangeStatus,
  canChangeDestination,
  cities,
  districts = [],
  agencies,
  returnFee,
  internalView = true,
  sellerIsPro = false,
  canCancelDelivery = false,
  canAdminFieldStatus = false,
  canCorrectStatus = false,
  transferCouriers = null,
  statusCouriers = null,
  adminClients = null,
  readOnly = false,
}: {
  /** D-257 : colis supprimé — aucun geste, la fiche et son historique se lisent. */
  readOnly?: boolean;
  /**
   * D-256 : la liste des clients du « Changer de client », lue par la page
   * pour le SUPER_ADMIN seul. `null` : ni « Supprimer » ni « Changer de client ».
   */
  adminClients?: ReadonlyArray<{ id: string; name: string; isActive?: boolean }> | null;
  /**
   * D-246 : les livreurs du champ « Livreur » quand le bureau pose « En cours
   * de livraison ». Lus par la page (D-018) pour qui change les statuts.
   */
  statusCouriers?: Courier[] | null;
  parcel: ParcelDetail;
  /** `parcels.editFees` — C11 bloc 4, D-063. Séparé de `parcels.write`. */
  canEditFees: boolean;
  /**
   * LES DEUX GESTES DE TERRAIN — chantier « Correctifs avant démo », point 1.
   *
   * Ils portaient la permission seule, et l'espace vendeur — qui enveloppe ces
   * mêmes écrans (D-090) — affichait donc « Changer le statut » et « Changer la
   * destination » à l'e-commerçant. Un vendeur qui déclare lui-même « livré »
   * ce que personne n'a livré fabrique une facture.
   *
   * La page les calcule en croisant la permission ET l'absence de rattachement
   * client, exactement comme `NotMerchantGuard` côté API. L'écran ne propose
   * donc rien que le serveur refuserait — et le serveur refuse, même si
   * quelqu'un remet le bouton.
   */
  canChangeStatus: boolean;
  canChangeDestination: boolean;
  /** Les villes servies, pour le sélecteur de destination. */
  cities: City[];
  /** Les quartiers connus, suggérés dans le dialogue de destination. */
  districts?: District[];
  /**
   * ÉCHANGER — chantier « Étiquette orange », point 2.
   *
   * Les agences de retrait et les frais de retour, lus par la page : le
   * dialogue est un composant client, il ne peut pas aller les chercher
   * lui-même (D-018). Les frais viennent des réglages métier, par agence.
   */
  agencies: Agency[];
  returnFee: number;
  /**
   * Vue du comptoir (back-office) ou du vendeur — étanchéité vendeur,
   * point 2. Le vendeur ne voit ni le journal de la fiche (e-mails et rôles
   * du personnel) ni les auteurs du fil ; l'API les lui refuse aussi.
   */
  internalView?: boolean;
  /**
   * L'e-commerçant du colis est Client Pro (`isPro`) — recette 3, point 3.
   * Ouvre la ligne « Paiement attendu », et la question BL / paiement dans la
   * modification vendeur.
   */
  sellerIsPro?: boolean;
  /** « Annuler la livraison » — lot I, I2 (D-210) : SUPER_ADMIN, colis livré. */
  canCancelDelivery?: boolean;
  /** « Changer le statut (terrain) » (D-245) : SUPER_ADMIN, l'API refuse tout autre compte. */
  canAdminFieldStatus?: boolean;
  /** « Corriger le statut » (D-250) : SUPER_ADMIN avec `parcels.correctStatus`. */
  canCorrectStatus?: boolean;
  /**
   * « Transférer » — lot I, I1 (D-211) : les livreurs proposés, lus par la page
   * (D-018) quand le colis est sur un circuit de livraison ouvert et que le
   * compte compose les circuits. `null` : pas de bouton.
   */
  transferCouriers?: Courier[] | null;
}) {
  const t = useTranslations('parcels.detail');
  const tGroup = useTranslations('statusGroup');
  const format = useFormatter();

  const status = findParcelStatus(parcel.statusId);

  /*
   * « 20 MAD × 4 = 80 MAD » — lot G, D-208. Le frais stocké est le TOTAL ;
   * l'unitaire s'en déduit. Un frais retouché à la main après ramassage
   * (« Modifier les frais ») peut ne plus se diviser : on montre alors le
   * total et le nombre de cartons, sans inventer d'unitaire.
   */
  const quantity = parcel.quantity ?? 1;
  const money = (cents: number) => format.number(cents / 100, 'currency');
  const feeLabel =
    quantity <= 1
      ? money(parcel.deliveryFee)
      : parcel.deliveryFee % quantity === 0
        ? t('cod.deliveryFeeByCartons', {
            unit: money(parcel.deliveryFee / quantity),
            quantity: String(quantity),
            total: money(parcel.deliveryFee),
          })
        : t('cod.deliveryFeeCartons', {
            total: money(parcel.deliveryFee),
            quantity: String(quantity),
          });

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-4">
        {/*
          « RETOUR » RECULE — chantier « Bon de retour livreur », point 4.

          Le lien vers la liste renvoyait à son PREMIER écran : l'agent qui
          venait de filtrer trois mille colis, page 7, recommençait. Les
          filtres, la recherche, la page et le tri vivent dans l'URL — reculer
          les rend intacts. Le `fallbackHref` ne sert qu'aux liens ouverts de
          nulle part.
        */}
        <BackButton fallbackHref="/parcels" label={t('backToList')} />

        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0">
            <h1 className="font-mono text-xl font-semibold tracking-tight sm:text-2xl">
              {parcel.code}
            </h1>
            <div className="mt-2 flex flex-wrap items-center gap-2">
              <ParcelStatusBadge statusId={parcel.statusId} />
              {/*
                LES DEUX AXES CÔTE À CÔTE — point 1, D-105. Le carton et son
                argent, dans cet ordre, et jamais fondus en un seul badge : la
                fiche doit pouvoir dire « livré » et « pas encore facturé » sans
                que l'un des deux ait à céder la place.

                L'e-commerçant lit SON axe (lot O, D-227) — Facturé, Payé… —
                et le code de la facture, déjà servi par la fiche.
              */}
              <FinancialStatusBadge
                status={parcel.financialStatus}
                merchantStatus={parcel.merchantFinancialStatus}
              />
              {(parcel.merchantFinancialStatus === 'facture' ||
                parcel.merchantFinancialStatus === 'paye') &&
              parcel.invoicedOn ? (
                <span className="text-muted-foreground font-mono text-xs">
                  {t('invoiceCode', { code: parcel.invoicedOn })}
                </span>
              ) : null}
              {status ? (
                <Badge variant="secondary" className="rounded-full font-normal">
                  {tGroup(status.group)}
                </Badge>
              ) : null}
              {parcel.externalCode ? (
                <Badge variant="outline" className="rounded-full font-mono font-normal">
                  {parcel.externalCode}
                </Badge>
              ) : null}
              <span className="text-muted-foreground text-xs tabular-nums">
                {t('updatedAt', {
                  date: format.dateTime(new Date(parcel.statusAt), 'stamp'),
                })}
              </span>
            </div>
          </div>

          {readOnly ? null : (
          <div className="flex flex-wrap items-center gap-2">
            {canChangeStatus ? (
              <StatusActionButtons
                code={parcel.code}
                currentStatusId={parcel.statusId}
                couriers={statusCouriers}
                parcelId={parcel.id}
                canCorrectStatus={canCorrectStatus}
              />
            ) : null}
            {/*
              « STATUT LIBRE POUR L'ADMIN » (D-245) : à côté de la machine à
              états du bureau, qui ne change pas. Le bouton se retire de
              lui-même quand le statut n'est pas un statut de terrain.
            */}
            {canAdminFieldStatus ? (
              <AdminFieldStatusButton code={parcel.code} statusId={parcel.statusId} />
            ) : null}
            {canCancelDelivery && parcel.statusId === DELIVERED ? (
              <CancelDeliveryDialog code={parcel.code} />
            ) : null}
            {/* « CORRIGER LE STATUT » (D-250) : vers n'importe quel statut, rattachements alignés. */}
            {canCorrectStatus ? (
              <StatusCorrectionButton code={parcel.code} statusId={parcel.statusId} />
            ) : null}
            {/*
              D-256 : l'administrateur change le client (tant que l'argent n'a
              pas bougé, le serveur décide) et supprime un colis pas encore pris
              en charge, comme le vendeur.
            */}
            {adminClients && parcel.kind === 'parcel' ? (
              <ChangeClientButton
                code={parcel.code}
                clientId={parcel.clientId}
                clients={adminClients}
              />
            ) : null}
            {/*
              21/11/2026 : un colis d'échange (ECH-) se supprime aussi ; le
              serveur emporte son RET- et annule le dossier, ou refuse en le
              nommant.
            */}
            {adminClients &&
            (parcel.kind === 'parcel' || parcel.kind === 'exchange') &&
            isEditableBySeller(parcel.statusId) ? (
              <AdminDeleteParcelButton code={parcel.code} />
            ) : null}
            {/*
              « ÉCHANGER » — point 2, SUR UN COLIS LIVRÉ UNIQUEMENT.
              Admin et vendeur : c'est une demande du vendeur, pas une décision
              d'agence. L'API rend 422 sur tout autre statut.
            */}
            {parcel.statusId === DELIVERED ? (
              <ParcelExchangeDialog
                parcel={parcel}
                agencies={agencies}
                cities={cities}
                returnFee={returnFee}
              />
            ) : null}
            {/*
              MODIFIER / SUPPRIMER — recette 3, point 3. Le vendeur, sur SON
              colis, tant qu'il n'est pas ramassé ; l'API rend 422 après.
            */}
            {!internalView && isEditableBySeller(parcel.statusId) && parcel.kind === 'parcel' ? (
              <SellerParcelActions parcel={parcel} cities={cities} isPro={sellerIsPro} />
            ) : null}
            <PrintButton
              kind="label"
              subject={parcel.code}
              label={t('label')}
              onPrint={(format) => parcelLabelAction(parcel.code, format)}
            />
            {/* Réclamer DEPUIS le colis : le formulaire s'ouvre avec le code
                déjà posé. Recopier quinze caractères à la main est le genre de
                friction qui fait qu'on ne réclame pas (C7 bloc 2). */}
            <Button asChild variant="outline" size="sm" className="gap-1.5">
              <Link href={`/claims/new?parcel=${encodeURIComponent(parcel.code)}`}>
                <MessageSquareWarning className="size-4" aria-hidden />
                <span className="hidden sm:inline">{t('openClaim')}</span>
              </Link>
            </Button>
          </div>
          )}
        </div>
        {/* D-257 : la fiche d'un colis supprimé, en lecture seule. */}
        {readOnly && parcel.deletedAt ? (
          <p className="border-destructive/40 bg-destructive/10 text-destructive rounded-md border px-3 py-2 text-sm">
            {t('deletedBanner', { date: format.dateTime(new Date(parcel.deletedAt), 'stamp') })}
          </p>
        ) : null}
      </header>

      {internalView ? <ParcelTraceCard changes={parcel.fieldChanges} /> : null}

      <div className="grid gap-6 lg:grid-cols-2">
        <div className="flex flex-col gap-6">
          <Card>
            <CardHeader>
              <CardTitle>{t('recipient.title')}</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-4">
              <Field icon={User} label={t('recipient.name')} value={parcel.recipientName} />
              <Field icon={Phone} label={t('recipient.phone')} value={parcel.recipientPhone} mono />

              <Separator />

              <Field
                icon={Store}
                label={t('addresses.pickup')}
                value={parcel.pickupAddress}
                sub={parcel.originCityName}
              />
              <Field
                icon={MapPin}
                label={t('addresses.delivery')}
                value={parcel.deliveryAddress}
                sub={[parcel.destinationDistrictName, parcel.destinationCityName]
                  .filter(Boolean)
                  .join(' · ')}
              />

              {/*
                CHANGER LA DESTINATION — chantier « Finance », point 7. Le
                bouton vit SOUS l'adresse : c'est là qu'on lit ce qu'on veut
                corriger, et le chercher en haut de page aurait été une
                devinette.
              */}
              <ParcelDestinationEditor
                parcel={parcel}
                cities={cities}
                districts={districts}
                /*
                  LA DESTINATION EST VERROUILLÉE — chantier « Étiquette orange »,
                  point 3. Livré, Retourné, Annulé, clôturé : il n'y a plus de
                  livraison à rediriger, et l'API le refuse en 422. Le bouton
                  disparaît plutôt que de s'afficher pour rendre une erreur.
                */
                canEdit={canChangeDestination && !isDestinationLocked(parcel.statusId)}
              />

              <Separator />

              <div className="grid grid-cols-2 gap-4">
                <Field icon={Building2} label={t('agency')} value={parcel.agencyName} />
                <Field
                  icon={Truck}
                  label={t('courier')}
                  value={parcel.courierName ?? t('noCourier')}
                />
              </div>
              {/* TRANSFÉRER — lot I, I1 : sous le livreur, c'est lui qu'on change. */}
              {transferCouriers ? (
                <div>
                  <ParcelTransferButton
                    parcelId={parcel.id}
                    couriers={transferCouriers}
                    currentCourierId={parcel.courierId}
                    // D-246 : hors de tout circuit ouvert, « Transférer à un livreur ».
                    offRoute={!parcel.routes.some((route) => route.status !== 'closed')}
                  />
                </div>
              ) : null}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Banknote className="size-4" aria-hidden />
                {t('cod.title')}
              </CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-4">
              {/* DOMAIN.md §3.8 : le montant à encaisser N'EST PAS `codAmount`. */}
              {/* SC-005 (lot W3, D-238) : livré et encaissé, la carte dit « Encaissé ». */}
              <div className="bg-accent text-accent-foreground flex items-baseline justify-between gap-3 rounded-lg px-4 py-3">
                <span className="text-sm font-medium">
                  {parcel.collected ? t('cod.collected') : t('cod.toCollect')}
                </span>
                <span className="text-xl font-semibold tabular-nums">
                  {format.number((parcel.collected?.amount ?? parcel.codToCollect) / 100, 'currency')}
                </span>
              </div>

              <dl className="grid gap-2 text-sm">
                {parcel.collected && parcel.collected.cheque > 0 ? (
                  <>
                    <Row
                      label={t('cod.collectedCash')}
                      value={format.number(parcel.collected.cash / 100, 'currency')}
                    />
                    <Row
                      label={t('cod.collectedCheque')}
                      value={format.number(parcel.collected.cheque / 100, 'currency')}
                    />
                  </>
                ) : null}
                <Row
                  label={t('cod.price')}
                  value={format.number(parcel.codAmount / 100, 'currency')}
                />
                <Row label={t('cod.deliveryFee')} value={feeLabel} />
                {parcel.failureFee !== null ? (
                  <Row
                    label={t('cod.failureFee')}
                    value={format.number(parcel.failureFee / 100, 'currency')}
                  />
                ) : null}
              </dl>

              {/* Recette 3 point 3 : colis Pro seulement (ou déjà renseigné). */}
              {sellerIsPro || parcel.expectedPayment ? (
                <ExpectedPaymentRow
                  code={parcel.code}
                  value={parcel.expectedPayment}
                  canChange={internalView && canChangeDestination}
                />
              ) : null}

              <div className="flex flex-wrap gap-2">
                <Flag on={parcel.feeIncluded} label={t('cod.feeIncluded')} />
                <Flag on={parcel.alreadyPaid} label={t('cod.alreadyPaid')} />
                <Flag on={parcel.pickupWaived} label={t('cod.pickupWaived')} />
              </div>

              <p className="text-muted-foreground text-xs">
                {parcel.feeIncluded ? t('cod.explainIncluded') : t('cod.explainExcluded')}
              </p>

              {/* C11 bloc 4 — les frais se corrigent, et la correction se lit. */}
              <ParcelFeesEditor parcel={parcel} canEdit={canEditFees} />
              <ParcelFeeHistory changes={parcel.feeChanges} />
            </CardContent>
          </Card>

          {/*
            LA VUE UNIQUE — chantier « Finance », point 11, D-113.

            Sous la carte des montants, parce qu'on lit d'abord ce qu'il
            fallait encaisser et ensuite ce qui l'a été. Les deux cartes ne
            se recouvrent pas : celle du dessus est le CONTRAT du colis,
            celle-ci son exécution.

            Aucune des deux ne porte de statut logistique : le carton a son
            badge, en tête de fiche, et il n'en a qu'un (D-105).
          */}
          <ParcelFinanceCard finance={parcel.finance} />
          <ParcelRoutesCard routes={parcel.routes} />

          {/*
            LA RELATION NAVIGABLE — chantier « Finance », point 6. Depuis ce
            colis, on atteint le dossier d'échange et l'autre carton. La carte
            disparaît quand il n'y a rien : une carte vide sur toutes les fiches
            aurait dilué celles qui portent un dossier.
          */}
          <ParcelExchangesCard exchanges={parcel.exchanges} />

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Box className="size-4" aria-hidden />
                {t('content.title')}
              </CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-4">
              {/*
                Deux natures de contenu, pas deux affichages concurrents :
                - un colis saisi à la main n'a que `designation`, un texte libre
                  (DOMAIN.md §1.9 — le legacy n'avait rien d'autre) ;
                - un colis né d'une commande d'entrepôt porte ses LIGNES, avec
                  leur référence et leur quantité (C5). Elles étaient écrites
                  depuis C5 et n'étaient lues nulle part : l'agent ouvrait la
                  fiche d'un colis dont il ne pouvait pas dire ce qu'il contenait.
              */}
              {parcel.items.length > 0 ? (
                <ul className="flex flex-col gap-2">
                  {parcel.items.map((item) => (
                    <li
                      key={item.productId}
                      className="flex items-start gap-3 rounded-lg border p-3"
                    >
                      <span className="bg-muted flex size-9 shrink-0 items-center justify-center rounded-md">
                        <Package className="size-4" aria-hidden />
                      </span>
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-medium">{item.productName}</p>
                        {item.productReference ? (
                          <p className="text-muted-foreground font-mono text-xs">
                            {item.productReference}
                          </p>
                        ) : null}
                      </div>
                      <span className="shrink-0 text-sm font-medium tabular-nums">
                        × {item.quantity}
                      </span>
                    </li>
                  ))}
                </ul>
              ) : (
                <div className="flex items-start gap-3 rounded-lg border p-3">
                  <span className="bg-muted flex size-9 shrink-0 items-center justify-center rounded-md">
                    <Package className="size-4" aria-hidden />
                  </span>
                  <div className="min-w-0">
                    <p className="text-sm font-medium">{parcel.description}</p>
                    <p className="text-muted-foreground text-xs">
                      {t('content.quantity', { count: 1 })}
                    </p>
                  </div>
                </div>
              )}

              {parcel.items.length > 0 && parcel.description ? (
                <p className="text-muted-foreground text-xs">{parcel.description}</p>
              ) : null}

              <div className="flex flex-wrap gap-2">
                {parcel.fragile ? (
                  <Badge variant="outline" className="gap-1.5 rounded-full font-normal">
                    <FlameKindling className="size-3" aria-hidden />
                    {t('content.fragile')}
                  </Badge>
                ) : null}
                {parcel.openOnDelivery ? (
                  <Badge variant="outline" className="gap-1.5 rounded-full font-normal">
                    <PackageOpen className="size-3" aria-hidden />
                    {t('content.openOnDelivery')}
                  </Badge>
                ) : null}
                {parcel.scheduledAt ? (
                  <Badge variant="outline" className="rounded-full font-normal">
                    {t('content.scheduledAt', {
                      date: format.dateTime(new Date(parcel.scheduledAt), 'short'),
                    })}
                  </Badge>
                ) : null}
              </div>
            </CardContent>
          </Card>
        </div>

        <ParcelTimeline events={parcel.events} showAuthors={internalView} />
      </div>

      {/*
        Lot V, V2 : l'API ne rend aucune note au vendeur et lui refuse l'écriture
        (`NotMerchantGuard`) ; le bloc « Notes internes », vide, n'avait rien à lui dire.
      */}
      {internalView ? <ParcelNotes code={parcel.code} notes={parcel.notes} /> : null}

      {/*
        C11 bloc 5 (D-064) : une CARTE en bas de fiche, et non un onglet.
        Les quatre fiches concernées n'ont pas la même structure — deux
        colonnes ici, des onglets ailleurs, un formulaire plein là — et une
        carte se pose pareil sur les quatre. L'historique se lit au même
        endroit d'un écran à l'autre, ce qui vaut mieux qu'un onglet ici et
        une carte là.
      */}
      {internalView ? <EntityHistory entity="Parcel" entityId={parcel.id} /> : null}
    </div>
  );
}

function Field({
  icon: Icon,
  label,
  value,
  sub,
  mono,
}: {
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  value: string;
  sub?: string;
  mono?: boolean;
}) {
  return (
    <div className="flex items-start gap-3">
      <Icon className="text-muted-foreground mt-0.5 size-4 shrink-0" aria-hidden />
      <div className="min-w-0">
        <p className="text-muted-foreground text-xs">{label}</p>
        <p className={mono ? 'font-mono text-sm' : 'text-sm'}>{value}</p>
        {sub ? <p className="text-muted-foreground text-xs">{sub}</p> : null}
      </div>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="tabular-nums">{value}</dd>
    </div>
  );
}

/*
 * Un indicateur ÉTEINT ne s'affiche pas (D-169, Anass 11/10) : le badge en
 * contour se lisait « posé », et « Déjà payé » semblait vrai sur des colis où
 * il ne l'était pas.
 */
function Flag({ on, label }: { on: boolean; label: string }) {
  if (!on) return null;
  return (
    <Badge variant="default" className="rounded-full font-normal">
      {label}
    </Badge>
  );
}
