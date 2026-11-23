'use client';

import { isBulkJobAccepted } from '@delivery/shared';
import type { Courier, ParcelTransferOptions } from '@delivery/shared';
import { ArrowRightLeft, Loader2, RotateCcw, TriangleAlert } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useRouter } from '@/i18n/navigation';
import { useRef, useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { SearchableSelect } from '@/components/searchable-select';
import { useRouteError } from '@/features/routes/use-route-error';
import { transferOptionsAction, transferParcelsAction } from '@/lib/actions/routes';

/** Les villes de B, telles que la boîte les a demandées (lot N, D-223). */
type CityState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; options: ParcelTransferOptions };

/**
 * « TRANSFÉRER » — lot I, I1 (D-211). Les colis passent du circuit de leur
 * livreur à celui d'un autre : le serveur choisit le circuit de B (sa tournée
 * en cours, sinon son brouillon), refuse un colis livré, rentré, sur un RL- ou
 * et prévient les deux livreurs. Motif facultatif.
 *
 * Lot N (D-223, précise D-219) : le champ « Ville de destination » est TOUJOURS
 * là dès que B est choisi — avant, il n'apparaissait qu'après un premier clic
 * refusé `transfer-city-required`, et jamais quand B couvrait déjà la
 * destination (le « parfois » d'Anass). Les villes viennent du serveur
 * (`transferOptionsAction`, le calcul que le scan lit aussi) :
 *
 *  - pré-rempli de la destination actuelle si B la couvre, sinon vide et
 *    obligatoire ; une seule ville : pré-remplie, non modifiable ;
 *  - un colis : une autre ville = changement de destination (chemin L1) ;
 *  - en masse : chacun garde la sienne si B la couvre, sinon la ville choisie ;
 *  - aucune ville COCHÉE (D-224, lot N2) : « aucune ville cochée : complétez sa
 *    fiche », bouton coupé — même si sa ville de fiche couvrait ;
 *  - chargement, erreur + « Réessayer » ; une réponse d'un livreur qui n'est
 *    plus le choisi est ignorée (`latest`), le menu livreur est figé pendant
 *    l'envoi.
 *
 * Contrôlée : la page circuit l'ouvre depuis sa barre de sélection, la fiche
 * colis depuis `ParcelTransferButton`. Les deux ne la MONTENT que pendant
 * qu'elle est ouverte : chaque ouverture repart de zéro (avant, la fiche colis
 * gardait le livreur et les villes d'une ouverture à l'autre).
 */
export function ParcelTransferDialog({
  parcelIds,
  couriers,
  currentCourierId,
  open,
  onOpenChange,
  onDone,
}: {
  parcelIds: string[];
  couriers: Courier[];
  /** Le livreur qui a les colis : il n'est pas proposé. */
  currentCourierId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const t = useTranslations('routes.transfer');
  const translateError = useRouteError();
  const [courierId, setCourierId] = useState('');
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);
  const [cities, setCities] = useState<CityState>({ status: 'idle' });
  const [cityId, setCityId] = useState('');
  // La dernière demande de villes : une réponse plus ancienne n'est jamais posée.
  const latest = useRef(0);

  const choices = couriers.filter((courier) => courier.isActive && courier.id !== currentCourierId);
  const count = parcelIds.length;

  const loadCities = async (next: string) => {
    const ticket = ++latest.current;
    setCities({ status: 'loading' });
    setCityId('');
    const result = await transferOptionsAction({ parcelIds, courierId: next });
    if (ticket !== latest.current) return;

    if (!result.ok) {
      setCities({ status: 'error' });
      return;
    }
    const options = result.data;
    setCities({ status: 'ready', options });
    setCityId(
      options.keepCityId ?? (options.cities.length === 1 ? (options.cities[0]?.id ?? '') : ''),
    );
  };

  const pickCourier = (next: string) => {
    setCourierId(next);
    if (next) {
      void loadCities(next);
    } else {
      latest.current += 1;
      setCities({ status: 'idle' });
      setCityId('');
    }
  };

  const options = cities.status === 'ready' ? cities.options : null;
  // Lot N2 (D-224) : sans ville cochée, B ne reçoit rien — même pour la ville de sa fiche.
  const blocked = options !== null && options.noCity;
  // En masse, le choix ne sert qu'aux colis hors des villes de B.
  const cityUsed = options !== null && (count === 1 || options.toReroute > 0);
  const cityLocked = options !== null && (options.cities.length === 1 || !cityUsed);
  const ready = options !== null && !blocked && (!cityUsed || cityId !== '');
  const chosenName = options?.cities.find((city) => city.id === cityId)?.name ?? '';
  const changes =
    options !== null &&
    count === 1 &&
    cityId !== '' &&
    (options.toReroute > 0 || cityId !== options.keepCityId);

  const tJobs = useTranslations('bulkJobs');

  const submit = async () => {
    if (!courierId || !ready) return;
    setSaving(true);
    const result = await transferParcelsAction({
      parcelIds,
      courierId,
      reason,
      ...(cityUsed && cityId ? { destinationCityId: cityId } : {}),
    });
    setSaving(false);

    if (result.ok) {
      const data = result.data;
      if (isBulkJobAccepted(data)) {
        // Lot W2 (D-239) : plus de 300 colis — le transfert se fait par paquets ; la page le suit.
        toast.info(tJobs('started', { count: String(data.job.total) }));
        pickCourier('');
        setReason('');
        onDone();
        return;
      }
      const done =
        data.count > 0
          ? t('done', { count: data.count, route: data.routeCode ?? '' })
          : t('nothing');
      // « 3 colis transférés sur CIR-…, 2 changent de destination vers Tanger ».
      toast.success(
        data.rerouted && data.reroutedCityName
          ? `${done}, ${t('rerouted', { rerouted: data.rerouted, city: data.reroutedCityName })}`
          : done,
      );
      if (data.skipped > 0) toast.info(t('skipped', { count: data.skipped }));
      pickCourier('');
      setReason('');
      onDone();
      return;
    }
    toast.error(translateError(result));
    // Les villes de B ont pu changer entre-temps : la boîte les relit.
    if (
      result.reason === 'transfer-city-required' ||
      result.reason === 'transfer-city-not-served'
    ) {
      void loadCities(courierId);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(next) => (saving ? undefined : onOpenChange(next))}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t('title', { count })}</DialogTitle>
          <DialogDescription>{t('hint')}</DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="transfer-courier">{t('courier')}</Label>
            <SearchableSelect
              options={choices.map((courier) => ({
                value: courier.id,
                label: `${courier.firstName} ${courier.lastName}`,
              }))}
              value={courierId}
              onValueChange={pickCourier}
              placeholder={t('pick')}
              disabled={saving}
              id="transfer-courier"
            />
            {choices.length === 0 ? (
              <p className="text-muted-foreground text-xs">{t('noCourier')}</p>
            ) : null}
          </div>

          {courierId ? (
            <div className="flex flex-col gap-2">
              <Label htmlFor="transfer-city">{t('city')}</Label>
              {cities.status === 'loading' || cities.status === 'idle' ? (
                <p className="text-muted-foreground flex items-center gap-2 text-sm" role="status">
                  <Loader2 className="size-4 animate-spin" aria-hidden />
                  {t('loadingCities')}
                </p>
              ) : null}

              {cities.status === 'error' ? (
                <div
                  role="alert"
                  className="border-destructive/40 bg-destructive/10 flex flex-wrap items-center gap-2 rounded-lg border p-3 text-sm"
                >
                  <span className="flex-1">{t('loadError')}</span>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    className="gap-1.5"
                    onClick={() => void loadCities(courierId)}
                  >
                    <RotateCcw className="size-4" aria-hidden />
                    {t('retry')}
                  </Button>
                </div>
              ) : null}

              {blocked ? (
                <div
                  role="alert"
                  className="border-warning/40 bg-warning/10 flex items-start gap-2 rounded-lg border p-3 text-sm"
                >
                  <TriangleAlert className="text-warning mt-0.5 size-4 shrink-0" aria-hidden />
                  <span>{t('noCity')}</span>
                </div>
              ) : null}

              {options && !blocked ? (
                <>
                  <SearchableSelect
                    options={options.cities.map((city) => ({
                      value: city.id,
                      label:
                        city.id === options.keepCityId
                          ? t('currentCity', { city: city.name })
                          : city.name,
                    }))}
                    value={cityId}
                    onValueChange={setCityId}
                    placeholder={t('pickCity')}
                    disabled={saving || cityLocked}
                    id="transfer-city"
                  />
                  {count === 1 && changes && chosenName ? (
                    <p className="text-sm">{t('willReroute', { city: chosenName })}</p>
                  ) : null}
                  {count > 1 ? (
                    <p className="text-sm">
                      {options.toReroute > 0
                        ? t('cityRequired', { count: options.toReroute })
                        : t('keepAll', { count })}
                    </p>
                  ) : null}
                  {count > 1 ? (
                    <p className="text-muted-foreground text-xs">{t('massRule')}</p>
                  ) : null}
                </>
              ) : null}
            </div>
          ) : null}

          <div className="flex flex-col gap-2">
            <Label htmlFor="transfer-reason">{t('reason')}</Label>
            <Textarea
              id="transfer-reason"
              rows={2}
              maxLength={255}
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
          </div>
        </div>

        <DialogFooter className="gap-2 sm:gap-2">
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            {t('cancel')}
          </Button>
          <Button onClick={submit} disabled={saving || !courierId || !ready} className="gap-1.5">
            {saving ? <Loader2 className="size-4 animate-spin" /> : null}
            {t('confirm', { count: String(count) })}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Le bouton de la fiche colis : un colis, la même boîte. */
export function ParcelTransferButton({
  parcelId,
  couriers,
  currentCourierId,
  offRoute = false,
}: {
  parcelId: string;
  couriers: Courier[];
  currentCourierId: string | null;
  /**
   * D-246 : le colis est « En cours de livraison » sur AUCUN circuit. Le bouton
   * dit « Transférer à un livreur », et son livreur actuel reste proposé : il
   * peut le reprendre sur son propre circuit.
   */
  offRoute?: boolean;
}) {
  const t = useTranslations('routes.transfer');
  const router = useRouter();
  const [open, setOpen] = useState(false);

  return (
    <>
      <Button variant="outline" size="sm" className="gap-1.5" onClick={() => setOpen(true)}>
        <ArrowRightLeft className="size-4" aria-hidden />
        <span>{t(offRoute ? 'toCourier' : 'single')}</span>
      </Button>
      {open ? (
        <ParcelTransferDialog
          parcelIds={[parcelId]}
          couriers={couriers}
          currentCourierId={offRoute ? null : currentCourierId}
          open
          onOpenChange={setOpen}
          onDone={() => {
            setOpen(false);
            router.refresh();
          }}
        />
      ) : null}
    </>
  );
}
