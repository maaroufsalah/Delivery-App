'use client';

import { EXCHANGE_STATUS_COLORS, type ParcelExchangeLink } from '@delivery/shared';
import { ArrowLeftRight } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { CellBadge } from '@/components/data-table/cells';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';

/**
 * « DEMANDE DE RETOUR / ÉCHANGE » — chantier « Finance » point 6 pour la
 * relation, « Étiquette orange » point 2 pour ce que la carte dit.
 *
 * « Bloc “Demande de retour/échange” sur la fiche d'origine (code, statut,
 * date, lien). » Les quatre y sont, et la DATE manquait : c'est elle qu'on
 * cherche en premier quand un client rappelle.
 *
 * **Chaque ligne dit ce que CE colis est.** « Remplacé par », « remplace » ou
 * « retour de » : sans cette phrase, la fiche afficherait un code sans qu'on
 * sache s'il désigne ce qui part ou ce qui revient. Le troisième rôle est né
 * avec le point 2, qui fabrique un colis de retour en plus du neuf.
 *
 * **Les DEUX colis dérivés sont nommés, chacun cliquable** — c'est la relation
 * navigable dans les trois fiches. Depuis l'original on atteint `RET-` et
 * `ECH-` ; depuis chacun d'eux, on revient.
 *
 * **La carte disparaît quand il n'y a rien.** Une carte vide sur toutes les
 * fiches aurait dilué celles qui portent un dossier.
 */
export function ParcelExchangesCard({ exchanges }: { exchanges: ParcelExchangeLink[] }) {
  const t = useTranslations('exchanges.onParcel');
  const tStatus = useTranslations('exchanges.status');
  const format = useFormatter();

  if (exchanges.length === 0) return null;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <ArrowLeftRight className="flip-rtl size-4" aria-hidden />
          {t('title')}
        </CardTitle>
      </CardHeader>

      <CardContent className="flex flex-col gap-2">
        {exchanges.map((link) => (
          <div
            key={link.exchangeId}
            className="flex flex-wrap items-start justify-between gap-2 rounded-lg border p-3"
          >
            <div className="min-w-0">
              <Link
                href={`/exchanges/${link.exchangeCode}`}
                className="block font-mono text-sm font-medium underline-offset-4 hover:underline"
              >
                {link.exchangeCode}
              </Link>

              <span className="text-muted-foreground block truncate text-xs">
                {link.counterpartCode
                  ? link.role === 'original'
                    ? t('asOriginal', { code: link.counterpartCode })
                    : link.role === 'return'
                      ? t('asReturn', { code: link.counterpartCode })
                      : t('asNew', { code: link.counterpartCode })
                  : t('pending')}
                {' · '}
                {link.returnedProduct} → {link.newProduct}
              </span>

              {/*
                LES DEUX COLIS, nommés et cliquables — point 2. Depuis la fiche
                d'origine, on atteint ce qui revient ET ce qui repart sans
                passer par le dossier.
              */}
              <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
                {link.returnParcelCode ? (
                  <Link
                    href={`/parcels/${encodeURIComponent(link.returnParcelCode)}`}
                    className="font-mono underline-offset-4 hover:underline"
                  >
                    {link.returnParcelCode}
                  </Link>
                ) : null}
                {link.counterpartCode && link.role === 'original' ? (
                  <Link
                    href={`/parcels/${encodeURIComponent(link.counterpartCode)}`}
                    className="font-mono underline-offset-4 hover:underline"
                  >
                    {link.counterpartCode}
                  </Link>
                ) : null}
                <span className="text-muted-foreground tabular-nums">
                  {format.dateTime(new Date(link.requestedAt), 'stamp')}
                </span>
              </div>
            </div>

            <CellBadge label={tStatus(link.status)} color={EXCHANGE_STATUS_COLORS[link.status]} />
          </div>
        ))}
      </CardContent>
    </Card>
  );
}
