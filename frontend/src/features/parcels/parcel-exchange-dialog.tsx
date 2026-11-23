'use client';

import type { Agency, City, ParcelDetail } from '@delivery/shared';
import { ArrowLeftRight, Loader2 } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useMemo, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { SearchableSelect } from '@/components/searchable-select';
import { cn } from '@/lib/utils';
import { parcelExchangeAction } from '@/lib/actions/exchanges';
import { useRouter } from '@/i18n/navigation';

/**
 * ÉCHANGER DEPUIS LE COLIS LIVRÉ — chantier « Étiquette orange », point 2.
 *
 * Le geste de Wimo, et il se fait DEVANT UNE LIVRAISON : le client rappelle, il
 * veut un autre article. Deux décisions, et rien d'autre à saisir —
 * l'expéditeur, le destinataire et l'adresse se recopient du colis d'origine.
 *
 *  1. **Ce qui revient.** Le vendeur le récupère dans une de nos agences — il
 *     dit laquelle — ou chez lui. Les frais de retour s'affichent AVANT le clic :
 *     c'est lui qui les paie, et une facture qui surprend est une réclamation.
 *  2. **Ce qui repart.** Le livreur va chercher le neuf à l'adresse
 *     d'enlèvement habituelle, ou à l'adresse de retour qu'on vient de choisir
 *     — le cas du vendeur qui prépare l'échange à l'agence pendant qu'il y est.
 *
 * **Le bouton n'existe QUE sur un colis livré**, et l'API rend 422 sinon : on
 * n'échange pas ce qui n'est pas arrivé.
 */
export function ParcelExchangeDialog({
  parcel,
  agencies,
  cities,
  returnFee,
}: {
  parcel: ParcelDetail;
  /** Les agences de retrait, pour « Récupérer dans l'une de nos agences ». */
  agencies: Agency[];
  /** Les villes servies : elles nomment l'agence dans la liste. */
  cities: City[];
  /**
   * Les frais de retour, lus dans les réglages métier par le serveur (D-019).
   *
   * Affichés, jamais recalculés ici : l'API les gèle sur le colis à la
   * création, et deux calculs auraient fini par ne plus dire le même chiffre.
   */
  returnFee: number;
}) {
  const t = useTranslations('parcels.exchange');
  const tCommon = useTranslations('common');
  const format = useFormatter();
  const router = useRouter();

  const [open, setOpen] = useState(false);
  const [pending, startTransition] = useTransition();

  const [returnTarget, setReturnTarget] = useState<'agency' | 'home'>('agency');
  const [returnAgencyId, setReturnAgencyId] = useState('');
  const [exchangePickup, setExchangePickup] = useState<'initial' | 'return-address'>('initial');
  const [price, setPrice] = useState('');
  const [alreadyPaid, setAlreadyPaid] = useState(false);
  const [newProduct, setNewProduct] = useState('');
  const [note, setNote] = useState('');

  const cityOf = useMemo(() => {
    const byId = new Map(cities.map((city) => [city.id, city.name]));
    return (id: string) => byId.get(id) ?? '';
  }, [cities]);

  const money = (cents: number) => format.number(cents / 100, 'currency');

  const submit = () => {
    startTransition(async () => {
      const result = await parcelExchangeAction(parcel.code, {
        returnTarget,
        returnAgencyId: returnTarget === 'agency' ? returnAgencyId : undefined,
        exchangePickup,
        // Le prix est saisi en unités, il voyage en CENTIMES : aucun flottant
        // ne traverse le calcul (CLAUDE.md, conventions).
        price: alreadyPaid ? 0 : Math.round(Number(price.replace(',', '.') || 0) * 100),
        alreadyPaid,
        newProduct: newProduct.trim() || undefined,
        note: note.trim() || undefined,
      });

      if (!result.ok) {
        toast.error(result.error);
        return;
      }

      toast.success(t('done'));
      setOpen(false);
      router.refresh();
    });
  };

  const incomplete = returnTarget === 'agency' && returnAgencyId === '';

  return (
    <>
      <Button variant="outline" size="sm" className="gap-1.5" onClick={() => setOpen(true)}>
        <ArrowLeftRight className="flip-rtl size-4" aria-hidden />
        <span className="hidden sm:inline">{t('action')}</span>
      </Button>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <ArrowLeftRight className="flip-rtl size-4" aria-hidden />
              {t('title')}
            </DialogTitle>
            <DialogDescription>{t('description', { code: parcel.code })}</DialogDescription>
          </DialogHeader>

          <div className="flex flex-col gap-5">
            {/* ------------------------------------------- le colis de retour */}
            <section className="flex flex-col gap-3">
              <h3 className="text-sm font-semibold">{t('return.title')}</h3>

              <fieldset className="flex flex-col gap-2">
                <legend className="sr-only">{t('return.title')}</legend>
                <Choice
                  selected={returnTarget === 'agency'}
                  label={t('return.atAgency')}
                  onSelect={() => setReturnTarget('agency')}
                />
                <Choice
                  selected={returnTarget === 'home'}
                  label={t('return.atHome')}
                  onSelect={() => setReturnTarget('home')}
                />
              </fieldset>

              {returnTarget === 'agency' ? (
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="exchange-agency">{t('return.agency')}</Label>
                  <SearchableSelect
                    options={agencies.map((agency) => ({
                      value: agency.id,
                      // La VILLE avec l'agence : la mission le demande, et
                      // une société qui a trois agences à Casablanca ne se
                      // choisit pas au nom seul.
                      label: cityOf(agency.cityId)
                        ? `${cityOf(agency.cityId)} — ${agency.name}`
                        : agency.name,
                    }))}
                    value={returnAgencyId}
                    onValueChange={setReturnAgencyId}
                    placeholder={t('return.agencyPlaceholder')}
                    id="exchange-agency"
                    className="w-full"
                  />
                </div>
              ) : null}

              <p className="text-muted-foreground text-xs">
                {t('return.fee', { amount: money(returnFee) })}
              </p>
            </section>

            {/* ------------------------------------------ le colis d'échange */}
            <section className="flex flex-col gap-3 border-t pt-4">
              <h3 className="text-sm font-semibold">{t('exchange.title')}</h3>

              <fieldset className="flex flex-col gap-2">
                <legend className="sr-only">{t('exchange.title')}</legend>
                <Choice
                  selected={exchangePickup === 'initial'}
                  label={t('exchange.fromInitial')}
                  onSelect={() => setExchangePickup('initial')}
                />
                <Choice
                  selected={exchangePickup === 'return-address'}
                  label={t('exchange.fromReturn')}
                  onSelect={() => setExchangePickup('return-address')}
                />
              </fieldset>

              <div className="flex flex-col gap-1.5">
                <Label htmlFor="exchange-product">{t('exchange.product')}</Label>
                <Input
                  id="exchange-product"
                  value={newProduct}
                  placeholder={parcel.description}
                  onChange={(event) => setNewProduct(event.target.value)}
                />
              </div>

              <div className="flex flex-col gap-1.5">
                <Label htmlFor="exchange-price">{t('exchange.price')}</Label>
                <Input
                  id="exchange-price"
                  inputMode="decimal"
                  value={alreadyPaid ? '' : price}
                  placeholder="0"
                  disabled={alreadyPaid}
                  aria-describedby="exchange-price-hint"
                  onChange={(event) => setPrice(event.target.value)}
                />
                {/*
                  LE PRIX EST LE COD DE L'ECH- (lot C, C4, D-178) : le montant que
                  le livreur encaisse, lu partout. « Déjà payé » le met à 0 — le
                  serveur refuse les deux ensemble.
                */}
                <p id="exchange-price-hint" className="text-muted-foreground text-xs">
                  {alreadyPaid ? t('exchange.paidHint') : t('exchange.priceHint')}
                </p>
              </div>

              <Label className="flex items-center gap-3 font-normal">
                <Checkbox
                  checked={alreadyPaid}
                  onCheckedChange={(value) => {
                    setAlreadyPaid(value === true);
                    if (value === true) setPrice('');
                  }}
                />
                {t('exchange.alreadyPaid')}
              </Label>

              {/* Les frais sont toujours inclus — dernières corrections, point 3. */}
              <p className="text-muted-foreground text-xs">
                {t('exchange.fee', { amount: money(parcel.deliveryFee) })}
              </p>
            </section>

            <div className="flex flex-col gap-1.5 border-t pt-4">
              <Label htmlFor="exchange-note">{t('note')}</Label>
              <Textarea
                id="exchange-note"
                rows={2}
                value={note}
                onChange={(event) => setNote(event.target.value)}
              />
            </div>
          </div>

          <DialogFooter className="gap-2 sm:gap-2">
            <Button variant="ghost" onClick={() => setOpen(false)} disabled={pending}>
              {tCommon('cancel')}
            </Button>
            <Button onClick={submit} disabled={pending || incomplete} className="gap-1.5">
              {pending ? <Loader2 className="size-4 animate-spin" /> : null}
              {t('confirm')}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

/**
 * UN CHOIX EXCLUSIF, comme le sélecteur de format de `print-button.tsx`.
 *
 * Le socle n'a pas de `RadioGroup` shadcn, et ce n'est pas le moment d'en
 * ajouter un : l'application exprime déjà ce motif avec `role="radio"` sur un
 * bouton, et deux façons de poser la même question se mettraient à diverger —
 * au clavier d'abord, là où personne ne regarde.
 */
function Choice({
  selected,
  label,
  onSelect,
}: {
  selected: boolean;
  label: string;
  onSelect: () => void;
}) {
  return (
    <button
      type="button"
      role="radio"
      aria-checked={selected}
      onClick={onSelect}
      className={cn(
        'rounded-lg border p-3 text-start text-sm transition-colors',
        'focus-visible:ring-ring outline-none focus-visible:ring-2',
        selected ? 'border-primary bg-accent' : 'hover:bg-muted/60',
      )}
    >
      {label}
    </button>
  );
}
