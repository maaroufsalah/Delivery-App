'use client';

import { apiErrorStatus, errorReference } from '@delivery/shared';
import { Ban, Loader2, TriangleAlert, WifiOff } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useEffect } from 'react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { useRouter } from '@/i18n/navigation';

/**
 * LA FRONTIÈRE D'ERREUR, QUI SAIT ENFIN CE QUI A ÉCHOUÉ — C15 bloc 3.
 *
 * **Le défaut corrigé.** Next remplace le message d'une erreur serveur par un
 * identifiant avant de l'envoyer au navigateur : cet écran ne savait donc rien,
 * et affichait « Une erreur est survenue » aussi bien pour une session expirée
 * — le cas le plus fréquent, et le plus bénin — que pour une panne réelle.
 * L'utilisateur perdait son écran sans comprendre, et rechargeait.
 *
 * `ApiError` pose désormais le statut HTTP dans son `digest`, le seul champ que
 * Next laisse traverser. Trois issues, trois écrans :
 *
 *  - **401** — la session est finie. On ne montre rien à lire : on renvoie vers
 *    la connexion, en gardant la page demandée, et on ne laisse qu'un
 *    « Reconnexion… » le temps du saut.
 *  - **403** — c'est un refus, pas une panne. Le dire, et proposer de revenir
 *    en arrière ; « Réessayer » relancerait exactement le même refus.
 *  - **réseau et 5xx** — le service est momentanément indisponible. « Réessayer »
 *    a du sens, et `reset()` rejoue le segment sans recharger la page : ce qui
 *    est saisi ailleurs dans l'écran survit.
 */
export default function ErrorBoundary({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const t = useTranslations('error');
  const router = useRouter();
  const status = apiErrorStatus(error.digest);
  // « Référence : … » — l'identifiant de requête, que le client nous lit (recette 4, 5 c).
  const reference = errorReference(error.digest);

  useEffect(() => {
    if (status !== 401) return;

    /*
     * Le chemin est lu ICI, dans le navigateur : la frontière d'erreur ne
     * reçoit pas de `params`, et c'est la seule façon de savoir où l'utilisateur
     * voulait aller. `location.pathname` porte déjà le préfixe de langue.
     */
    const next = `${window.location.pathname}${window.location.search}`;
    router.replace(`/login?next=${encodeURIComponent(next)}`);
  }, [router, status]);

  if (status === 401) {
    return (
      <Shell
        icon={<Loader2 className="size-6 animate-spin" />}
        tone="muted"
        title={t('sessionTitle')}
        description={t('sessionDescription')}
      />
    );
  }

  if (status === 403) {
    return (
      <Shell
        icon={<Ban className="size-6" />}
        tone="destructive"
        title={t('forbiddenTitle')}
        description={t('forbiddenDescription')}
        reference={reference}
        action={
          <Button size="sm" variant="outline" onClick={() => router.back()}>
            {t('back')}
          </Button>
        }
      />
    );
  }

  const unavailable = status === 0 || (status !== null && status >= 500);

  return (
    <Shell
      icon={unavailable ? <WifiOff className="size-6" /> : <TriangleAlert className="size-6" />}
      tone="destructive"
      title={unavailable ? t('unavailableTitle') : t('genericTitle')}
      description={unavailable ? t('unavailableDescription') : t('genericDescription')}
      reference={reference}
      action={
        <Button size="sm" variant="outline" onClick={reset}>
          {t('retry')}
        </Button>
      }
    />
  );
}

/** La même carte pour les quatre cas : seuls l'icône et les mots changent. */
function Shell({
  icon,
  tone,
  title,
  description,
  action,
  reference,
}: {
  icon: React.ReactNode;
  tone: 'muted' | 'destructive';
  title: string;
  description: string;
  action?: React.ReactNode;
  /** L'identifiant à nous transmettre — recette SpeedyTrans 4, point 5 c. */
  reference?: string | null;
}) {
  const t = useTranslations('error');

  return (
    <div className="flex min-h-dvh items-center justify-center p-6">
      <Card className="w-full max-w-md items-center gap-4 p-8 text-center">
        <span
          className={`flex size-12 items-center justify-center rounded-full ${
            tone === 'muted'
              ? 'bg-muted text-muted-foreground'
              : 'bg-destructive/10 text-destructive'
          }`}
        >
          {icon}
        </span>
        <h1 className="text-lg font-semibold tracking-tight">{title}</h1>
        <p className="text-muted-foreground text-sm">{description}</p>
        {reference ? (
          <p className="text-muted-foreground font-mono text-xs select-all">
            {t('reference', { id: reference })}
          </p>
        ) : null}
        {action}
      </Card>
    </div>
  );
}
