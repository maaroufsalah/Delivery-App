'use client';

import type { NotificationSettings, NotificationTopicKey } from '@delivery/shared';
import { Lock } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { Switch } from '@/components/ui/switch';
import { setNotificationPreferenceAction } from '@/lib/actions/notifications';

/**
 * LES SUJETS DE NOTIFICATION, sur le web — L2 bloc 9, maquette 13.
 *
 * **La préférence est SERVEUR**, pas locale : elle vit sur le compte et non sur
 * l'appareil. Couper « colis reporté » depuis le navigateur de l'agence le
 * coupe aussi sur le téléphone, et c'est le comportement attendu — c'est la
 * même personne qui reçoit.
 *
 * **Optimiste, et réconcilié.** L'interrupteur bascule tout de suite ; l'action
 * rend l'état complet que le serveur a retenu, et on le pose. Attendre
 * l'aller-retour ferait un interrupteur qui « colle » sur une 3G d'agence ; ne
 * jamais réconcilier laisserait un interrupteur menteur si le serveur refuse.
 *
 * **Un sujet CRITIQUE reste visible, et verrouillé.** Le cacher laisserait
 * croire qu'on ne le reçoit pas ; l'ouvrir permettrait à un livreur de ne plus
 * voir arriver son travail et de croire l'application cassée (D-081).
 */
export function NotificationSwitches({ settings }: { settings: NotificationSettings }) {
  const t = useTranslations('courier.settings');
  const tTopic = useTranslations('courier.notif');

  const [topics, setTopics] = useState(settings.topics);
  const [pending, startTransition] = useTransition();

  const toggle = (key: NotificationTopicKey, enabled: boolean) => {
    setTopics((current) =>
      current.map((topic) => (topic.key === key ? { ...topic, enabled } : topic)),
    );

    startTransition(async () => {
      const result = await setNotificationPreferenceAction(key, enabled);
      if (result.ok) setTopics(result.data.topics);
    });
  };

  if (topics.length === 0) return null;

  return (
    <ul className="flex flex-col gap-3">
      {topics.map((topic) => (
        <li key={topic.key} className="flex items-center justify-between gap-3">
          <span className="min-w-0">
            <span className="block text-sm">{tTopic(topic.key as NotificationTopicKey)}</span>
            {topic.critical ? (
              <span className="text-muted-foreground flex items-center gap-1 text-xs">
                <Lock className="size-3" aria-hidden />
                {t('locked')}
              </span>
            ) : null}
          </span>

          <Switch
            checked={topic.enabled}
            disabled={topic.critical || pending}
            aria-label={tTopic(topic.key as NotificationTopicKey)}
            onCheckedChange={(next) => toggle(topic.key as NotificationTopicKey, next)}
          />
        </li>
      ))}
    </ul>
  );
}
