'use server';

import type { CompanyStatus, CreatedTenantResult, TenantListItem } from '@delivery/shared';
import { createTenantSchema } from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import {
  createTenant,
  extendTenantTrial,
  type ImpersonationGrant,
  impersonateTenant,
  setTenantStatus,
} from '@/lib/api/platform';

/**
 * Écritures de la console plateforme — C8 bloc 4, D-050.
 *
 * Toutes passent par l'API, qui exige `platform.*` : ces actions ne décident de
 * rien, elles transmettent. Un `PLATFORM_ADMIN` est le seul à les voir aboutir,
 * et chacune laisse une trace dans `PlatformAuditLog` — écrite côté API, pour
 * qu'aucun chemin ne puisse l'oublier.
 */

function revalidateConsole(): void {
  revalidatePath('/[locale]/(platform)/platform', 'page');
}

export async function createTenantAction(
  input: unknown,
): Promise<ReferentialResult<CreatedTenantResult>> {
  const parsed = createTenantSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid' };

  try {
    const created = await createTenant(parsed.data);
    revalidateConsole();

    /*
     * Le mot de passe temporaire remonte jusqu'à l'écran, et s'arrête là : il
     * n'est stocké nulle part en clair, seul son hachage est en base. La console
     * l'affiche une fois et ne saura plus le redonner.
     */
    return { ok: true, data: created };
  } catch (error) {
    return toFailure(error);
  }
}

export async function setTenantStatusAction(
  id: string,
  status: CompanyStatus,
): Promise<ReferentialResult<TenantListItem>> {
  try {
    const tenant = await setTenantStatus(id, status);
    revalidateConsole();
    return { ok: true, data: tenant };
  } catch (error) {
    return toFailure(error);
  }
}

export async function extendTrialAction(
  id: string,
  days: number,
): Promise<ReferentialResult<TenantListItem>> {
  try {
    const tenant = await extendTenantTrial(id, days);
    revalidateConsole();
    return { ok: true, data: tenant };
  } catch (error) {
    return toFailure(error);
  }
}

/**
 * « Se connecter en tant que ».
 *
 * L'API trace l'accès et rend le slug ; c'est l'écran qui emmène ensuite
 * l'administrateur sur l'hôte du tenant. On ne fabrique aucune session ici :
 * elle appartient au domaine du tenant, et un cookie posé sur le domaine de la
 * plateforme n'y serait de toute façon pas lisible (cookies par hôte, C8
 * bloc 5).
 */
export async function impersonateTenantAction(
  id: string,
): Promise<ReferentialResult<ImpersonationGrant>> {
  try {
    const tenant = await impersonateTenant(id);
    revalidateConsole();
    return { ok: true, data: tenant };
  } catch (error) {
    return toFailure(error);
  }
}
