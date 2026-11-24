// Produit api/src/Delivery.Application/Common/Documents/DocumentLabels.Data.cs depuis
// `common/documents/labels.ts` de Speedy (libellés des documents et des exports, fr/en/ar).
// Usage : node scripts/ts/dump-labels.mjs <speedy>/apps/api/src/common/documents/labels.ts
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';

const source = readFileSync(process.argv[2], 'utf8');

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
  return Function(`return (${source.slice(begin, i + 1)});`)();
}

const tables = { ParcelStatus: literal('PARCEL_STATUS'), Enum: literal('ENUM_LABELS') };
const q = (s) => JSON.stringify(s);
const lines = ['namespace Delivery.Application.Common.Documents;', '', '/// <summary>Libellés de `labels.ts` (documents, exports), par langue.</summary>', 'public static partial class DocumentLabels', '{'];
for (const [name, table] of Object.entries(tables)) {
  lines.push(`    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ${name}Table = new Dictionary<string, IReadOnlyDictionary<string, string>>`, '    {');
  for (const [locale, entries] of Object.entries(table)) {
    lines.push(`        [${q(locale)}] = new Dictionary<string, string>`, '        {');
    for (const [key, value] of Object.entries(entries)) lines.push(`            [${q(key)}] = ${q(value)},`);
    lines.push('        },');
  }
  lines.push('    };', '');
}
lines.push('}');
mkdirSync('api/src/Delivery.Application/Common/Documents', { recursive: true });
writeFileSync('api/src/Delivery.Application/Common/Documents/DocumentLabels.Data.cs', lines.join('\n') + '\n');
console.log(Object.fromEntries(Object.entries(tables).map(([k, v]) => [k, Object.keys(v.fr).length])));
