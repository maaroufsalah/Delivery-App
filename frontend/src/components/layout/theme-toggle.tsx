'use client';

import { Moon, Sun } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useTheme } from 'next-themes';
import { Button } from '@/components/ui/button';
import { useMounted } from '@/hooks/use-mounted';
import { cn } from '@/lib/utils';

/**
 * La bascule de thème — C9 bloc 2.
 *
 * Un menu déroulant à trois entrées pour un réglage qui n'en a que deux
 * demandait deux gestes là où un seul suffit. Le mode système a disparu avec
 * lui (C9 bloc 1) : il ne restait donc qu'un aller-retour clair ↔ sombre, et
 * c'est exactement ce qu'un bouton unique exprime.
 *
 * **Les deux icônes sont toujours rendues**, l'une masquée par l'autre en
 * rotation. Monter et démonter un SVG au clic ferait clignoter le bouton et lui
 * ferait perdre le focus clavier au moment précis où l'utilisateur vient de
 * l'actionner.
 */
export function ThemeToggle({ className }: { className?: string }) {
  const t = useTranslations('theme');
  const { theme, setTheme } = useTheme();

  // Le thème n'est lisible qu'après hydratation : avant, l'étiquette annoncerait
  // une bascule vers le mode déjà actif.
  const mounted = useMounted();
  const isDark = mounted && theme === 'dark';

  return (
    <Button
      type="button"
      variant="ghost"
      size="icon"
      className={cn(className)}
      onClick={() => setTheme(isDark ? 'light' : 'dark')}
      /*
       * L'étiquette dit vers OÙ l'on va, pas où l'on est : un bouton se nomme
       * par son action. Un lecteur d'écran annonce « Passer en mode sombre »,
       * ce qui se comprend sans voir l'icône.
       */
      aria-label={t(isDark ? 'switchToLight' : 'switchToDark')}
      title={t(isDark ? 'switchToLight' : 'switchToDark')}
    >
      <Sun className="size-4 scale-100 rotate-0 transition-all dark:scale-0 dark:-rotate-90" />
      <Moon className="absolute size-4 scale-0 rotate-90 transition-all dark:scale-100 dark:rotate-0" />
    </Button>
  );
}
