const { withAppBuildGradle } = require('@expo/config-plugins');

/**
 * Désactive `ExtraTranslation` sur le lint Android.
 *
 * **Pourquoi ce greffon survit à `expo-build-properties`.** Le PO a autorisé ce
 * paquet en lui demandant de reprendre deux choses : R8 et ce lint. Il prend
 * bien R8 (`enableMinifyInReleaseBuilds`, `enableShrinkResourcesInReleaseBuilds`)
 * mais **n'expose aucune option de lint** — vérifié dans
 * `build/pluginConfig.d.ts` : `cmakeVersion`, `kotlinVersion`,
 * `enableMinifyInReleaseBuilds`, `enableShrinkResourcesInReleaseBuilds`,
 * `enablePngCrunchInReleaseBuilds`, `extraProguardRules`, `packagingOptions`,
 * `networkInspector`. Rien d'autre.
 *
 * Passé en configuration, `lintOptions` était **silencieusement ignoré** : le
 * `build.gradle` produit n'en portait aucune trace, et le build aurait échoué
 * exactement comme le quatrième.
 *
 * **Le défaut qu'il ferme.** `expo.locales` traduit les chaînes de
 * justification de permission en fr/ar/en, ce que la mission demandait. Ces
 * clés sont celles d'iOS (`NSCameraUsageDescription` et consorts) — mais Expo
 * écrit le bloc `locales` dans les ressources des DEUX plateformes. Android se
 * retrouve avec `values-b+fr/strings.xml` qui traduit des clés absentes de sa
 * locale par défaut, et `lintVitalRelease` refuse :
 *
 *     Error: "NSCameraUsageDescription" is translated here but not found in
 *     default locale [ExtraTranslation]
 *
 * `ios.locales` n'existe pas dans le schéma d'Expo — vérifié, `expo-doctor` le
 * rejette. Les chaînes doivent rester au niveau racine.
 *
 * **Le lint a raison, et on le désactive quand même.** Ces clés sont du bruit
 * côté Android : la plateforme n'a pas de notion de chaîne de justification
 * dans `strings.xml`, ses explications se donnent à l'exécution. Les traduire
 * n'a aucun effet, et les retirer coûterait la traduction iOS, qui est le but.
 *
 * **Une règle, pas le lint entier.** `lintVitalRelease` attrape de vraies
 * erreurs — ressources manquantes, API trop récentes. Le couper en bloc
 * échangerait un build vert contre une application qui plante sur un téléphone
 * ancien. Seule `ExtraTranslation` est levée.
 *
 * Déclaré APRÈS `expo-build-properties` dans `app.json` : il modifie le
 * `build.gradle` que celui-ci vient d'écrire.
 */
module.exports = function withAndroidLint(config) {
  return withAppBuildGradle(config, (gradleConfig) => {
    const contents = gradleConfig.modResults.contents;

    // Idempotent : `prebuild` peut repasser sur un fichier déjà modifié.
    if (contents.includes("disable 'ExtraTranslation'")) return gradleConfig;

    const anchor = 'android {';
    const index = contents.indexOf(anchor);

    if (index === -1) {
      throw new Error(
        'with-android-lint : bloc `android {` introuvable dans app/build.gradle. ' +
          'Le gabarit a changé ; le greffon doit être relu avant de passer outre.',
      );
    }

    const injection = [
      '',
      '    lint {',
      '        // Voir plugins/with-android-lint.js : les clés NS* viennent des',
      "        // locales d'Expo et n'ont pas de sens côté Android.",
      "        disable 'ExtraTranslation'",
      '    }',
    ].join('\n');

    gradleConfig.modResults.contents =
      contents.slice(0, index + anchor.length) + injection + contents.slice(index + anchor.length);

    return gradleConfig;
  });
};
