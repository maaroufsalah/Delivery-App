'use client';

import { Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useEffect, useRef, useState } from 'react';
import { useRouter } from '@/i18n/navigation';
import { impersonateAction } from '@/lib/actions/auth';

/**
 * Le relais de « se connecter en tant que » — C8 bloc 4, D-051.
 *
 * L'administrateur arrive de la console avec un jeton dans l'URL. On l'échange
 * contre une vraie session, puis on l'emmène au tableau de bord.
 *
 * **Composant client, et il le faut** : l'échange pose des cookies, or Next
 * n'autorise l'écriture d'un cookie que dans une Server Action ou `proxy.ts` —
 * jamais pendant le rendu d'une page. Le déclencher depuis un effet est ce qui
 * permet à la Server Action de s'exécuter.
 *
 * **L'échange n'est tenté qu'une fois.** Un jeton consommé ne vaut plus rien ;
 * rejouer l'appel sur un re-rendu afficherait une erreur alors que la session
 * vient d'être ouverte — le garde `attempted` évite ce faux négatif.
 */
export function ImpersonationHandoff({ token }: { token: string }) {
  const t = useTranslations('auth.login');
  const router = useRouter();
  const attempted = useRef(false);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (attempted.current) return;
    attempted.current = true;

    void (async () => {
      const result = await impersonateAction(token);

      if (!result.ok) {
        setFailed(true);
        return;
      }

      /*
       * `replace` et non `push` : le jeton est dans l'URL courante, et la
       * laisser dans l'historique la rendrait rejouable d'un retour arrière —
       * sur un lien périmé, donc sur une erreur.
       */
      router.replace('/dashboard');
      router.refresh();
    })();
  }, [router, token]);

  if (failed) {
    return (
      <p role="alert" className="text-destructive text-sm">
        {t('impersonationFailed')}
      </p>
    );
  }

  return (
    <p className="text-muted-foreground flex items-center justify-center gap-2 py-4 text-sm">
      <Loader2 className="size-4 animate-spin" aria-hidden />
      {t('impersonationPending')}
    </p>
  );
}
