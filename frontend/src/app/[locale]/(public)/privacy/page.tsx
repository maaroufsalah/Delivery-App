import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { LegalDocument } from '@/features/public/legal-document';

/**
 * Politique de confidentialité — M1 bloc 9.
 *
 * Exigée par l'App Store et par Google Play : la fiche de l'application doit
 * pointer une URL publique, atteignable sans compte, et la même page doit
 * exister dans les trois langues que l'application propose.
 */
export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('legal');
  return { title: t('privacy.title'), description: t('privacy.lead') };
}

/** Le sommaire du document. Le catalogue ne porte que les textes. */
const SECTIONS = [
  'who',
  'collected',
  'why',
  'not',
  'photos',
  // M2/M3 — le jeton d'appareil et le chemin d'une notification. Placé après
  // les photos : les deux répondent à « qu'est-ce qui quitte mon téléphone ».
  'notifications',
  'keeping',
  'shared',
  'rights',
  'security',
  'complaint',
] as const;

export default async function PrivacyPage() {
  const t = await getTranslations('legal.privacy.sections');

  return (
    <LegalDocument
      doc="privacy"
      sections={SECTIONS.map((key) => ({
        key,
        title: t(`${key}.title`),
        body: t(`${key}.body`),
      }))}
    />
  );
}
