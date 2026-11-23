import { findParcelStatus, isLockedStatus } from '@delivery/shared';
import type { Metadata } from 'next';
import { getFormatter, getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { BackButton } from '@/components/back-button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { BlCollectCard } from '@/features/courier/bl-collect-card';
import { ContactActions } from '@/features/courier/contact-actions';
import { OutcomeDialog } from '@/features/courier/outcome-dialog';
import { getParcelBl } from '@/lib/api/bl';
import { getCourierOutcomes } from '@/lib/api/courier';
import { getParcel } from '@/lib/api/parcels';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ code: string }>;
}): Promise<Metadata> {
  const { code } = await params;
  return { title: decodeURIComponent(code) };
}

/**
 * LA FICHE COLIS DU LIVREUR — L1 bloc 3.
 *
 * **Le miroir de la fiche mobile**, avec la même timeline et les mêmes trois
 * gestes du terrain : appeler, WhatsApp, itinéraire.
 *
 * **La portée serveur fait tout le travail de sécurité.** `GET /parcels/:code`
 * est borné à `courierId` : un livreur qui tape le code d'un colis qui n'est
 * pas le sien reçoit un 404, et l'écran affiche « ce colis ne vous est pas
 * confié » plutôt qu'une page blanche (**D-082**). Aucun filtre n'est posé ici
 * — un filtre d'écran serait une garantie de façade.
 *
 * **Les issues viennent du SERVEUR** (`GET /outcomes`), jamais d'une liste
 * écrite ici : la machine à états vit dans `packages/shared` et le filtre de
 * rôle dans l'API. Les recopier ferait une troisième table qui diverge au
 * premier ajustement, et un bouton qui échoue devant un client est pire que
 * pas de bouton.
 */
export default async function CourierParcelPage({ params }: { params: Promise<{ code: string }> }) {
  const { code: raw } = await params;
  const code = decodeURIComponent(raw);

  const parcel = await getParcel(code).catch(() => null);
  if (!parcel) notFound();

  const [t, tNav, tStatus, tPayment, format, outcomes] = await Promise.all([
    getTranslations('courier.parcel'),
    getTranslations('courier.nav'),
    getTranslations('status'),
    getTranslations('parcels.expectedPayment'),
    getFormatter(),
    // Best-effort : sans la liste, la fiche reste lisible et le bouton
    // disparaît. Une erreur ici n'a pas à emporter l'écran.
    getCourierOutcomes(code).catch(() => [] as number[]),
  ]);

  /*
   * LE BL — chantier « Retours, BL Clients Pro », point 5.
   *
   * Best-effort comme les issues : sans cette lecture, la fiche reste lisible
   * et le bandeau disparaît. Il ne s'affiche de toute façon que si le colis
   * demande un BL, ce qui n'arrive que pour un Client Pro (D-099).
   */
  const bl = await getParcelBl(code).catch(() => null);

  const definition = findParcelStatus(parcel.statusId);

  return (
    <div className="flex max-w-3xl flex-col gap-5">
      <div className="flex items-center gap-3">
        {/*
          La flèche RECULE — point 4. Elle renvoyait au premier écran de la
          liste : le livreur qui cherchait un code au milieu de sa tournée
          recommençait sa recherche à chaque fiche ouverte.
        */}
        <BackButton fallbackHref="/me/parcels" label={tNav('parcels')} iconOnly />
        <h1 className="font-mono text-lg font-semibold">{parcel.code}</h1>
        {definition ? (
          <Badge
            variant="outline"
            style={{ borderColor: definition.color, color: definition.color }}
          >
            {tStatus(definition.key)}
          </Badge>
        ) : null}
      </div>

      {bl ? <BlCollectCard code={parcel.code} tracking={bl} /> : null}

      {/* --- À qui, où, combien --- */}
      <Card>
        <CardContent className="flex flex-col gap-4 p-5">
          <div className="flex flex-col gap-1">
            <span className="text-muted-foreground text-xs">{t('recipient')}</span>
            <span className="text-lg font-semibold">{parcel.recipientName}</span>
            <span className="text-sm">
              {parcel.deliveryAddress}
              {parcel.destinationDistrictName ? ` · ${parcel.destinationDistrictName}` : ''}
              {` · ${parcel.destinationCityName}`}
            </span>
          </div>

          <ContactActions
            phone={parcel.recipientPhone}
            address={`${parcel.deliveryAddress} ${parcel.destinationCityName}`}
          />

          <div className="flex flex-col gap-1 border-t pt-4">
            <span className="text-muted-foreground text-xs">{t('collect')}</span>
            <span className="text-2xl font-bold tabular-nums">
              {format.number(parcel.codToCollect / 100, { style: 'currency', currency: 'MAD' })}
            </span>
            {/* Recette 3 point 3 : le paiement attendu d'un colis Pro, pour
                information — l'encaissement se saisit comme avant. */}
            {parcel.expectedPayment ? (
              <span className="text-sm font-medium">
                {tPayment('label')} : {tPayment(parcel.expectedPayment)}
              </span>
            ) : null}
          </div>
        </CardContent>
      </Card>

      {/*
        DEUX RAISONS DE N'AVOIR AUCUN BOUTON — retouches Anass, point 6.

        « Livré » posé, le statut est VERROUILLÉ côté livreur : seule l'agence
        peut y revenir, et elle doit motiver (DOMAIN.md §2.2). Le dire
        explicitement évite qu'un livreur cherche un bouton qui n'existera pas.
        Le silence, lui, ressemblait à un défaut d'affichage.
      */}
      {outcomes.length > 0 ? (
        <OutcomeDialog code={parcel.code} outcomes={outcomes} dueInCents={parcel.codToCollect} />
      ) : isLockedStatus(parcel.statusId) ? (
        <p className="bg-muted/60 text-muted-foreground rounded-md px-3 py-2 text-xs">
          {t('locked')}
        </p>
      ) : null}

      {/* --- L'historique, du plus récent au plus ancien --- */}
      <section className="flex flex-col gap-3">
        <h2 className="text-base font-semibold">{t('history')}</h2>

        <ol className="flex flex-col">
          {parcel.events.map((event, index) => {
            const target = findParcelStatus(event.statusId);
            const last = index === parcel.events.length - 1;

            return (
              <li key={event.id} className="flex gap-3">
                <span className="flex w-4 flex-col items-center">
                  <span
                    className="mt-1.5 size-2.5 shrink-0 rounded-full"
                    style={{ backgroundColor: target?.color ?? 'var(--muted-foreground)' }}
                  />
                  {last ? null : <span className="bg-border w-px flex-1" />}
                </span>

                <span className={`flex flex-col gap-0.5 ${last ? 'pb-1' : 'pb-5'}`}>
                  <span
                    className="text-sm font-medium"
                    style={index === 0 && target ? { color: target.color } : undefined}
                  >
                    {target ? tStatus(target.key) : String(event.statusId)}
                  </span>
                  <span className="text-muted-foreground text-xs">
                    {format.dateTime(new Date(event.createdAt), 'stamp')}
                    {event.authorName ? ` · ${event.authorName}` : ''}
                  </span>
                  {event.comment ? <span className="text-sm">{event.comment}</span> : null}
                </span>
              </li>
            );
          })}
        </ol>
      </section>
    </div>
  );
}
