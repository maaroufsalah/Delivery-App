import type { CompanyStatus } from '../enums';
import type { Id, IsoDateTime } from './common';

/**
 * Console plateforme — C8 bloc 4.
 *
 * Ces types ne portent AUCUNE donnée métier : des décomptes, des dates, un
 * statut. La console dit combien de colis a un tenant, jamais lesquels — le
 * seul accès au contenu passe par « se connecter en tant que », qui laisse une
 * trace au journal.
 */
export interface TenantListItem {
  id: Id;
  slug: string;
  customDomain: string | null;
  name: string;
  status: CompanyStatus;
  plan: string | null;
  trialEndsAt: IsoDateTime | null;
  /** Jours restants, arrondis au supérieur. Négatif = expiré. `null` = pas d'essai. */
  trialDaysLeft: number | null;
  parcelCount: number;
  userCount: number;
  lastActivityAt: IsoDateTime | null;
  createdAt: IsoDateTime;
}

/** Une ligne du journal. Append-only : rien ne s'y modifie. */
export interface PlatformAuditEntry {
  id: Id;
  actorName: string;
  /** `tenant.create`, `tenant.suspend`, `tenant.impersonate`… */
  action: string;
  targetCompanyId: Id | null;
  targetCompanyName: string;
  details: Record<string, unknown> | null;
  createdAt: IsoDateTime;
}

/** Ce que rend la création d'un tenant. Le mot de passe n'est rendu qu'ICI. */
export interface CreatedTenantResult {
  id: Id;
  slug: string;
  name: string;
  adminEmail: string;
  /** Affiché UNE fois : seul son hachage est en base, il ne se redonne pas. */
  temporaryPassword: string;
  citiesCreated: number;
  districtsCreated: number;
}
