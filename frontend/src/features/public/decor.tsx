import { cn } from '@/lib/utils';

/**
 * Décor de la landing : halos, badges de section, titres.
 *
 * Aucun `use client` ici — ce sont des composants serveur, rendus dans le HTML
 * statique. Rien à animer : les halos sont immobiles, seule leur apparition est
 * animée par le `<Reveal>` qui les entoure, quand il y en a un.
 *
 * Toutes les couleurs sortent de `var(--primary)` / `var(--secondary)`, jamais
 * d'une valeur en dur : changer la couleur primaire dans Paramètres > Société
 * repeint le décor (règle 3 de CLAUDE.md).
 */

/**
 * Halo diffus. Purement décoratif, donc `aria-hidden` et hors flux.
 *
 * `start`/`end` plutôt que `left`/`right` : en arabe le halo bascule de l'autre
 * côté, et la composition reste équilibrée au lieu de se retrouver à contresens.
 */
export function Halo({
  className,
  tone = 'primary',
  size = 'lg',
}: {
  className?: string;
  tone?: 'primary' | 'secondary';
  size?: 'md' | 'lg' | 'xl';
}) {
  const dimensions = {
    md: 'size-64 sm:size-80',
    lg: 'size-80 sm:size-[28rem]',
    xl: 'size-96 sm:size-[34rem]',
  }[size];

  return (
    <span
      aria-hidden
      className={cn(
        'pointer-events-none absolute -z-10 rounded-full',
        // Flou large et opacité faible : on veut une teinte dans l'air, pas
        // une tache. Le flou est plus marqué en sombre, où le halo doit
        // rester perceptible sans éclaircir le fond.
        'blur-[80px] sm:blur-[110px]',
        dimensions,
        className,
      )}
      style={{
        background:
          tone === 'primary'
            ? 'radial-gradient(circle, color-mix(in oklab, var(--primary) 55%, transparent), transparent 70%)'
            : 'radial-gradient(circle, color-mix(in oklab, var(--chart-3) 45%, transparent), transparent 70%)',
        opacity: 0.22,
      }}
    />
  );
}

/** Conteneur de section : borne la largeur et sert d'ancre aux halos. */
export function Section({
  children,
  className,
  id,
  muted = false,
}: {
  children: React.ReactNode;
  className?: string;
  id?: string;
  muted?: boolean;
}) {
  return (
    <section
      id={id}
      className={cn('relative isolate overflow-hidden', muted && 'bg-muted/40 border-y', className)}
    >
      <div className="mx-auto w-full max-w-6xl px-4 py-20 sm:px-6 sm:py-28">{children}</div>
    </section>
  );
}

/** Badge pill posé au-dessus d'un titre de section. */
export function SectionBadge({ children }: { children: React.ReactNode }) {
  return (
    <span
      className="inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-medium"
      style={{
        backgroundColor: 'color-mix(in oklab, var(--primary) 10%, transparent)',
        borderColor: 'color-mix(in oklab, var(--primary) 28%, transparent)',
        color: 'var(--accent-foreground)',
      }}
    >
      <span
        className="size-1.5 rounded-full"
        style={{ backgroundColor: 'var(--primary)' }}
        aria-hidden
      />
      {children}
    </span>
  );
}

/**
 * Titre de section : badge, titre, sous-titre.
 *
 * `highlight` met UN mot en couleur primaire. Il est passé séparément plutôt
 * qu'inséré dans la chaîne traduite : mettre du balisage dans un fichier de
 * messages rend la traduction impossible à relire, et le mot accentué n'est pas
 * au même endroit de la phrase en arabe.
 */
export function SectionHeading({
  badge,
  title,
  highlight,
  description,
  align = 'start',
  className,
}: {
  badge: string;
  title: string;
  highlight?: string;
  description?: string;
  align?: 'start' | 'center';
  className?: string;
}) {
  return (
    <div className={cn('max-w-2xl', align === 'center' && 'mx-auto text-center', className)}>
      <SectionBadge>{badge}</SectionBadge>

      <h2 className="mt-5 text-3xl font-semibold tracking-tight text-balance sm:text-4xl lg:text-[2.75rem] lg:leading-[1.1]">
        {title}
        {highlight ? (
          <>
            {' '}
            <span className="text-primary">{highlight}</span>
          </>
        ) : null}
      </h2>

      {description ? (
        <p className="text-muted-foreground mt-4 text-base text-pretty sm:text-lg">{description}</p>
      ) : null}
    </div>
  );
}

/**
 * Card de la landing : grand rayon, ombre douce, et une élévation au survol.
 *
 * L'élévation est une transition CSS, pas une animation `motion` : un effet de
 * survol doit répondre au premier pixel, sans attendre qu'un moteur d'animation
 * s'initialise. `motion-reduce:transform-none` la neutralise pour qui a demandé
 * moins de mouvement.
 */
export function LandingCard({
  children,
  className,
  interactive = true,
}: {
  children: React.ReactNode;
  className?: string;
  interactive?: boolean;
}) {
  return (
    <div
      className={cn(
        'bg-card relative h-full overflow-hidden rounded-2xl border p-6',
        'shadow-[0_1px_2px_rgba(0,0,0,0.04),0_8px_24px_-12px_rgba(0,0,0,0.10)]',
        interactive &&
          'transition-[transform,box-shadow,border-color] duration-300 hover:-translate-y-1 hover:shadow-[0_2px_4px_rgba(0,0,0,0.04),0_20px_40px_-16px_rgba(0,0,0,0.18)] motion-reduce:transform-none motion-reduce:transition-none',
        interactive && 'hover:border-primary/30',
        className,
      )}
    >
      {children}
    </div>
  );
}

/** Pastille d'icône, teintée de la couleur primaire. */
export function IconBadge({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <span
      className={cn('flex size-11 shrink-0 items-center justify-center rounded-xl', className)}
      style={{
        backgroundColor: 'color-mix(in oklab, var(--primary) 12%, transparent)',
        color: 'var(--accent-foreground)',
      }}
    >
      {children}
    </span>
  );
}
