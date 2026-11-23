import js from '@eslint/js';
import tseslint from 'typescript-eslint';
import prettier from 'eslint-config-prettier';

/** Shared flat config for every package of the monorepo. */
export default tseslint.config(
  { ignores: ['**/node_modules/**', '**/.next/**', '**/dist/**', '**/.turbo/**'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    rules: {
      // Aucune règle exigeant le type checker : eslint-config-next impose son propre
      // parser, qui ne transmet pas `parserOptions.project`. Le typage est vérifié
      // par `tsc` (strict + verbatimModuleSyntax), pas par ESLint.
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
    },
  },
  prettier,
);
