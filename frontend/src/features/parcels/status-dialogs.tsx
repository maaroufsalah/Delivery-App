'use client';

import {
  allowedTransitions,
  changeStatusSchema,
  type ChangeStatusInput,
  type Courier,
  disputeStatusSchema,
  type DisputeStatusInput,
  disputeTransitions,
  findParcelStatus,
  isLockedStatus,
  type ParcelTransferOptions,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2, ShieldAlert } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useRef, useState, useTransition } from 'react';
import { useForm, useWatch } from 'react-hook-form';
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
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { SearchableSelect } from '@/components/searchable-select';
import { ParcelStatusBadge } from '@/features/parcels/status-badge';
import { StatusCorrectionDialog } from '@/features/parcels/status-correction-dialog';
import { changeParcelStatusAction, disputeParcelStatusAction } from '@/lib/actions/parcels';
import { transferOptionsAction } from '@/lib/actions/routes';

/** `13 En cours de livraison` : le seul statut qui demande un livreur (D-246). */
const OUT_FOR_DELIVERY = 13;

/** Statuts qui demandent une date: report et programmation (DOMAIN.md §1.9 `date_report`). */
export const STATUSES_WITH_DATE = [14, 38];

/**
 * Changement de statut — la liste proposée est CELLE des transitions autorisées
 * depuis le statut courant (`transitions.ts`), pas le référentiel complet.
 *
 * C'est l'inverse du legacy, où `traking_form` offrait tous les statuts sauf 1
 * et 2 et n'en vérifiait aucun (DOMAIN.md §2.3).
 */
export function ChangeStatusDialog({
  code,
  currentStatusId,
  open,
  onOpenChange,
  couriers = null,
  parcelId,
  onCorrect,
}: {
  /** D-246 : l'identifiant du colis, pour lire les villes du livreur choisi. */
  parcelId?: string;
  code: string;
  currentStatusId: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** D-246 : les livreurs proposés quand le statut choisi est « En cours de livraison ». */
  couriers?: Courier[] | null;
  /**
   * Ouvre « Corriger le statut » (D-250). Passé SEULEMENT pour un admin qui en
   * a le droit : sans lui, ni lien ni bouton, la fenêtre est celle d'avant.
   */
  onCorrect?: () => void;
}) {
  const t = useTranslations('parcels.detail.changeStatus');
  const tStatus = useTranslations('status');
  const tErrors = useTranslations('parcels.errors');
  const [pending, startTransition] = useTransition();
  // Le dernier refus du serveur, gardé dans la fenêtre pour lui offrir la correction.
  const [refusal, setRefusal] = useState<string | null>(null);

  // La machine à états ne suffit pas : on quitte cette fenêtre pour la correction.
  const toCorrection = onCorrect
    ? () => {
        setRefusal(null);
        onOpenChange(false);
        onCorrect();
      }
    : null;

  const targets = allowedTransitions(currentStatusId);

  const form = useForm<ChangeStatusInput>({
    resolver: zodResolver(changeStatusSchema),
    defaultValues: { statusId: undefined, comment: '', scheduledDate: '', courierId: '' },
  });

  // `useWatch` et non `form.watch()` : ce dernier renvoie une valeur que le
  // compilateur React ne sait pas mémoriser, et il le signale.
  const selected = useWatch({ control: form.control, name: 'statusId' });
  const courierId = useWatch({ control: form.control, name: 'courierId' });
  const cityId = useWatch({ control: form.control, name: 'destinationCityId' });
  // Faux tant que le livreur choisi demande une ville qui n'est pas encore posée.
  const [cityReady, setCityReady] = useState(true);
  const needsDate = STATUSES_WITH_DATE.includes(Number(selected));
  const needsCourier = Number(selected) === OUT_FOR_DELIVERY;

  function onSubmit(values: ChangeStatusInput) {
    startTransition(async () => {
      const result = await changeParcelStatusAction(code, {
        ...values,
        courierId: needsCourier ? values.courierId : undefined,
        destinationCityId:
          needsCourier && values.destinationCityId ? values.destinationCityId : undefined,
      });
      if (result.ok) {
        toast.success(t('done'));
        form.reset({ statusId: undefined, comment: '', scheduledDate: '', courierId: '' });
        setRefusal(null);
        onOpenChange(false);
      } else {
        const message = statusRefusal(result, tErrors, 'forbidden-transition');
        if (toCorrection) {
          // L'admin n'est pas laissé bloqué : le refus mène à la correction.
          setRefusal(message);
          toast.error(message, { action: { label: t('correct'), onClick: toCorrection } });
        } else {
          toast.error(message);
        }
      }
    });
  }

  // Le lien sous la liste : « Le statut voulu n'est pas dans la liste ? Corriger le statut ».
  const correctionLink = toCorrection ? (
    <p className="text-muted-foreground text-sm">
      {t('notInList')}{' '}
      <button
        type="button"
        onClick={toCorrection}
        className="text-primary font-medium underline-offset-4 hover:underline"
      >
        {t('correct')}
      </button>
    </p>
  ) : null;

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) setRefusal(null);
        onOpenChange(next);
      }}
    >
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t('title')}</DialogTitle>
          <DialogDescription>{t('description')}</DialogDescription>
        </DialogHeader>

        <div className="flex items-center gap-2 text-sm">
          <span className="text-muted-foreground">{t('current')}</span>
          <ParcelStatusBadge statusId={currentStatusId} />
        </div>

        {refusal && toCorrection ? (
          <div
            role="alert"
            className="border-destructive/40 bg-destructive/5 flex flex-col gap-2 rounded-md border p-3 text-sm"
          >
            <p className="text-destructive">{refusal}</p>
            <p className="text-muted-foreground">{t('refusedHint')}</p>
            <Button
              type="button"
              size="sm"
              variant="outline"
              className="self-start"
              onClick={toCorrection}
            >
              {t('correct')}
            </Button>
          </div>
        ) : null}

        {targets.length === 0 ? (
          <>
            <p className="text-muted-foreground text-sm">{t('noTransition')}</p>
            {correctionLink}
          </>
        ) : (
          <Form {...form}>
            <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-4">
              <FormField
                control={form.control}
                name="statusId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('newStatus')}</FormLabel>
                    <FormControl>
                      <SearchableSelect
                        options={targets.map((id) => {
                          const status = findParcelStatus(id);
                          return {
                            value: String(id),
                            label: status ? tStatus(status.key) : `#${id}`,
                            color: status?.color,
                          };
                        })}
                        value={field.value ? String(field.value) : ''}
                        onValueChange={(value) => field.onChange(Number(value))}
                        placeholder={t('pick')}
                        className="w-full"
                      />
                    </FormControl>
                    <FormDescription>{t('allowedOnly')}</FormDescription>
                    <FormMessage />
                    {correctionLink}
                  </FormItem>
                )}
              />

              {needsDate ? (
                <FormField
                  control={form.control}
                  name="scheduledDate"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('scheduledDate')}</FormLabel>
                      <FormControl>
                        <Input type="date" {...field} value={field.value ?? ''} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              ) : null}

              {needsCourier ? (
                <CourierPicker
                  couriers={couriers}
                  parcelId={parcelId}
                  value={courierId ?? ''}
                  cityId={cityId ?? ''}
                  onChange={(next) => form.setValue('courierId', next)}
                  onCityChange={(next) => form.setValue('destinationCityId', next)}
                  onReadyChange={setCityReady}
                  disabled={pending}
                />
              ) : null}

              <FormField
                control={form.control}
                name="comment"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('comment')}</FormLabel>
                    <FormControl>
                      <Textarea rows={3} {...field} value={field.value ?? ''} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <DialogFooter className="gap-2 sm:gap-2">
                <Button
                  type="button"
                  variant="ghost"
                  onClick={() => onOpenChange(false)}
                  disabled={pending}
                >
                  {t('cancel')}
                </Button>
                <Button
                  type="submit"
                  className="gap-1.5"
                  disabled={pending || (needsCourier && (!courierId || !cityReady))}
                >
                  {pending ? <Loader2 className="size-4 animate-spin" /> : null}
                  {t('confirm')}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        )}
      </DialogContent>
    </Dialog>
  );
}

/**
 * Contestation d'un statut terminal.
 *
 * DOMAIN.md §2.2 relève 39 transitions `4 → 6` en production : le legacy
 * laissait sortir d'un état terminal sans trace ni motif, ce qui rendait la
 * facturation non déterministe. Ici la sortie existe toujours — le métier en a
 * besoin — mais elle est nommée, réservée au staff, et exige un motif.
 */
export function DisputeDialog({
  code,
  currentStatusId,
  open,
  onOpenChange,
  couriers = null,
  parcelId,
}: {
  /** D-246 : l'identifiant du colis, pour lire les villes du livreur choisi. */
  parcelId?: string;
  code: string;
  currentStatusId: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** D-246 : les livreurs proposés quand la cible est « En cours de livraison ». */
  couriers?: Courier[] | null;
}) {
  const t = useTranslations('parcels.detail.dispute');
  const tStatus = useTranslations('status');
  const tErrors = useTranslations('parcels.errors');
  const [pending, startTransition] = useTransition();

  const targets = disputeTransitions(currentStatusId);

  const form = useForm<DisputeStatusInput>({
    resolver: zodResolver(disputeStatusSchema),
    defaultValues: { statusId: undefined, comment: '', courierId: '' },
  });

  const selected = useWatch({ control: form.control, name: 'statusId' });
  const courierId = useWatch({ control: form.control, name: 'courierId' });
  const cityId = useWatch({ control: form.control, name: 'destinationCityId' });
  // Faux tant que le livreur choisi demande une ville qui n'est pas encore posée.
  const [cityReady, setCityReady] = useState(true);
  const needsCourier = Number(selected) === OUT_FOR_DELIVERY;

  function onSubmit(values: DisputeStatusInput) {
    startTransition(async () => {
      const result = await disputeParcelStatusAction(code, {
        ...values,
        courierId: needsCourier ? values.courierId : undefined,
        destinationCityId:
          needsCourier && values.destinationCityId ? values.destinationCityId : undefined,
      });
      if (result.ok) {
        toast.success(t('done'));
        form.reset({ statusId: undefined, comment: '', courierId: '' });
        onOpenChange(false);
      } else {
        toast.error(statusRefusal(result, tErrors, 'unexpected'));
      }
    });
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <ShieldAlert className="text-destructive size-4" aria-hidden />
            {t('title')}
          </DialogTitle>
          <DialogDescription>{t('description')}</DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-4">
            <FormField
              control={form.control}
              name="statusId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('newStatus')}</FormLabel>
                  <Select
                    value={field.value ? String(field.value) : undefined}
                    onValueChange={(value) => field.onChange(Number(value))}
                  >
                    <FormControl>
                      <SelectTrigger className="w-full">
                        <SelectValue placeholder={t('pick')} />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {targets.map((id) => {
                        const status = findParcelStatus(id);
                        return (
                          <SelectItem key={id} value={String(id)}>
                            <span className="flex items-center gap-2">
                              <span
                                className="size-2 shrink-0 rounded-full"
                                style={{ backgroundColor: status?.color }}
                              />
                              {status ? tStatus(status.key) : `#${id}`}
                            </span>
                          </SelectItem>
                        );
                      })}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />

            {needsCourier ? (
              <CourierPicker
                couriers={couriers}
                parcelId={parcelId}
                value={courierId ?? ''}
                cityId={cityId ?? ''}
                onChange={(next) => form.setValue('courierId', next)}
                onCityChange={(next) => form.setValue('destinationCityId', next)}
                onReadyChange={setCityReady}
                disabled={pending}
              />
            ) : null}

            <FormField
              control={form.control}
              name="comment"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('reason')}</FormLabel>
                  <FormControl>
                    <Textarea rows={4} {...field} />
                  </FormControl>
                  <FormDescription>{t('reasonHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <DialogFooter className="gap-2 sm:gap-2">
              <Button
                type="button"
                variant="ghost"
                onClick={() => onOpenChange(false)}
                disabled={pending}
              >
                {t('cancel')}
              </Button>
              <Button
                type="submit"
                variant="destructive"
                className="gap-1.5"
                disabled={pending || (needsCourier && (!courierId || !cityReady))}
              >
                {pending ? <Loader2 className="size-4 animate-spin" /> : null}
                {t('confirm')}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}

/**
 * « EN COURS DE LIVRAISON » DEMANDE UN LIVREUR — D-246.
 *
 * Le serveur est seul juge : sans livreur il refuse (`courier-required`), et
 * c'est lui qui range le colis sur le circuit de livraison du livreur. Ce
 * champ ne fait que poser la question au bon moment.
 */
/** Les villes du livreur choisi, telles que le serveur les rend (`transferOptionsAction`). */
type CourierCities =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; options: ParcelTransferOptions };

/**
 * LE LIVREUR NE COUVRE PAS LA VILLE DU COLIS : la destination suit (alignement
 * du 12/11 sur le transfert, D-219). Une seule ville : pré-remplie. Plusieurs :
 * l'admin choisit. Il couvre déjà la ville : rien n'est demandé. Les villes
 * viennent du serveur, par le calcul que la boîte « Transférer » lit aussi ;
 * si elles ne se chargent pas, le geste part quand même et le serveur répond.
 */
function CourierPicker({
  couriers,
  parcelId,
  value,
  cityId,
  onChange,
  onCityChange,
  onReadyChange,
  disabled,
}: {
  couriers: Courier[] | null;
  /** Absent : aucune ville n'est proposée, le serveur reste seul juge. */
  parcelId?: string;
  value: string;
  cityId: string;
  onChange: (next: string) => void;
  onCityChange: (next: string) => void;
  /** Faux tant qu'une ville obligatoire n'est pas choisie, ou que le livreur n'en a aucune. */
  onReadyChange: (ready: boolean) => void;
  disabled: boolean;
}) {
  const t = useTranslations('parcels.detail.outForDelivery');
  const tTransfer = useTranslations('routes.transfer');
  const choices = (couriers ?? []).filter((courier) => courier.isActive);
  const [cities, setCities] = useState<CourierCities>({ status: 'idle' });
  // La dernière demande : la réponse d'un livreur qui n'est plus le choisi est ignorée.
  const latest = useRef(0);

  const pick = async (next: string) => {
    onChange(next);
    onCityChange('');
    const ticket = ++latest.current;
    if (!next || !parcelId) {
      setCities({ status: 'idle' });
      onReadyChange(true);
      return;
    }
    setCities({ status: 'loading' });
    onReadyChange(false);
    const result = await transferOptionsAction({ parcelIds: [parcelId], courierId: next });
    if (ticket !== latest.current) return;
    if (!result.ok) {
      setCities({ status: 'error' });
      onReadyChange(true);
      return;
    }
    const options = result.data;
    setCities({ status: 'ready', options });
    if (options.noCity) {
      onReadyChange(false);
      return;
    }
    if (options.toReroute === 0) {
      onReadyChange(true);
      return;
    }
    const only = options.cities.length === 1 ? (options.cities[0]?.id ?? '') : '';
    onCityChange(only);
    onReadyChange(only !== '');
  };

  const options = cities.status === 'ready' ? cities.options : null;
  const asksCity = options !== null && !options.noCity && options.toReroute > 0;
  const chosenName = options?.cities.find((city) => city.id === cityId)?.name ?? '';

  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor="status-courier">{t('courier')}</Label>
      <SearchableSelect
        id="status-courier"
        options={choices.map((courier) => ({
          value: courier.id,
          label: `${courier.firstName} ${courier.lastName}`,
        }))}
        value={value}
        onValueChange={(next) => void pick(next)}
        placeholder={t('pick')}
        disabled={disabled}
        className="w-full"
      />
      <p className="text-muted-foreground text-xs">
        {choices.length === 0 ? t('noCourier') : t('hint')}
      </p>

      {cities.status === 'loading' ? (
        <p className="text-muted-foreground text-xs">{tTransfer('loadingCities')}</p>
      ) : null}
      {options?.noCity ? (
        <p className="text-destructive text-sm" role="alert">
          {tTransfer('noCity')}
        </p>
      ) : null}
      {asksCity ? (
        <div className="flex flex-col gap-2">
          <Label htmlFor="status-courier-city">{tTransfer('city')}</Label>
          <SearchableSelect
            id="status-courier-city"
            options={(options?.cities ?? []).map((city) => ({ value: city.id, label: city.name }))}
            value={cityId}
            onValueChange={(next) => {
              onCityChange(next);
              onReadyChange(next !== '');
            }}
            placeholder={tTransfer('pickCity')}
            disabled={disabled || (options?.cities.length ?? 0) === 1}
            className="w-full"
          />
          <p className="text-muted-foreground text-xs">
            {cityId ? tTransfer('willReroute', { city: chosenName }) : t('cityRequired')}
          </p>
        </div>
      ) : null}
    </div>
  );
}

/** Le refus du serveur, dit tel quel quand il porte un motif ; sinon le message générique. */
function statusRefusal(
  result: { error: string; reason?: string },
  tErrors: (key: 'invalid' | 'forbidden-transition' | 'unexpected') => string,
  fallback: 'forbidden-transition' | 'unexpected',
): string {
  if (result.error === 'invalid') return tErrors('invalid');
  return result.reason ? result.error : tErrors(fallback);
}

/** Bouton + dialogue, assemblés : le détail comme la ligne de liste s'en servent. */
export function StatusActionButtons({
  code,
  currentStatusId,
  variant = 'default',
  couriers = null,
  parcelId,
  canCorrectStatus = false,
}: {
  /** D-246 : l'identifiant du colis, pour lire les villes du livreur choisi. */
  parcelId?: string;
  code: string;
  currentStatusId: number;
  variant?: 'default' | 'outline';
  /** D-246 : lus par la page (D-018), pour « En cours de livraison ». */
  couriers?: Courier[] | null;
  /**
   * « Corriger le statut » (D-250) proposé depuis « Changer le statut » :
   * SUPER_ADMIN avec `parcels.correctStatus`, comme le bouton de la fiche.
   * Faux pour tout autre compte : rien ne change pour lui.
   */
  canCorrectStatus?: boolean;
}) {
  const t = useTranslations('parcels.detail');
  const [changeOpen, setChangeOpen] = useState(false);
  const [disputeOpen, setDisputeOpen] = useState(false);
  const [correctionOpen, setCorrectionOpen] = useState(false);
  const locked = isLockedStatus(currentStatusId);

  return (
    <>
      {locked ? (
        <Button
          variant="outline"
          size="sm"
          onClick={() => setDisputeOpen(true)}
          className="gap-1.5"
        >
          <ShieldAlert className="size-4" aria-hidden />
          {t('dispute.action')}
        </Button>
      ) : (
        <Button variant={variant} size="sm" onClick={() => setChangeOpen(true)}>
          {t('changeStatus.action')}
        </Button>
      )}

      <ChangeStatusDialog
        code={code}
        currentStatusId={currentStatusId}
        open={changeOpen}
        onOpenChange={setChangeOpen}
        couriers={couriers}
        parcelId={parcelId}
        onCorrect={canCorrectStatus ? () => setCorrectionOpen(true) : undefined}
      />
      {/* Monté à l'ouverture, comme depuis le bouton de la fiche : un formulaire vide à chaque fois. */}
      {canCorrectStatus && correctionOpen ? (
        <StatusCorrectionDialog
          code={code}
          statusId={currentStatusId}
          onOpenChange={setCorrectionOpen}
        />
      ) : null}
      <DisputeDialog
        code={code}
        currentStatusId={currentStatusId}
        open={disputeOpen}
        onOpenChange={setDisputeOpen}
        couriers={couriers}
        parcelId={parcelId}
      />
    </>
  );
}
