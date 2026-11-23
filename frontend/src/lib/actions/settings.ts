'use server';

import {
  type CompanyLogoPayload,
  companyLogoSchema,
  type CompanySettings,
  type CompanySettingsPatchPayload,
  companySettingsPatchSchema,
  type FinanceSettings,
  type FinanceSettingsPayload,
  financeSettingsSchema,
  type ThemeOverridesPayload,
  themeOverridesSchema,
} from '@delivery/shared';
import { revalidatePath } from 'next/cache';
import { type ReferentialResult, toFailure } from '@/lib/actions/action-result';
import {
  restoreThemeSnapshot,
  uploadCompanyLogo,
  updateCompanySettings,
  updateCompanyTheme,
  updateFinanceSettings,
} from '@/lib/api/settings';

/**
 * Configuration société.
 *
 * Le formulaire est un composant client : il ne peut pas appeler la couche API
 * directement, qui vit côté serveur. L'action est le passage obligé — et elle
 * revalide la saisie, parce qu'une Server Action est un endpoint POST public.
 */
export async function updateCompanySettingsAction(
  input: unknown,
): Promise<ReferentialResult<CompanySettings>> {
  const parsed = companySettingsPatchSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid', code: 'VALIDATION_FAILED' };

  try {
    const saved = await updateCompanySettings(parsed.data as CompanySettingsPatchPayload);
    // La marque est lue par le layout racine : toutes les pages la portent.
    revalidatePath('/', 'layout');
    return { ok: true, data: saved };
  } catch (error) {
    return toFailure(error, 'settings.company');
  }
}

/**
 * Thème avancé — écran Paramètres > Société > Thème.
 *
 * La validation passe ICI aussi, et pas seulement dans le formulaire : une
 * Server Action est un endpoint POST public. C'est elle qui refuse un couple
 * fond/texte sous 4,5:1 quand quelqu'un contourne l'écran.
 */
export async function updateCompanyThemeAction(
  input: unknown,
): Promise<ReferentialResult<CompanySettings>> {
  const parsed = themeOverridesSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid', code: 'VALIDATION_FAILED' };

  try {
    const saved = await updateCompanyTheme(parsed.data as ThemeOverridesPayload);
    revalidatePath('/', 'layout');
    return { ok: true, data: saved };
  } catch (error) {
    return toFailure(error, 'settings.theme');
  }
}

/**
 * Paramètres > Finance.
 *
 * Les montants traversent en dirhams et la TVA en pourcent : c'est ce que
 * l'opérateur a saisi, et c'est l'API qui convertit. Revalider la section
 * entière suffit — ces valeurs sont lues par la facturation, pas par le layout.
 */
export async function updateFinanceSettingsAction(
  input: unknown,
): Promise<ReferentialResult<FinanceSettings>> {
  const parsed = financeSettingsSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid', code: 'VALIDATION_FAILED' };

  try {
    const saved = await updateFinanceSettings(parsed.data as FinanceSettingsPayload);
    revalidatePath('/[locale]/(app)/settings/finance', 'page');
    return { ok: true, data: saved };
  } catch (error) {
    return toFailure(error, 'settings.finance');
  }
}

/**
 * « Revenir au standard » et « Annuler la dernière modification » — B-10.
 *
 * Deux gestes, un seul endpoint : ils reposent tous deux un instantané connu,
 * et seule la source change. Les distinguer en deux actions n'aurait dupliqué
 * que la gestion d'erreur.
 */
export async function restoreThemeAction(
  which: 'standard' | 'previous',
): Promise<ReferentialResult<CompanySettings>> {
  if (which !== 'standard' && which !== 'previous') {
    return { ok: false, error: 'invalid', code: 'VALIDATION_FAILED' };
  }

  try {
    const saved = await restoreThemeSnapshot(which);
    revalidatePath('/', 'layout');
    return { ok: true, data: saved };
  } catch (error) {
    /*
     * 404 : il n'y a rien à restaurer — aucun standard semé, ou aucune
     * modification encore faite. Ce n'est PAS une panne, et l'écran a un
     * message à lui ; on garde donc le motif machine plutôt que le message
     * brut de l'API.
     */
    const failure = toFailure(error, 'settings.theme.restore');
    return failure.code === 'NOT_FOUND'
      ? { ...failure, reason: 'nothing-to-restore' }
      : failure;
  }
}

/**
 * L'ENVOI DU LOGO — chantier « Audit des paramètres société », bloc 3.
 *
 * **Son propre geste, et son propre aller-retour.** Le logo n'attend plus
 * l'enregistrement du formulaire : il part dès qu'on le choisit, vers une route
 * qui le range comme un FICHIER. La configuration société ne garde ensuite que
 * son URL, et un changement de couleur ne réémet plus 683 ko.
 *
 * `revalidatePath('/', 'layout')` : la marque est lue par le layout racine, et
 * le nouveau logo doit apparaître dans la barre latérale sans rechargement dur.
 */
export async function uploadCompanyLogoAction(
  input: unknown,
): Promise<ReferentialResult<CompanySettings>> {
  const parsed = companyLogoSchema.safeParse(input);
  if (!parsed.success) return { ok: false, error: 'invalid', code: 'VALIDATION_FAILED' };

  try {
    const saved = await uploadCompanyLogo(parsed.data as CompanyLogoPayload);
    revalidatePath('/', 'layout');
    return { ok: true, data: saved };
  } catch (error) {
    return toFailure(error, 'settings.logo');
  }
}
