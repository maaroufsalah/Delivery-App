import type {
  AuditLogDetail,
  AuditLogListItem,
  CompanyStatus,
  CreatedTenantResult,
  CreateTenantPayload,
  Paginated,
  PlatformAuditEntry,
  ServerErrorGroup,
  ServerErrorOccurrence,
  TenantListItem,
} from '@delivery/shared';
import { apiFetch } from '@/lib/http';

/**
 * Console plateforme — `GET /platform/tenants`, `GET /platform/audit`,
 * `POST /platform/tenants`, `PATCH …/status`, `PATCH …/trial`,
 * `POST …/impersonate`.
 *
 * Ces fonctions ne rendent JAMAIS de donnée métier : des décomptes, des dates,
 * un statut. La console dit combien de colis a un tenant, jamais lesquels.
 * L'unique accès au contenu passe par « se connecter en tant que », qui ouvre
 * une session ordinaire du tenant et laisse une trace au journal.
 *
 * L'API refuse tout cela à qui n'est pas `PLATFORM_ADMIN` : les écrans qui
 * suivent vérifient le rôle pour ne pas afficher une page vouée au 403, pas
 * pour tenir la sécurité — elle est côté serveur.
 */

export interface TenantListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: CompanyStatus;
}

export async function getTenants(query: TenantListQuery = {}): Promise<Paginated<TenantListItem>> {
  return apiFetch<Paginated<TenantListItem>>('/platform/tenants', {
    query: {
      page: query.page,
      pageSize: query.pageSize,
      search: query.search,
      status: query.status,
    },
  });
}

export async function getPlatformAudit(
  query: { page?: number; pageSize?: number } = {},
): Promise<Paginated<PlatformAuditEntry>> {
  return apiFetch<Paginated<PlatformAuditEntry>>('/platform/audit', {
    query: { page: query.page, pageSize: query.pageSize },
  });
}

export async function createTenant(payload: CreateTenantPayload): Promise<CreatedTenantResult> {
  return apiFetch<CreatedTenantResult>('/platform/tenants', {
    method: 'POST',
    body: payload,
  });
}

export async function setTenantStatus(id: string, status: CompanyStatus): Promise<TenantListItem> {
  return apiFetch<TenantListItem>(`/platform/tenants/${encodeURIComponent(id)}/status`, {
    method: 'PATCH',
    body: { status },
  });
}

export async function extendTenantTrial(id: string, days: number): Promise<TenantListItem> {
  return apiFetch<TenantListItem>(`/platform/tenants/${encodeURIComponent(id)}/trial`, {
    method: 'PATCH',
    body: { days },
  });
}

export interface ImpersonationGrant {
  id: string;
  slug: string;
  name: string;
  /** Jeton court, à présenter sur l'hôte du tenant. Deux minutes. */
  token: string;
}

export async function impersonateTenant(id: string): Promise<ImpersonationGrant> {
  return apiFetch<ImpersonationGrant>(`/platform/tenants/${encodeURIComponent(id)}/impersonate`, {
    method: 'POST',
  });
}

/* -------------------------------------------------------------------------- */
/* La vue technique — recette SpeedyTrans 4, point 5 (D-134)                  */
/* -------------------------------------------------------------------------- */

/**
 * Le journal BRUT d'une société : identifiants, avant/après, requête, adresse.
 * La seule lecture de contenu de la console hors « se connecter en tant que »,
 * demandée telle quelle ; elle ne porte que sur le journal.
 */
export async function getTechAudit(query: {
  companyId: string;
  page?: number;
  pageSize?: number;
  search?: string;
  entity?: string;
}): Promise<Paginated<AuditLogListItem>> {
  return apiFetch<Paginated<AuditLogListItem>>('/platform/tech-audit', { query });
}

export async function getTechAuditEntry(companyId: string, id: string): Promise<AuditLogDetail> {
  return apiFetch<AuditLogDetail>(`/platform/tech-audit/entry/${encodeURIComponent(id)}`, {
    query: { companyId },
  });
}

/** Les erreurs serveur récentes, groupées par route et message. */
export async function getServerErrors(query: {
  page?: number;
  pageSize?: number;
  search?: string;
}): Promise<Paginated<ServerErrorGroup>> {
  return apiFetch<Paginated<ServerErrorGroup>>('/platform/errors', { query });
}

/** Les occurrences d'un identifiant de requête, pile comprise. */
export async function getServerErrorOccurrences(
  requestId: string,
): Promise<ServerErrorOccurrence[]> {
  return apiFetch<ServerErrorOccurrence[]>('/platform/errors/occurrences', {
    query: { requestId },
  });
}
