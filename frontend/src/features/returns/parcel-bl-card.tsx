'use client';

import { DELIVERY_SLIP_STATUS_COLORS, type ParcelBlTracking } from '@delivery/shared';
import { FileSignature } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { CellBadge } from '@/components/data-table/cells';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

/**
 * LE SUIVI DU BL SUR LA FICHE COLIS — chantier « Retours, BL Clients Pro »,
 * point 5, dernier alinéa : « Vendeur Pro : suivi du BL sur la fiche colis
 * (historique des étapes). »
 *
 * **La première marche est celle du COLIS**, pas du BL : « BL à récupérer »
 * existe dès que la demande est cochée, et le papier n'existe pas encore. La
 * frise la montre quand même, parce que c'est bien la première chose qui est
 * arrivée — et parce qu'un vendeur qui ne verrait rien croirait que sa demande
 * s'est perdue.
 *
 * **La carte ne s'affiche pas du tout si rien n'est demandé.** C'est la lettre
 * de la mission : « aucun champ, aucune question » pour un colis sans BL. Une
 * carte vide qui dirait « aucun BL » serait déjà une question.
 */
export function ParcelBlCard({ tracking }: { tracking: ParcelBlTracking }) {
  const t = useTranslations('returns.bl');
  const tSlip = useTranslations('returns.bl.slipStatus');
  const format = useFormatter();

  if (!tracking.requested && !tracking.slip) return null;

  const slip = tracking.slip;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <FileSignature className="size-4" />
          {t('tracking')}
          {slip ? (
            <CellBadge
              label={tSlip(slip.status)}
              color={DELIVERY_SLIP_STATUS_COLORS[slip.status]}
            />
          ) : null}
        </CardTitle>
        <CardDescription>{t('trackingHint')}</CardDescription>
      </CardHeader>

      <CardContent className="flex flex-col gap-3">
        {slip?.reference ? (
          <p className="text-sm">
            <span className="text-muted-foreground">{t('reference')} : </span>
            <span className="font-mono">{slip.reference}</span>
          </p>
        ) : null}

        <ol className="flex flex-col gap-3">
          {/*
            LA PREMIÈRE MARCHE, déduite du colis. Elle n'a pas d'auteur ni de
            date propre : la demande vit sur le colis, elle a été cochée avec
            lui ou plus tard, et le journal du colis porte les deux.
          */}
          {tracking.requested ? (
            <li className="flex flex-wrap items-baseline gap-x-3 gap-y-1 text-sm">
              <span
                className="size-2 shrink-0 rounded-full"
                style={{ backgroundColor: 'var(--muted-foreground)' }}
                aria-hidden
              />
              <span className="font-medium">{tSlip('to-collect')}</span>
            </li>
          ) : null}

          {(slip?.steps ?? []).map((step) => (
            <li key={step.id} className="flex flex-wrap items-baseline gap-x-3 gap-y-1 text-sm">
              <span
                className="size-2 shrink-0 rounded-full"
                style={{ backgroundColor: DELIVERY_SLIP_STATUS_COLORS[step.status] }}
                aria-hidden
              />
              <span className="font-medium">{tSlip(step.status)}</span>
              <span className="text-muted-foreground text-xs tabular-nums">
                {format.dateTime(new Date(step.createdAt), 'stamp')}
              </span>
              {step.authorName ? (
                <span className="text-muted-foreground text-xs">{step.authorName}</span>
              ) : null}
            </li>
          ))}
        </ol>
      </CardContent>
    </Card>
  );
}
