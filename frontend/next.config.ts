import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import createNextIntlPlugin from 'next-intl/plugin';
import type { NextConfig } from 'next';

const here = dirname(fileURLToPath(import.meta.url));

const withNextIntl = createNextIntlPlugin({
  experimental: {
    // Génère le typage des clés de messages à partir de la locale de référence.
    createMessagesDeclaration: './messages/fr.json',
  },
});

const nextConfig: NextConfig = {
  // Package interne consommé en source, sans étape de build.
  transpilePackages: ['@delivery/shared'],

  /*
   * LA LIMITE DES SERVER ACTIONS, CHOISIE — chantier « Audit des paramètres
   * société », bloc 3.
   *
   * Next applique 1 Mo par défaut. Un logo de 512 ko encodé en base64 pèse
   * ~683 ko : il passait, mais sans marge, et le plafond du web aurait masqué
   * le correctif posé côté API. On l'aligne sur la limite de l'API (2 Mo) pour
   * que le refus vienne d'un seul endroit, avec un message lisible.
   */
  experimental: {
    serverActions: { bodySizeLimit: '2mb' },
    /*
     * `forbidden()` et `forbidden.tsx` — lot W1 (E2E-009, D-236). Une page que
     * l'API refuse est rendue « Accès refusé » en HTTP 403, dans la coque, au
     * lieu d'une erreur 500 (`ApiError` 403 porte le `digest` de `forbidden()`).
     */
    authInterrupts: true,
  },

  /*
   * Sortie autonome : Next trace les fichiers réellement atteints et les copie
   * dans `.next/standalone`, avec un serveur minimal. L'image de production n'a
   * alors ni `node_modules` complet, ni gestionnaire de paquets.
   */
  output: 'standalone',

  /*
   * Le traçage doit partir de la RACINE du monorepo, pas de `apps/web` : les
   * dépendances de pnpm vivent dans le `node_modules/.pnpm` de la racine, et
   * `@delivery/shared` est un lien symbolique vers `packages/shared`. Sans
   * cela, Next trace un sous-arbre et l'image démarre sur un module introuvable.
   */
  outputFileTracingRoot: join(here, '../..'),
};

export default withNextIntl(nextConfig);
