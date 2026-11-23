'use client';

import { EXCHANGE_STATUS_COLORS, type ExchangeDetail } from '@delivery/shared';
import { ArrowLeftRight, Ban, CheckCircle2, Loader2, PackageCheck, Truck } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { BackButton } from '@/components/back-button';
import { CellBadge } from '@/components/data-table/cells';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Textarea } from '@/components/ui/textarea';
import { Link } from '@/i18n/navigation';
import { advanceExchangeAction } from '@/lib/actions/exchanges';

type Step = 'pickup' | 'deliver' | 'close' | 'cancel';

/**
 * LA FICHE D'UN ÉCHANGE — chantier « Finance », point 6.
 *
 * **Les trois colis sont cliquables**, et c'est la « relation navigable » que
 * la mission demande : l'ancien, le dossier, le neuf. Depuis n'importe lequel
 * on atteint les deux autres en un clic.
 *
 * **Les quatre gestes viennent du SERVEUR.** `pickupable`, `deliverable`,
 * `closable`, `cancellable` sont calculés par l'API ; l'écran ne recalcule
 * aucun d'entre eux — trois espaces qui recopieraient la machine à états, ce
 * sont trois façons de se tromper (D-102).
 */
export function ExchangeDetailScreen({ exchange }: { exchange: ExchangeDetail }) {
  const t = useTranslations('exchanges');
  const tStatus = useTranslations('exchanges.status');
  const tCommon = useTranslations('common');
  const format = useFormatter();

  const [current, setCurrent] = useState(exchange);
  const [dialog, setDialog] = useState<Step | null>(null);
  const [comment, setComment] = useState('');
  const [pending, startTransition] = useTransition();

  const money = (cents: number) => format.number(cents / 100, 'currency');

  const advance = (step: Step) => {
    startTransition(async () => {
      const result = await advanceExchangeAction(current.code, step, comment.trim());
      setDialog(null);
      setComment('');

      if (result.ok) {
        setCurrent(result.data);
        toast.success(t(`done.${step}`));
        return;
      }

      toast.error(result.error);
    });
  };

  return (
    <div className="flex flex-col gap-4">
      <BackButton fallbackHref="/exchanges" />

      <div className="flex flex-wrap items-center gap-2">
        <h1 className="font-mono text-xl font-semibold tracking-tight sm:text-2xl">
          {current.code}
        </h1>

        <CellBadge label={tStatus(current.status)} color={EXCHANGE_STATUS_COLORS[current.status]} />

        <span className="text-muted-foreground text-sm">{current.clientName}</span>

        <div className="ms-auto flex flex-wrap items-center gap-2">
          {current.pickupable ? (
            <Button
              size="sm"
              variant="outline"
              className="gap-1.5"
              onClick={() => setDialog('pickup')}
            >
              <Truck className="size-4" />
              {t('actions.pickup')}
            </Button>
          ) : null}

          {current.deliverable ? (
            <Button
              size="sm"
              variant="outline"
              className="gap-1.5"
              onClick={() => setDialog('deliver')}
            >
              <PackageCheck className="size-4" />
              {t('actions.deliver')}
            </Button>
          ) : null}

          {current.closable ? (
            <Button size="sm" className="gap-1.5" onClick={() => setDialog('close')}>
              <CheckCircle2 className="size-4" />
              {t('actions.close')}
            </Button>
          ) : null}

          {current.cancellable ? (
            <Button
              size="sm"
              variant="ghost"
              className="text-muted-foreground hover:text-destructive gap-1.5"
              onClick={() => setDialog('cancel')}
            >
              <Ban className="size-4" />
              {t('actions.cancel')}
            </Button>
          ) : null}
        </div>
      </div>

      {/* --- Les deux colis, et la flèche entre eux ---------------------- */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('parcels')}</CardTitle>
        </CardHeader>

        <CardContent className="flex flex-wrap items-center gap-3">
          <div className="min-w-0 flex-1">
            <span className="text-muted-foreground block text-xs">{t('original')}</span>
            <Link
              href={`/parcels/${current.originalParcelCode}`}
              className="block font-mono text-sm font-medium underline-offset-4 hover:underline"
            >
              {current.originalParcelCode}
            </Link>
            <span className="text-muted-foreground block truncate text-xs">
              {current.returnedProduct} · {current.oldDestination}
            </span>
          </div>

          <ArrowLeftRight className="flip-rtl text-muted-foreground size-5 shrink-0" aria-hidden />

          <div className="min-w-0 flex-1">
            <span className="text-muted-foreground block text-xs">{t('replacement')}</span>
            {current.newParcelCode ? (
              <Link
                href={`/parcels/${current.newParcelCode}`}
                className="block font-mono text-sm font-medium underline-offset-4 hover:underline"
              >
                {current.newParcelCode}
              </Link>
            ) : (
              <span className="text-muted-foreground block text-sm">—</span>
            )}
            <span className="text-muted-foreground block truncate text-xs">
              {current.newProduct} · {current.newDestination}
            </span>
          </div>

          <div className="flex flex-col items-end tabular-nums">
            <span className="text-sm font-medium">{money(current.amount)}</span>
            <span className="text-muted-foreground text-xs">
              {t('fees', { amount: money(current.fees) })}
            </span>
          </div>
        </CardContent>
      </Card>

      {/* --- Le journal -------------------------------------------------- */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('journal')}</CardTitle>
        </CardHeader>

        <CardContent>
          <ol className="flex flex-col gap-2">
            {current.events.map((event) => (
              <li key={event.id} className="flex flex-wrap items-center gap-2">
                <span
                  className="size-2 shrink-0 rounded-full"
                  style={{ backgroundColor: EXCHANGE_STATUS_COLORS[event.status] }}
                  aria-hidden
                />
                <span className="font-medium">{tStatus(event.status)}</span>
                <span className="text-muted-foreground text-xs tabular-nums">
                  {format.dateTime(new Date(event.createdAt), 'stamp')}
                </span>
                {event.authorName ? (
                  <span className="text-muted-foreground text-xs">{event.authorName}</span>
                ) : null}
                {event.comment ? (
                  <span className="text-muted-foreground w-full text-xs">{event.comment}</span>
                ) : null}
              </li>
            ))}
          </ol>
        </CardContent>
      </Card>

      <Dialog open={dialog !== null} onOpenChange={(open) => (open ? null : setDialog(null))}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{dialog ? t(`actions.${dialog}`) : ''}</DialogTitle>
            <DialogDescription>{dialog ? t(`hints.${dialog}`) : ''}</DialogDescription>
          </DialogHeader>

          <Textarea
            rows={2}
            value={comment}
            placeholder={t('commentPlaceholder')}
            onChange={(event) => setComment(event.target.value)}
          />

          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="outline" onClick={() => setDialog(null)} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            <Button
              variant={dialog === 'cancel' ? 'destructive' : 'default'}
              disabled={pending}
              className="gap-1.5"
              onClick={() => dialog && advance(dialog)}
            >
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {dialog ? t(`actions.${dialog}`) : ''}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
