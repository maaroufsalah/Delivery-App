import {
  type BrandPalette,
  type CompanyLogoPayload,
  type CompanySettings,
  type CompanySettingsPatchPayload,
  DEFAULT_COMPANY_SETTINGS,
  type FinanceSettings,
  type FinanceSettingsPayload,
  PLATFORM_COMPANY_SETTINGS,
  type ThemeOverridesPayload,
  type ThemeSnapshot,
} from '@delivery/shared';
import { headers } from 'next/headers';
import { ApiError, apiFetch, publicFetch } from '@/lib/http';
import { isPlatformHost } from '@/lib/platform-domain';

/**
 * Configuration société — `GET /public/company`, `PATCH /settings/company`.
 *
 * Une seule société par installation (docs/DECISIONS.md D-013) : ces fonctions
 * ne prennent pas d'identifiant, l'API résout la société côté serveur.
 *
 * La LECTURE passe par l'endpoint public : le nom, le logo et les couleurs sont
 * affichés sur la landing et le suivi de colis, que personne n'authentifie.
 * L'ÉCRITURE reste réservée au super-admin.
 */

export async function getCompanySettings(): Promise<CompanySettings> {
  try {
    // Une minute de cache : la marque change à peine plus souvent qu'un logo.
    return await publicFetch<CompanySettings>('/public/company', { revalidate: 60 });
  } catch (error) {
    /*
     * Deux replis délibérés. Le layout racine appelle cette fonction sur
     * TOUTES les routes : ce qui échoue ici blanchit la page entière.
     *
     *  - `NETWORK` : l'API est injoignable. Sans ce repli, un `next build`
     *    sans base échouerait, et un incident sur l'API rendrait la landing
     *    blanche plutôt que mal peinte.
     *
     *  - `NOT_FOUND` : l'hôte n'appartient à AUCUNE société — le domaine nu de
     *    la plateforme (C8 bloc 5, D-049). Il n'y a alors pas de marque à
     *    peindre, et c'est normal, pas une panne. Avant C8 le cas n'existait
     *    pas : l'API rendait la première société de la table, donc la landing
     *    de Nocido portait la marque d'un client. La corriger a fait
     *    apparaître ce 404, et le layout tombait en 500.
     *
     * Toute autre erreur (500, 403) remonte : celles-là sont des pannes.
     */
    if (error instanceof ApiError && (error.code === 'NETWORK' || error.code === 'NOT_FOUND')) {
      /*
       * Le domaine NU a sa propre marque — C9 bloc 5.
       *
       * Les deux cas ci-dessus remontaient tous deux en marque neutre, et
       * c'est ce qui rendait la vitrine grise : « aucun tenant sur cet hôte »
       * n'est pas un incident, c'est la situation NORMALE de
       * `delivery.nocido.com`. Le repli gris dit « configuration absente » ;
       * sur le domaine de la plateforme, il n'y a rien d'absent.
       *
       * Le test d'hôte vient APRÈS l'erreur, pas avant l'appel : un jour une
       * société pourra porter ce domaine, et l'API restera seule à en décider.
       */
      const store = await headers();
      if (isPlatformHost(store.get('x-forwarded-host') ?? store.get('host'))) {
        return PLATFORM_COMPANY_SETTINGS;
      }

      return DEFAULT_COMPANY_SETTINGS;
    }
    throw error;
  }
}

export async function updateCompanySettings(
  input: CompanySettingsPatchPayload,
): Promise<CompanySettings> {
  return apiFetch<CompanySettings>('/settings/company', { method: 'PATCH', body: input });
}

/**
 * Paramètres > Finance — tout ce que la facturation lit (D-019).
 *
 * Endpoint séparé des réglages métier : ce sont deux écrans, deux moments, et
 * une TVA ne se change pas en même temps qu'une langue d'interface.
 */
export async function getFinanceSettings(): Promise<FinanceSettings> {
  return apiFetch<FinanceSettings>('/settings/finance');
}

export async function updateFinanceSettings(
  input: FinanceSettingsPayload,
): Promise<FinanceSettings> {
  return apiFetch<FinanceSettings>('/settings/finance', { method: 'PATCH', body: input });
}

/**
 * Thème avancé — `PATCH /settings/theme`, réservé au super-administrateur.
 *
 * Endpoint séparé de `PATCH /settings/company` à dessein : ce n'est ni le même
 * geste ni le même droit. Changer le nom de la société corrige un état civil ;
 * repeindre l'interface engage tous ceux qui l'utilisent.
 */
export async function updateCompanyTheme(theme: ThemeOverridesPayload): Promise<CompanySettings> {
  return apiFetch<CompanySettings>('/settings/theme', { method: 'PATCH', body: theme });
}

/* -------------------------------------------------------------------------- */
/* Palettes, standard et retour arrière — B-10                                */
/* -------------------------------------------------------------------------- */

export interface ThemeWorkbench {
  /** Palettes proposées, lues en base (`Setting theme.presets`). */
  palettes: BrandPalette[];
  /** L'instantané du défaut, posé au seed. `null` si le seed n'a pas tourné. */
  standard: ThemeSnapshot | null;
  /** L'état d'avant la dernière modification. `null` s'il n'y en a pas eu. */
  previous: ThemeSnapshot | null;
}

/**
 * L'atelier du thème.
 *
 * Les palettes viennent de la BASE et non d'une constante du bundle : en
 * ajouter une pour un client ne doit pas demander un déploiement (D-019, et
 * règle n°3 de `CLAUDE.md`).
 */
export async function getThemeWorkbench(): Promise<ThemeWorkbench> {
  return apiFetch<ThemeWorkbench>('/settings/theme');
}

export async function restoreThemeSnapshot(
  which: 'standard' | 'previous',
): Promise<CompanySettings> {
  return apiFetch<CompanySettings>(`/settings/theme/restore/${which}`, { method: 'POST' });
}

/** Envoie le logo — `POST /settings/logo`, chantier « Audit des paramètres ». */
export async function uploadCompanyLogo(body: CompanyLogoPayload): Promise<CompanySettings> {
  return apiFetch<CompanySettings>('/settings/logo', { method: 'POST', body });
}
