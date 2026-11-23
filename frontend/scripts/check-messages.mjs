#!/usr/bin/env node
/**
 * Aucun tiret cadratin dans les catalogues de traduction — C9 bloc 4.
 *
 * **Pourquoi un script et pas une règle ESLint.** ESLint ne lit pas les `.json`
 * sans un plugin dédié, et `apps/web` n'a pas de lanceur de tests : les deux
 * ajouteraient une dépendance hors de la liste de `CLAUDE.md`. Une vingtaine de
 * lignes de Node, accrochées au script `lint` déjà exigé avant tout push, font
 * le même travail sans rien installer.
 *
 * **Pourquoi un garde-fou tout court.** Le tiret cadratin revient tout seul :
 * il est produit par la correction automatique des traitements de texte, par
 * les claviers macOS, et par les modèles de langue. Retirer les occurrences
 * sans fermer la porte, c'est recommencer au prochain lot de traductions.
 *
 * Le remplacement se choisit selon le contexte — virgule, deux-points, point,
 * tiret simple, ou rien. C'est pourquoi ce script REFUSE au lieu de corriger :
 * un remplacement automatique produirait de la ponctuation fausse.
 */
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const EM_DASH = '—';
const MESSAGES_DIR = resolve(dirname(fileURLToPath(import.meta.url)), '..', 'messages');

/** Chemin JSON lisible (`settings.finance.vatRate`) de chaque valeur fautive. */
function findEmDashes(node, path, found) {
  if (typeof node === 'string') {
    if (node.includes(EM_DASH)) found.push({ path, value: node });
    return found;
  }

  if (node !== null && typeof node === 'object') {
    for (const [key, value] of Object.entries(node)) {
      findEmDashes(value, path === '' ? key : `${path}.${key}`, found);
    }
  }

  return found;
}

const failures = [];

for (const file of readdirSync(MESSAGES_DIR).filter((name) => name.endsWith('.json'))) {
  const raw = readFileSync(join(MESSAGES_DIR, file), 'utf8');
  for (const hit of findEmDashes(JSON.parse(raw), '', [])) {
    failures.push({ file, ...hit });
  }
}

if (failures.length > 0) {
  console.error(`\n${failures.length} tiret(s) cadratin(s) dans les catalogues de traduction :\n`);
  for (const { file, path, value } of failures) {
    console.error(`  ${file}  ${path}`);
    console.error(`     ${value}\n`);
  }
  console.error(
    'Remplacez selon le contexte : virgule, deux-points, point, tiret simple, ou rien.\n',
  );
  process.exit(1);
}

/*
 * AUCUN POINT DANS UNE CLÉ — recette SpeedyTrans 4, point 3.
 *
 * next-intl lit `t('new.hint')` comme `new` → `hint`. Une clé écrite
 * `"new.hint": "…"` n'est donc JAMAIS trouvée, et l'écran affiche le chemin
 * brut : c'était le cas des familles de statuts et du véhicule du livreur, dans
 * les trois langues. Le typage de `fr.json` ne le voit pas — la clé existe.
 */
function findDottedKeys(node, path, found) {
  if (node !== null && typeof node === 'object') {
    for (const [key, value] of Object.entries(node)) {
      const next = path === '' ? key : `${path} > ${key}`;
      if (key.includes('.')) found.push(next);
      findDottedKeys(value, next, found);
    }
  }
  return found;
}

const dotted = [];
for (const file of readdirSync(MESSAGES_DIR).filter((name) => name.endsWith('.json'))) {
  const raw = readFileSync(join(MESSAGES_DIR, file), 'utf8');
  for (const path of findDottedKeys(JSON.parse(raw), '', [])) dotted.push(`${file}  ${path}`);
}

if (dotted.length > 0) {
  console.error(`\n${dotted.length} clé(s) contenant un point, introuvables par next-intl :\n`);
  for (const line of dotted) console.error(`  ${line}`);
  console.error('\nImbriquez plutôt : { "new": { "hint": "…" } } ou un objet voisin.\n');
  process.exit(1);
}

console.log('messages : aucun tiret cadratin, aucune clé à point.');
