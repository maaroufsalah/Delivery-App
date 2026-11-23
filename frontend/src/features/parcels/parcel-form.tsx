'use client';

import {
  type Agency,
  type City,
  type Client,
  createParcelSchema,
  type CreateParcelInput,
  type District,
  PARCEL_QUANTITY_MAX,
  PICKUP_ADDRESS_MIN,
  type Quote,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { ArrowLeft, ArrowRight, Check, Loader2, MapPin, Package, Store } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useCallback, useEffect, useState, useTransition } from 'react';
import { type FieldErrors, useForm, useWatch } from 'react-hook-form';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
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
import { Textarea } from '@/components/ui/textarea';
import { CitySelect } from '@/components/city-select';
import { SearchableSelect } from '@/components/searchable-select';
import { useRouter } from '@/i18n/navigation';
import { createParcelAction, previewParcelDraftAction } from '@/lib/actions/parcels';
import { ExpectedPaymentField } from '@/features/parcels/expected-payment-field';
import { DistrictInput } from './district-input';
import { cn } from '@/lib/utils';

/**
 * Création d'un colis — trois sections, un seul formulaire.
 *
 * Sur grand écran les trois sections sont empilées et toutes visibles ; sous
 * `lg` elles deviennent un stepper, parce qu'un formulaire de 18 champs sur un
 * téléphone se remplit par étapes ou ne se remplit pas.
 *
 * Le tarif est calculé en direct (DOMAIN.md §3.1) : l'agent voit le prix avant
 * de valider, ce que le legacy ne montrait qu'après création.
 */

const SECTIONS = [
  { key: 'sender', icon: Store, fields: ['clientId', 'agencyId', 'originCityId', 'pickupAddress'] },
  {
    key: 'recipient',
    icon: MapPin,
    fields: [
      'recipientName',
      'recipientPhone',
      'destinationCityId',
      'destinationDistrictName',
      'deliveryAddress',
    ],
  },
  { key: 'parcel', icon: Package, fields: ['description', 'codAmount', 'quantity'] },
] as const;

type SectionKey = (typeof SECTIONS)[number]['key'];
type FieldName = keyof CreateParcelInput;

export function ParcelForm({
  clients,
  cities,
  agencies,
  districts: allDistricts,
  selfClientId = null,
  sender = null,
}: {
  clients: Client[];
  cities: City[];
  /**
   * L'AGENCE DE PRISE EN CHARGE — retouches Anass, point 4.
   *
   * Vide pour un vendeur : elle n'a qu'une réponse possible, la sienne, et le
   * serveur la déduit de sa fiche. L'administration, elle, la voit préremplie
   * depuis le client choisi et peut en changer — un colis part parfois
   * d'ailleurs qu'à l'ordinaire.
   */
  agencies: Agency[];
  /**
   * SA FICHE, quand c'est un vendeur qui saisit — retouches Anass, point 9.
   *
   * « Ne plus afficher le choix "Ville de départ" par défaut. La ville et
   * l'adresse de ramassage sont celles de la fiche du vendeur. »
   *
   * Le formulaire posait la question à chaque colis, et la réponse était
   * toujours la même — sauf le jour où elle ne l'est pas, et c'est ce jour-là
   * qu'il faut pouvoir en changer. D'où la case « Ramasser depuis une autre
   * adresse » plutôt que le retrait pur et simple du champ.
   *
   * `null` pour l'administration : son formulaire ne change pas.
   */
  sender?: { cityId: string; cityName: string; address: string | null } | null;
  /** Tous les quartiers : le filtrage par ville est local, donc instantané. */
  districts: District[];
  /**
   * LE VENDEUR NE CHOISIT PAS SON PROPRE NOM — V2 vendeur, règle d'Anass.
   *
   * « Sur création de colis par vendeur, pas la peine de donner le choix de
   * vendeur : à supprimer pour le compte client, à laisser pour l'admin. »
   *
   * Quand cette propriété est posée, le champ disparaît et la valeur est
   * fixée. Ce n'est PAS une garantie — le serveur borne déjà la création à la
   * portée du compte (D-082) — c'est une question de moins à poser à quelqu'un
   * qui n'a qu'une réponse possible, sur un formulaire qui en compte dix-huit.
   */
  selfClientId?: string | null;
}) {
  const t = useTranslations('parcels.form');
  const tErrors = useTranslations('parcels.errors');
  const format = useFormatter();
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  const [step, setStep] = useState(0);

  /*
   * « RAMASSER DEPUIS UNE AUTRE ADRESSE » — retouches Anass, point 9.
   *
   * Décochée, la ville et l'adresse sont celles de la fiche, en lecture ;
   * cochée, les deux redeviennent modifiables et le vendeur choisit sa ville
   * dans celles que la société dessert. Le tarif se calcule dans les deux cas
   * sur la ville de départ EFFECTIVE — c'est `originCityId` qui le décide, et
   * il vaut toujours quelque chose.
   *
   * Elle démarre COCHÉE quand la fiche n'a pas d'adresse exploitable : sans
   * cela, le vendeur verrait un champ vide et en lecture seule, et un
   * formulaire qui refuse d'avancer sans dire où le corriger. Le seuil est
   * celui de `createParcelSchema` — `PICKUP_ADDRESS_MIN`, non vide (D-131) —
   * et pas un jugement sur la qualité de l'adresse.
   */
  const senderAddressUsable = (sender?.address ?? '').trim().length >= PICKUP_ADDRESS_MIN;
  const [pickupElsewhere, setPickupElsewhere] = useState(Boolean(sender) && !senderAddressUsable);

  const fromSenderRecord = Boolean(sender) && !pickupElsewhere;
  /**
   * Le devis est mémorisé AVEC la clé qui l'a produit : une réponse arrivée
   * après un changement de ville est simplement ignorée, sans avoir à remettre
   * l'état à zéro dans le corps de l'effet (ce qui déclencherait un rendu en
   * cascade, cf. DECISIONS.md D-011).
   */
  const [preview, setPreview] = useState<{ key: string; quote: Quote; code: string } | null>(null);

  const form = useForm<CreateParcelInput>({
    resolver: zodResolver(createParcelSchema),
    defaultValues: {
      clientId: selfClientId ?? '',
      agencyId: '',
      externalCode: '',
      description: '',
      recipientName: '',
      recipientPhone: '',
      originCityId: sender?.cityId ?? '',
      pickupAddress: sender?.address ?? '',
      pickupAtSender: true,
      destinationCityId: '',
      destinationDistrictName: '',
      deliveryTarget: 'district',
      deliveryAddress: '',
      codAmount: 0,
      feeIncluded: true,
      openOnDelivery: false,
      fragile: false,
      blRequested: false,
      quantity: 1,
      // Espèces par défaut (point 3) ; l'API ne l'enregistre que pour un Pro.
      expectedPayment: 'cash',
      /*
       * `alreadyPaid` et `scheduledAt` NE SONT PLUS SAISIS — C12 bloc 1.
       *
       * Les colonnes restent en base pour les colis déjà créés (D-032) et
       * `createParcelSchema` accepte toujours les deux valeurs : l'import Excel
       * et une éventuelle intégration peuvent encore les poser. Simplement,
       * l'écran de création ne les propose plus — le client ne s'en servait
       * pas, et deux champs de plus sur un formulaire de dix-huit se paient en
       * saisies fausses.
       *
       * Les laisser hors de `defaultValues` suffit : le schéma porte leurs
       * défauts (`false`, absent), et le formulaire n'a plus à les connaître.
       */
    },
    mode: 'onBlur',
  });

  /**
   * Décocher REMET les valeurs de la fiche.
   *
   * Sans cela, un vendeur qui coche la case, saisit une adresse, puis se ravise
   * partirait avec l'adresse saisie sous un libellé qui annonce celle de sa
   * fiche — et le tarif suivrait la mauvaise ville.
   */
  const togglePickupElsewhere = (checked: boolean) => {
    setPickupElsewhere(checked);
    if (checked || !sender) return;

    form.setValue('originCityId', sender.cityId, { shouldValidate: true });
    form.setValue('pickupAddress', sender.address ?? '', { shouldValidate: true });
  };

  const originCityId = useWatch({ control: form.control, name: 'originCityId' });
  const destinationCityId = useWatch({ control: form.control, name: 'destinationCityId' });
  const deliveryTarget = useWatch({ control: form.control, name: 'deliveryTarget' });
  const clientId = useWatch({ control: form.control, name: 'clientId' });
  const codAmount = useWatch({ control: form.control, name: 'codAmount' });
  const quantityValue = useWatch({ control: form.control, name: 'quantity' });

  /*
   * L'AGENCE SUIT LE CLIENT CHOISI — retouches Anass, point 4.
   *
   * « À la création d'un colis par l'admin, l'agence est préremplie depuis la
   * fiche du client sélectionné, changeable. » Elle ne l'était pas : le colis
   * se rattachait, côté serveur, à l'agence de sa VILLE DE DÉPART. Un client
   * de Casablanca qui expédie depuis Rabat voyait ses colis passer à Rabat,
   * loin de l'agence qui le facture.
   *
   * Le préremplissage ne s'impose pas : il laisse tranquille un champ que
   * quelqu'un a modifié à la main (`dirtyFields`). Vide, le serveur décide —
   * fiche du client, puis ville de départ, puis siège.
   */
  useEffect(() => {
    if (!clientId || form.formState.dirtyFields.agencyId) return;

    const agencyId = clients.find((client) => client.id === clientId)?.agencyId ?? '';
    if (agencyId && agencyId !== form.getValues('agencyId')) {
      form.setValue('agencyId', agencyId);
    }
  }, [clientId, clients, form]);

  const target = deliveryTarget ?? 'district';
  /*
   * LE VENDEUR CHOISI EST-IL PRO ? — point 5.
   *
   * L'annuaire léger porte `isPro` depuis le point 4, précisément pour éviter
   * un aller-retour par changement de sélecteur. Dans l'espace vendeur, la
   * liste ne contient qu'une fiche — la sienne — et la réponse est la même.
   */
  const selectedClientIsPro = clients.find((client) => client.id === clientId)?.isPro ?? false;

  /*
   * « COMMANDE EN PLUSIEURS COLIS » — lot G, D-208 ; pour tout client depuis
   * D-264 (16/11/2026), normal ou Pro.
   *
   * La case révèle la quantité (2 au minimum) ; décochée, le colis part en un
   * carton quoi que le champ contienne.
   */
  const [multiCarton, setMultiCarton] = useState(false);
  const cartonsActive = multiCarton;
  const quantity = cartonsActive ? Math.max(Math.trunc(Number(quantityValue)) || 1, 1) : 1;

  const toggleMultiCarton = (checked: boolean) => {
    setMultiCarton(checked);
    form.setValue('quantity', checked ? 2 : 1, { shouldValidate: true });
  };

  const previewKey = `${originCityId}|${destinationCityId}|${target}|${clientId ?? ''}`;
  const canQuote = Boolean(originCityId && destinationCityId);

  // Tarif et code en direct dès que le couple de villes est connu — DOMAIN.md §3.1.
  useEffect(() => {
    if (!canQuote) return;

    let active = true;
    void previewParcelDraftAction({
      originCityId,
      destinationCityId,
      deliveryTarget: target,
      clientId: clientId || undefined,
    }).then((result) => {
      if (!active || !result.ok) return;
      setPreview({ key: previewKey, quote: result.data.quote, code: result.data.code });
    });

    return () => {
      active = false;
    };
  }, [canQuote, previewKey, originCityId, destinationCityId, target, clientId]);

  const current = preview?.key === previewKey ? preview : null;
  const quote = current?.quote ?? null;
  const codePreview = current?.code ?? null;

  /*
   * LE QUARTIER n'est jamais exigé — chantier « Quartier en saisie libre ».
   * Texte libre, facultatif ; le serveur le rapproche du référentiel.
   */
  const goNext = useCallback(async () => {
    const section = SECTIONS[step];
    if (!section) return;
    const valid = await form.trigger(section.fields as unknown as FieldName[]);
    if (valid) setStep((current) => Math.min(current + 1, SECTIONS.length - 1));
  }, [form, step]);

  function onSubmit(values: CreateParcelInput) {
    startTransition(async () => {
      const result = await createParcelAction({
        ...values,
        quantity: cartonsActive ? quantity : 1,
      });
      if (result.ok) {
        toast.success(t('created', { code: result.data }));
        router.push(`/parcels/${result.data}`);
      } else if (result.reason === 'external-code-taken') {
        // La référence est prise dans la société (point 7 du 26/09) : l'erreur
        // se pose SUR le champ, et l'écran revient à l'étape qui le porte.
        form.setError('externalCode', { message: tErrors('external-code-taken') });
        const owner = SECTIONS.findIndex((section) =>
          (section.fields as readonly string[]).includes('externalCode'),
        );
        if (owner >= 0) setStep(owner);
        toast.error(tErrors('external-code-taken'));
      } else {
        toast.error(tErrors(result.error === 'invalid' ? 'invalid' : 'unexpected'));
      }
    });
  }

  /*
   * UN REFUS DE VALIDATION N'EST JAMAIS MUET — recette SpeedyTrans 4, point 1.
   *
   * Le vendeur cliquait « Créer le colis » et rien ne se passait : le champ
   * refusé (l'adresse recopiée de sa fiche) était MASQUÉ, et sur mobile sa
   * section aussi. Désormais : un toast, l'écran revient à la première section
   * fautive, et un champ de fiche refusé redevient visible et modifiable.
   */
  function onInvalid(errors: FieldErrors<CreateParcelInput>) {
    const invalid = Object.keys(errors);
    if (fromSenderRecord && (errors.pickupAddress || errors.originCityId)) setPickupElsewhere(true);

    const owner = SECTIONS.findIndex((section) =>
      (section.fields as readonly string[]).some((field) => invalid.includes(field)),
    );
    if (owner >= 0) {
      setStep(owner);
      document
        .getElementById(`section-${SECTIONS[owner]?.key}`)
        ?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
    toast.error(t('invalidSubmit'));
  }

  const codInDirhams = Number(codAmount) || 0;
  const unitFeeInDirhams = quote ? quote.fee / 100 : 0;
  // Frais × cartons (D-208) ; le prix à encaisser, lui, n'est jamais multiplié.
  const feeInDirhams = unitFeeInDirhams * quantity;
  // Les frais sont TOUJOURS inclus (point 3) : le livreur encaisse le prix.
  const total = codInDirhams;

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit, onInvalid)} className="flex flex-col gap-6">
        <Stepper step={step} onStep={setStep} />

        <div className="grid gap-6 lg:grid-cols-3">
          <div className="flex flex-col gap-6 lg:col-span-2">
            <Section
              index={0}
              step={step}
              sectionKey="sender"
              icon={Store}
              title={t('sender.title')}
              description={t('sender.description')}
            >
              {/* Caché pour un vendeur : il n'a qu'une réponse possible. */}
              <FormField
                control={form.control}
                name="clientId"
                render={({ field }) =>
                  selfClientId ? (
                    <></>
                  ) : (
                    <FormItem>
                      <FormLabel>{t('fields.client')}</FormLabel>
                      <FormControl>
                        <SearchableSelect
                          options={clients
                            .filter((client) => client.isActive)
                            .map((client) => ({ value: client.id, label: client.name }))}
                          value={field.value}
                          onValueChange={field.onChange}
                          placeholder={t('fields.clientPlaceholder')}
                          className="w-full"
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )
                }
              />

              {/*
                Caché pour un vendeur : son agence est celle de sa fiche, et
                le serveur la pose. La lui faire choisir dans la liste des
                agences de la société lui montrerait l'organisation interne du
                transporteur — retouches Anass, points 2 et 4.
              */}
              {selfClientId ? null : (
                <FormField
                  control={form.control}
                  name="agencyId"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('fields.agency')}</FormLabel>
                      <FormControl>
                        <SearchableSelect
                          options={agencies.map((agency) => ({
                            value: agency.id,
                            label: agency.name,
                          }))}
                          value={field.value ?? ''}
                          onValueChange={field.onChange}
                          placeholder={t('fields.agencyPlaceholder')}
                          className="w-full"
                        />
                      </FormControl>
                      <FormDescription>{t('fields.agencyHint')}</FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}

              <FormField
                control={form.control}
                name="externalCode"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('fields.externalCode')}</FormLabel>
                    <FormControl>
                      <Input {...field} value={field.value ?? ''} className="font-mono" />
                    </FormControl>
                    <FormDescription>{t('fields.externalCodeHint')}</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />

              {/*
                LE VENDEUR NE REDIT PAS SON ADRESSE À CHAQUE COLIS — point 9.

                Sa ville et son adresse de ramassage sont celles de sa fiche,
                affichées en lecture. Elles restent dans le formulaire — donc
                dans ce qui part au serveur, donc dans le calcul du tarif — mais
                elles ne se saisissent plus.
              */}
              {fromSenderRecord ? (
                <div className="bg-muted/40 flex flex-col gap-3 rounded-lg border p-3 sm:col-span-2">
                  <div className="grid gap-3 sm:grid-cols-2">
                    <div>
                      <p className="text-muted-foreground text-xs">{t('fields.originCity')}</p>
                      <p className="text-sm font-medium">{sender?.cityName}</p>
                    </div>
                    <div>
                      <p className="text-muted-foreground text-xs">{t('fields.pickupAddress')}</p>
                      <p className="text-sm">{sender?.address}</p>
                    </div>
                  </div>
                  <p className="text-muted-foreground text-xs">{t('fields.fromRecordHint')}</p>
                </div>
              ) : null}

              {fromSenderRecord ? null : (
                <FormField
                  control={form.control}
                  name="originCityId"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>{t('fields.originCity')}</FormLabel>
                      <FormControl>
                        <CitySelect
                          cities={cities}
                          value={field.value}
                          onValueChange={field.onChange}
                          placeholder={t('fields.cityPlaceholder')}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}

              {fromSenderRecord ? null : (
                <FormField
                  control={form.control}
                  name="pickupAddress"
                  render={({ field }) => (
                    <FormItem className="sm:col-span-2">
                      <FormLabel>{t('fields.pickupAddress')}</FormLabel>
                      <FormControl>
                        <Textarea rows={2} {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}

              {/*
                La case n'existe QUE pour le vendeur : l'administration saisit
                déjà les deux champs, et lui proposer de les « remplacer par la
                fiche » n'aurait rien à remplacer.
              */}
              {sender ? (
                <div className="sm:col-span-2">
                  <SwitchRow
                    checked={pickupElsewhere}
                    onChange={togglePickupElsewhere}
                    label={t('fields.pickupElsewhere')}
                    hint={t('fields.pickupElsewhereHint')}
                  />
                </div>
              ) : null}

              <FormField
                control={form.control}
                name="pickupAtSender"
                render={({ field }) => (
                  <FormItem className="sm:col-span-2">
                    <FormControl>
                      <SwitchRow
                        checked={field.value ?? true}
                        onChange={field.onChange}
                        label={t('fields.pickupAtSender')}
                        hint={t('fields.pickupAtSenderHint')}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </Section>

            <Section
              index={1}
              step={step}
              sectionKey="recipient"
              icon={MapPin}
              title={t('recipient.title')}
              description={t('recipient.description')}
            >
              <FormField
                control={form.control}
                name="recipientName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('fields.recipientName')}</FormLabel>
                    <FormControl>
                      <Input {...field} autoComplete="name" />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="recipientPhone"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('fields.recipientPhone')}</FormLabel>
                    <FormControl>
                      <Input
                        {...field}
                        type="tel"
                        inputMode="tel"
                        dir="ltr"
                        placeholder="0612345678"
                        className="font-mono"
                      />
                    </FormControl>
                    {/* D-244 : un à trois numéros dans le même champ. */}
                    <FormDescription>{t('fields.recipientPhoneHint')}</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="destinationCityId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('fields.destinationCity')}</FormLabel>
                    <FormControl>
                      <CitySelect
                        cities={cities}
                        value={field.value}
                        onValueChange={field.onChange}
                        placeholder={t('fields.cityPlaceholder')}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="destinationDistrictName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('fields.district')}</FormLabel>
                    <FormControl>
                      <DistrictInput
                        {...field}
                        value={field.value ?? ''}
                        districts={allDistricts}
                        cityId={destinationCityId}
                        placeholder={t('fields.districtPlaceholder')}
                      />
                    </FormControl>
                    <FormDescription>{t('fields.districtHint')}</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="deliveryAddress"
                render={({ field }) => (
                  <FormItem className="sm:col-span-2">
                    <FormLabel>{t('fields.deliveryAddress')}</FormLabel>
                    <FormControl>
                      <Textarea rows={2} {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </Section>

            <Section
              index={2}
              step={step}
              sectionKey="parcel"
              icon={Package}
              title={t('parcel.title')}
              description={t('parcel.description')}
            >
              <FormField
                control={form.control}
                name="description"
                render={({ field }) => (
                  <FormItem className="sm:col-span-2">
                    <FormLabel>{t('fields.description')}</FormLabel>
                    <FormControl>
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="codAmount"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('fields.codAmount')}</FormLabel>
                    <FormControl>
                      <Input
                        type="number"
                        min={0}
                        step="0.01"
                        inputMode="decimal"
                        value={field.value ?? 0}
                        onChange={(event) => field.onChange(event.target.valueAsNumber || 0)}
                        onBlur={field.onBlur}
                        name={field.name}
                        className="tabular-nums"
                      />
                    </FormControl>
                    <FormDescription>{t('fields.codHint')}</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <div className="flex flex-col gap-3 sm:col-span-2">
                {/*
                  « Frais de livraison inclus dans le prix » n'est PLUS saisi —
                  dernières corrections, point 3. Les frais sont toujours
                  inclus, et l'API l'impose à la création quoi qu'on poste.
                */}
                <FormField
                  control={form.control}
                  name="fragile"
                  render={({ field }) => (
                    <FormItem>
                      <FormControl>
                        <SwitchRow
                          checked={field.value ?? false}
                          onChange={field.onChange}
                          label={t('fields.fragile')}
                        />
                      </FormControl>
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="openOnDelivery"
                  render={({ field }) => (
                    <FormItem>
                      <FormControl>
                        <SwitchRow
                          checked={field.value ?? false}
                          onChange={field.onChange}
                          label={t('fields.openOnDelivery')}
                          hint={t('fields.openOnDeliveryHint')}
                        />
                      </FormControl>
                    </FormItem>
                  )}
                />

                {/*
                  « BL À RÉCUPÉRER LORS DE LA LIVRAISON ? » — chantier « Retours,
                  BL Clients Pro », point 5.

                  **La question n'apparaît QUE si le vendeur choisi est Pro** :
                  « aucun champ, aucune question, aucun workflow BL pour un
                  client normal ». Elle suit donc le sélecteur d'e-commerçant,
                  et disparaît si l'agent en change.

                  C'est une commodité, pas une sécurité : l'API refuse le champ
                  pour un client qui n'est pas Pro, et c'est là que la garantie
                  tient (D-099).
                */}
                {selectedClientIsPro ? (
                  <FormField
                    control={form.control}
                    name="blRequested"
                    render={({ field }) => (
                      <FormItem>
                        <FormControl>
                          <SwitchRow
                            checked={field.value ?? false}
                            onChange={field.onChange}
                            label={t('fields.blRequested')}
                            hint={t('fields.blRequestedHint')}
                          />
                        </FormControl>
                      </FormItem>
                    )}
                  />
                ) : null}

                {/* Recette 3 point 3 : même règle que le BL — Pro seulement. */}
                {selectedClientIsPro ? (
                  <FormField
                    control={form.control}
                    name="expectedPayment"
                    render={({ field }) => (
                      <FormItem>
                        <FormControl>
                          <ExpectedPaymentField value={field.value} onChange={field.onChange} />
                        </FormControl>
                      </FormItem>
                    )}
                  />
                ) : null}

                {/* Lot G (D-208) : plusieurs cartons, pour tout client (D-264). */}
                <SwitchRow
                  checked={multiCarton}
                  onChange={toggleMultiCarton}
                  label={t('fields.multiCarton')}
                  hint={t('fields.multiCartonHint')}
                />
                {cartonsActive ? (
                  <FormField
                    control={form.control}
                    name="quantity"
                    render={({ field }) => (
                      <FormItem className="sm:max-w-xs">
                        <FormLabel>{t('fields.quantity')}</FormLabel>
                        <FormControl>
                          <Input
                            type="number"
                            min={2}
                            max={PARCEL_QUANTITY_MAX}
                            step={1}
                            inputMode="numeric"
                            value={field.value === undefined ? '' : String(field.value)}
                            onChange={(event) => field.onChange(event.target.valueAsNumber || '')}
                            onBlur={field.onBlur}
                            name={field.name}
                            className="tabular-nums"
                          />
                        </FormControl>
                        <FormDescription>{t('fields.quantityHint')}</FormDescription>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                ) : null}
              </div>
            </Section>
          </div>

          {/* Récapitulatif : collant sur grand écran, simple carte en bas sinon. */}
          <aside className="lg:sticky lg:top-22 lg:self-start">
            <Card>
              <CardHeader>
                <CardTitle>{t('summary.title')}</CardTitle>
                <CardDescription>{t('summary.description')}</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-col gap-4">
                <div>
                  <p className="text-muted-foreground text-xs">{t('summary.code')}</p>
                  <p className="font-mono text-sm font-medium">
                    {codePreview ?? t('summary.codePending')}
                  </p>
                </div>

                <dl className="grid gap-2 text-sm">
                  <div className="flex items-baseline justify-between gap-3">
                    <dt className="text-muted-foreground">{t('summary.cod')}</dt>
                    <dd className="tabular-nums">{format.number(codInDirhams, 'currency')}</dd>
                  </div>
                  <div className="flex items-baseline justify-between gap-3">
                    <dt className="text-muted-foreground">{t('summary.fee')}</dt>
                    <dd className="text-end tabular-nums">
                      {quote ? format.number(feeInDirhams, 'currency') : '-'}
                      {quote && quantity > 1 ? (
                        <span className="text-muted-foreground block text-xs">
                          {t('summary.feeByCartons', {
                            unit: format.number(unitFeeInDirhams, 'currency'),
                            quantity: String(quantity),
                          })}
                        </span>
                      ) : null}
                    </dd>
                  </div>
                </dl>

                <div className="bg-accent text-accent-foreground flex items-baseline justify-between gap-3 rounded-lg px-3 py-2.5">
                  <span className="text-sm font-medium">{t('summary.toCollect')}</span>
                  <span className="text-lg font-semibold tabular-nums">
                    {format.number(total, 'currency')}
                  </span>
                </div>

                {quote ? (
                  <ul className="text-muted-foreground flex flex-col gap-1 text-xs">
                    <li>{quote.sameCity ? t('summary.sameCity') : t('summary.crossCity')}</li>
                    {quote.discounted ? <li>{t('summary.discounted')}</li> : null}
                    {!quote.matched ? <li>{t('summary.zoneFallback')}</li> : null}
                  </ul>
                ) : (
                  <p className="text-muted-foreground text-xs">{t('summary.pickCities')}</p>
                )}
              </CardContent>
            </Card>
          </aside>
        </div>

        {/* Barre d'action : « suivant » tant qu'on est dans le stepper mobile. */}
        <div className="bg-background/95 sticky bottom-20 z-10 flex items-center gap-2 rounded-lg border p-3 backdrop-blur md:bottom-4">
          <Button
            type="button"
            variant="ghost"
            className="gap-1.5 lg:hidden"
            onClick={() => setStep((current) => Math.max(current - 1, 0))}
            disabled={step === 0 || pending}
          >
            <ArrowLeft className="flip-rtl size-4" aria-hidden />
            {t('previous')}
          </Button>

          {step < SECTIONS.length - 1 ? (
            <Button type="button" className="ms-auto gap-1.5 lg:hidden" onClick={goNext}>
              {t('next')}
              <ArrowRight className="flip-rtl size-4" aria-hidden />
            </Button>
          ) : null}

          <Button
            type="submit"
            className={cn(
              'gap-1.5',
              step < SECTIONS.length - 1 ? 'ms-auto hidden lg:inline-flex' : 'ms-auto',
            )}
            disabled={pending}
          >
            {pending ? <Loader2 className="size-4 animate-spin" /> : <Check className="size-4" />}
            {t('submit')}
          </Button>
        </div>
      </form>
    </Form>
  );
}

/** Fil d'étapes — visible seulement quand les sections sont pliées, donc sous `lg`. */
function Stepper({ step, onStep }: { step: number; onStep: (step: number) => void }) {
  const t = useTranslations('parcels.form');

  return (
    <ol className="flex items-center gap-2 lg:hidden" aria-label={t('steps')}>
      {SECTIONS.map((section, index) => {
        const active = index === step;
        const done = index < step;
        return (
          <li key={section.key} className="flex flex-1 items-center gap-2">
            <button
              type="button"
              onClick={() => onStep(index)}
              aria-current={active ? 'step' : undefined}
              className={cn(
                'flex w-full flex-col items-start gap-1 rounded-md border-b-2 px-1 pb-2 text-start transition-colors',
                'focus-visible:ring-ring outline-none focus-visible:ring-2',
                active ? 'border-primary' : done ? 'border-primary/40' : 'border-border',
              )}
            >
              <span
                className={cn(
                  'text-[11px] font-medium tabular-nums',
                  active ? 'text-primary' : 'text-muted-foreground',
                )}
              >
                {index + 1}/{SECTIONS.length}
              </span>
              <span
                className={cn(
                  'truncate text-xs',
                  active ? 'text-foreground font-medium' : 'text-muted-foreground',
                )}
              >
                {t(`${section.key}.short`)}
              </span>
            </button>
          </li>
        );
      })}
    </ol>
  );
}

/**
 * Une section : carte complète sur grand écran, panneau du stepper sous `lg`.
 * `hidden lg:block` plutôt qu'un démontage : les champs restent enregistrés
 * dans react-hook-form et la validation croisée continue de fonctionner.
 */
function Section({
  index,
  step,
  sectionKey,
  icon: Icon,
  title,
  description,
  children,
}: {
  index: number;
  step: number;
  sectionKey: SectionKey;
  icon: React.ComponentType<{ className?: string }>;
  title: string;
  description: string;
  children: React.ReactNode;
}) {
  return (
    <Card id={`section-${sectionKey}`} className={cn(index === step ? 'block' : 'hidden lg:block')}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Icon className="size-4" aria-hidden />
          {title}
        </CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-5 sm:grid-cols-2">{children}</CardContent>
    </Card>
  );
}

/** Case à cocher + libellé + explication, sur une ligne. */
function SwitchRow({
  checked,
  onChange,
  label,
  hint,
}: {
  checked: boolean;
  onChange: (checked: boolean) => void;
  label: string;
  hint?: string;
}) {
  return (
    <label className="flex cursor-pointer items-start gap-3 rounded-md border p-3">
      <Checkbox checked={checked} onCheckedChange={(value) => onChange(value === true)} />
      <span className="min-w-0">
        <span className="block text-sm font-medium">{label}</span>
        {hint ? <span className="text-muted-foreground block text-xs">{hint}</span> : null}
      </span>
    </label>
  );
}
