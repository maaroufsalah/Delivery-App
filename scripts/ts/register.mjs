// Résout les imports sans extension de packages/shared (`./enums` → `./enums.ts`).
import { registerHooks } from 'node:module';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

registerHooks({
  resolve(specifier, context, next) {
    // Alias de Speedy : le paquet partagé (copie locale) et `@/` (sources de l'API, lecture seule).
    if (specifier === '@delivery/shared') {
      return next(new URL('../../packages/shared/src/index.ts', import.meta.url).href, context);
    }
    if (specifier.startsWith('@/')) {
      const api = process.env.SPEEDY_API_SRC ?? 'D:/Work/Projects/Freelance/Anass/delivery-platform/apps/api/src/';
      const base = new URL(specifier.slice(2), `file:///${api}`);
      for (const suffix of ['.ts', '/index.ts', '']) {
        const candidate = new URL(base.href + suffix);
        if (existsSync(fileURLToPath(candidate))) return next(candidate.href, context);
      }
    }
    if ((specifier.startsWith('.') || specifier.startsWith('/')) && context.parentURL) {
      const base = new URL(specifier, context.parentURL);
      for (const suffix of ['', '.ts', '/index.ts']) {
        const candidate = new URL(base.href + suffix);
        const path = fileURLToPath(candidate);
        if (suffix === '' && !/[.][cm]?[jt]s$/.test(path)) continue;
        if (existsSync(path)) return next(candidate.href, context);
      }
    }
    try {
      return next(specifier, context);
    } catch (error) {
      // Paquets npm (zod…) : résolus depuis l'installation de Speedy, en lecture seule.
      const speedy = process.env.SPEEDY_SHARED ?? 'D:/Work/Projects/Freelance/Anass/delivery-platform/packages/shared/package.json';
      if (specifier.startsWith('.') || !existsSync(speedy)) throw error;
      return next(specifier, { ...context, parentURL: new URL(`file:///${speedy}`).href });
    }
  },
});
