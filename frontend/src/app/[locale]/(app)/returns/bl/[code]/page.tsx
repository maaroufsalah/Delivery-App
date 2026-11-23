import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { notFound } from 'next/navigation';
import { PageHeader } from '@/components/layout/page-header';
import { BlNoteDetailScreen } from '@/features/returns/bl-note-detail';
import { getBlNote } from '@/lib/api/bl';
import { ApiError } from '@/lib/http';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ code: string }>;
}): Promise<Metadata> {
  const { code } = await params;
  return { title: decodeURIComponent(code) };
}

export default async function BlNoteDetailPage({ params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;
  const t = await getTranslations('returns.bl');

  const note = await getBlNote(decodeURIComponent(code)).catch((error: unknown) => {
    if (error instanceof ApiError && error.status === 404) notFound();
    throw error;
  });

  return (
    <>
      <PageHeader backHref="/returns/bl/notes" title={note.code} subtitle={t('subtitle')} />
      <BlNoteDetailScreen note={note} />
    </>
  );
}
