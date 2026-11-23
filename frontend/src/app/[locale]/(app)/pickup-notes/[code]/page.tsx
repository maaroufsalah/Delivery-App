import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { NoteDetailView } from '@/features/notes/note-detail-view';
import { getLatestBulkJob } from '@/lib/api/bulk-jobs';
import { getCurrentUser } from '@/lib/api/auth';
import { ApiError } from '@/lib/api/client';
import { getDeliveryNote } from '@/lib/api/routes';

type Params = Promise<{ code: string }>;

export async function generateMetadata({ params }: { params: Params }): Promise<Metadata> {
  const { code } = await params;
  return { title: decodeURIComponent(code) };
}

export default async function NoteDetailPage({ params }: { params: Params }) {
  const { code } = await params;

  const [note, user] = await Promise.all([
    getDeliveryNote(decodeURIComponent(code)).catch((error: unknown) => {
      if (error instanceof ApiError && error.status === 404) notFound();
      throw error;
    }),
    getCurrentUser(),
  ]);

  /*
    Composer et VALIDER demandent `notes.write` — retouches Anass, point 3.

    Le vendeur lit son bon et l'imprime ; il ne lui ajoute pas de colis et ne
    le valide pas. Les deux boutons lui étaient offerts et rendaient tous les
    deux « Permission insuffisante ».
  */
  const canManage = user?.permissions.includes('notes.write') ?? false;

  // Les candidats se lisent dans la modale, par pages (chantier « Plafond de 200 »).
  // Lot W2 (D-239) : la validation en cours (ou la dernière) se retrouve après un rechargement.
  // Le vendeur ne lit pas les tâches (NotMerchantGuard) : seulement pour qui valide.
  const bulkJob = canManage ? await getLatestBulkJob(note.code).catch(() => null) : null;

  return <NoteDetailView note={note} canManage={canManage} bulkJob={bulkJob} />;
}
