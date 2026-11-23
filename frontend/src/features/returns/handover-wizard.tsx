'use client';

import type { Agency, Courier, ReturnParcelLine } from '@delivery/shared';
import { ArrowLeft, Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { SearchableSelect } from '@/components/searchable-select';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { FormSheet } from '@/features/referential/form-sheet';
import { HandoverParcelPicker } from '@/features/returns/handover-parcel-picker';
import { useReturnError } from '@/features/returns/use-return-error';
import { createHandoverAction, handoverCandidatesAction } from '@/lib/actions/returns';

/**
 * CRÉER UN BON DE RETOUR — chantier « Retours, BL Clients Pro », point 2.
 *
 * « Étape 1 choisir le livreur ; étape 2 liste des colis éligibles chez ce
 * livreur […] multi-sélection ; étape 3 “Créer le bon de retour” → statut
 * Retour demandé. »
 *
 * **Deux étapes à l'écran, trois dans la mission**, et ce n'est pas une
 * simplification : l'étape 3 est un BOUTON, pas une page. Une troisième vue qui
 * ne montrerait qu'un récapitulatif de ce qu'on vient de cocher ajouterait un
 * clic sans rien apprendre.
 *
 * **Le formulaire d'avant n'a pas disparu, il a fondu dedans.** L'agent qui
 * veut ouvrir un bon vide et le remplir à la douchette le fait toujours : il
 * choisit son livreur et crée sans rien cocher. C'est ce que faisait
 * `HandoverForm`, et c'est pour cela que la sélection est facultative côté API.
 *
 * **La liste est chargée à CHAQUE changement de livreur**, et pas une fois pour
 * toutes : la pile d'un livreur bouge pendant qu'on la regarde — une tournée
 * qui rentre, un collègue qui ouvre un bon sur les mêmes cartons. La recharger
 * est le seul moyen de ne pas proposer ce qui vient d'être pris.
 */
export function HandoverWizard({
  couriers,
  agencies,
  onDone,
  onCreated,
}: {
  couriers: Courier[];
  agencies: Agency[];
  onDone: () => void;
  onCreated: (code: string) => void;
}) {
  const t = useTranslations('returns.handovers');
  const tCommon = useTranslations('common');
  const errorMessage = useReturnError();

  const [step, setStep] = useState<'courier' | 'parcels'>('courier');
  const [courierId, setCourierId] = useState('');
  const [agencyId, setAgencyId] = useState(agencies.length === 1 ? (agencies[0]?.id ?? '') : '');
  const [note, setNote] = useState('');
  const [courierCanEdit, setCourierCanEdit] = useState(false);

  const [candidates, setCandidates] = useState<ReturnParcelLine[]>([]);
  const [selected, setSelected] = useState<string[]>([]);
  const [search, setSearch] = useState('');

  const [loading, startLoading] = useTransition();
  const [saving, setSaving] = useState(false);

  const goToParcels = () => {
    if (!courierId || !agencyId) return;

    startLoading(async () => {
      const result = await handoverCandidatesAction(courierId);

      if (!result.ok) {
        toast.error(errorMessage(result));
        return;
      }

      setCandidates(result.data);
      setSelected([]);
      setSearch('');
      setStep('parcels');
    });
  };

  const create = async () => {
    setSaving(true);
    const result = await createHandoverAction({
      courierId,
      agencyId,
      note,
      courierCanEdit,
      parcelIds: selected,
    });
    setSaving(false);

    if (result.ok) {
      toast.success(t('created', { code: result.data.code }));
      onDone();
      onCreated(result.data.code);
      return;
    }

    toast.error(errorMessage(result));
  };

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={t('new')}
      description={step === 'courier' ? t('wizardCourierHint') : t('wizardParcelsHint')}
      hideFooter
    >
      <div className="flex flex-col gap-4">
        {/* --- Étape 1 : le livreur ------------------------------------- */}
        {step === 'courier' ? (
          <>
            <div>
              <Label htmlFor="wizard-courier">{t('fields.courier')}</Label>
              <SearchableSelect
                options={couriers.map((courier) => ({
                  value: courier.id,
                  label: `${courier.firstName} ${courier.lastName}`,
                }))}
                value={courierId}
                onValueChange={setCourierId}
                placeholder={t('fields.courier')}
                id="wizard-courier"
                className="mt-1.5 w-full"
              />
            </div>

            <div>
              <Label htmlFor="wizard-agency">{t('fields.agency')}</Label>
              <SearchableSelect
                options={agencies.map((agency) => ({ value: agency.id, label: agency.name }))}
                value={agencyId}
                onValueChange={setAgencyId}
                placeholder={t('fields.agency')}
                id="wizard-agency"
                className="mt-1.5 w-full"
              />
            </div>

            <div>
              <Label htmlFor="wizard-note">{t('fields.note')}</Label>
              <Input
                id="wizard-note"
                value={note}
                onChange={(event) => setNote(event.target.value)}
                className="mt-1.5"
              />
            </div>

            {/*
              « Modification autorisée par le livreur » — point 2. Décochée par
              défaut : un bon d'origine admin est une DEMANDE, et le livreur
              l'exécute. La case l'ouvre quand l'agent sait que la pile bougera
              encore d'ici la tournée.
            */}
            <label className="hover:bg-muted/40 flex cursor-pointer items-start gap-3 rounded-lg border p-3">
              <Checkbox
                checked={courierCanEdit}
                onCheckedChange={(value) => setCourierCanEdit(value === true)}
                className="mt-0.5"
              />
              <span className="min-w-0 flex-1 text-sm">
                {t('courierCanEdit')}
                <span className="text-muted-foreground block text-xs">
                  {t('courierCanEditHint')}
                </span>
              </span>
            </label>

            <Button
              onClick={goToParcels}
              disabled={!courierId || !agencyId || loading}
              className="gap-1.5"
            >
              {loading ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('wizardParcels')}
            </Button>
          </>
        ) : null}

        {/* --- Étape 2 : les colis, et l'étape 3 en bouton -------------- */}
        {step === 'parcels' ? (
          <>
            <Button
              variant="ghost"
              size="sm"
              className="-ms-2 w-fit gap-1.5"
              onClick={() => setStep('courier')}
              disabled={saving}
            >
              <ArrowLeft className="flip-rtl size-4" aria-hidden />
              {t('wizardCourier')}
            </Button>

            <HandoverParcelPicker
              candidates={candidates}
              selected={selected}
              onSelectedChange={setSelected}
              search={search}
              onSearchChange={setSearch}
            />

            <div className="bg-background sticky bottom-0 flex flex-col gap-2 border-t pt-3">
              <p className="text-muted-foreground text-xs">
                {t('selected', { count: selected.length })}
              </p>
              <div className="flex gap-2">
                <Button variant="outline" onClick={onDone} disabled={saving} className="flex-1">
                  {tCommon('cancel')}
                </Button>
                <Button onClick={create} disabled={saving} className="flex-1 gap-1.5">
                  {saving ? <Loader2 className="size-4 animate-spin" /> : null}
                  {t('wizardCreate')}
                </Button>
              </div>
            </div>
          </>
        ) : null}
      </div>
    </FormSheet>
  );
}
