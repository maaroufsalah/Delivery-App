'use client';

import { AlertCircle, RotateCcw } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { Component, type ReactNode, Suspense } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useRouter } from '@/i18n/navigation';

/**
 * LA FRONTIÈRE D'UN WIDGET — chantier « Dashboard de pilotage », spec § 12.
 *
 * Chaque section du tableau de bord se charge, échoue et se vide TOUTE SEULE :
 *
 *  - `Suspense` rend le squelette pendant que l'agrégat arrive, et laisse les
 *    autres sections s'afficher (le serveur diffuse en continu) ;
 *  - la frontière d'erreur attrape une API en panne ou une 403 et rend une
 *    carte « indisponible » avec un bouton Réessayer, au lieu de faire tomber
 *    toute la page sur un seul agrégat.
 *
 * Composant CLIENT, parce que `getDerivedStateFromError` n'existe qu'en classe
 * côté client ; les enfants, eux, sont des composants SERVEUR asynchrones —
 * React autorise à les passer en `children`.
 */
export function WidgetBoundary({
  title,
  fallback,
  children,
}: {
  /** Le nom de la section, pour la carte d'erreur. */
  title: string;
  fallback: ReactNode;
  children: ReactNode;
}) {
  return (
    <WidgetErrorBoundary title={title}>
      <Suspense fallback={fallback}>{children}</Suspense>
    </WidgetErrorBoundary>
  );
}

class WidgetErrorBoundary extends Component<
  { title: string; children: ReactNode },
  { failed: boolean }
> {
  override state = { failed: false };

  static getDerivedStateFromError(): { failed: boolean } {
    return { failed: true };
  }

  override render(): ReactNode {
    if (this.state.failed) {
      return <WidgetError title={this.props.title} onRetry={() => this.setState({ failed: false })} />;
    }
    return this.props.children;
  }
}

function WidgetError({ title, onRetry }: { title: string; onRetry: () => void }) {
  const t = useTranslations('dashboard.widget');
  const router = useRouter();

  return (
    <Card className="border-destructive/40">
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <AlertCircle className="text-destructive size-4" aria-hidden />
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <p className="text-muted-foreground text-sm">{t('error')}</p>
        <Button
          variant="outline"
          size="sm"
          className="w-fit gap-1.5"
          onClick={() => {
            onRetry();
            router.refresh();
          }}
        >
          <RotateCcw className="size-4" aria-hidden />
          {t('retry')}
        </Button>
      </CardContent>
    </Card>
  );
}
