import { Bike, Car, ChevronRight, History, LifeBuoy, Truck, TrendingUp } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import type { VehicleType } from '@delivery/shared';
import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { Card, CardContent } from '@/components/ui/card';
import { ProfileForm } from '@/features/accounts/profile-form';
import { ChangePasswordForm } from '@/features/accounts/change-password-form';
import { Link } from '@/i18n/navigation';
import { getCurrentUser } from '@/lib/api/auth';
import { getCourierMe } from '@/lib/api/courier';
import { getCompanySettings } from '@/lib/api/settings';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courier.nav');
  return { title: t('profile') };
}

/** Une icône par véhicule : c'est la première chose qu'on reconnaît. */
const VEHICLE_ICONS: Record<VehicleType, LucideIcon> = {
  motorbike: Bike,
  car: Car,
  van: Truck,
};

/**
 * MON PROFIL — L2 bloc 9, maquette 12, le miroir de l'écran mobile.
 *
 * **Les mêmes deux formulaires que le back-office**, réutilisés tels quels :
 * `PATCH /users/me` et `POST /users/me/password` (C13 bloc 4) ne sont gardés
 * par aucune permission, précisément pour qu'un livreur puisse corriger son
 * téléphone sans qu'on lui ouvre `accounts.write`. En recopier une version
 * « livreur » aurait fait deux écrans à corriger au prochain champ ajouté.
 *
 * **Ce que L2 ajoute**, et c'est ce que la maquette 12 demande : la carte
 * d'identité avec le rôle et le rattachement, **« Mon véhicule »** — qui
 * existait en base (`Courier.vehicleType`) mais n'était affiché nulle part —
 * et les trois destinations : statistiques, historique, aide.
 *
 * **Sur le web, tout tient sur une page.** Le téléphone en fait un sommaire de
 * cinq lignes parce qu'un écran de 375 points ne porte pas un formulaire ET un
 * menu ; ici l'écran est large, et faire cliquer pour atteindre « Mes
 * informations » serait un clic pour rien.
 */
export default async function CourierProfilePage() {
  const [t, tNav, tHome, tProfile, user, me, company] = await Promise.all([
    getTranslations('courier.profile'),
    getTranslations('courier.nav'),
    getTranslations('courier.home'),
    getTranslations('courier.profile'),
    getCurrentUser(),
    getCourierMe().catch(() => null),
    getCompanySettings(),
  ]);

  if (!user) return null;

  const VehicleIcon = me ? (VEHICLE_ICONS[me.vehicleType] ?? Truck) : Truck;

  return (
    <div className="flex max-w-2xl flex-col gap-6">
      <h1 className="text-xl font-semibold tracking-tight">{tNav('profile')}</h1>

      {/* --- La carte d'identité de la maquette -------------------------- */}
      <Card>
        <CardContent className="flex items-center gap-4 p-4">
          <span className="bg-primary/10 text-primary flex size-14 shrink-0 items-center justify-center rounded-full text-lg font-bold">
            {initials(`${user.firstName} ${user.lastName}`)}
          </span>

          <span className="min-w-0 flex-1">
            <span className="block truncate text-base font-semibold">
              {user.firstName} {user.lastName}
            </span>
            <span className="text-muted-foreground block text-sm">{tHome('role')}</span>
            {me ? (
              <span className="text-muted-foreground block truncate text-sm">
                {[me.agencyName, me.cityName].filter(Boolean).join(' · ')}
              </span>
            ) : null}
          </span>
        </CardContent>
      </Card>

      {/* --- Mon véhicule, mes statistiques, mon historique, l'aide ------ */}
      <Card>
        <CardContent className="flex flex-col p-1">
          <div className="flex min-h-13 items-center gap-3 px-3 text-sm">
            <VehicleIcon className="text-muted-foreground size-4 shrink-0" aria-hidden />
            <span className="min-w-0 flex-1 truncate">{t('myVehicle')}</span>
            <span className="text-muted-foreground">
              {me ? t(`vehicle.${me.vehicleType}`) : '—'}
            </span>
          </div>

          <Row icon={TrendingUp} label={tNav('stats')} href="/me/stats" />
          <Row icon={History} label={t('history')} href="/me/billing" />

          {/*
            AIDE & SUPPORT : le numéro de la société, pas un formulaire. Un
            livreur bloqué appelle. Les coordonnées viennent des réglages de la
            société, déjà chargés pour la marque — aucun appel de plus — et la
            ligne disparaît quand aucun numéro n'est renseigné.
          */}
          {company.phone ? (
            <a
              href={`tel:${company.phone}`}
              className="hover:bg-muted focus-visible:ring-ring flex min-h-13 items-center gap-3 rounded-lg px-3 text-sm transition-colors outline-none focus-visible:ring-2"
            >
              <LifeBuoy className="text-muted-foreground size-4 shrink-0" aria-hidden />
              <span className="min-w-0 flex-1 truncate">{t('help')}</span>
              <span className="text-muted-foreground">{company.phone}</span>
            </a>
          ) : null}
        </CardContent>
      </Card>

      <h2 className="text-base font-semibold">{tProfile('myInfo')}</h2>
      <ProfileForm profile={user} />
      <ChangePasswordForm />
    </div>
  );
}

/** Les initiales, faute de photo : deux lettres valent mieux qu'une silhouette. */
function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('');
}

/** Une ligne de sommaire : icône, libellé, chevron. */
function Row({ icon: Icon, label, href }: { icon: LucideIcon; label: string; href: string }) {
  return (
    <Link
      href={href}
      className="hover:bg-muted focus-visible:ring-ring flex min-h-13 items-center gap-3 rounded-lg px-3 text-sm transition-colors outline-none focus-visible:ring-2"
    >
      <Icon className="text-muted-foreground size-4 shrink-0" aria-hidden />
      <span className="min-w-0 flex-1 truncate">{label}</span>
      <ChevronRight className="text-muted-foreground size-4 rtl:rotate-180" aria-hidden />
    </Link>
  );
}
