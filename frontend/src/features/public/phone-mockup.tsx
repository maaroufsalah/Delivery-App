import { CheckCircle2, MapPin, PackageSearch, Truck } from 'lucide-react';
import { getTranslations } from 'next-intl/server';

/**
 * Mockup du suivi public, en CSS.
 *
 * Aucune image : c'est la contrainte de LCP. Un visuel de téléphone en PNG
 * pèserait quelques centaines de kilo-octets et serait, sur cette page, l'élément
 * le plus grand — donc celui que mesure le LCP. Ici le plus grand élément est
 * du texte, déjà présent dans le HTML.
 *
 * Le contenu n'est pas décoratif non plus : ce sont les vraies familles de
 * statut du suivi public (`public.track.groups`), traduites. Ce que l'écran
 * montre est ce que le visiteur obtiendra.
 *
 * Composant SERVEUR : il est dans le HTML statique, il ne coûte pas un octet de
 * JavaScript.
 */

const STEPS = [
  { key: 'terminal', icon: CheckCircle2, color: '#6AB04C', time: '14:32' },
  { key: 'pivot', icon: MapPin, color: '#FFA801', time: '09:10' },
  { key: 'transit', icon: Truck, color: '#2F3640', time: 'J-1' },
  { key: 'initial', icon: PackageSearch, color: '#D35400', time: 'J-2' },
] as const;

export async function PhoneMockup() {
  const [t, tGroup] = await Promise.all([
    getTranslations('public.hero'),
    getTranslations('public.track.groups'),
  ]);

  return (
    <div className="relative mx-auto w-[17rem] shrink-0 sm:w-[19rem]">
      {/* Châssis. Le dégradé de bordure évite l'aplat gris d'un `border` nu. */}
      <div
        className="rounded-[2.5rem] p-2.5 shadow-[0_24px_60px_-20px_rgba(0,0,0,0.35)]"
        style={{
          background:
            'linear-gradient(160deg, color-mix(in oklab, var(--secondary) 92%, white), color-mix(in oklab, var(--secondary) 100%, black))',
        }}
      >
        <div className="bg-background relative overflow-hidden rounded-[2rem]">
          {/* Encoche */}
          <div className="absolute inset-x-0 top-0 z-10 flex justify-center">
            <span
              className="mt-2 h-5 w-24 rounded-full"
              style={{ backgroundColor: 'color-mix(in oklab, var(--secondary) 100%, black)' }}
              aria-hidden
            />
          </div>

          <div className="px-4 pt-10 pb-5">
            {/* En-tête de l'écran de suivi */}
            <div className="flex items-center justify-between gap-2">
              <span className="font-mono text-[11px] font-medium tracking-tight">
                CAS110012609RAB
              </span>
              <span
                className="rounded-full px-2 py-0.5 text-[10px] font-medium"
                style={{
                  backgroundColor: 'color-mix(in oklab, #6AB04C 16%, transparent)',
                  color: '#6AB04C',
                }}
              >
                {tGroup('terminal')}
              </span>
            </div>

            <p className="text-muted-foreground mt-1 text-[11px]">{t('mockupDestination')}</p>

            {/* Timeline — même vocabulaire que /track */}
            <ol className="mt-4 flex flex-col">
              {STEPS.map((step, index) => {
                const Icon = step.icon;
                const last = index === STEPS.length - 1;

                return (
                  <li key={step.key} className="flex gap-2.5">
                    <div className="flex flex-col items-center">
                      <span
                        className="flex size-6 shrink-0 items-center justify-center rounded-full border"
                        style={{
                          backgroundColor: `color-mix(in oklab, ${step.color} 14%, transparent)`,
                          borderColor: `color-mix(in oklab, ${step.color} 32%, transparent)`,
                          color: step.color,
                        }}
                      >
                        <Icon className="size-3" aria-hidden />
                      </span>
                      {last ? null : <span className="bg-border w-px flex-1" aria-hidden />}
                    </div>

                    <div className={last ? 'min-w-0 flex-1 pb-0' : 'min-w-0 flex-1 pb-4'}>
                      <p className="truncate text-[11px] font-medium">{tGroup(step.key)}</p>
                      <p className="text-muted-foreground text-[10px] tabular-nums">{step.time}</p>
                    </div>
                  </li>
                );
              })}
            </ol>
          </div>
        </div>
      </div>

      {/* Reflet discret, pour que le châssis ne paraisse pas plat. */}
      <span
        aria-hidden
        className="pointer-events-none absolute inset-0 rounded-[2.5rem]"
        style={{
          background: 'linear-gradient(150deg, rgba(255,255,255,0.14), transparent 42%)',
        }}
      />
    </div>
  );
}
