/**
 * Babel — Expo SDK 57.
 *
 * `babel-preset-expo` porte déjà Reanimated et NativeWind en SDK 57 : le
 * plugin `react-native-reanimated/plugin`, qu'on ajoutait à la main jusqu'en
 * SDK 52, est inclus et le déclarer deux fois casse la compilation des
 * worklets.
 */
module.exports = function babel(api) {
  api.cache(true);

  return {
    presets: [['babel-preset-expo', { jsxImportSource: 'nativewind' }], 'nativewind/babel'],
  };
};
