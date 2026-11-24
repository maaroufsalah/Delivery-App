// Produit api/src/Delivery.Application/Audit/Journal/JournalCatalog.Data.cs depuis
// `modules/audit/journal/journal-catalog.ts` et `journal-labels.ts` de Speedy : entités
// (catégorie, priorité, nature des champs), compagnons, champs techniques, libellés.
// Les fonctions `type`, `reference` et `href` sont reprises à la main en C#.
// Usage : SHARED_ENTRY=types/journal.ts node --import ./scripts/ts/register.mjs scripts/ts/dump-journal.mjs
import { readFileSync, writeFileSync } from 'node:fs';

const api = process.env.SPEEDY_API_SRC ?? 'D:/Work/Projects/Freelance/Anass/delivery-platform/apps/api/src/';
const catalog = await import(`file:///${api}modules/audit/journal/journal-catalog.ts`);
// `journal-labels.ts` tire le service d'export (TypeScript non effaçable) : ses tables sont lues comme littéraux.
const source = readFileSync(`${api}modules/audit/journal/journal-labels.ts`, 'utf8');
function literal(name) {
  const start = source.search(new RegExp(`const ${name}\\b[^=]*=\\s*\\{`));
  if (start < 0) throw new Error(`introuvable : ${name}`);
  let i = source.indexOf('{', start);
  let depth = 0;
  const begin = i;
  for (; i < source.length; i++) {
    const ch = source[i];
    if (ch === '"' || ch === "'" || ch === '`') {
      const quote = ch;
      for (i++; i < source.length && source[i] !== quote; i++) if (source[i] === '\\') i++;
      continue;
    }
    if (source.startsWith('//', i)) { i = source.indexOf('\n', i); continue; }
    if (source.startsWith('/*', i)) { i = source.indexOf('*/', i) + 1; continue; }
    if (ch === '{') depth++;
    if (ch === '}') { depth--; if (depth === 0) break; }
  }
  return Function("SHORT_NAME_TOKEN", `return (${source.slice(begin, i + 1)});`)("%shortName%");
}
const labels = Object.fromEntries(['OBJECT_LABELS', 'ACTION_LABELS', 'FIELD_LABELS', 'ENUM_VALUE_LABELS', 'ROLE_LABELS', 'SOURCE_LABELS'].map((n) => [n, literal(n)]));
const entities = Object.fromEntries(Object.entries(catalog.ENTITIES).map(([name, config]) => [name, {
  type: typeof config.type === 'string' ? config.type : null,
  category: config.category,
  priority: config.priority,
  fields: config.fields,
}]));
const data = {
  entities,
  fallback: { type: catalog.FALLBACK.type, category: catalog.FALLBACK.category, priority: catalog.FALLBACK.priority, fields: catalog.FALLBACK.fields },
  companions: [...catalog.COMPANION_ENTITIES],
  technical: catalog.TECHNICAL_FIELDS,
  objectLabels: labels.OBJECT_LABELS,
  actionLabels: labels.ACTION_LABELS,
  fieldLabels: labels.FIELD_LABELS,
  enumValueLabels: labels.ENUM_VALUE_LABELS,
  roleLabels: labels.ROLE_LABELS,
  sourceLabels: labels.SOURCE_LABELS,
};
const json = JSON.stringify(data, null, 2);
writeFileSync('api/src/Delivery.Application/Audit/Journal/JournalCatalog.Data.cs', [
  'namespace Delivery.Application.Audit.Journal;',
  '',
  '/// <summary>Tables de `journal-catalog.ts` et `journal-labels.ts`, telles quelles (JSON).</summary>',
  'public static partial class JournalCatalog',
  '{',
  '    private const string Json = """',
  ...json.split('\n').map((l) => `        ${l}`),
  '        """;',
  '}',
  '',
].join('\n'));
console.log(Object.keys(entities).length, 'entités');
