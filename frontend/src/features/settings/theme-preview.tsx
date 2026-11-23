'use client';

import { DEFAULT_RADIUS, type ThemeTokens } from '@delivery/shared';
import { useTranslations } from 'next-intl';

/**
 * Aperçu d'un jeu de tokens, en vase clos.
 *
 * Il n'utilise AUCUNE classe Tailwind de couleur : tout passe par des styles
 * inline qui lisent les tokens passés en paramètre. C'est ce qui permet
 * d'afficher le mode clair et le mode sombre CÔTE À CÔTE, dans la même page —
 * impossible autrement, puisque `.dark` est une classe unique posée sur `html`.
 *
 * Voir les deux modes ensemble n'est pas un luxe : un admin règle presque
 * toujours en clair, et découvre son sombre illisible le lendemain, par le
 * signalement d'un agent qui travaille de nuit.
 */
export function ThemePreview({ tokens, label }: { tokens: ThemeTokens; label: string }) {
  const t = useTranslations('settings.theme.preview');

  const surface = {
    background: tokens.background,
    color: tokens.foreground,
    borderColor: tokens.border,
    borderRadius: DEFAULT_RADIUS,
  };

  return (
    <div className="flex flex-col gap-2">
      <p className="text-muted-foreground text-xs font-medium tracking-wide uppercase">{label}</p>

      <div className="overflow-hidden border" style={surface}>
        <div className="flex" style={{ minHeight: 240 }}>
          {/* Barre latérale */}
          <div
            className="flex w-28 shrink-0 flex-col gap-1 p-2"
            style={{
              background: tokens.sidebar,
              color: tokens['sidebar-foreground'],
              borderInlineEnd: `1px solid ${tokens['sidebar-border']}`,
            }}
          >
            <div
              className="rounded px-2 py-1.5 text-[10px] font-medium"
              style={{
                background: tokens['sidebar-accent'],
                color: tokens['sidebar-accent-foreground'],
              }}
            >
              {t('active')}
            </div>
            {[t('items.parcels'), t('items.routes'), t('items.clients')].map((item) => (
              <div key={item} className="px-2 py-1.5 text-[10px] opacity-80">
                {item}
              </div>
            ))}
          </div>

          <div className="flex min-w-0 flex-1 flex-col">
            {/* En-tête */}
            <div
              className="flex items-center gap-2 px-3 py-2.5"
              style={{
                background: tokens.header,
                color: tokens['header-foreground'],
                borderBottom: `1px solid ${tokens['header-border']}`,
              }}
            >
              <span className="text-[11px] font-semibold">{t('header')}</span>
              <span
                className="ms-auto rounded-full px-2 py-0.5 text-[9px]"
                style={{ background: tokens.badge, color: tokens['badge-foreground'] }}
              >
                {t('badge')}
              </span>
            </div>

            {/* Tableau */}
            <div className="p-3">
              <div
                className="overflow-hidden border"
                style={{ borderColor: tokens['table-border'], borderRadius: '6px' }}
              >
                <div
                  className="px-2 py-1.5 text-[9px] font-semibold tracking-wide uppercase"
                  style={{
                    background: tokens['table-header'],
                    color: tokens['table-header-foreground'],
                  }}
                >
                  {t('tableHeader')}
                </div>
                {['DDC-2609-000123', 'DDC-2609-000124'].map((code, index) => (
                  <div
                    key={code}
                    className="px-2 py-1.5 font-mono text-[10px]"
                    style={{
                      background: index === 0 ? tokens['table-row-hover'] : 'transparent',
                      borderTop: `1px solid ${tokens['table-border']}`,
                    }}
                  >
                    {code}
                  </div>
                ))}
              </div>

              <div className="mt-3 flex gap-2">
                <span
                  className="rounded px-2.5 py-1 text-[10px] font-medium"
                  style={{
                    background: tokens.primary,
                    color: tokens['primary-foreground'],
                    borderRadius: '6px',
                  }}
                >
                  {t('button')}
                </span>
                <span
                  className="rounded px-2.5 py-1 text-[10px]"
                  style={{
                    border: `1px solid ${tokens.border}`,
                    color: tokens.foreground,
                    borderRadius: '6px',
                  }}
                >
                  {t('secondary')}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
