'use client';

import {
  type ClaimAttachmentPayload,
  type ClaimType,
  type Client,
  createClaimSchema,
  type CreateClaimInput,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { toast } from 'sonner';
import { SearchableSelect } from '@/components/searchable-select';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
import { Textarea } from '@/components/ui/textarea';
import { AttachmentPicker } from '@/features/claims/attachment-picker';
import { useRouter } from '@/i18n/navigation';
import { createClaimAction } from '@/lib/actions/claims';

/**
 * Ouverture d'une réclamation.
 *
 * Le champ « colis » n'apparaît QUE si le type en exige un : l'e-commerçant qui
 * signale un règlement manquant n'a pas de code colis à donner, et lui montrer
 * un champ vide obligatoire est la façon la plus sûre de le faire abandonner.
 * La règle est la même côté API — c'est le type qui décide (§1.19).
 *
 * `parcelCode` peut être préchargé : la fiche d'un colis ouvre ce formulaire
 * avec son code déjà posé.
 */
export function ClaimForm({
  types,
  clients,
  lockedClientId,
  hideClient,
  presetParcelCode,
  basePath = '/claims',
}: {
  types: ClaimType[];
  clients: Client[];
  /** Renseigné pour un e-commerçant : il réclame pour lui-même. */
  lockedClientId?: string;
  /**
   * Cache le choix de l'e-commerçant SANS en imposer un — le cas du LIVREUR.
   *
   * Il n'en désigne aucun : le serveur déduit le client du COLIS, et le lui
   * faire choisir reviendrait à lui montrer l'annuaire des clients de la
   * société (D-082). Différent de `lockedClientId`, qui en impose un connu.
   */
  hideClient?: boolean;
  presetParcelCode?: string;
  /**
   * Où mène la réclamation créée. Le back-office et l'e-commerçant la lisent
   * sous `/claims`, le livreur sous `/me/claims` : une seule route en dur
   * aurait renvoyé l'un des deux vers une page que sa coque n'a pas.
   */
  basePath?: string;
}) {
  const t = useTranslations('claims.form');
  const tCommon = useTranslations('common');
  const router = useRouter();
  const [pending, startTransition] = useTransition();
  const [attachments, setAttachments] = useState<ClaimAttachmentPayload[]>([]);

  const usable = types.filter((type) => type.isActive);

  const form = useForm<CreateClaimInput>({
    resolver: zodResolver(createClaimSchema),
    defaultValues: {
      typeId: usable[0]?.id ?? '',
      subject: '',
      message: '',
      parcelCode: presetParcelCode ?? '',
      clientId: lockedClientId ?? '',
      attachments: [],
    },
  });

  const typeId = useWatch({ control: form.control, name: 'typeId' });
  const needsParcel = usable.find((type) => type.id === typeId)?.subjectType === 'parcel';

  const submit = form.handleSubmit((values) => {
    startTransition(async () => {
      const result = await createClaimAction({
        ...values,
        /*
         * `undefined` pour le livreur, et non une chaîne vide : le champ ne
         * part pas du tout, et le serveur déduit le client du colis.
         */
        clientId: hideClient ? undefined : (lockedClientId ?? values.clientId),
        attachments,
      });

      if (!result.ok) {
        // L'API dit QUEL champ ne va pas : le poser sur le champ vaut mieux
        // qu'un toast que l'utilisateur doit traduire lui-même.
        if (result.field) {
          form.setError(result.field as keyof CreateClaimInput, { message: result.error });
        }
        toast.error(result.error);
        return;
      }

      toast.success(t('created', { code: result.data.code }));
      router.push(`${basePath}/${encodeURIComponent(result.data.code)}`);
    });
  });

  return (
    <Form {...form}>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <Card>
          <CardContent className="flex flex-col gap-4 pt-6">
            {lockedClientId || hideClient ? null : (
              <FormField
                control={form.control}
                name="clientId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('fields.client')}</FormLabel>
                    <FormControl>
                      <SearchableSelect
                        options={clients.map((client) => ({
                          value: client.id,
                          label: client.name,
                        }))}
                        value={field.value ?? ''}
                        onValueChange={field.onChange}
                        placeholder={t('fields.clientPlaceholder')}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}

            <FormField
              control={form.control}
              name="typeId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.type')}</FormLabel>
                  <FormControl>
                    <SearchableSelect
                      options={usable.map((type) => ({ value: type.id, label: type.label }))}
                      value={field.value}
                      onValueChange={field.onChange}
                      placeholder={t('fields.typePlaceholder')}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            {needsParcel ? (
              <FormField
                control={form.control}
                name="parcelCode"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('fields.parcel')}</FormLabel>
                    <FormControl>
                      <Input {...field} value={field.value ?? ''} className="font-mono" />
                    </FormControl>
                    <FormDescription>{t('fields.parcelHint')}</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            ) : null}

            <FormField
              control={form.control}
              name="subject"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.subject')}</FormLabel>
                  <FormControl>
                    <Input {...field} placeholder={t('fields.subjectPlaceholder')} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="message"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.message')}</FormLabel>
                  <FormControl>
                    <Textarea {...field} rows={6} placeholder={t('fields.messagePlaceholder')} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="flex flex-col gap-1.5">
              <Label>{t('fields.attachments')}</Label>
              <AttachmentPicker value={attachments} onChange={setAttachments} disabled={pending} />
            </div>
          </CardContent>
        </Card>

        <div className="flex flex-wrap justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => router.back()} disabled={pending}>
            {tCommon('cancel')}
          </Button>
          <Button type="submit" disabled={pending} className="gap-1.5">
            {pending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
            {t('submit')}
          </Button>
        </div>
      </form>
    </Form>
  );
}
