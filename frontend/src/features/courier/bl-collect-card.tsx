'use client';

import { DELIVERY_SLIP_STATUS_COLORS, type ParcelBlTracking } from '@delivery/shared';
import { FileSignature, Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { CellBadge } from '@/components/data-table/cells';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useReturnError } from '@/features/returns/use-return-error';
import { collectParcelBlAction } from '@/lib/actions/bl';

/**
 * LE BANDEAU BL DU LIVREUR, côté web — chantier « Retours, BL Clients Pro »,
 * point 5, et le miroir exact de l'écran mobile (D-090).
 *
 * « Bandeau visible 📄 BL à récupérer : OUI ; à la livraison, question BL
 * récupéré ? » Il est haut dans la page, avant l'historique : c'est ce qu'il
 * faut savoir avant de sonner, pas après avoir tendu le colis.
 *
 * **« Non, pas cette fois » n'écrit rien**, et le dit. Le destinataire n'avait
 * pas le papier, ou l'a gardé ; la question se reposera. Enregistrer un « non »
 * aurait fait un BL qui n'a jamais existé, que l'admin aurait cherché.
 */
export function BlCollectCard({ code, tracking }: { code: string; tracking: ParcelBlTracking }) {
  const t = useTranslations('returns.bl');
  const tSlip = useTranslations('returns.bl.slipStatus');
  const errorMessage = useReturnError();

  const [current, setCurrent] = useState(tracking);
  const [reference, setReference] = useState('');
  const [skipped, setSkipped] = useState(false);
  const [pending, startTransition] = useTransition();

  if (!current.requested && !current.slip) return null;

  // Le papier est là : on montre où il en est, plus aucune question.
  if (current.slip) {
    return (
      <Card>
        <CardContent className="flex flex-wrap items-center gap-3 p-4">
          <FileSignature className="text-primary size-4 shrink-0" aria-hidden />
          <span className="text-sm font-medium">{t('tracking')}</span>
          <CellBadge
            label={tSlip(current.slip.status)}
            color={DELIVERY_SLIP_STATUS_COLORS[current.slip.status]}
          />
          {current.slip.reference ? (
            <span className="text-muted-foreground font-mono text-xs">
              {current.slip.reference}
            </span>
          ) : null}
        </CardContent>
      </Card>
    );
  }

  const collect = () => {
    startTransition(async () => {
      const result = await collectParcelBlAction(code, { collected: true, reference });

      if (result.ok) {
        setCurrent(result.data);
        toast.success(t('collectDone'));
      } else toast.error(errorMessage(result));
    });
  };

  return (
    <Card className="border-primary/40">
      <CardContent className="flex flex-col gap-3 p-4">
        <div className="flex items-center gap-2">
          <FileSignature className="text-primary size-4 shrink-0" aria-hidden />
          <span className="font-semibold">{t('parcelBanner')}</span>
        </div>

        <p className="text-muted-foreground text-sm">{t('parcelBannerHint')}</p>

        {skipped ? <p className="text-muted-foreground text-xs">{t('collectSkipped')}</p> : null}

        <div>
          <Label htmlFor="bl-reference">{t('reference')}</Label>
          <Input
            id="bl-reference"
            value={reference}
            onChange={(event) => setReference(event.target.value)}
            className="mt-1.5 font-mono"
          />
        </div>

        <div className="flex flex-wrap gap-2">
          <Button onClick={collect} disabled={pending} className="gap-1.5">
            {pending ? <Loader2 className="size-4 animate-spin" /> : null}
            {t('collectYes')}
          </Button>
          <Button variant="outline" disabled={pending} onClick={() => setSkipped(true)}>
            {t('collectNo')}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
