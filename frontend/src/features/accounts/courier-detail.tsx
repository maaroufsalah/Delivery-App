'use client';

import type { Agency, City, CourierDetail as CourierDetailData, Zone } from '@delivery/shared';
import { BadgeCheck, Pencil, UserX } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { CellBadge } from '@/components/data-table/cells';
import { Button } from '@/components/ui/button';
import { CourierCitiesGrid } from '@/features/accounts/courier-cities-grid';
import { CourierForm } from '@/features/accounts/courier-form';
import { RejectDialog, ValidateDialog } from '@/features/accounts/validation-dialogs';
import { rejectCourierAction, validateCourierAction } from '@/lib/actions/accounts';
import { cn } from '@/lib/utils';

/**
 * Fiche livreur : identité et grille de paie par ville.
 *
 * Les montants de cette grille sont ceux DUS AU LIVREUR — ils n'ont rien à voir
 * avec ceux facturés au client, que porte le référentiel tarifaire. Le legacy
 * mélangeait les deux dans `livreur_villes` sans jamais le dire (DOMAIN.md
 * §1.6), d'où le rappel affiché au-dessus du tableau.
 *
 * La grille est remplacée EN BLOC, comme la matrice des rôles : envoyer une
 * ligne à la fois laisserait la grille à moitié écrite si le réseau lâche.
 */
export function CourierDetail({
  courier,
  cities,
  agencies,
  zones,
}: {
  courier: CourierDetailData;
  cities: City[];
  agencies: Agency[];
  /** Les 3 zones du référentiel : le filtre du tableau des villes (lot F, D-206). */
  zones: Zone[];
}) {
  const t = useTranslations('accounts.couriers');
  const tValidation = useTranslations('accounts.validation');
  const tCommon = useTranslations('common');

  const [editing, setEditing] = useState(false);
  const [validating, setValidating] = useState(false);
  const [rejecting, setRejecting] = useState(false);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        {courier.validatedAt ? (
          <CellBadge label={t('validated')} color="var(--chart-4)" icon={BadgeCheck} />
        ) : courier.rejectedAt ? (
          <CellBadge label={t('rejected')} color="var(--destructive)" icon={UserX} />
        ) : (
          <CellBadge label={t('notValidated')} />
        )}
        {courier.isActive ? (
          <CellBadge label={tCommon('active')} color="var(--chart-4)" />
        ) : (
          <CellBadge label={tCommon('inactive')} />
        )}

        {/* Le motif, à côté du badge : c'est ce que l'agent lira quand la
            personne rappellera — C13 bloc 2. */}
        {courier.rejectedAt ? (
          <span className="text-muted-foreground text-xs">{courier.rejectionReason}</span>
        ) : null}

        {/* Décider depuis la FICHE : un agent qui ouvre le dossier pour
            l'examiner ne doit pas retourner à la liste pour trancher. */}
        {!courier.validatedAt ? (
          <>
            <Button size="sm" className="ms-auto gap-1.5" onClick={() => setValidating(true)}>
              <BadgeCheck className="size-4" />
              {tValidation('validate')}
            </Button>
            <Button
              size="sm"
              variant="outline"
              className="gap-1.5"
              onClick={() => setRejecting(true)}
            >
              <UserX className="size-4" />
              {tValidation('reject')}
            </Button>
          </>
        ) : null}

        <Button
          size="sm"
          variant="outline"
          className={cn('gap-1.5', courier.validatedAt && 'ms-auto')}
          onClick={() => setEditing(true)}
        >
          <Pencil className="size-4" />
          {tCommon('edit')}
        </Button>
      </div>

      <CourierCitiesGrid courier={courier} cities={cities} zones={zones} />

      {/* L'accès au compte du livreur — C12 bloc 2a. */}

      {validating ? (
        <ValidateDialog
          label={`${courier.firstName} ${courier.lastName}`}
          agencies={agencies}
          onConfirm={(agencyId) => validateCourierAction(courier.id, agencyId)}
          onClose={() => setValidating(false)}
        />
      ) : null}

      {rejecting ? (
        <RejectDialog
          label={`${courier.firstName} ${courier.lastName}`}
          onConfirm={(reason) => rejectCourierAction(courier.id, reason)}
          onClose={() => setRejecting(false)}
        />
      ) : null}

      {editing ? (
        <CourierForm
          courier={courier}
          cities={cities}
          agencies={agencies}
          onDone={() => setEditing(false)}
        />
      ) : null}
    </div>
  );
}
