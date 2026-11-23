import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { NewsForm } from '@/features/referential/news-form';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('referential.news');
  return { title: t('new') };
}

export default async function NewAnnouncementPage() {
  const t = await getTranslations('referential.news');

  return (
    <>
      <PageHeader backHref="/news" title={t('new')} subtitle={t('formSubtitle')} />
      <NewsForm />
    </>
  );
}
