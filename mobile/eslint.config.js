const expo = require('eslint-config-expo/flat');

/**
 * ESLint — la configuration d'Expo, plus ce que le dépôt exige.
 *
 * `eslint-config-expo` porte déjà les règles de React Native, de Hooks et de
 * l'import.
 *
 * **La variable inutilisée est attrapée par `tsc`, pas ici.** La règle native
 * `no-unused-vars` ne sait pas lire du TypeScript : elle prend les paramètres
 * d'une signature de méthode d'interface (`t: (key, values) => string`) pour
 * des variables mortes et refuse le fichier. La règle typée existe, mais elle
 * demanderait le plugin `@typescript-eslint` — une dépendance de plus, que la
 * mission interdit sans accord.
 *
 * Alors la garantie est posée là où elle est VRAIE : `noUnusedLocals` et
 * `noUnusedParameters` dans `tsconfig.json`, vérifiés par `pnpm typecheck`. Le
 * compilateur, lui, distingue une déclaration de type d'un corps de fonction.
 */
module.exports = [
  ...expo,
  {
    ignores: ['node_modules/**', '.expo/**', 'android/**', 'ios/**', 'expo-env.d.ts'],
  },
  {
    files: ['**/*.ts', '**/*.tsx'],
    rules: {
      'no-unused-vars': 'off',
    },
  },
];
