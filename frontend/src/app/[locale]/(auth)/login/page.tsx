import { safeReturnPath } from '@delivery/shared';
import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ImpersonationHandoff } from '@/features/auth/impersonation-handoff';
import { LoginForm } from '@/features/auth/login-form';
import { Link } from '@/i18n/navigation';
import { getCompanySettings } from '@/lib/api/settings';
import { CompanyLogo } from '@/components/layout/company-logo';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('auth.login');
  return { title: t('title') };
}

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const [settings, t, params] = await Promise.all([
    getCompanySettings(),
    getTranslations('auth.login'),
    searchParams,
  ]);

  /*
   * « Se connecter en tant que » (C8, D-051) : la console renvoie ici avec un
   * jeton court. On n'affiche alors pas le formulaire — il n'y a rien à saisir,
   * juste un échange à faire.
   */
  const handoff = params.impersonation;
  const token = Array.isArray(handoff) ? handoff[0] : handoff;

  /*
   * LA PAGE DEMANDÉE AVANT LA COUPURE — C15 bloc 3.
   *
   * Le proxy l'a posée dans `?next=` en renvoyant ici. Elle est RÉ-ASSAINIE :
   * ce qui arrive dans une URL n'a pas forcément été posé par nous, et une
   * redirection ouverte renverrait un utilisateur fraîchement authentifié sur
   * un site qui n'est pas le nôtre (`safeReturnPath`).
   */
  const requested = params.next;
  const next = safeReturnPath(Array.isArray(requested) ? requested[0] : requested);

  /*
   * POURQUOI LA SESSION S'EST ARRÊTÉE — C17.
   *
   * Le proxy pose `?reason=other-space` quand le jeton présenté appartient à un
   * autre espace : les cookies portent le domaine de la plateforme, donc un
   * `dp.at` obtenu sur `wimo.…` est bel et bien envoyé à `speedy.…`. Sans ce
   * mot, l'utilisateur voit un écran de connexion qui n'explique rien, alors
   * qu'il vient de se connecter — et il recommence.
   *
   * Une valeur inconnue n'affiche RIEN : ce qui arrive dans une URL n'a pas
   * forcément été posé par nous.
   */
  const claimed = params.reason;
  const reason = (Array.isArray(claimed) ? claimed[0] : claimed) === 'other-space';

  return (
    <div className="flex min-h-dvh flex-col items-center justify-center px-4 py-12">
      <Link
        href="/"
        className="focus-visible:ring-ring mb-6 flex items-center gap-2.5 rounded-md outline-none focus-visible:ring-2"
      >
        <CompanyLogo src={settings.logo} background={settings.logoBackground} size="md" />
        <span className="text-base font-semibold tracking-tight">{settings.name}</span>
      </Link>

      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>{t('title')}</CardTitle>
          <CardDescription>{t('description')}</CardDescription>
        </CardHeader>
        <CardContent className="gap-4">
          {reason ? (
            <p
              role="status"
              className="border-destructive/30 bg-destructive/10 text-destructive rounded-md border px-3 py-2 text-sm"
            >
              {t('otherSpace')}
            </p>
          ) : null}

          {token ? <ImpersonationHandoff token={token} /> : <LoginForm next={next} />}
        </CardContent>
      </Card>
    </div>
  );
}
