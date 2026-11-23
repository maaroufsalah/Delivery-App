import {
  ArrowRight,
  Banknote,
  Bike,
  CheckCircle2,
  ClipboardCheck,
  MapPin,
  PackageCheck,
  PlayCircle,
  RefreshCw,
  Search,
  Truck,
  Undo2,
  Warehouse,
} from 'lucide-react';
import { getTranslations } from 'next-intl/server';
import { Button } from '@/components/ui/button';
import { Halo, IconBadge, LandingCard, Section, SectionBadge, SectionHeading } from './decor';
import { Reveal, RevealGroup } from './motion-primitives';
import { PhoneMockup } from './phone-mockup';
import { TrackingSearch } from '@/features/public/tracking-view';
import { Link } from '@/i18n/navigation';
import { getCompanySettings } from '@/lib/api/settings';

/**
 * Sections de la landing.
 *
 * Composants SERVEUR : le contenu est dans le HTML statique. Seules les
 * enveloppes `<Reveal>` sont clientes, et elles ne portent qu'une apparition —
 * retirer le JavaScript laisserait une page complète et lisible.
 *
 * Le hero n'est PAS animé : c'est le contenu au-dessus de la ligne de flottaison
 * et l'élément qui décide du LCP. Le faire apparaître en fondu retarderait
 * mécaniquement la mesure.
 */

export async function LandingHero() {
  const [settings, t] = await Promise.all([getCompanySettings(), getTranslations('public.hero')]);

  return (
    <section className="relative isolate overflow-hidden">
      {/* Halos : un chaud côté texte, un froid derrière le mockup. */}
      <Halo className="start-[-8rem] -top-32" size="xl" />
      <Halo className="end-[-10rem] top-24" size="lg" tone="secondary" />

      <div className="mx-auto w-full max-w-6xl px-4 pt-16 pb-20 sm:px-6 sm:pt-24 sm:pb-28">
        <div className="grid items-center gap-14 lg:grid-cols-[1.05fr_1fr]">
          <div>
            <SectionBadge>{t('badge')}</SectionBadge>

            <h1 className="mt-5 text-4xl font-semibold tracking-tight text-balance sm:text-5xl lg:text-6xl lg:leading-[1.05]">
              {t('titleLead', { company: settings.name })}{' '}
              <span className="text-primary">{t('titleHighlight')}</span>
            </h1>

            <p className="text-muted-foreground mt-5 max-w-xl text-base text-pretty sm:text-lg">
              {t('subtitle')}
            </p>

            <div className="mt-8 flex flex-wrap gap-3">
              <Button asChild size="lg" className="h-12 gap-1.5 rounded-xl px-6 text-base">
                <Link href="/login">
                  {t('ctaPrimary')}
                  <ArrowRight className="flip-rtl size-4" aria-hidden />
                </Link>
              </Button>
              <Button
                asChild
                size="lg"
                variant="outline"
                className="h-12 gap-1.5 rounded-xl px-6 text-base"
              >
                <Link href="/track">
                  <Search className="size-4" aria-hidden />
                  {t('ctaSecondary')}
                </Link>
              </Button>
            </div>

            {/* Suivi en accès direct : c'est la première question d'un visiteur. */}
            <LandingCard interactive={false} className="mt-10 max-w-md rounded-2xl p-5">
              <p className="text-sm font-medium">{t('trackTitle')}</p>
              <div className="mt-3">
                <TrackingSearch />
              </div>
              <p className="text-muted-foreground mt-2.5 text-xs">{t('trackHint')}</p>
            </LandingCard>
          </div>

          <div className="justify-self-center lg:justify-self-end">
            <PhoneMockup />
          </div>
        </div>
      </div>
    </section>
  );
}

export async function LandingStats() {
  const t = await getTranslations('public.stats');
  const items = ['cities', 'parcels', 'couriers', 'rate'] as const;

  return (
    <section className="border-y">
      <div className="mx-auto w-full max-w-6xl px-4 py-14 sm:px-6">
        <RevealGroup className="grid grid-cols-2 gap-8 lg:grid-cols-4">
          {items.map((key) => (
            <div key={key}>
              <p className="text-primary text-3xl font-semibold tracking-tight tabular-nums sm:text-4xl">
                {t(`${key}.value`)}
              </p>
              <p className="text-muted-foreground mt-1.5 text-sm">{t(`${key}.label`)}</p>
            </div>
          ))}
        </RevealGroup>
      </div>
    </section>
  );
}

const SERVICE_ICONS = {
  delivery: Truck,
  pickup: ClipboardCheck,
  cod: Banknote,
  returns: Undo2,
  warehouse: Warehouse,
  tracking: MapPin,
} as const;

export async function LandingServices() {
  const t = await getTranslations('public.services');
  const keys = Object.keys(SERVICE_ICONS) as Array<keyof typeof SERVICE_ICONS>;

  return (
    <Section id="services">
      <Halo className="end-[-12rem] top-10" size="lg" />

      <Reveal>
        <SectionHeading
          badge={t('badge')}
          title={t('titleLead')}
          highlight={t('titleHighlight')}
          description={t('description')}
        />
      </Reveal>

      <RevealGroup className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
        {keys.map((key) => {
          const Icon = SERVICE_ICONS[key];
          return (
            <LandingCard key={key}>
              <IconBadge>
                <Icon className="size-5" aria-hidden />
              </IconBadge>
              <h3 className="mt-4 text-base font-semibold">{t(`items.${key}.title`)}</h3>
              <p className="text-muted-foreground mt-2 text-sm">{t(`items.${key}.description`)}</p>
            </LandingCard>
          );
        })}
      </RevealGroup>
    </Section>
  );
}

const STEP_ICONS = [ClipboardCheck, Truck, RefreshCw, PackageCheck, Banknote];

export async function LandingHowItWorks() {
  const t = await getTranslations('public.howItWorks');
  const steps = ['create', 'pickup', 'transit', 'deliver', 'remit'] as const;

  return (
    <Section muted>
      <Halo className="start-[-12rem] top-1/3" size="lg" />

      <Reveal>
        <SectionHeading
          badge={t('badge')}
          title={t('titleLead')}
          highlight={t('titleHighlight')}
          description={t('description')}
          align="center"
        />
      </Reveal>

      <div className="relative mt-14">
        {/*
          Ligne pointillée reliant les étapes. Une bordure `dashed` plutôt qu'un
          SVG : elle n'a ni début ni fin à retourner en arabe, et elle disparaît
          sous `lg` où les étapes s'empilent.
        */}
        <div
          aria-hidden
          className="border-border absolute inset-x-[10%] top-6 hidden border-t-2 border-dashed lg:block"
        />

        <RevealGroup as="ul" className="relative grid gap-8 sm:grid-cols-2 lg:grid-cols-5">
          {steps.map((step, index) => {
            const Icon = STEP_ICONS[index] ?? ClipboardCheck;
            return (
              <div key={step} className="flex h-full flex-col items-center text-center">
                <span className="bg-primary text-primary-foreground ring-background relative z-10 flex size-12 items-center justify-center rounded-full text-sm font-semibold tabular-nums ring-8">
                  {index + 1}
                </span>
                <Icon className="text-primary mt-4 size-5" aria-hidden />
                <h3 className="mt-3 text-sm font-semibold">{t(`steps.${step}.title`)}</h3>
                <p className="text-muted-foreground mt-1.5 text-sm">
                  {t(`steps.${step}.description`)}
                </p>
              </div>
            );
          })}
        </RevealGroup>
      </div>
    </Section>
  );
}

export async function LandingCourierCta() {
  const t = await getTranslations('public.courier');
  const perks = ['income', 'schedule', 'app'] as const;

  return (
    <Section>
      <Halo className="end-[-10rem] bottom-0" size="lg" />

      <Reveal>
        <LandingCard interactive={false} className="rounded-3xl p-8 sm:p-12">
          <div className="grid gap-10 lg:grid-cols-2 lg:items-center">
            <div>
              <IconBadge className="size-12">
                <Bike className="size-6" aria-hidden />
              </IconBadge>

              <h2 className="mt-5 text-3xl font-semibold tracking-tight text-balance sm:text-4xl">
                {t('titleLead')} <span className="text-primary">{t('titleHighlight')}</span>
              </h2>
              <p className="text-muted-foreground mt-4 text-base text-pretty">{t('description')}</p>

              <Button asChild size="lg" className="mt-7 h-12 gap-1.5 rounded-xl px-6 text-base">
                <Link href="/login">
                  {t('cta')}
                  <ArrowRight className="flip-rtl size-4" aria-hidden />
                </Link>
              </Button>
            </div>

            <ul className="flex flex-col gap-5">
              {perks.map((perk) => (
                <li key={perk} className="flex items-start gap-3.5">
                  <CheckCircle2 className="text-primary mt-0.5 size-5 shrink-0" aria-hidden />
                  <div>
                    <p className="text-sm font-semibold">{t(`perks.${perk}.title`)}</p>
                    <p className="text-muted-foreground mt-1 text-sm">
                      {t(`perks.${perk}.description`)}
                    </p>
                  </div>
                </li>
              ))}
            </ul>
          </div>
        </LandingCard>
      </Reveal>
    </Section>
  );
}

export async function LandingTutorials() {
  const t = await getTranslations('public.tutorials');
  const videos = ['create', 'track', 'invoice'] as const;

  return (
    <Section muted>
      <Reveal>
        <SectionHeading
          badge={t('badge')}
          title={t('titleLead')}
          highlight={t('titleHighlight')}
          description={t('description')}
        />
      </Reveal>

      <RevealGroup className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
        {videos.map((video) => (
          <LandingCard key={video} className="flex flex-col p-0">
            {/*
              Vignette : `aspect-video` et un dégradé de la couleur primaire.
              Ni fichier vidéo, ni image — la contrainte de LCP tient à ce que
              rien de lourd ne soit chargé sur cette page.
            */}
            <div
              className="relative flex aspect-video max-w-full items-center justify-center"
              style={{
                background:
                  'linear-gradient(135deg, color-mix(in oklab, var(--primary) 18%, transparent), color-mix(in oklab, var(--chart-3) 14%, transparent))',
              }}
            >
              <span className="bg-background/90 flex size-14 items-center justify-center rounded-full shadow-lg">
                <PlayCircle className="text-primary size-7" aria-hidden />
              </span>
              <span className="bg-background/90 absolute end-3 bottom-3 rounded-md px-2 py-0.5 text-[11px] font-medium tabular-nums">
                {t(`videos.${video}.duration`)}
              </span>
            </div>

            <div className="flex flex-col gap-2 p-6">
              <h3 className="text-base font-semibold">{t(`videos.${video}.title`)}</h3>
              <p className="text-muted-foreground text-sm">{t(`videos.${video}.description`)}</p>
            </div>
          </LandingCard>
        ))}
      </RevealGroup>
    </Section>
  );
}

/** Dernier appel à l'action, dans une grande card centrée. */
export async function LandingFinalCta() {
  const t = await getTranslations('public.finalCta');

  return (
    <Section>
      {/* `inset-0 m-auto` centre un élément de taille fixe sans translation :
          une translation horizontale serait à inverser en arabe. */}
      <Halo className="inset-0 m-auto" size="xl" />

      <Reveal>
        <LandingCard
          interactive={false}
          className="rounded-3xl px-6 py-14 text-center sm:px-12 sm:py-20"
        >
          <div className="mx-auto max-w-2xl">
            <SectionBadge>{t('badge')}</SectionBadge>

            <h2 className="mt-5 text-3xl font-semibold tracking-tight text-balance sm:text-4xl lg:text-5xl lg:leading-[1.1]">
              {t('titleLead')} <span className="text-primary">{t('titleHighlight')}</span>
            </h2>
            <p className="text-muted-foreground mt-4 text-base text-pretty sm:text-lg">
              {t('description')}
            </p>

            <div className="mt-8 flex flex-wrap justify-center gap-3">
              <Button asChild size="lg" className="h-12 gap-1.5 rounded-xl px-6 text-base">
                <Link href="/login">
                  {t('ctaPrimary')}
                  <ArrowRight className="flip-rtl size-4" aria-hidden />
                </Link>
              </Button>
              <Button
                asChild
                size="lg"
                variant="outline"
                className="h-12 gap-1.5 rounded-xl px-6 text-base"
              >
                <Link href="/track">
                  <Search className="size-4" aria-hidden />
                  {t('ctaSecondary')}
                </Link>
              </Button>
            </div>
          </div>
        </LandingCard>
      </Reveal>
    </Section>
  );
}
