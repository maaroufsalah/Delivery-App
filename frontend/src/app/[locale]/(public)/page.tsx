import { getTranslations } from 'next-intl/server';
import { headers } from 'next/headers';
import { Halo, Section, SectionHeading } from '@/features/public/decor';
import {
  LandingCourierCta,
  LandingFinalCta,
  LandingHero,
  LandingHowItWorks,
  LandingServices,
  LandingStats,
  LandingTutorials,
} from '@/features/public/landing-sections';
import { Reveal } from '@/features/public/motion-primitives';
import { PublicRatesTable } from '@/features/public/rates-table';
import { PlatformLanding } from '@/features/platform/platform-landing';
import { getPublicRates } from '@/lib/api/public';
import { isPlatformHost } from '@/lib/platform-domain';

export default async function LandingPage() {
  /*
   * Le domaine NU ne sert aucun client — C8 bloc 5. Il n'y a donc ni grille
   * tarifaire à y montrer, ni marque : on y présente le PRODUIT à quelqu'un qui
   * n'a pas encore d'espace.
   */
  const store = await headers();
  if (isPlatformHost(store.get('x-forwarded-host') ?? store.get('host'))) {
    return <PlatformLanding />;
  }

  const [rates, t] = await Promise.all([getPublicRates(), getTranslations('public.rates')]);

  return (
    <>
      <LandingHero />
      <LandingStats />
      <LandingServices />

      <Section id="rates" muted>
        <Halo className="start-[-12rem] top-20" size="lg" />

        <Reveal>
          <SectionHeading
            badge={t('badge')}
            title={t('titleLead')}
            highlight={t('titleHighlight')}
            description={t('description')}
          />
        </Reveal>

        {/* La grille publique reste LA DataTable de l'application (règle 2 de
            CLAUDE.md) : seule son enveloppe change, pas son fonctionnement. */}
        <Reveal className="mt-12">
          <PublicRatesTable rates={rates} />
        </Reveal>
      </Section>

      <LandingHowItWorks />
      <LandingCourierCta />
      <LandingTutorials />
      <LandingFinalCta />
    </>
  );
}
