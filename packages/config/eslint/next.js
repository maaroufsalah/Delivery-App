import next from 'eslint-config-next';
import nextCoreWebVitals from 'eslint-config-next/core-web-vitals';
import base from './base.js';

/**
 * Flat config de l'app Next.
 * `eslint-config-next` v16 exporte directement un tableau de configs (CJS
 * `export = config`), pas une fonction : il se déstructure, il ne s'appelle pas.
 */
export default [...base, ...next, ...nextCoreWebVitals];
