import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { getFormatter, getTranslations } from 'next-intl/server';
import { PageHeader } from '@/components/layout/page-header';
import { Card, CardContent } from '@/components/ui/card';
import { RichText } from '@/features/referential/rich-text';
import { getAnnouncement } from '@/lib/api/announcements';
import { ApiError } from '@/lib/http';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ id: string }>;
}): Promise<Metadata> {
  const { id } = await params;

  try {
    const announcement = await getAnnouncement(id);
    return { title: announcement.title };
  } catch {
    return {};
  }
}

export default async function AnnouncementPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;

  let announcement;
  try {
    announcement = await getAnnouncement(id);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  }

  const [t, format] = await Promise.all([getTranslations('referential.news'), getFormatter()]);

  return (
    <>
      <PageHeader
        backHref="/news"
        title={announcement.title}
        subtitle={
          announcement.publishedAt
            ? `${format.dateTime(new Date(announcement.publishedAt), 'short')} · ${announcement.authorName}`
            : `${t('draft')} · ${announcement.authorName}`
        }
      />

      <Card>
        <CardContent className="pt-6">
          {announcement.intro ? (
            <p className="text-muted-foreground mb-4 text-sm font-medium">{announcement.intro}</p>
          ) : null}
          <RichText body={announcement.body} />
        </CardContent>
      </Card>
    </>
  );
}
