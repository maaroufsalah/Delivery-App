import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { TrackingSearch } from '@/features/public/tracking-view';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('public.track');
  return { title: t('title') };
}

/** Aucune lecture de `searchParams` : la page reste statique. */
export default async function TrackPage() {
  const t = await getTranslations('public.track');

  return (
    <div className="mx-auto w-full max-w-2xl px-4 py-14 sm:px-6 sm:py-20">
      <Card>
        <CardHeader>
          <CardTitle>{t('title')}</CardTitle>
          <CardDescription>{t('description')}</CardDescription>
        </CardHeader>
        <CardContent>
          <TrackingSearch />
        </CardContent>
      </Card>
    </div>
  );
}
