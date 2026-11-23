'use client';

import type {
  Agency,
  City,
  ClientDetail as ClientDetailData,
  District,
  UserDetail,
  Zone,
} from '@delivery/shared';
import { useTranslations } from 'next-intl';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useTabParam } from '@/hooks/use-tab-param';
import { AccountPanel } from '@/features/accounts/account-panel';
import { ClientDetail, type ClientProVat } from '@/features/accounts/client-detail';
import { ClientPickupTerms, type PickupDefaults } from '@/features/accounts/client-pickup-terms';
import { type ClientCompanyRate, ClientPricing } from '@/features/accounts/client-pricing';
import { EntityHistory } from '@/features/audit/entity-history';
import { createClientAccountAction } from '@/lib/actions/accounts';

/**
 * La fiche e-commerçant en onglets — C13 bloc 3 ; le troisième, « Tarifs &
 * remises », au lot K (D-218).
 *
 * Elle n'en avait aucun : la carte « Accès au compte » vivait tout en bas,
 * après les remises, les adresses de ramassage et les documents. Le geste le
 * plus urgent — « ce client ne peut plus se connecter » — était le plus loin.
 *
 * Le même découpage que la fiche livreur, et pour la même raison : on ouvre la
 * première page pour corriger une adresse, la seconde pour rendre un accès.
 */
export function ClientTabs({
  client,
  account,
  cities,
  districts,
  zones,
  agencies,
  companyRate,
  canManageAccounts,
  proVat,
  pickupDefaults,
}: {
  client: ClientDetailData;
  /** Compte de connexion lié, `null` quand la fiche n'en a pas encore. */
  account: UserDetail | null;
  cities: City[];
  districts: District[];
  zones: Zone[];
  agencies: Agency[];
  /** Le tarif société du client, sans remise — onglet « Tarifs & remises ». */
  companyRate: ClientCompanyRate;
  /** `accounts.write` : ouvre l'onglet « Compte » en écriture. */
  canManageAccounts: boolean;
  /** Lot R (D-230) : le taux de TVA du Client Pro (administration seule). */
  proVat: ClientProVat;
  /** Lot T (D-232) : le ramassage global, affiché en grisé sous les champs vides. */
  pickupDefaults: PickupDefaults | null;
}) {
  const t = useTranslations('accounts.clients');
  const tAccount = useTranslations('accounts.account');
  const [tab, setTab] = useTabParam('profile');

  return (
    <Tabs value={tab} onValueChange={setTab} className="gap-4">
      <TabsList>
        <TabsTrigger value="profile">{t('tabs.profile')}</TabsTrigger>
        <TabsTrigger value="account">{tAccount('tab')}</TabsTrigger>
        <TabsTrigger value="pricing">{t('tabs.pricing')}</TabsTrigger>
      </TabsList>

      <TabsContent value="profile">
        <ClientDetail
          client={client}
          cities={cities}
          districts={districts}
          agencies={agencies}
          proVat={proVat}
        />
      </TabsContent>

      <TabsContent value="pricing" className="flex flex-col gap-4">
        <ClientPricing client={client} cities={cities} zones={zones} companyRate={companyRate} />
        {/* Lot T (D-232) : frais de ramassage et seuil de gratuité du client. */}
        <ClientPickupTerms
          client={client}
          defaults={pickupDefaults}
          canEdit={proVat.canEdit}
        />
      </TabsContent>

      <TabsContent value="account">
        <AccountPanel
          user={account}
          agencies={agencies}
          canManage={canManageAccounts}
          fixedRole="MERCHANT"
          /*
           * Prérempli depuis la fiche — le nom de la boutique se coupe en
           * prénom / nom, faute de mieux : une fiche client porte une RAISON
           * SOCIALE, pas une identité. L'administrateur corrige, et c'est
           * exactement pour cela que ce sont des valeurs par défaut.
           */
          defaults={{
            email: client.email,
            phone: client.phone,
            firstName: client.name.split(' ')[0] ?? client.name,
            lastName: client.name.split(' ').slice(1).join(' ') || client.name,
            roles: ['MERCHANT'],
            agencyId: client.agencyId ?? '',
          }}
          onCreate={(values) => createClientAccountAction(client.id, values)}
        />
      </TabsContent>

      {/* SOUS les onglets : l'historique porte sur la fiche entière. */}
      <EntityHistory entity="Client" entityId={client.id} />
    </Tabs>
  );
}
