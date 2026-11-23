import { findParcelStatus, pickupNoteState, RETURN_HANDOVER_STATUS_COLORS } from '@delivery/shared';
import { FileText, History, PackageX, Receipt, Undo2 } from 'lucide-react';
import type { Metadata } from 'next';
import { getFormatter, getTranslations } from 'next-intl/server';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';
import { CourierHandoverFab } from '@/features/courier/courier-handover-fab';
import { getCourierCheques, getCourierNotes } from '@/lib/api/courier';
import { getHandoverPage, getReturnsPending } from '@/lib/api/returns';
import { cn } from '@/lib/utils';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courier.nav');
  return { title: t('returns') };
}

type Volet = 'parcels' | 'handovers' | 'cheques' | 'notes' | 'history';

const VOLETS: Volet[] = ['parcels', 'handovers', 'cheques', 'notes', 'history'];

/**
 * LES RETOURS, EN TROIS VOLETS — L2 bloc 9, maquettes 8 et 9, le miroir de
 * l'écran mobile.
 *
 * **Ce que l'écran montre, et ce qu'il ne fait pas.** Il liste ce que le
 * livreur rapporte à l'agence en fin de journée — les colis qui repartent, les
 * chèques, les bons de livraison signés. Il ne CRÉE pas de bon de retour : ceux
 * -ci se clôturent par le bon signé du client et sont traités au back-office
 * (C3, **D-024**). Même frontière que sur le téléphone.
 *
 * **Trois sources, trois états de déploiement, et l'écran le dit.** Les colis
 * viennent de `GET /returns/pending`, qui tourne aujourd'hui — le livreur a
 * `returns.read` et la portée le borne aux siens (D-082). Les chèques et les
 * bons viennent des endpoints du chantier L2, que la production n'a pas : leurs
 * volets affichent un état vide qui le dit, jamais une erreur.
 *
 * **Le volet passe par l'URL**, comme les onglets des ramassages : une page
 * qu'on met en favori, et qui revient au bouton retour.
 */
export default async function CourierReturnsPage({
  searchParams,
}: {
  searchParams: Promise<{ tab?: string }>;
}) {
  const { tab: raw } = await searchParams;
  const volet: Volet = VOLETS.includes(raw as Volet) ? (raw as Volet) : 'parcels';

  const [t, tNav, tStatus, tParcels, tPickups, tHandover, tHandoverLabels, format] =
    await Promise.all([
      getTranslations('courier.returns'),
      getTranslations('courier.nav'),
      getTranslations('status'),
      getTranslations('courier.parcels'),
      getTranslations('courier.pickups'),
      getTranslations('returns.handovers.status'),
      getTranslations('returns.handovers'),
      getFormatter(),
    ]);

  /*
   * Les trois lectures partent ENSEMBLE, même si un seul volet s'affiche : les
   * compteurs des onglets viennent des trois, et un livreur doit voir qu'il a
   * deux chèques en poche sans avoir à cliquer pour le découvrir.
   */
  const [groups, cheques, notes, handovers, closedHandovers] = await Promise.all([
    getReturnsPending().catch(() => []),
    getCourierCheques().catch(() => null),
    getCourierNotes('dropoff').catch(() => null),
    /*
     * SES bons de retour — point 3. Aucun `courierId` n'est passé : la portée
     * du jeton borne déjà `/return-handovers` au livreur de la session
     * (`handoverWhere`), et le passer ici laisserait croire que l'écran choisit
     * ce qu'il a le droit de voir.
     */
    getHandoverPage({ page: 1, pageSize: 50, order: 'desc' }).catch(() => null),
    getHandoverPage({ page: 1, pageSize: 50, order: 'desc', status: 'closed' }).catch(() => null),
  ]);

  const parcels = groups.flatMap((group) => group.parcels);
  /*
   * CE QUI ATTEND ENCORE QUELQUE CHOSE DE LUI — chantier « Bon de retour
   * livreur », point 2 : « un bon reçu quitte la vue En attente ».
   *
   * Un bon CONFIRMÉ est entre les mains de l'agence : le livreur n'a plus rien
   * à en faire, et le laisser dans l'onglet lui ferait croire qu'on l'attend.
   * Il rejoint l'historique, avec les clôturés et les annulés.
   */
  const openHandovers = (handovers?.items ?? []).filter(
    (handover) =>
      handover.status !== 'confirmed' &&
      handover.status !== 'closed' &&
      handover.status !== 'cancelled',
  );

  const counts: Record<Volet, number> = {
    parcels: parcels.length,
    handovers: openHandovers.length,
    cheques: cheques?.length ?? 0,
    notes: notes?.length ?? 0,
    history: closedHandovers?.items.length ?? 0,
  };

  const money = (cents: number) =>
    format.number(cents / 100, { style: 'currency', currency: 'MAD' });

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold tracking-tight">{tNav('returns')}</h1>

      <nav className="flex flex-wrap gap-2" aria-label={tNav('returns')}>
        {VOLETS.map((key) => (
          <Link
            key={key}
            href={key === 'parcels' ? '/me/returns' : `/me/returns?tab=${key}`}
            aria-current={volet === key ? 'page' : undefined}
            className={cn(
              'focus-visible:ring-ring flex min-h-10 items-center gap-1.5 rounded-full border px-4 text-sm transition-colors outline-none focus-visible:ring-2',
              volet === key
                ? 'border-primary bg-primary text-primary-foreground font-medium'
                : 'hover:bg-muted text-muted-foreground',
            )}
          >
            {t(key)}
            <span className="tabular-nums opacity-70">{counts[key]}</span>
          </Link>
        ))}
      </nav>

      {/* --- VOLET 1 : les colis qui repartent, maquette 8 --------------- */}
      {volet === 'parcels' ? (
        parcels.length === 0 ? (
          <Empty icon={PackageX} title={t('noParcels')} hint={t('noParcelsHint')} />
        ) : (
          <ul className="flex flex-col gap-2">
            {parcels.map((parcel) => {
              const definition = findParcelStatus(parcel.statusId);

              return (
                <li key={parcel.id}>
                  <Card>
                    <CardContent className="flex items-center gap-3 p-4">
                      <span className="min-w-0 flex-1">
                        <Link
                          href={`/me/parcels/${parcel.code}`}
                          className="block truncate font-mono text-sm font-medium"
                        >
                          {parcel.code}
                        </Link>
                        {/*
                          Le « motif » de la maquette est le STATUT du colis :
                          « client absent », « refus client » sont des statuts
                          du référentiel, pas un champ libre. Une seule vérité,
                          et la couleur avec.
                        */}
                        <span className="text-muted-foreground block truncate text-xs">
                          {t('reason')} : {definition ? tStatus(definition.key) : '—'} ·{' '}
                          {parcel.recipientName} · {parcel.destinationCityName}
                        </span>
                      </span>

                      {definition ? (
                        <Badge
                          variant="outline"
                          style={{ borderColor: definition.color, color: definition.color }}
                        >
                          {tStatus(definition.key)}
                        </Badge>
                      ) : null}
                    </CardContent>
                  </Card>
                </li>
              );
            })}
          </ul>
        )
      ) : null}

      {/* --- VOLET 2 : les chèques, maquette 9 --------------------------- */}
      {volet === 'cheques' ? (
        !cheques || cheques.length === 0 ? (
          <Empty
            icon={Receipt}
            title={t(cheques ? 'noCheques' : 'chequesUnavailable')}
            hint={t(cheques ? 'noChequesHint' : 'chequesUnavailableHint')}
          />
        ) : (
          <ul className="flex flex-col gap-2">
            {cheques.map((cheque) => (
              <li key={cheque.id}>
                <Card>
                  <CardContent className="flex flex-col gap-1.5 p-4">
                    <div className="flex items-center justify-between gap-3">
                      <span className="font-mono text-sm font-semibold">
                        {cheque.statementCode}
                      </span>
                      <Badge variant={cheque.handedOver ? 'secondary' : 'outline'}>
                        {t(cheque.handedOver ? 'handedOver' : 'toHandOver')}
                      </Badge>
                    </div>

                    <span className="text-muted-foreground text-sm">
                      {t('chequeNumber')} : {cheque.number || '—'} · {cheque.bank ?? '—'}
                    </span>
                    <span className="text-sm font-semibold tabular-nums">
                      {t('amount')} : {money(cheque.amountInCents)}
                    </span>
                  </CardContent>
                </Card>
              </li>
            ))}
          </ul>
        )
      ) : null}

      {/* --- VOLET 3 : les bons de livraison, spec §5C ------------------- */}
      {volet === 'notes' ? (
        !notes || notes.length === 0 ? (
          <Empty
            icon={FileText}
            title={t(notes ? 'noNotes' : 'notesUnavailable')}
            hint={t(notes ? 'noNotesHint' : 'notesUnavailableHint')}
          />
        ) : (
          <ul className="flex flex-col gap-2">
            {notes.map((note) => {
              const state = pickupNoteState(note);

              return (
                <li key={note.id}>
                  <Card>
                    <CardContent className="flex flex-col gap-1.5 p-4">
                      <div className="flex items-center justify-between gap-3">
                        <span className="font-mono text-sm font-semibold">{note.code}</span>
                        <Badge variant={state === 'done' ? 'secondary' : 'outline'}>
                          {tPickups(state)}
                        </Badge>
                      </div>

                      <span className="text-muted-foreground text-sm">
                        {tParcels('client')} : {note.clientName}
                      </span>
                      <span className="text-muted-foreground text-sm">
                        {tParcels('addressLabel')} : {note.address}, {note.cityName}
                      </span>
                    </CardContent>
                  </Card>
                </li>
              );
            })}
          </ul>
        )
      ) : null}
      {/* --- VOLET 4 : ses bons de retour, point 3 ---------------------- */}
      {volet === 'handovers' ? (
        openHandovers.length === 0 ? (
          <Empty icon={Undo2} title={t('noHandovers')} hint={t('noHandoversHint')} />
        ) : (
          <ul className="flex flex-col gap-2">
            {openHandovers.map((handover) => (
              <li key={handover.id}>
                <Card>
                  <CardContent className="flex flex-col gap-1.5 p-4">
                    <div className="flex items-center justify-between gap-3">
                      {/*
                        SON écran, pas celui du back-office. Le lien pointait
                        vers `/returns/handovers/:code`, qui vit dans la coque
                        de l'agence : le garde de rôle renvoyait le livreur
                        chez lui sans rien expliquer.
                      */}
                      <Link
                        href={`/me/returns/${handover.code}`}
                        className="font-mono text-sm font-semibold"
                      >
                        {handover.code}
                      </Link>
                      <Badge
                        variant="outline"
                        style={{
                          borderColor: RETURN_HANDOVER_STATUS_COLORS[handover.status],
                          color: RETURN_HANDOVER_STATUS_COLORS[handover.status],
                        }}
                      >
                        {tHandover(handover.status)}
                      </Badge>
                    </div>

                    {/*
                      « Le bon apparaît immédiatement chez le livreur avec la
                      mention Créé par l'admin » — point 2. C'est cette mention
                      qui lui dit qu'il exécute une demande, et non sa propre
                      liste.
                    */}
                    {/*
                      L'ORIGINE, en clair — point 1 : « Créé par l'admin » /
                      « Créé par moi ». Les deux branches rendaient le même
                      libellé, ce qui revenait à ne rien dire : le livreur ne
                      savait pas s'il exécutait une demande ou relisait sa
                      propre liste.
                    */}
                    <span className="text-muted-foreground text-sm">
                      {handover.origin === 'admin'
                        ? tHandoverLabels('createdByAdmin')
                        : tHandoverLabels('createdByMe')}{' '}
                      · {t('count', { count: handover.parcelCount })} · {handover.agencyName}
                    </span>

                    {handover.clientNames.length > 0 ? (
                      <span className="text-muted-foreground truncate text-xs">
                        {handover.clientNames.join(', ')}
                      </span>
                    ) : null}
                  </CardContent>
                </Card>
              </li>
            ))}
          </ul>
        )
      ) : null}

      {/*
        LE BOUTON FLOTTANT — chantier « Bon de retour livreur », point 1.
        Seulement sur l'onglet des bons : ailleurs, il proposerait une action
        sans rapport avec ce qu'on regarde.
      */}
      {volet === 'handovers' ? <CourierHandoverFab /> : null}

      {/*
        --- VOLET 5 : l'historique des bons de retour, lecture seule ---

        Les circuits passés n'y sont plus — Recette 5, point 2, validé par
        Anass : le livreur ne voit que ses circuits OUVERTS ; l'historique des
        circuits reste à l'admin. Sans circuits, le filtre de période n'avait
        plus rien à borner : il est retiré.
      */}
      {volet === 'history' ? (
        <div className="flex flex-col gap-4">
          <div className="flex">
            <Badge variant="secondary" className="ms-auto rounded-full font-normal">
              {t('readOnly')}
            </Badge>
          </div>

          {(closedHandovers?.items.length ?? 0) === 0 ? (
            <Empty icon={History} title={t('noHistory')} hint={t('noHistoryHint')} />
          ) : (
            <>
              {(closedHandovers?.items.length ?? 0) > 0 ? (
                <section className="flex flex-col gap-2">
                  <h2 className="text-muted-foreground text-xs font-medium">
                    {t('closedHandovers')}
                  </h2>
                  {(closedHandovers?.items ?? []).map((handover) => (
                    <Card key={handover.id}>
                      <CardContent className="flex items-center justify-between gap-3 p-4">
                        <span className="min-w-0">
                          <span className="block truncate font-mono text-sm font-medium">
                            {handover.code}
                          </span>
                          <span className="text-muted-foreground block truncate text-xs">
                            {handover.parcelCount} · {handover.agencyName}
                          </span>
                        </span>
                        <span className="text-muted-foreground shrink-0 text-xs tabular-nums">
                          {handover.closedAt
                            ? format.dateTime(new Date(handover.closedAt), 'stamp')
                            : ''}
                        </span>
                      </CardContent>
                    </Card>
                  ))}
                </section>
              ) : null}
            </>
          )}
        </div>
      ) : null}
    </div>
  );
}

/** Un volet vide : ce qui manque, et pourquoi — jamais une erreur technique. */
function Empty({ icon: Icon, title, hint }: { icon: typeof Receipt; title: string; hint: string }) {
  return (
    <Card>
      <CardContent className="text-muted-foreground flex flex-col items-center gap-3 p-10 text-center text-sm">
        <Icon className="size-10 opacity-40" aria-hidden />
        <span className="font-medium">{title}</span>
        {hint}
      </CardContent>
    </Card>
  );
}
