'use client';

import type { ReturnParcelLine } from '@delivery/shared';
import { ArrowLeft, Loader2, Plus } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useEffect, useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { FormSheet } from '@/features/referential/form-sheet';
import { HandoverParcelPicker } from '@/features/returns/handover-parcel-picker';
import { useReturnError } from '@/features/returns/use-return-error';
import { useRouter } from '@/i18n/navigation';
import { createHandoverAction, handoverCandidatesAction } from '@/lib/actions/returns';

/**
 * LE LIVREUR OUVRE SON BON DE RETOUR — chantier « Bon de retour livreur »,
 * point 1, côté web.
 *
 * **Le miroir de l'écran mobile** (D-090), et le même parcours : ses colis
 * rapportables, une recherche, une multi-sélection, un récapitulatif, puis la
 * création. Le bon naît en « Retour demandé », origine LIVREUR — il n'y a pas
 * de brouillon de ce côté : quand il valide, il a déjà les cartons en main.
 *
 * **Aucun identifiant ne circule.** Ni dans la requête des colis, ni dans le
 * corps de la création : le livreur et son agence viennent de sa fiche, lue par
 * le SERVEUR. C'est la forme de `POST /merchant/me/pickups`, et elle vaut pour
 * la même raison — un écran qui passe son propre identifiant laisse croire
 * qu'il pourrait en passer un autre (D-082).
 *
 * **Le récapitulatif est une ÉTAPE, pas une phrase.** La mission le demande, et
 * c'est juste : cocher douze lignes dans une liste qui défile ne dit pas ce
 * qu'on s'apprête à signer. On les relit à part, on recule si besoin, on valide.
 */
export function CourierHandoverWizard({ onDone }: { onDone: () => void }) {
  const t = useTranslations('returns.handovers');
  const tCourier = useTranslations('courier.returns');
  const tCommon = useTranslations('common');
  const errorMessage = useReturnError();
  const router = useRouter();

  const [step, setStep] = useState<'pick' | 'review'>('pick');
  const [candidates, setCandidates] = useState<ReturnParcelLine[] | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  /*
   * LA MÊME BOUCLE QUE LE MODAL DE MASSE — trouvée par
   * `render-side-effects.spec.ts` en écrivant le garde-fou, pas en cherchant
   * ici.
   *
   * Ce chargement vivait dans le CORPS DE RENDU. React refuse
   * `startTransition` pendant un rendu : la garde `!loading` ne se fermait
   * jamais, le composant suspendait avant d'avoir committé, perdait son état,
   * et repartait. Une requête par aller-retour, sans borne — sur le TÉLÉPHONE
   * d'un livreur, en données mobiles.
   */
  useEffect(() => {
    let vivant = true;

    void (async () => {
      const result = await handoverCandidatesAction();
      if (!vivant) return;

      if (result.ok) setCandidates(result.data);
      else {
        toast.error(errorMessage(result));
        setCandidates([]);
      }

      setLoading(false);
    })();

    // L'assistant peut se fermer avant la réponse.
    return () => {
      vivant = false;
    };
    // `errorMessage` vient d'un hook et se recrée à chaque rendu ; le remettre
    // en dépendance relancerait exactement la boucle qu'on vient de fermer.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const chosen = (candidates ?? []).filter((parcel) => selected.includes(parcel.id));

  const create = async () => {
    setSaving(true);
    /*
     * AUCUN IDENTIFIANT NE CIRCULE — D-082. Le livreur et son agence viennent de
     * sa fiche, côté serveur : même forme que `POST /merchant/me/pickups`.
     */
    const result = await createHandoverAction({
      note: '',
      courierCanEdit: false,
      parcelIds: selected,
    });
    setSaving(false);

    if (result.ok) {
      toast.success(t('created', { code: result.data.code }));
      onDone();
      router.push(`/me/returns/${result.data.code}`);
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
      title={tCourier('newHandover')}
      description={step === 'pick' ? t('wizardParcelsHint') : tCourier('reviewHint')}
      hideFooter
    >
      <div className="flex flex-col gap-4">
        {step === 'pick' ? (
          <>
            {loading ? (
              <p className="text-muted-foreground py-6 text-center text-sm">{tCommon('loading')}</p>
            ) : (
              <HandoverParcelPicker
                candidates={candidates ?? []}
                selected={selected}
                onSelectedChange={setSelected}
                search={search}
                onSearchChange={setSearch}
              />
            )}

            <div className="bg-background sticky bottom-0 flex flex-col gap-2 border-t pt-3">
              <p className="text-muted-foreground text-xs">
                {t('selected', { count: selected.length })}
              </p>
              <div className="flex gap-2">
                <Button variant="outline" onClick={onDone} className="flex-1">
                  {tCommon('cancel')}
                </Button>
                <Button
                  onClick={() => setStep('review')}
                  disabled={selected.length === 0}
                  className="flex-1"
                >
                  {tCourier('review')}
                </Button>
              </div>
            </div>
          </>
        ) : (
          <>
            <Button
              variant="ghost"
              size="sm"
              className="-ms-2 w-fit gap-1.5"
              onClick={() => setStep('pick')}
              disabled={saving}
            >
              <ArrowLeft className="flip-rtl size-4" aria-hidden />
              {t('wizardParcels')}
            </Button>

            <ul className="flex flex-col gap-2">
              {chosen.map((parcel) => (
                <li key={parcel.id} className="rounded-lg border p-3">
                  <span className="block font-mono text-sm font-medium">{parcel.code}</span>
                  <span className="text-muted-foreground block truncate text-xs">
                    {parcel.recipientName} · {parcel.clientName}
                  </span>
                </li>
              ))}
            </ul>

            <div className="bg-background sticky bottom-0 flex flex-col gap-2 border-t pt-3">
              <p className="text-muted-foreground text-xs">
                {t('selected', { count: chosen.length })}
              </p>
              <Button onClick={create} disabled={saving} className="gap-1.5">
                {saving ? <Loader2 className="size-4 animate-spin" /> : <Plus className="size-4" />}
                {tCourier('newHandover')}
              </Button>
            </div>
          </>
        )}
      </div>
    </FormSheet>
  );
}
