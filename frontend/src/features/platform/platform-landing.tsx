import { ArrowRight, Boxes, Mail, Palette, PhoneCall, Rocket, ShieldCheck } from 'lucide-react';
import { getTranslations } from 'next-intl/server';
import { Button } from '@/components/ui/button';
import {
  Halo,
  IconBadge,
  LandingCard,
  Section,
  SectionBadge,
  SectionHeading,
} from '@/features/public/decor';
import { Reveal, RevealGroup } from '@/features/public/motion-primitives';
import { TrialRequestForm } from '@/features/platform/trial-request-form';

/**
 * La landing du domaine NU — C8 bloc 5, réécrite au C9 bloc 5.
 *
 * `delivery.nocido.com` sans sous-domaine n'appartient à aucun client : il n'y
 * a donc ni grille tarifaire ni suivi de colis à y montrer. Cette page parle du
 * PRODUIT, à quelqu'un qui n'a pas encore d'espace.
 *
 * **Elle était grise, et ce n'était pas un choix de design.** Le domaine nu
 * n'ayant pas de tenant, `GET /public/company` répondait 404 et le web
 * retombait sur la marque NEUTRE — celle qui sert quand une configuration
 * manque. La vitrine commerciale était donc peinte avec la palette d'un
 * incident. `PLATFORM_COMPANY_SETTINGS` corrige la cause : le domaine nu a
 * maintenant sa marque, et tout ce qui lit `var(--primary)` s'y accorde.
 *
 * **La page reste NEUTRE vis-à-vis des CLIENTS** — c'était déjà la règle, elle
 * ne change pas. Aucune couleur de tenant, aucun nom de client : les sociétés
 * hébergées n'ont pas à se découvrir l'une l'autre ici. La marque appliquée est
 * celle de la plateforme, qui n'appartient à aucune d'elles.
 *
 * **Aucune inscription automatique en v1** (D-050). Le formulaire dépose une
 * demande que la console affiche ; un humain rappelle et crée l'espace. Les
 * trois étapes le disent en clair plutôt que de le laisser découvrir : un
 * formulaire qui ressemble à une inscription et n'en est pas une laisse le
 * visiteur attendre un e-mail qui ne viendra jamais.
 *
 * Le décor et les apparitions sont ceux des landings tenants, importés tels
 * quels. Il n'y a pas deux bibliothèques de décor à tenir.
 */

/** L'adresse à laquelle on écrit quand le formulaire ne passe pas. */
const CONTACT_EMAIL = 'contact@nocido.com';

/** L'ancre du formulaire, visée par les deux appels à l'action. */
const TRIAL_ANCHOR = 'essai';

export async function PlatformLanding() {
  const t = await getTranslations('platform.landing');

  const stats = ['trial', 'locales', 'cities', 'setup'] as const;
  const points = [
    { key: 'brand', icon: Palette },
    { key: 'isolated', icon: ShieldCheck },
    { key: 'ready', icon: Boxes },
  ] as const;
  const steps = [
    { key: 'one', icon: Mail },
    { key: 'two', icon: PhoneCall },
    { key: 'three', icon: Rocket },
  ] as const;

  return (
    <>
      {/*
        Le hero n'est PAS enveloppé d'un `<Reveal>` : c'est le contenu au-dessus
        de la ligne de flottaison et l'élément qui décide du LCP. Le faire
        apparaître en fondu retarderait mécaniquement la mesure.
      */}
      <section className="relative isolate overflow-hidden">
        <Halo className="start-[-8rem] -top-32" size="xl" />
        <Halo className="end-[-10rem] top-20" size="lg" tone="secondary" />

        <div className="mx-auto w-full max-w-6xl px-4 pt-16 pb-20 sm:px-6 sm:pt-24 sm:pb-28">
          <div className="max-w-3xl">
            <SectionBadge>{t('hero.badge')}</SectionBadge>

            <h1 className="mt-5 text-4xl font-semibold tracking-tight text-balance sm:text-5xl lg:text-6xl lg:leading-[1.05]">
              {t('hero.titleLead')} <span className="text-primary">{t('hero.titleHighlight')}</span>
            </h1>

            <p className="text-muted-foreground mt-5 max-w-2xl text-base text-pretty sm:text-lg">
              {t('hero.subtitle')}
            </p>

            <div className="mt-8 flex flex-wrap gap-3">
              <Button asChild size="lg" className="h-12 gap-1.5 rounded-xl px-6 text-base">
                <a href={`#${TRIAL_ANCHOR}`}>
                  {t('hero.ctaPrimary')}
                  <ArrowRight className="flip-rtl size-4" aria-hidden />
                </a>
              </Button>
              <Button
                asChild
                size="lg"
                variant="outline"
                className="h-12 gap-1.5 rounded-xl px-6 text-base"
              >
                <a href={`mailto:${CONTACT_EMAIL}`}>
                  <Mail className="size-4" aria-hidden />
                  {t('hero.ctaSecondary')}
                </a>
              </Button>
            </div>

            <p className="text-muted-foreground mt-4 text-sm">{t('hero.trustNote')}</p>
          </div>

          {/*
            Bandeau de chiffres. Ce sont des FAITS du produit (durée d'essai,
            langues livrées, référentiel marocain présent au premier jour), pas
            des métriques d'usage : nous n'avons pas de chiffres d'adoption à
            afficher, et en inventer sur une page commerciale serait un
            mensonge qui se retournerait au premier rendez-vous.
          */}
          <Reveal className="mt-16">
            <dl className="grid grid-cols-2 gap-x-6 gap-y-8 border-t pt-10 lg:grid-cols-4">
              {stats.map((key) => (
                <div key={key} className="flex flex-col gap-1">
                  <dt className="text-primary text-2xl font-semibold tracking-tight sm:text-3xl">
                    {t(`stats.${key}.value`)}
                  </dt>
                  <dd className="text-muted-foreground text-sm text-pretty">
                    {t(`stats.${key}.label`)}
                  </dd>
                </div>
              ))}
            </dl>
          </Reveal>
        </div>
      </section>

      <Section id="produit" muted>
        <Halo className="start-[-12rem] top-16" size="lg" />

        <Reveal>
          <SectionHeading
            badge={t('features.badge')}
            title={t('features.title')}
            highlight={t('features.titleHighlight')}
            description={t('features.description')}
          />
        </Reveal>

        <RevealGroup className="mt-12 grid gap-6 sm:grid-cols-3">
          {points.map(({ key, icon: Icon }) => (
            <LandingCard key={key} className="flex flex-col gap-3">
              <IconBadge>
                <Icon className="size-5" aria-hidden />
              </IconBadge>
              <h3 className="text-base font-semibold">{t(`points.${key}.title`)}</h3>
              <p className="text-muted-foreground text-sm text-pretty">{t(`points.${key}.body`)}</p>
            </LandingCard>
          ))}
        </RevealGroup>
      </Section>

      <Section id="demarche">
        <Halo className="end-[-12rem] top-24" size="lg" tone="secondary" />

        <Reveal>
          <SectionHeading
            badge={t('steps.badge')}
            title={t('steps.title')}
            highlight={t('steps.titleHighlight')}
            description={t('steps.description')}
          />
        </Reveal>

        <RevealGroup className="mt-12 grid gap-6 sm:grid-cols-3">
          {steps.map(({ key, icon: Icon }, index) => (
            <LandingCard key={key} className="flex flex-col gap-3">
              <div className="flex items-center gap-3">
                <IconBadge>
                  <Icon className="size-5" aria-hidden />
                </IconBadge>
                {/*
                  Le rang est décoratif : l'ordre est déjà porté par la lecture
                  et par la grille. `aria-hidden` évite qu'un lecteur d'écran
                  annonce « un » avant chaque titre.
                */}
                <span
                  className="text-muted-foreground/50 text-3xl font-semibold tabular-nums"
                  aria-hidden
                >
                  {index + 1}
                </span>
              </div>
              <h3 className="text-base font-semibold">{t(`steps.${key}.title`)}</h3>
              <p className="text-muted-foreground text-sm text-pretty">{t(`steps.${key}.body`)}</p>
            </LandingCard>
          ))}
        </RevealGroup>
      </Section>

      <Section id={TRIAL_ANCHOR} muted className="scroll-mt-16">
        <Halo className="start-[-10rem] top-10" size="lg" />

        <Reveal>
          <SectionHeading
            badge={t('badge')}
            title={t('final.title')}
            highlight={t('final.titleHighlight')}
            description={t('final.description')}
            align="center"
            className="mx-auto"
          />
        </Reveal>

        <Reveal className="mx-auto mt-12 w-full max-w-2xl">
          <TrialRequestForm contactEmail={CONTACT_EMAIL} />
        </Reveal>
      </Section>
    </>
  );
}
