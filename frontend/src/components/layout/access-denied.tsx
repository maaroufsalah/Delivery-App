import { Ban } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';
import { cn } from '@/lib/utils';

/**
 * « ACCÈS REFUSÉ », EN HTTP 403 — lot W1 (E2E-009), D-236.
 *
 * Rendu par les `forbidden.tsx` quand l'API refuse une page (lien direct,
 * marque-page) : `ApiError` 403 porte le `digest` de `forbidden()`. Mêmes mots
 * que la frontière d'erreur (`error.forbidden*`), mais servi en 403 et, dans le
 * back-office, DANS la coque — le menu reste là pour repartir.
 */
export function AccessDenied({ fullScreen = false }: { fullScreen?: boolean }) {
  const t = useTranslations('error');
  const tSoon = useTranslations('soon');

  return (
    <div
      className={cn(
        'flex items-center justify-center p-6',
        fullScreen ? 'min-h-dvh' : 'min-h-[50vh]',
      )}
    >
      <Card className="w-full max-w-md items-center gap-4 p-8 text-center">
        <span className="bg-destructive/10 text-destructive flex size-12 items-center justify-center rounded-full">
          <Ban className="size-6" />
        </span>
        <h1 className="text-lg font-semibold tracking-tight">{t('forbiddenTitle')}</h1>
        <p className="text-muted-foreground text-sm">{t('forbiddenDescription')}</p>
        <Button asChild size="sm" variant="outline">
          <Link href="/dashboard">{tSoon('backToDashboard')}</Link>
        </Button>
      </Card>
    </div>
  );
}
