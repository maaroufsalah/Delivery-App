// Usage : node --import ./scripts/ts/register.mjs scripts/ts/dump-shared.mjs <module> <export,...>
import { pathToFileURL } from 'node:url';
import { resolve as resolvePath } from 'node:path';
const [, , modulePath, names] = process.argv;
const m = await import(pathToFileURL(resolvePath(modulePath)).href);
const out = {};
for (const name of names.split(',')) out[name] = m[name];
process.stdout.write(JSON.stringify(out, (_k, v) => (v instanceof Set ? [...v] : v instanceof Map ? Object.fromEntries(v) : v), 2));
