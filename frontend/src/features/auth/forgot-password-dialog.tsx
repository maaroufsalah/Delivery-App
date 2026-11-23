'use client';

import type { ForgotPasswordResult } from '@delivery/shared';
import { Loader2, Phone } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { forgotPasswordAction } from '@/lib/actions/auth';

/**
 * « Mot de passe oublié » — C12 bloc 2b.
 *
 * **L'écran dit la vérité : personne ne recevra d'e-mail.** Tant que B-13
 * n'est pas livré, la seule issue est d'appeler son agence, et le numéro
 * s'affiche — celui de la société, lu en base, pas un « contactez le support »
 * qui ne désigne personne. Promettre un message qui ne partira pas ferait
 * attendre une heure avant d'appeler.
 *
 * **La confirmation est la même que l'adresse existe ou non.** C'est la règle
 * de l'API, et l'écran ne la contourne pas : distinguer les deux cas ferait de
 * ce formulaire un annuaire des comptes de la société.
 *
 * Le seul échec affiché est « API injoignable » — parce qu'alors, réellement,
 * rien n'a été enregistré et il faut réessayer.
 */
export function ForgotPasswordDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const t = useTranslations('auth.forgot');

  const [email, setEmail] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<ForgotPasswordResult | null>(null);
  const [pending, startTransition] = useTransition();

  const close = (next: boolean) => {
    if (!next) {
      setEmail('');
      setError(null);
      setDone(null);
    }
    onOpenChange(next);
  };

  const submit = () => {
    startTransition(async () => {
      setError(null);
      const result = await forgotPasswordAction({ email: email.trim() });

      if (result.ok) setDone(result.data);
      else setError(result.error === 'invalid-email' ? t('invalidEmail') : t('unreachable'));
    });
  };

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t('title')}</DialogTitle>
          <DialogDescription>{done ? t('doneHint') : t('hint')}</DialogDescription>
        </DialogHeader>

        {done ? (
          <div className="flex flex-col gap-3">
            {/* Le numéro de l'agence, en grand et sélectionnable : il sera
                composé sur un téléphone posé à côté du clavier. */}
            {done.supportPhone ? (
              <a
                href={`tel:${done.supportPhone}`}
                className="hover:bg-muted flex items-center gap-3 rounded-lg border p-3"
              >
                <Phone className="text-muted-foreground size-4 shrink-0" aria-hidden />
                <span className="min-w-0">
                  <span className="block text-sm font-medium">{done.companyName}</span>
                  <span className="block font-mono text-base tracking-wider select-all" dir="ltr">
                    {done.supportPhone}
                  </span>
                </span>
              </a>
            ) : (
              <p className="text-muted-foreground text-sm">{t('noPhone')}</p>
            )}

            <p className="text-muted-foreground text-xs">
              {done.emailEnabled ? t('mailSent') : t('mailPending')}
            </p>
          </div>
        ) : (
          <div className="flex flex-col gap-2">
            <Label htmlFor="forgot-email">{t('email')}</Label>
            <Input
              id="forgot-email"
              type="email"
              autoComplete="username"
              dir="ltr"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' && !pending) submit();
              }}
            />
            {error ? <p className="text-destructive text-sm">{error}</p> : null}
          </div>
        )}

        <DialogFooter>
          {done ? (
            <Button onClick={() => close(false)}>{t('close')}</Button>
          ) : (
            <>
              <Button variant="outline" onClick={() => close(false)} disabled={pending}>
                {t('cancel')}
              </Button>
              <Button
                onClick={submit}
                disabled={pending || email.trim() === ''}
                className="gap-1.5"
              >
                {pending ? <Loader2 className="size-4 animate-spin" /> : null}
                {t('submit')}
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
