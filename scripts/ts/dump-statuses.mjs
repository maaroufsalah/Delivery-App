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
lines.push('');
lines.push('    /// <summary>`courierOutcomesFor(statusId, routeKind)` évalué pour chaque statut et genre de circuit (clé `""` = sans circuit).</summary>');
lines.push('    public static readonly IReadOnlyDictionary<(int StatusId, string Kind), int[]> CourierOutcomes = new Dictionary<(int, string), int[]>', '    {');
for (const s of statuses.PARCEL_STATUSES) {
  for (const kind of [undefined, 'DELIVERY', 'PICKUP', 'RETURN']) {
    lines.push(`        [(${s.id}, ${q(kind ?? '')})] = ${list(transitions.courierOutcomesFor(s.id, kind))},`);
  }
}
lines.push('    };', '');
lines.push(`    public static readonly int[] CourierDeliveryOutcomeIds = ${list(transitions.courierDeliveryOutcomeIds())};`);
lines.push(`    public static readonly int[] CourierSourceStatusIds = ${list(transitions.COURIER_SOURCE_STATUS_IDS)};`);
lines.push('    public static readonly IReadOnlyDictionary<int, int[]> AdminFieldTargetsById = new Dictionary<int, int[]>', '    {');
for (const s of statuses.PARCEL_STATUSES) lines.push(`        [${s.id}] = ${list(transitions.adminFieldTargets(s.id))},`);
lines.push('    };', '');
for (const name of ['DASHBOARD_RETURN_STATUS_IDS', 'DASHBOARD_IN_TRANSIT_STATUS_IDS', 'DASHBOARD_FAILED_STATUS_IDS', 'DASHBOARD_IN_PROGRESS_STATUS_IDS', 'DASHBOARD_CLOSED_STATUS_IDS', 'COURIER_RETURN_STATUS_IDS', 'RETURN_EXCLUDED_STATUS_IDS', 'MERCHANT_RETURN_STATUS_IDS', 'MERCHANT_RETURNED_STATUS_IDS']) {
  const pascal = name.toLowerCase().split('_').map((w) => w[0].toUpperCase() + w.slice(1)).join('');
  lines.push(`    public static readonly int[] ${pascal} = ${list(statuses[name])};`);
}
lines.push('}');
mkdirSync('api/src/Delivery.Domain/Parcels', { recursive: true });
writeFileSync('api/src/Delivery.Domain/Parcels/StatusCatalog.Data.cs', lines.join('\n') + '\n');
console.log(`${statuses.PARCEL_STATUSES.length} statuts`);
