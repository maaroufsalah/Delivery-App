// Coche dans docs/contract/INVENTAIRE.md chaque route servie par un contrôleur .NET.
// Usage : node scripts/check-inventory.mjs [--strict]
//   --strict : code de sortie 1 si une route .NET n'existe pas dans l'inventaire.
import { readFileSync, writeFileSync, globSync } from 'node:fs';

const INVENTORY = 'docs/contract/INVENTAIRE.md';
const files = globSync('api/src/Delivery.Api/Controllers/**/*.cs');
const routes = new Set();
const VERB = /\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]/;

for (const file of files) {
  const lines = readFileSync(file, 'utf8').split(/\r?\n/);
  let prefix = null;
  for (const line of lines) {
    const route = line.match(/^\s*\[Route\("([^"]*)"\)\]/);
    if (route) prefix = route[1];
    const verb = line.match(VERB);
    if (verb && prefix !== null) {
      const path = ['api', prefix, verb[2] ?? ''].filter(Boolean).join('/').replace(/\{(\w+)(?::[^}]*)?\}/g, ':$1');
      routes.add(`${verb[1].toUpperCase()} /${path}`);
    }
  }
}

// Les paramètres de route ne portent pas forcément le même nom des deux côtés.
const shape = (route) => route.replace(/:\w+/g, ':p');
const served = new Set([...routes].map(shape));
let checked = 0;
const known = new Set();
const md = readFileSync(INVENTORY, 'utf8').replace(/^- \[( |x)\] `(\w+ [^`]+)`/gm, (all, box, route) => {
  known.add(shape(route));
  if (box === '-') return all;
  const done = served.has(shape(route));
  if (done) checked++;
  return `- [${done ? 'x' : ' '}] \`${route}\``;
});
writeFileSync(INVENTORY, md);
const unknown = [...routes].filter((r) => !known.has(shape(r)));
const total = (md.match(/^- \[[ x]\] /gm) ?? []).length;
console.log(`${checked}/${total} routes cochées`);
if (unknown.length) {
  console.log('Routes .NET absentes de l’inventaire :\n  ' + unknown.join('\n  '));
  if (process.argv.includes('--strict')) process.exit(1);
}
