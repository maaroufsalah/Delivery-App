import { FileQuestion } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Link } from '@/i18n/navigation';

export default function NotFound() {
  const t = useTranslations('error');
  const tSoon = useTranslations('soon');

  return (
    <div className="flex min-h-dvh items-center justify-center p-6">
      <Card className="w-full max-w-md items-center gap-4 p-8 text-center">
        <span className="bg-accent text-accent-foreground flex size-12 items-center justify-center rounded-full">
          <FileQuestion className="size-6" />
        </span>
        <h1 className="text-lg font-semibold tracking-tight">{t('notFoundTitle')}</h1>
        <p className="text-muted-foreground text-sm">{t('notFoundDescription')}</p>
        <Button asChild size="sm" variant="outline">
          <Link href="/dashboard">{tSoon('backToDashboard')}</Link>
        </Button>
      </Card>
    </div>
  );
}
