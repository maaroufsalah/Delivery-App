import { CHEQUE_STATUS_COLORS, type ChequeDetail } from '@delivery/shared';
import { getFormatter, getTranslations } from 'next-intl/server';
import { BackButton } from '@/components/back-button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';
import { ChequeSlipButton } from './cheque-slip-button';

/**
 * FICHE D'UN CHÈQUE — chantier « Correctifs avant démo », point 6.
 *
 * « Liste de ses chèques uniquement (numéro, montant, colis, date, statut,
 * historique daté). » La liste porte les cinq premiers ; l'historique demande
 * une fiche, et il n'en existait aucune — la route `GET /cheques/:id` était
 * écrite depuis le chantier « Finance » point 9 et n'était appelée par rien.
 *
 * **Composant SERVEUR, et sans un seul bouton.** Un chèque est l'argent du
 * vendeur ; la société ne fait que le transporter, et les trois gestes du cycle
 * appartiennent à l'agence et au livreur. La fiche RACONTE, elle n'agit pas —
 * c'est la lecture seule que la mission demande, obtenue en n'écrivant aucune
 * action plutôt qu'en désactivant des boutons. Une seule exception, D-265 : le
 * bon de remise, qui imprime un papier et n'avance aucune marche (masqué au
 * vendeur).
 *
 * Le même écran sert l'agence et le livreur : la portée du serveur borne déjà
 * ce que chacun peut ouvrir (`ChequesService.scoped`, chantier « Chèques »
 * point 2), et deux fiches pour la même donnée auraient divergé.
 */
export async function ChequeDetailView({
  cheque,
  canPrintSlip,
}: {
  cheque: ChequeDetail;
  canPrintSlip: boolean;
}) {
  const t = await getTranslations('finance.cheques');
  const tStatus = await getTranslations('finance.cheques.status');
  const format = await getFormatter();

  return (
    <div className="flex flex-col gap-6">
      <BackButton fallbackHref="/cheques" label={t('backToList')} />

      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <h1 className="font-mono text-xl font-semibold tracking-tight sm:text-2xl">
            {cheque.number}
          </h1>
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <StatusPill label={tStatus(cheque.status)} color={CHEQUE_STATUS_COLORS[cheque.status]} />
            <span className="text-muted-foreground text-xs tabular-nums">
              {t('receivedOn', {
                date: format.dateTime(new Date(cheque.receivedAt), 'stamp'),
              })}
            </span>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <p className="text-2xl font-semibold tabular-nums">
            {format.number(cheque.amount / 100, 'currency')}
          </p>
          {/* D-265 : le bon de remise. Le vendeur suit, il n'édite pas. */}
          {canPrintSlip ? <ChequeSlipButton id={cheque.id} parcelCode={cheque.parcelCode} /> : null}
        </div>
      </header>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>{t('detailTitle')}</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="grid gap-3 text-sm">
              <Row label={t('columns.number')} value={cheque.number} mono />
              <Row label={t('bank')} value={cheque.bank ?? '—'} />
              <Row
                label={t('chequeDate')}
                value={
                  cheque.chequeDate
                    ? format.dateTime(new Date(cheque.chequeDate), 'short')
                    : '—'
                }
              />
              <Row label={t('payer')} value={cheque.payerName} />
              <Row
                label={t('columns.parcel')}
                value={
                  /* Le colis est la PIÈCE : c'est lui qu'on rapproche quand le
                     vendeur demande « ce chèque, c'est laquelle de mes ventes ? ». */
                  <Link
                    href={`/parcels/${encodeURIComponent(cheque.parcelCode)}`}
                    className="font-mono underline-offset-4 hover:underline"
                  >
                    {cheque.parcelCode}
                  </Link>
                }
              />
              <Row label={t('columns.courier')} value={cheque.courierName} />
              {cheque.invoiceCode ? (
                <Row label={t('invoice')} value={cheque.invoiceCode} mono />
              ) : null}
              {cheque.rejectionReason ? (
                <Row label={t('rejectReason')} value={cheque.rejectionReason} />
              ) : null}
            </dl>
            {/* D-237 : le rejet est un litige entre le vendeur et son destinataire. */}
            {cheque.status === 'rejete' ? (
              <p className="text-destructive mt-3 text-sm">{t('rejectedNotice')}</p>
            ) : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{t('history')}</CardTitle>
          </CardHeader>
          <CardContent>
            {cheque.events.length === 0 ? (
              <p className="text-muted-foreground text-sm">{t('historyEmpty')}</p>
            ) : (
              <ol className="flex flex-col gap-4">
                {cheque.events.map((event) => (
                  <li key={event.id} className="flex items-start gap-3">
                    <span
                      className="mt-1.5 size-2 shrink-0 rounded-full"
                      style={{ backgroundColor: CHEQUE_STATUS_COLORS[event.status] }}
                      aria-hidden
                    />
                    <div className="min-w-0">
                      <p className="text-sm font-medium">
                        {/* D-265 : une trace de bon de remise n'est pas une marche. */}
                        {event.kind === 'slip' ? t('slipEvent') : tStatus(event.status)}
                      </p>
                      <p className="text-muted-foreground text-xs tabular-nums">
                        {format.dateTime(new Date(event.createdAt), 'stamp')}
                        {event.authorName ? ` · ${event.authorName}` : ''}
                      </p>
                      {event.comment ? (
                        <p className="text-muted-foreground mt-1 text-xs">{event.comment}</p>
                      ) : null}
                    </div>
                  </li>
                ))}
              </ol>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

/**
 * La pastille d'état, écrite ICI plutôt qu'empruntée à `data-table/cells`.
 *
 * Ce module-là porte `RowActions`, qui appelle `useTranslations` : l'importer
 * depuis un composant SERVEUR ferait entrer un hook dans le graphe serveur pour
 * une pastille de douze lignes. La couleur, elle, vient bien du référentiel
 * partagé (`CHEQUE_STATUS_COLORS`) — règle 3 de CLAUDE.md : aucune page ne
 * choisit sa palette.
 */
function StatusPill({ label, color }: { label: string; color: string }) {
  return (
    <span
      className="inline-flex items-center rounded-full border px-2.5 py-1 text-xs font-medium"
      style={{ color, borderColor: `${color}55`, backgroundColor: `${color}1A` }}
    >
      {label}
    </span>
  );
}

function Row({
  label,
  value,
  mono,
}: {
  label: string;
  value: React.ReactNode;
  mono?: boolean;
}) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className={mono ? 'text-end font-mono' : 'text-end'}>{value}</dd>
    </div>
  );
}
