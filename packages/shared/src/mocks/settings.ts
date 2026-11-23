import { type CompanySettings, DEFAULT_COMPANY_SETTINGS } from '../settings';

/**
 * Configuration société mockée — une seule, l'installation n'en héberge qu'une
 * (docs/DECISIONS.md D-013).
 *
 * L'écriture est faite en mémoire : elle survit à une navigation client mais
 * pas à un redémarrage du serveur. En cible, `PATCH /settings/company`.
 */

let current: CompanySettings = { ...DEFAULT_COMPANY_SETTINGS };

export function readCompanySettings(): CompanySettings {
  return current;
}

export function writeCompanySettings(patch: Partial<CompanySettings>): CompanySettings {
  current = { ...current, ...patch };
  return current;
}
