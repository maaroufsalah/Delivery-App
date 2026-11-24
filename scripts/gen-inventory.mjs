// Génère docs/contract/INVENTAIRE.md depuis les contrôleurs NestJS de Speedy.
// Usage : node scripts/gen-inventory.mjs <chemin speedy apps/api/src>
import { readFileSync, writeFileSync, globSync } from 'node:fs';
import { join } from 'node:path';

const src = process.argv[2];
const out = process.argv[3] ?? 'docs/contract/INVENTAIRE.md';
const files = globSync('**/*.controller.ts', { cwd: src })
  .map((f) => f.split(String.fromCharCode(92)).join('/')).sort();

// Module du chantier (2..10) par dossier Nest.
const MODULE_OF = {
  auth: 2, users: 2, roles: 2, staff: 2, public: 2, health: 2,
  settings: 3, cities: 3, zones: 3, districts: 3, agencies: 3, pricing: 3, 'delivery-types': 3, directory: 3,
  clients: 4, couriers: 4, merchant: 4,
  parcels: 5, audit: 5, 'bulk-jobs': 5, provider: 5, warehouse: 5, 'courier-stock': 5, courier: 5,
  routes: 6,
  returns: 7, bl: 7,
  finance: 8, 'petty-cash': 8,
  claims: 10, notifications: 10, dashboard: 10, announcements: 10,
  platform: 0,
};
const MODULE_NAMES = {
  0: 'Hors périmètre (console /platform, mono-société)',
  2: 'Identité et accès', 3: 'Société et référentiel', 4: 'Clients et livreurs', 5: 'Colis',
  6: 'Bons et circuits', 7: 'Retours', 8: 'Caisse et chèques', 9: 'Facturation',
  10: 'Réclamations, notifications, tableaux de bord',
};
// Contrôleurs du dossier finance qui relèvent de la facturation (module 9).
const BILLING = new Set(['invoices', 'invoices-pro', 'courier-statements', 'transfer-orders', 'cod-payouts']);

// Routes rangées dans le module de leur métier plutôt que celui de leur contrôleur.
const ROUTE_MODULE = [
  [/^\/api\/public\/(company|logo|rates|mobile-config|cities)/, 3],
  [/^\/api\/public\/track/, 5],
  [/^\/api\/staff\/me\/alerts/, 6],
  [/^\/api\/merchant\/me\/summary/, 5],
  [/^\/api\/merchant\/me\/(pickups|dropoffs|notes)/, 6],
  // Le livreur dans ses circuits (tournées, scan, issues) : avec les circuits.
  [/^\/api\/courier\/me\/(notes|routes|deliveries|scan)/, 6],
  [/^\/api\/courier\/parcels\/:code\/(status|proof)/, 6],
  [/^\/api\/courier-stock/, 6],
  // Sa caisse (chèques, solde, retour de fonds) : avec la caisse.
  [/^\/api\/courier\/me\/(cheques|balance|remittance)/, 8],
];

const VERBS = ['Get', 'Post', 'Put', 'Patch', 'Delete'];
const rows = [];
for (const file of files) {
  const text = readFileSync(join(src, file), 'utf8');
  const lines = text.split(/\r?\n/);
  const folder = file.split('/')[1];
  let classPath = null, classGuards = [], classPerms = [], pending = [];
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i].trim();
    const ctrl = line.match(/^@Controller\(\s*(?:'([^']*)')?/);
    if (ctrl) { classPath = ctrl[1] ?? ''; classGuards = pending.flatMap(guardsOf); classPerms = pending.flatMap(permsOf); pending = []; continue; }
    if (line.startsWith('@')) { // décorateur éventuellement multi-lignes
      let deco = line, depth = (line.match(/\(/g) ?? []).length - (line.match(/\)/g) ?? []).length;
      while (depth > 0 && i + 1 < lines.length) { i++; deco += ' ' + lines[i].trim(); depth += (lines[i].match(/\(/g) ?? []).length - (lines[i].match(/\)/g) ?? []).length; }
      pending.push(deco); continue;
    }
    if (line.startsWith('export class')) { continue; }
    const method = line.match(/^(?:async\s+)?([a-zA-Z0-9_]+)\s*\(/);
    if (method && classPath !== null) {
      const verbDeco = pending.find((d) => VERBS.some((v) => d.startsWith(`@${v}(`)));
      if (verbDeco) {
        const verb = verbDeco.match(/^@(\w+)/)[1].toUpperCase();
        const sub = verbDeco.match(/\(\s*'([^']*)'/)?.[1] ?? '';
        const route = '/api/' + [classPath, sub].filter(Boolean).join('/');
        // signature jusqu'à l'accolade
        let sig = line, j = i;
        while (!/\)\s*(:[^{]*)?\{\s*$/.test(sig) && j + 1 < lines.length && j - i < 25) { j++; sig += ' ' + lines[j].trim(); }
        const body = [...sig.matchAll(/zodBody\((\w+)\)/g)].map((m) => m[1]);
        const queryParams = [...sig.matchAll(/@Query\('(\w+)'/g)].map((m) => m[1]);
        const isPublic = pending.some((d) => d.startsWith('@Public('));
        const perms = [...classPerms, ...pending.flatMap(permsOf)];
        const guards = [...classGuards, ...pending.flatMap(guardsOf)];
        const ret = sig.match(/\)\s*:\s*([^{]+)\{\s*$/)?.[1]?.trim() ?? '';
        let mod = MODULE_OF[folder] ?? 99;
        if (folder === 'finance' && BILLING.has(route.split('/')[2])) mod = 9;
        for (const [pattern, target] of ROUTE_MODULE) if (pattern.test(route)) mod = target;
        rows.push({ mod, folder, file, verb, route, handler: method[1], perms, guards, isPublic, body, queryParams, ret, line: i + 1 });
      }
      pending = [];
    } else if (!line.startsWith('@') && line && !line.startsWith('//') && !line.startsWith('*') && !line.startsWith('/*')) {
      if (!line.startsWith(')') && !line.startsWith('}')) pending = pending.length && line.startsWith('constructor') ? [] : pending;
    }
  }
}
function guardsOf(d) { const m = d.match(/^@UseGuards\((.*)\)/); return m ? m[1].split(',').map((s) => s.trim()).filter(Boolean) : []; }
function permsOf(d) { const m = d.match(/^@RequirePermission\((.*)\)/); return m ? [...m[1].matchAll(/'([^']+)'/g)].map((x) => x[1]) : []; }

rows.sort((a, b) => a.mod - b.mod || a.folder.localeCompare(b.folder) || a.file.localeCompare(b.file) || a.line - b.line);
let md = `# Inventaire du contrat API Speedy\n\nProduit par \`node scripts/gen-inventory.mjs <speedy>/apps/api/src\` — ne pas éditer à la main sauf pour cocher.\n\n`;
md += `Total : **${rows.length} endpoints** dans ${files.length} contrôleurs. Préfixe global \`/api\`.\n\n`;
md += `Légende garde : \`public\` = sans session ; sinon JWT obligatoire. Les permissions sont celles de \`@RequirePermission\` (toutes requises).\n\n`;
let cur = null, curFile = null;
for (const r of rows) {
  if (r.mod !== cur) { cur = r.mod; md += `\n## Module ${r.mod} — ${MODULE_NAMES[r.mod] ?? 'À classer'}\n`; curFile = null; }
  if (r.file !== curFile) { curFile = r.file; md += `\n### \`${r.file}\`\n\n`; }
  const meta = [
    r.isPublic ? 'public' : null,
    r.guards.length ? `gardes: ${r.guards.join(', ')}` : null,
    r.perms.length ? `perm: ${r.perms.join(' + ')}` : null,
    r.body.length ? `entrée: ${r.body.join(', ')}` : null,
    r.queryParams.length ? `query: ${r.queryParams.join(', ')}` : null,
    r.ret ? `sortie: \`${r.ret.replace(/\|/g, '\|')}\`` : null,
  ].filter(Boolean).join(' · ');
  const box = r.mod === 0 ? '[-]' : '[ ]';
  md += `- ${box} \`${r.verb} ${r.route}\` — \`${r.handler}\` (L${r.line})${meta ? ' · ' + meta : ''}\n`;
}
writeFileSync(out, md);
console.log(`${rows.length} endpoints -> ${out}`);
