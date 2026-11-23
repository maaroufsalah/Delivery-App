/// <reference types="nativewind/types" />

/*
 * NativeWind ajoute `className` aux composants de React Native — M1.
 *
 * Sans cette référence, `tsc` refuse chaque `className` : les types de React
 * Native ne le connaissent pas, et c'est le transformateur Babel qui le traduit
 * en `style` à la compilation. Le fichier doit être INCLUS par `tsconfig.json`,
 * ce que fait son motif de fichiers TypeScript.
 */
