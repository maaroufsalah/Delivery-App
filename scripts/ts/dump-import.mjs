// Produit api/src/Delivery.Application/Parcels/Import/ImportColumns.Data.cs depuis
// `modules/parcels/parcel-import.columns.ts` de Speedy : en-têtes, alias, feuille
// d'anomalies, aide du modèle, mots oui/non. Le JSON est relu tel quel au démarrage.
// Usage : SHARED_ENTRY=types/parcel.ts node --import ./scripts/ts/register.mjs scripts/ts/dump-import.mjs
import { writeFileSync, mkdirSync } from 'node:fs';

const api = process.env.SPEEDY_API_SRC ?? 'D:/Work/Projects/Freelance/Anass/delivery-platform/apps/api/src/';
const columns = await import(`file:///${api}modules/parcels/parcel-import.columns.ts`);
const shared = await import(new URL('../../packages/shared/src/types/parcel.ts', import.meta.url).href);
const data = {
  fields: shared.PARCEL_IMPORT_FIELDS,
  headers: columns.IMPORT_HEADERS,
  aliases: columns.IMPORT_ALIASES,
  required: columns.IMPORT_REQUIRED_FIELDS,
  anomalySheet: columns.ANOMALY_SHEET,
  help: columns.IMPORT_HELP,
  trueWords: columns.TRUE_WORDS,
  falseWords: columns.FALSE_WORDS,
};
const json = JSON.stringify(data, null, 2);
const out = 'api/src/Delivery.Application/Parcels/Import';
mkdirSync(out, { recursive: true });
writeFileSync(`${out}/ImportColumns.Data.cs`, [
  'namespace Delivery.Application.Parcels.Import;',
  '',
  '/// <summary>Tables de `parcel-import.columns.ts`, telles quelles (JSON).</summary>',
  'public static partial class ImportColumns',
  '{',
  '    private const string Json = """',
  ...json.split('\n').map((l) => `        ${l}`),
  '        """;',
  '}',
  '',
].join('\n'));
console.log(Object.keys(data).map((k) => `${k}`).join(', '));
