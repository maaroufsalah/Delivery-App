'use client';

import {
  ACCEPTED_DOCUMENT_TYPES,
  type Agency,
  type City,
  CLIENT_DOCUMENT_KINDS,
  type ClientDetail as ClientDetailData,
  type ClientDocumentKind,
  type ClientPickupAddressInput,
  type ClientPickupAddressPayload,
  clientPickupAddressSchema,
  type District,
  MAX_DOCUMENT_BYTES,
  PRO_VAT_RATES,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  BadgeCheck,
  ClipboardList,
  Download,
  FileSignature,
  FileText,
  Loader2,
  MapPin,
  Pencil,
  Plus,
  Trash2,
  Upload,
  UserX,
} from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useRef, useState, useTransition } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { CellBadge } from '@/components/data-table/cells';
import { ALL_VALUE } from '@/components/data-table/data-table-toolbar';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Form,
  FormControl,
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
import { Switch } from '@/components/ui/switch';
import { CitySelect } from '@/components/city-select';
import { SearchableSelect } from '@/components/searchable-select';
import { ClientForm } from '@/features/accounts/client-form';
import { RejectDialog, ValidateDialog } from '@/features/accounts/validation-dialogs';
import { NoteForm } from '@/features/notes/note-form';
import { FormSheet } from '@/features/referential/form-sheet';
import { useRouter } from '@/i18n/navigation';
import {
  addClientDocumentAction,
  addClientPickupAddressAction,
  deleteClientDocumentAction,
  deleteClientPickupAddressAction,
  downloadClientDocumentAction,
  validateClientAction,
  rejectClientAction,
  setClientProAction,
  setClientVatRateAction,
} from '@/lib/actions/accounts';

/**
 * Fiche e-commerçant : Client Pro, adresses de ramassage, pièces jointes. Les
 * remises vivent dans l'onglet « Tarifs & remises » (`client-pricing.tsx`, lot K).
 *
 * Les trois listes sont servies par la page, jamais chargées à la demande : un
 * composant client n'appelle pas `lib/api` (D-018), et une fiche qui tient en
 * une requête vaut mieux que trois allers-retours.
 */
/**
 * Lot R (D-230) — ce que la fiche sait de la TVA pro : qui peut la régler
 * (`finance.validate`, l'administration) et le défaut société en centièmes
 * (`null` s'il n'a pas pu être lu : le choix « défaut » reste sans chiffre).
 */
export interface ClientProVat {
  canEdit: boolean;
  companyDefault: number | null;
}

/** Aucun taux propre : la valeur du `Select`, qui n'accepte pas la chaîne vide. */
const DEFAULT_VAT = '__default__';

export function ClientDetail({
  client,
  cities,
  districts,
  agencies,
  proVat,
}: {
  client: ClientDetailData;
  cities: City[];
  districts: District[];
  agencies: Agency[];
  proVat: ClientProVat;
}) {
  const t = useTranslations('accounts.clients');
  const tValidation = useTranslations('accounts.validation');
  const tDetail = useTranslations('accounts.clients.detail');
  const tCommon = useTranslations('common');

  const [editing, setEditing] = useState(false);
  const [addingAddress, setAddingAddress] = useState(false);
  const [pickupNote, setPickupNote] = useState(false);
  const [validating, setValidating] = useState(false);
  const [rejecting, setRejecting] = useState(false);
  /*
   * L'état de l'interrupteur « Client Pro », semé par le SERVEUR et corrigé par
   * lui : `client.isPro` est la vérité, cette copie n'existe que pour que le
   * doigt ait une réponse avant l'aller-retour.
   */
  const [isPro, setIsPro] = useState(client.isPro);
  const [proPending, startProTransition] = useTransition();
  const [vatPending, startVatTransition] = useTransition();
  const vatLabel = (rate: number) =>
    rate === 0 ? tDetail('vatExempt') : tDetail('vatPercent', { rate: String(rate / 100) });

  const changeVat = (value: string) => {
    const vatRate = value === DEFAULT_VAT ? null : Number(value);
    startVatTransition(async () => {
      const result = await setClientVatRateAction(client.id, vatRate);
      if (result.ok) {
        toast.success(tDetail('vatSaved'));
        router.refresh();
      } else toast.error(result.error);
    });
  };
  const [, startTransition] = useTransition();
  const router = useRouter();

  const remove = (run: () => Promise<{ ok: boolean; error?: string }>) => {
    startTransition(async () => {
      const result = await run();
      if (!result.ok) toast.error(result.error ?? tCommon('delete'));
    });
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        {client.validatedAt ? (
          <CellBadge label={t('validated')} color="var(--chart-4)" icon={BadgeCheck} />
        ) : client.rejectedAt ? (
          <CellBadge label={t('rejected')} color="var(--destructive)" icon={UserX} />
        ) : (
          <CellBadge label={t('notValidated')} />
        )}
        {client.isActive ? (
          <CellBadge label={tCommon('active')} color="var(--chart-4)" />
        ) : (
          <CellBadge label={tCommon('inactive')} />
        )}
        {/*
          CLIENT PRO — point 4 du chantier « Retours, BL Clients Pro ». Il
          figure ici parce que c'est le premier endroit où un agent vérifie
          pourquoi un vendeur voit — ou ne voit pas — la question du BL.
        */}
        {client.isPro ? <CellBadge label={t('fields.isPro')} color="var(--chart-1)" /> : null}
        {client.validatedByName ? (
          <span className="text-muted-foreground text-xs">
            {t('validatedBy', { name: client.validatedByName })}
          </span>
        ) : null}

        {/* Le motif, à côté du badge : c'est ce que l'agent lira quand la
            personne rappellera — C13 bloc 2. */}
        {client.rejectedAt ? (
          <span className="text-muted-foreground text-xs">{client.rejectionReason}</span>
        ) : null}

        {/* Valider et Refuser vivent AUSSI sur la fiche : un agent qui ouvre le
            dossier pour l'examiner ne doit pas avoir à retourner à la liste
            pour décider. */}
        {!client.validatedAt ? (
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

        {/* Le geste le plus fréquent depuis une fiche : ouvrir un ramassage
            pour CE client. Le formulaire n'a alors plus de client à demander. */}
        <Button
          size="sm"
          variant="outline"
          className={cn('gap-1.5', client.validatedAt && 'ms-auto')}
          onClick={() => setPickupNote(true)}
        >
          <ClipboardList className="size-4" />
          <span className="hidden sm:inline">{tDetail('newPickupNote')}</span>
        </Button>

        <Button size="sm" variant="outline" className="gap-1.5" onClick={() => setEditing(true)}>
          <Pencil className="size-4" />
          {tCommon('edit')}
        </Button>
      </div>

      {validating ? (
        <ValidateDialog
          label={client.name}
          agencies={agencies}
          onConfirm={(agencyId) => validateClientAction(client.id, agencyId)}
          onClose={() => setValidating(false)}
        />
      ) : null}

      {rejecting ? (
        <RejectDialog
          label={client.name}
          onConfirm={(reason) => rejectClientAction(client.id, reason)}
          onClose={() => setRejecting(false)}
        />
      ) : null}

      {pickupNote ? (
        <NoteForm
          kind="pickup"
          clientId={client.id}
          clients={[]}
          agencies={agencies}
          cities={cities}
          onDone={() => setPickupNote(false)}
          onCreated={(code) => router.push(`/pickup-notes/${code}`)}
        />
      ) : null}

      {/*
        « CLIENT PRO » SUR LA FICHE, ET RENDU AU SSR — chantier « Bon de retour
        livreur », point 0.

        **Le badge ne suffisait pas.** Il n'apparaît que si `isPro` est VRAI :
        sur une fiche ordinaire, rien ne disait que l'option existe, et un agent
        qui cherchait pourquoi un vendeur ne voit pas la question du BL devait
        ouvrir « Modifier », faire défiler douze champs et lire un interrupteur
        éteint. L'absence d'information se lisait comme une absence de
        fonctionnalité.

        **Il est dans le HTML de la page**, pas dans une boîte qui ne se monte
        qu'au clic : l'état se voit sans interaction, et il se lit même si le
        JavaScript n'a pas encore pris la main.

        **Un seul champ part sur le fil** (`PATCH {isPro}`), ce que le même
        point 0 vient de rendre possible. Basculer l'option ne renvoie plus les
        onze autres champs — donc ne peut plus en écraser un au passage.
      */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <FileSignature className="size-4" />
            {t('fields.isPro')}
          </CardTitle>
          <CardDescription>{t('fields.isProHint')}</CardDescription>
        </CardHeader>

        <CardContent>
          <label className="hover:bg-muted/40 flex cursor-pointer items-center justify-between gap-3 rounded-lg border p-3">
            <span className="min-w-0 text-sm">{isPro ? tDetail('proOn') : tDetail('proOff')}</span>
            <Switch
              checked={isPro}
              disabled={proPending}
              onCheckedChange={(next) => {
                // OPTIMISTE, et borné : l'interrupteur suit le doigt, puis
                // revient si le serveur refuse. Attendre l'aller-retour ferait
                // douter du clic ; ne jamais revenir ferait mentir l'écran.
                setIsPro(next);
                startProTransition(async () => {
                  const result = await setClientProAction(client.id, next);

                  if (result.ok) {
                    toast.success(next ? tDetail('proEnabled') : tDetail('proDisabled'));
                    router.refresh();
                  } else {
                    setIsPro(!next);
                    toast.error(result.error);
                  }
                });
              }}
            />
          </label>

          {/*
            TAUX DE TVA APPLICABLE — lot R (D-230). Client Pro seulement : la TVA
            ne s'applique qu'à eux, et le champ n'existe pas pour un client
            normal. Réglable par l'administration ; lu par les autres. Vide =
            le défaut société (Paramètres > Finance).
          */}
          {client.isPro ? (
            <div className="mt-3 flex flex-col gap-1.5">
              <Label htmlFor="client-vat-rate">{tDetail('vatRate')}</Label>
              {proVat.canEdit ? (
                <Select
                  value={client.vatRate === null ? DEFAULT_VAT : String(client.vatRate)}
                  onValueChange={changeVat}
                  disabled={vatPending}
                >
                  <SelectTrigger id="client-vat-rate" className="w-full sm:w-72">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={DEFAULT_VAT}>
                      {proVat.companyDefault === null
                        ? tDetail('vatDefault')
                        : tDetail('vatDefaultValue', { rate: vatLabel(proVat.companyDefault) })}
                    </SelectItem>
                    {PRO_VAT_RATES.map((rate) => (
                      <SelectItem key={rate} value={String(rate)}>
                        {vatLabel(rate)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              ) : (
                <p id="client-vat-rate" className="text-sm">
                  {client.vatRate === null ? tDetail('vatDefault') : vatLabel(client.vatRate)}
                </p>
              )}
              <p className="text-muted-foreground text-xs">{tDetail('vatRateHint')}</p>
            </div>
          ) : null}
        </CardContent>
      </Card>

      {/* --- Adresses de ramassage ---------------------------------------- */}
      <Card>
        <CardHeader className="flex flex-row items-start justify-between gap-3">
          <div className="min-w-0">
            <CardTitle className="flex items-center gap-2 text-base">
              <MapPin className="size-4" />
              {tDetail('addresses')}
            </CardTitle>
            <CardDescription>{tDetail('addressesHint')}</CardDescription>
          </div>
          <Button size="sm" variant="outline" onClick={() => setAddingAddress(true)}>
            <Plus className="size-4" />
            <span className="hidden sm:inline">{tDetail('add')}</span>
          </Button>
        </CardHeader>

        <CardContent className="flex flex-col gap-2">
          {client.pickupAddresses.length === 0 ? (
            <p className="text-muted-foreground text-sm">{tDetail('noAddresses')}</p>
          ) : (
            client.pickupAddresses.map((address) => (
              <div key={address.id} className="flex items-start gap-3 rounded-lg border p-3">
                <div className="min-w-0 flex-1">
                  <p className="flex items-center gap-2 text-sm font-medium">
                    {address.label}
                    {address.isDefault ? <CellBadge label={tDetail('default')} /> : null}
                  </p>
                  <p className="text-muted-foreground text-xs">
                    {address.address}, {address.cityName}
                    {address.districtName ? ` · ${address.districtName}` : ''}
                  </p>
                </div>

                <Button
                  size="icon"
                  variant="ghost"
                  aria-label={tCommon('delete')}
                  onClick={() =>
                    remove(() => deleteClientPickupAddressAction(client.id, address.id))
                  }
                >
                  <Trash2 className="text-destructive size-4" />
                </Button>
              </div>
            ))
          )}
        </CardContent>
      </Card>

      {/* --- Documents ---------------------------------------------------- */}
      <DocumentsCard client={client} />

      {/* L'accès au compte de l'e-commerçant — C12 bloc 2a. */}

      {editing ? (
        <ClientForm
          client={client}
          cities={cities}
          agencies={agencies}
          onDone={() => setEditing(false)}
        />
      ) : null}

      {addingAddress ? (
        <AddressForm
          clientId={client.id}
          cities={cities}
          districts={districts}
          onDone={() => setAddingAddress(false)}
        />
      ) : null}
    </div>
  );
}

/**
 * Pièces jointes.
 *
 * Le fichier est lu par le navigateur, encodé en base64 et posté par une
 * Server Action — même chemin que l'import tarifaire. Les deux refus (taille,
 * format) sont prononcés AVANT l'envoi : faire monter 8 Mo pour s'entendre dire
 * non au retour est une dépense inutile, côté serveur comme côté forfait data.
 */
function DocumentsCard({ client }: { client: ClientDetailData }) {
  const tDetail = useTranslations('accounts.clients.detail');
  const tKind = useTranslations('accounts.clients.detail.kinds');
  const tCommon = useTranslations('common');
  const format = useFormatter();

  const inputRef = useRef<HTMLInputElement>(null);
  const [kind, setKind] = useState<ClientDocumentKind>('other');
  const [pending, startTransition] = useTransition();

  const upload = (file: File) => {
    if (file.size > MAX_DOCUMENT_BYTES) {
      toast.error(tDetail('fileTooLarge'));
      return;
    }

    if (!(ACCEPTED_DOCUMENT_TYPES as readonly string[]).includes(file.type)) {
      toast.error(tDetail('unsupported'));
      return;
    }

    startTransition(async () => {
      const bytes = new Uint8Array(await file.arrayBuffer());
      let binary = '';
      for (let index = 0; index < bytes.length; index += 8192) {
        binary += String.fromCharCode(...bytes.subarray(index, index + 8192));
      }

      const result = await addClientDocumentAction(client.id, {
        name: file.name,
        kind,
        mimeType: file.type,
        content: btoa(binary),
      });

      if (!result.ok) toast.error(result.error);
      if (inputRef.current) inputRef.current.value = '';
    });
  };

  const download = (documentId: string, name: string, mimeType: string) => {
    startTransition(async () => {
      const result = await downloadClientDocumentAction(client.id, documentId);
      if (!result.ok) {
        toast.error(result.error);
        return;
      }

      const bytes = Uint8Array.from(atob(result.data), (char) => char.charCodeAt(0));
      const url = URL.createObjectURL(new Blob([bytes], { type: mimeType }));
      const link = document.createElement('a');
      link.href = url;
      link.download = name;
      link.click();
      URL.revokeObjectURL(url);
    });
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-base">
          <FileText className="size-4" />
          {tDetail('documents')}
        </CardTitle>
        <CardDescription>{tDetail('documentsHint')}</CardDescription>
      </CardHeader>

      <CardContent className="flex flex-col gap-3">
        <div className="flex flex-wrap items-end gap-2">
          <div className="flex min-w-40 flex-1 flex-col gap-1.5">
            <span className="text-sm font-medium">{tDetail('kind')}</span>
            <Select value={kind} onValueChange={(value) => setKind(value as ClientDocumentKind)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {CLIENT_DOCUMENT_KINDS.map((entry) => (
                  <SelectItem key={entry} value={entry}>
                    {tKind(entry)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <input
            ref={inputRef}
            type="file"
            className="hidden"
            accept={ACCEPTED_DOCUMENT_TYPES.join(',')}
            onChange={(event) => {
              const file = event.target.files?.[0];
              if (file) upload(file);
            }}
          />

          <Button
            variant="outline"
            className="gap-1.5"
            disabled={pending}
            onClick={() => inputRef.current?.click()}
          >
            {pending ? <Loader2 className="size-4 animate-spin" /> : <Upload className="size-4" />}
            {tDetail('upload')}
          </Button>
        </div>

        {client.documents.length === 0 ? (
          <p className="text-muted-foreground text-sm">{tDetail('noDocuments')}</p>
        ) : (
          client.documents.map((document) => (
            <div key={document.id} className="flex items-center gap-3 rounded-lg border p-3">
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium">{document.name}</p>
                <p className="text-muted-foreground text-xs">
                  {tKind(document.kind)} · {Math.ceil(document.sizeBytes / 1024)} ko ·{' '}
                  {format.dateTime(new Date(document.createdAt), 'stamp')}
                </p>
              </div>

              <Button
                size="icon"
                variant="ghost"
                aria-label={tDetail('download')}
                disabled={pending}
                onClick={() => download(document.id, document.name, document.mimeType)}
              >
                <Download className="size-4" />
              </Button>

              <Button
                size="icon"
                variant="ghost"
                aria-label={tCommon('delete')}
                disabled={pending}
                onClick={() => {
                  startTransition(async () => {
                    const result = await deleteClientDocumentAction(client.id, document.id);
                    if (!result.ok) toast.error(result.error);
                  });
                }}
              >
                <Trash2 className="text-destructive size-4" />
              </Button>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}

/** Adresse de ramassage — proposée à la création d'un colis. */
function AddressForm({
  clientId,
  cities,
  districts,
  onDone,
}: {
  clientId: string;
  cities: City[];
  districts: District[];
  onDone: () => void;
}) {
  const tDetail = useTranslations('accounts.clients.detail');
  const t = useTranslations('accounts.clients');
  const tParcel = useTranslations('parcels.form.fields');
  const tError = useTranslations('validation');
  const [saving, setSaving] = useState(false);

  const form = useForm<ClientPickupAddressInput, unknown, ClientPickupAddressPayload>({
    resolver: zodResolver(clientPickupAddressSchema),
    defaultValues: {
      label: '',
      address: '',
      cityId: '',
      districtId: '',
      phone: '',
      isDefault: false,
    },
    mode: 'onBlur',
  });

  const cityId = form.watch('cityId');
  // Les quartiers descendent tous avec la page : les filtrer ici évite un
  // aller-retour serveur à chaque changement de ville.
  const available = districts.filter((district) => district.cityId === cityId);

  const submit = form.handleSubmit(async (values) => {
    setSaving(true);
    const result = await addClientPickupAddressAction(clientId, values);
    setSaving(false);

    if (result.ok) {
      onDone();
      return;
    }

    toast.error(result.error === 'invalid' ? tError('invalid') : result.error);
  });

  return (
    <FormSheet
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
      title={tDetail('addresses')}
      description={tDetail('addressesHint')}
      onSubmit={submit}
      submitting={saving}
    >
      <Form {...form}>
        <form className="flex flex-col gap-4" onSubmit={submit}>
          <FormField
            control={form.control}
            name="label"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.name')}</FormLabel>
                <FormControl>
                  <Input {...field} autoFocus />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="address"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.address')}</FormLabel>
                <FormControl>
                  <Input {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <div className="grid grid-cols-2 gap-3">
            <FormField
              control={form.control}
              name="cityId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.city')}</FormLabel>
                  <FormControl>
                    <CitySelect
                      cities={cities}
                      value={field.value}
                      onValueChange={(value) => {
                        field.onChange(value);
                        // Un quartier d'une autre ville n'a plus de sens.
                        form.setValue('districtId', '');
                      }}
                      placeholder={t('fields.city')}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="districtId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{tParcel('district')}</FormLabel>
                  <FormControl>
                    <SearchableSelect
                      options={available.map((district) => ({
                        value: district.id,
                        label: district.name,
                      }))}
                      value={field.value || ALL_VALUE}
                      onValueChange={(value) => field.onChange(value === ALL_VALUE ? '' : value)}
                      emptyOption={{ value: ALL_VALUE, label: '-' }}
                      disabled={available.length === 0}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <FormField
            control={form.control}
            name="phone"
            render={({ field }) => (
              <FormItem>
                <FormLabel>{t('fields.phone')}</FormLabel>
                <FormControl>
                  <Input {...field} inputMode="tel" />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="isDefault"
            render={({ field }) => (
              <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                <FormLabel>{tDetail('default')}</FormLabel>
                <FormControl>
                  <Switch checked={field.value} onCheckedChange={field.onChange} />
                </FormControl>
              </FormItem>
            )}
          />
        </form>
      </Form>
    </FormSheet>
  );
}
