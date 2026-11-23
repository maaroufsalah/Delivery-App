const { getDefaultConfig } = require('expo/metro-config');
const { withNativeWind } = require('nativewind/metro');
const path = require('node:path');

/**
 * Metro dans un MONOREPO pnpm — M1.
 *
 * Deux réglages, et ils ne sont pas cosmétiques :
 *
 *  1. `watchFolders` ajoute la racine, sinon `packages/shared` n'est pas
 *     surveillé : une correction de contrat n'arriverait jamais dans
 *     l'application sans redémarrer le bundler.
 *  2. `nodeModulesPaths` dit à Metro de remonter jusqu'à la racine. pnpm ne
 *     duplique pas les dépendances — elles vivent dans un magasin lié par
 *     symlinks — et le résolveur par défaut s'arrête au premier
 *     `node_modules` qu'il voit.
 *
 * `disableHierarchicalLookup` reste FAUX : le désactiver casse la résolution
 * des dépendances transitives de React Native, qui en dépendent.
 */
const projectRoot = __dirname;
const workspaceRoot = path.resolve(projectRoot, '../..');

const config = getDefaultConfig(projectRoot);

// AJOUTÉ aux défauts d'Expo, jamais substitué : `getDefaultConfig` en pose
// déjà, et les écraser retire des dossiers qu'Expo surveille pour ses propres
// besoins — `expo-doctor` le signale, à juste titre.
config.watchFolders = [...(config.watchFolders ?? []), workspaceRoot];
config.resolver.nodeModulesPaths = [
  path.resolve(projectRoot, 'node_modules'),
  path.resolve(workspaceRoot, 'node_modules'),
];

module.exports = withNativeWind(config, { input: './src/theme/global.css' });
