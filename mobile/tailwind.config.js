/**
 * Tailwind 3 — imposé par NativeWind 4, la ligne STABLE.
 *
 * Le web est en Tailwind 4 (CSS-first, sans ce fichier). Les deux cohabitent
 * parce que pnpm isole les dépendances par paquet : `apps/mobile` a sa
 * version, `apps/web` la sienne, et aucune configuration n'est partagée entre
 * eux — un thème natif et un thème navigateur ne produisent pas le même CSS.
 *
 * NativeWind 5 vise Tailwind 4 mais n'est qu'en release candidate ; la règle du
 * dépôt est de ne pas embarquer de préversion.
 *
 * **Aucune couleur n'est écrite ici.** Elles viennent du tenant, en variables
 * CSS posées au démarrage depuis `GET /public/company` (D-075) — exactement
 * comme `<CompanyStyle />` le fait sur le web.
 */
module.exports = {
  content: ['./app/**/*.{ts,tsx}', './src/**/*.{ts,tsx}'],
  presets: [require('nativewind/preset')],
  theme: {
    extend: {
      colors: {
        background: 'var(--background)',
        foreground: 'var(--foreground)',
        card: 'var(--card)',
        'card-foreground': 'var(--card-foreground)',
        primary: 'var(--primary)',
        'primary-foreground': 'var(--primary-foreground)',
        muted: 'var(--muted)',
        'muted-foreground': 'var(--muted-foreground)',
        border: 'var(--border)',
        destructive: 'var(--destructive)',
        'destructive-foreground': 'var(--destructive-foreground)',
        success: 'var(--success)',
        'success-foreground': 'var(--success-foreground)',
        warning: 'var(--warning)',
        'warning-foreground': 'var(--warning-foreground)',
      },
    },
  },
  plugins: [],
};
