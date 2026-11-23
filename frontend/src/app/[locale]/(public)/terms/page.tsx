import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { LegalDocument } from '@/features/public/legal-document';

/**
 * Conditions d'utilisation — M1 bloc 9.
 *
 * Apple les réclame pour toute application à comptes ; Google les attend dès
 * qu'un service est rendu. Elles vivent au même endroit que la politique de
 * confidentialité, pour que la fiche des deux magasins pointe un seul domaine.
 */
export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('legal');
  return { title: t('terms.title'), description: t('terms.lead') };
}

/** Le sommaire du document. Le catalogue ne porte que les textes. */
const SECTIONS = [
  'purpose',
  'access',
  'account',
  'use',
  'record',
  'offline',
  // M2/M3 — elles sont un agrément, pas une garantie. Placé après « hors ligne
  // et disponibilité », dont c'est le prolongement direct.
  'notifications',
  'version',
  'permissions',
  'end',
  'law',
] as const;

export default async function TermsPage() {
  const t = await getTranslations('legal.terms.sections');

  return (
    <LegalDocument
      doc="terms"
      sections={SECTIONS.map((key) => ({
        key,
        title: t(`${key}.title`),
        body: t(`${key}.body`),
      }))}
    />
  );
}
