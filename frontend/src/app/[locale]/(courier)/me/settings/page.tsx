import { Bell, Globe, Info, Sun } from 'lucide-react';
import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { LocaleSwitcher } from '@/components/layout/locale-switcher';
import { ThemeToggle } from '@/components/layout/theme-toggle';
import { Card, CardContent } from '@/components/ui/card';
import { NotificationSwitches } from '@/features/courier/notification-switches';
import { getNotificationSettings } from '@/lib/api/notifications';
import { getCompanySettings } from '@/lib/api/settings';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courier.nav');
  return { title: t('settings') };
}

/**
 * LES PARAMÈTRES — L2 bloc 9, maquette 13, le miroir de l'écran mobile.
 *
 * **Les mêmes réglages, par les mêmes commandes.** Le mode sombre et la langue
 * vivaient dans l'en-tête, où ils servent partout ; ils sont aussi ICI, parce
 * qu'un livreur qui cherche « Paramètres » ne pense pas à regarder la barre du
 * haut. Ce sont les MÊMES composants — `ThemeToggle` et `LocaleSwitcher` — donc
 * le même état, pas une seconde préférence.
 *
 * **Les notifications sont une préférence de COMPTE**, pas d'appareil : la
 * couper ici la coupe aussi sur le téléphone, et c'est ce qu'on veut — c'est la
 * même personne qui reçoit.
 *
 * **Ce que la maquette montre et qui n'est pas ici** : le « son des
 * notifications ». Un interrupteur qui ne coupe rien est pire qu'absent, et un
 * navigateur n'a pas de canal de notification à régler (D-L2-11, B-18).
 */
export default async function CourierSettingsPage() {
  const [t, tNav, company, notifications] = await Promise.all([
    getTranslations('courier.settings'),
    getTranslations('courier.nav'),
    getCompanySettings(),
    /*
     * Un compte sans audience de notification — cela arrive — rend une liste
     * vide, et la carte disparaît. Un 404 fait la même chose : la section se
     * tait plutôt que de montrer une erreur à quelqu'un qui cherchait le mode
     * sombre.
     */
    getNotificationSettings().catch(() => null),
  ]);

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold tracking-tight">{tNav('settings')}</h1>

      <Card>
        <CardContent className="flex flex-col gap-4 p-4">
          <Row icon={Sun} label={t('theme')}>
            <ThemeToggle />
          </Row>

          <Row icon={Globe} label={t('language')}>
            <LocaleSwitcher />
          </Row>
        </CardContent>
      </Card>

      {notifications && notifications.topics.length > 0 ? (
        <Card>
          <CardContent className="flex flex-col gap-4 p-4">
            <div className="flex items-center gap-2">
              <Bell className="text-muted-foreground size-4" aria-hidden />
              <h2 className="text-sm font-semibold">{t('notifications')}</h2>
            </div>

            <NotificationSwitches settings={notifications} />
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardContent className="flex items-center gap-3 p-4">
          <Info className="text-muted-foreground size-4 shrink-0" aria-hidden />
          <span className="min-w-0">
            <span className="block text-sm">{t('about')}</span>
            <span className="text-muted-foreground block truncate text-sm">{company.name}</span>
          </span>
        </CardContent>
      </Card>
    </div>
  );
}

/** Une ligne de réglage : l'icône, le libellé, la commande à droite. */
function Row({
  icon: Icon,
  label,
  children,
}: {
  icon: typeof Sun;
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex items-center justify-between gap-3">
      <span className="flex min-w-0 items-center gap-2">
        <Icon className="text-muted-foreground size-4 shrink-0" aria-hidden />
        <span className="truncate text-sm">{label}</span>
      </span>
      {children}
    </div>
  );
}
