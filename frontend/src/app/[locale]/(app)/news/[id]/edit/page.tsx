import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { NewsForm } from '@/features/referential/news-form';
import { getAnnouncement } from '@/lib/api/announcements';
import { ApiError } from '@/lib/http';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('referential.news');
  return { title: t('edit') };
}

export default async function EditAnnouncementPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  let announcement;
  try {
    announcement = await getAnnouncement(id);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  }

  const t = await getTranslations('referential.news');

  return (
    <>
      <PageHeader backHref="/news" title={t('edit')} subtitle={announcement.title} />
      <NewsForm announcement={announcement} />
    </>
  );
}
