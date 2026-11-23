import { Construction } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';
import type { NavItemKey } from '@/lib/navigation-config';

/**
 * Écran d'attente des entrées de menu non encore développées.
 * Un seul composant : aucune page « Bientôt » n'est écrite à la main.
 */
export function ComingSoon({ itemKey, label }: { itemKey: NavItemKey; label?: string }) {
  const t = useTranslations('soon');
  const tItems = useTranslations('nav.items');
  const page = label ?? tItems(itemKey);

  return (
    <div className="mx-auto flex max-w-xl flex-col items-center py-10 text-center sm:py-16">
      <Card className="w-full items-center gap-4 p-8">
        <span className="bg-accent text-accent-foreground flex size-12 items-center justify-center rounded-full">
          <Construction className="size-6" />
        </span>
        <Badge variant="secondary" className="rounded-full">
          {t('badge')}
        </Badge>
        <h1 className="text-lg font-semibold tracking-tight">{t('title', { page })}</h1>
        <p className="text-muted-foreground text-sm">{t('description')}</p>
        <Button asChild variant="outline" size="sm">
          <Link href="/dashboard">{t('backToDashboard')}</Link>
        </Button>
      </Card>
    </div>
  );
}
