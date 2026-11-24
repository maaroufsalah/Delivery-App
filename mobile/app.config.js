/**
 * LES DEUX APPLICATIONS — chantier UAT, D-200.
 *
 * `app.json` reste la source : c'est l'application DÉFINITIVE, « SpeedyTrans »,
 * `ma.nocido.delivery`, celle de l'UAT puis de la production. Ce fichier n'en
 * dérive qu'une variante, la recette, quand `APP_VARIANT=recette` (posé par le
 * profil EAS `preview` et par `scripts/mobile-update.ps1 -Env recette`) :
 *
 *   - un nom à part, « SpeedyTrans Recette » ;
 *   - un identifiant suffixé `.recette` : les deux applications coexistent sur
 *     un même téléphone, chacune avec ses jetons et son cache ;
 *   - une icône à BANDEAU (`scripts/mobile-recette-icons.mjs`), pour qu'on ne
 *     saisisse pas une vraie livraison dans la recette.
 *
 * L'environnement appelé, lui, n'est PAS décidé ici mais par le canal natif
 * (`src/config/environment.ts`). Sans variable, c'est l'identité définitive :
 * l'oubli ne fabrique jamais une application de production déguisée en recette.
 */
module.exports = ({ config }) => {
  if (process.env.APP_VARIANT !== 'recette') return config;

  return {
    ...config,
    name: 'Wimo Delivery Recette',
    icon: './assets/icon-recette.png',
    ios: {
      ...config.ios,
      bundleIdentifier: `${config.ios.bundleIdentifier}.recette`,
    },
    android: {
      ...config.android,
      package: `${config.android.package}.recette`,
      adaptiveIcon: {
        ...config.android.adaptiveIcon,
        foregroundImage: './assets/adaptive-icon-recette.png',
      },
    },
  };
};
