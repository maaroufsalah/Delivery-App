'use client';

import {
  CircleCheckIcon,
  InfoIcon,
  Loader2Icon,
  OctagonXIcon,
  TriangleAlertIcon,
} from 'lucide-react';
import { useTheme } from 'next-themes';
import { Toaster as Sonner, type ToasterProps } from 'sonner';

/**
 * Teinte d'un type de message : un fond à peine coloré, une bordure franche,
 * le texte du popover (lisible garanti, D-046). Les trois couleurs sont des
 * neutres fixes du thème (`destructive`, `warning`, `success`) — jamais la
 * marque : un client au primaire vert ne doit pas voir ses erreurs virer au vert.
 */
const tint = (token: string) => ({
  bg: `color-mix(in oklab, var(--${token}) 14%, var(--popover))`,
  border: `color-mix(in oklab, var(--${token}) 55%, var(--popover))`,
  text: 'var(--popover-foreground)',
});

const success = tint('success');
const warning = tint('warning');
const error = tint('destructive');
const info = tint('primary');

/**
 * LES NOTIFICATIONS — recette 6, point 4.
 *
 * « Trop petites, toutes en noir et blanc. » Un seul composant pour toute
 * l'application : plus grandes (texte 15 px, icône 20 px, 400 px de large), et
 * colorées par type — rouge erreur, orange alerte, vert succès, couleur de la
 * marque pour une information — avec l'icône dans la couleur du type et un
 * liseré au début. Les textes ne changent pas : `toast.success()` /
 * `toast.error()` / `toast.warning()` restent les appels de tous les écrans.
 */
const Toaster = ({ ...props }: ToasterProps) => {
  // `light` et non `system` : le mode système a été retiré (C9 bloc 1), et le
  // repli doit dire la même chose que le défaut de l'application.
  const { theme = 'light' } = useTheme();

  return (
    <Sonner
      theme={theme as ToasterProps['theme']}
      className="toaster group"
      richColors
      icons={{
        success: <CircleCheckIcon className="size-5" style={{ color: 'var(--success)' }} />,
        info: <InfoIcon className="size-5" style={{ color: 'var(--primary)' }} />,
        warning: <TriangleAlertIcon className="size-5" style={{ color: 'var(--warning)' }} />,
        error: <OctagonXIcon className="size-5" style={{ color: 'var(--destructive)' }} />,
        loading: <Loader2Icon className="size-5 animate-spin" />,
      }}
      toastOptions={{
        classNames: {
          toast: 'gap-3! px-4! py-3.5! text-[15px]! border-s-4!',
          title: 'font-medium! leading-snug!',
          description: 'text-sm!',
        },
      }}
      style={
        {
          '--width': '400px',
          '--normal-bg': 'var(--popover)',
          '--normal-text': 'var(--popover-foreground)',
          '--normal-border': 'var(--border)',
          '--success-bg': success.bg,
          '--success-border': success.border,
          '--success-text': success.text,
          '--warning-bg': warning.bg,
          '--warning-border': warning.border,
          '--warning-text': warning.text,
          '--error-bg': error.bg,
          '--error-border': error.border,
          '--error-text': error.text,
          '--info-bg': info.bg,
          '--info-border': info.border,
          '--info-text': info.text,
          '--border-radius': 'var(--radius)',
        } as React.CSSProperties
      }
      {...props}
    />
  );
};

export { Toaster };
