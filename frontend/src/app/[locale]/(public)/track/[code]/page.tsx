import { ArrowLeft } from 'lucide-react';
import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { TrackingResult, TrackingSearch } from '@/features/public/tracking-view';
import { Link } from '@/i18n/navigation';
import { ApiError } from '@/lib/api/client';
import { trackParcel } from '@/lib/api/public';

type Params = Promise<{ code: string }>;

export async function generateMetadata({ params }: { params: Params }): Promise<Metadata> {
  const { code } = await params;
  return { title: decodeURIComponent(code) };
}

export default async function TrackCodePage({ params }: { params: Params }) {
  const { code } = await params;
  const decoded = decodeURIComponent(code);
  const t = await getTranslations('public.track');

  // Un code inconnu n'est PAS un 404 : le visiteur s'est probablement trompé de
  // saisie, et doit pouvoir corriger sans perdre la page.
  const tracking = await trackParcel(decoded).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  });

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-col gap-6 px-4 py-14 sm:px-6 sm:py-20">
      <Button asChild variant="ghost" size="sm" className="-ms-2 w-fit gap-1.5">
        <Link href="/">
          <ArrowLeft className="flip-rtl size-4" aria-hidden />
          {t('backHome')}
        </Link>
      </Button>

      <Card>
        <CardHeader>
          <CardTitle>{t('title')}</CardTitle>
          <CardDescription>{t('description')}</CardDescription>
        </CardHeader>
        <CardContent>
          <TrackingSearch defaultCode={decoded} />
        </CardContent>
      </Card>

      {tracking ? (
        <TrackingResult tracking={tracking} />
      ) : (
        <Card>
          <CardContent className="flex flex-col gap-1 py-8 text-center">
            <p className="text-sm font-medium">{t('notFound')}</p>
            <p className="text-muted-foreground text-sm">{t('notFoundHint', { code: decoded })}</p>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
