'use client';

import type {
  CourierBalance,
  Agency,
  BankAccount,
  City,
  CourierDetail as CourierDetailData,
  CourierStatementListItem,
  UserDetail,
  Zone,
} from '@delivery/shared';
import { useTranslations } from 'next-intl';
import { EntityHistory } from '@/features/audit/entity-history';
import { AccountPanel } from '@/features/accounts/account-panel';
import { CourierDetail } from '@/features/accounts/courier-detail';
import { createCourierAccountAction } from '@/lib/actions/accounts';
import { CourierStatementsPanel } from '@/features/finance/courier-statements-panel';
import { CourierBalancePanel } from '@/features/finance/courier-balance-panel';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useTabParam } from '@/hooks/use-tab-param';

/**
 * La fiche livreur en deux onglets.
 *
 * L'identité et la grille de paie d'un côté, les relevés de rémunération de
 * l'autre (D-020). Deux lectures différentes du même homme : on ouvre la
 * première pour corriger un RIB, la seconde pour payer un mois — et les mêler
 * dans une page unique ferait défiler l'une pour atteindre l'autre.
 *
 * L'onglet « Relevés » n'apparaît qu'avec `finance.read` : un responsable
 * d'agence sans droit sur la facturation gère bien ses livreurs, il ne voit
 * simplement pas leur paie.
 */
export function CourierTabs({
  courier,
  account,
  cities,
  agencies,
  zones,
  statements,
  balance,
  accounts,
  showStatements,
  canWrite,
  canValidate,
  canPay,
  canManageAccounts,
}: {
  courier: CourierDetailData;
  /** Compte de connexion lié, `null` quand la fiche n'en a pas encore. */
  account: UserDetail | null;
  cities: City[];
  agencies: Agency[];
  zones: Zone[];
  statements: CourierStatementListItem[];
  /**
   * Le solde du livreur et son grand livre — point 4. `null` quand la portée
   * ne donne pas accès à la trésorerie : l'onglet disparaît alors, plutôt que
   * de s'afficher vide.
   */
  balance: CourierBalance | null;
  accounts: BankAccount[];
  showStatements: boolean;
  canWrite: boolean;
  canValidate: boolean;
  canPay: boolean;
  /** `accounts.write` : ouvre l'onglet « Compte » en écriture (C13 bloc 3). */
  canManageAccounts: boolean;
}) {
  const t = useTranslations('accounts.couriers');
  const tAccount = useTranslations('accounts.account');
  const tBalance = useTranslations('finance.balance');
  const [tab, setTab] = useTabParam('profile');

  const history = <EntityHistory entity="Courier" entityId={courier.id} />;

  /*
   * L'onglet « Compte » — C13 bloc 3. Il remplace la carte « Accès au compte »,
   * qui vivait au bas de la fiche : le mot de passe y voisinait la grille de
   * paie, deux sujets qui n'ont jamais eu de raison de se suivre.
   */
  const accountTab = (
    <AccountPanel
      user={account}
      agencies={agencies}
      canManage={canManageAccounts}
      fixedRole="COURIER"
      defaults={{
        firstName: courier.firstName,
        lastName: courier.lastName,
        phone: courier.phone,
        roles: ['COURIER'],
        agencyId: courier.agencyId ?? '',
      }}
      onCreate={(values) => createCourierAccountAction(courier.id, values)}
    />
  );

  return (
    <Tabs value={tab} onValueChange={setTab} className="gap-4">
      <TabsList>
        <TabsTrigger value="profile">{t('tabs.profile')}</TabsTrigger>
        <TabsTrigger value="account">{tAccount('tab')}</TabsTrigger>
        {showStatements ? (
          <TabsTrigger value="statements">{t('tabs.statements')}</TabsTrigger>
        ) : null}
        {/*
          LE SOLDE — chantier « Finance », point 4. Il vit sur la fiche du
          LIVREUR et non sur une caisse : une dette née mardi se règle jeudi, et
          la rattacher à la caisse de mardi aurait obligé à la retrouver pour
          l'apurer.
        */}
        {balance ? <TabsTrigger value="balance">{tBalance('tab')}</TabsTrigger> : null}
      </TabsList>

      <TabsContent value="profile">
        <CourierDetail courier={courier} cities={cities} agencies={agencies} zones={zones} />
      </TabsContent>

      <TabsContent value="account">{accountTab}</TabsContent>

      {balance ? (
        <TabsContent value="balance">
          <CourierBalancePanel balance={balance} canSettle={canPay} />
        </TabsContent>
      ) : null}

      {showStatements ? (
        <TabsContent value="statements">
          <CourierStatementsPanel
            courierId={courier.id}
            agencyId={courier.agencyId}
            payPeriod={courier.payPeriod}
            statements={statements}
            accounts={accounts}
            canWrite={canWrite}
            canValidate={canValidate}
            canPay={canPay}
          />
        </TabsContent>
      ) : null}

      {/*
        SOUS les onglets, pas dedans : l'historique porte sur la fiche entière,
        et le chercher dans l'onglet ouvert serait une devinette.
      */}
      {history}
    </Tabs>
  );
}
