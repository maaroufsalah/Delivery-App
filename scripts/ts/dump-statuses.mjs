// Produit api/src/Delivery.Domain/Parcels/StatusCatalog.Data.cs depuis packages/shared
// (statuses.ts, transitions.ts) : les tables restent la source de vérité de Speedy.
// Usage : node --import ./scripts/ts/register.mjs scripts/ts/dump-statuses.mjs
import { writeFileSync, mkdirSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';

const load = async (p) => import(pathToFileURL(resolve(p)).href);
const statuses = await load('packages/shared/src/statuses.ts');
const transitions = await load('packages/shared/src/transitions.ts');

const q = (s) => JSON.stringify(s);
const list = (values) => `[${values.join(', ')}]`;
const lines = [];
lines.push('namespace Delivery.Domain.Parcels;', '');
lines.push('/// <summary>Données de `statuses.ts` et `transitions.ts`, dans leur ordre.</summary>');
lines.push('public static partial class StatusCatalog', '{');
lines.push('    public static readonly IReadOnlyList<StatusDefinition> All =', '    [');
for (const s of statuses.PARCEL_STATUSES) {
  lines.push(`        new(${s.id}, ${q(s.key)}, ${q(s.group)}, ${q(s.color)}, ${q(s.icon)}),`);
}
lines.push('    ];', '');
lines.push('    public static readonly IReadOnlyDictionary<int, int[]> Nominal = new Dictionary<int, int[]>', '    {');
for (const s of statuses.PARCEL_STATUSES) lines.push(`        [${s.id}] = ${list(transitions.allowedTransitions(s.id))},`);
lines.push('    };', '');
lines.push(`    public static readonly int[] LockedStatusIds = ${list(transitions.LOCKED_STATUS_IDS)};`);
lines.push(`    public static readonly int[] DisputeTargets = ${list(transitions.DISPUTE_TARGETS)};`);
lines.push(`    public static readonly (int From, int To)[] ReturnReception = [${transitions.RETURN_RECEPTION_TRANSITIONS.map(([a, b]) => `(${a}, ${b})`).join(', ')}];`);
lines.push(`    public static readonly int[] DestinationLockedStatusIds = ${list(transitions.DESTINATION_LOCKED_STATUS_IDS)};`);
lines.push('}');
mkdirSync('api/src/Delivery.Domain/Parcels', { recursive: true });
writeFileSync('api/src/Delivery.Domain/Parcels/StatusCatalog.Data.cs', lines.join('\n') + '\n');
console.log(`${statuses.PARCEL_STATUSES.length} statuts`);
