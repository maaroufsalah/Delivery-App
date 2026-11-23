'use client';

import {
  type CreatedTenantResult,
  type CreateTenantInput,
  createTenantSchema,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Check, Copy, Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { useForm } from 'react-hook-form';
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
import { createTenantAction } from '@/lib/actions/platform';

/**
 * Création d'un espace client — C8 bloc 4, D-050.
 *
 * Côté API, l'opération est TRANSACTIONNELLE : société, siège, référentiel des
 * villes marocaines, réglages, palettes, matrice de permissions et compte
 * administrateur passent d'un bloc. Un espace à moitié créé est pire que pas
 * d'espace — une société sans agence ne peut pas créer de colis, une société
 * sans compte n'est accessible par personne, et rien de tout cela ne se voit
 * avant que le client n'essaie.
 *
 * **Le mot de passe temporaire n'apparaît qu'ICI, une seule fois.** Seul son
 * hachage entre en base ; la console ne saura pas le redonner. C'est la seule
 * façon honnête de le transmettre tant qu'aucun envoi d'e-mail n'est branché —
 * et c'est pourquoi l'écran de confirmation offre un bouton « copier » plutôt
 * que de compter sur la mémoire de qui le lit.
 */
export function CreateTenantDialog({
  platformDomain,
  onDone,
}: {
  platformDomain: string;
  onDone: () => void;
}) {
  const t = useTranslations('platform');
  const tCommon = useTranslations('common');
  const [pending, startTransition] = useTransition();
  const [created, setCreated] = useState<CreatedTenantResult | null>(null);

  const form = useForm<CreateTenantInput>({
    resolver: zodResolver(createTenantSchema),
    defaultValues: {
      name: '',
      slug: '',
      adminEmail: '',
      adminFirstName: '',
      adminLastName: '',
      trialDays: 30,
      plan: '',
    },
    mode: 'onBlur',
  });

  const onSubmit = (values: CreateTenantInput) => {
    startTransition(async () => {
      const result = await createTenantAction(values);

      if (!result.ok) {
        toast.error(result.error);
        return;
      }

      // On NE ferme pas : l'écran suivant porte le mot de passe temporaire, et
      // le fermer trop tôt le perdrait définitivement.
      setCreated(result.data);
    });
  };

  if (created) {
    return <CreatedPanel created={created} onClose={onDone} />;
  }

  return (
    <Dialog
      open
      onOpenChange={(next) => {
        if (!next) onDone();
      }}
    >
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{t('create.title')}</DialogTitle>
          <DialogDescription>{t('create.description')}</DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-4">
            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('create.name')}</FormLabel>
                  <FormControl>
                    <Input {...field} autoComplete="organization" />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="slug"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('create.slug')}</FormLabel>
                  <FormControl>
                    <Input {...field} dir="ltr" autoComplete="off" spellCheck={false} />
                  </FormControl>
                  <FormDescription>
                    {t('create.slugHint', {
                      host: `${field.value || 'client'}.${platformDomain}`,
                    })}
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="grid gap-4 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="adminFirstName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('create.adminFirstName')}</FormLabel>
                    <FormControl>
                      <Input {...field} autoComplete="given-name" />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="adminLastName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('create.adminLastName')}</FormLabel>
                    <FormControl>
                      <Input {...field} autoComplete="family-name" />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="adminEmail"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('create.adminEmail')}</FormLabel>
                  <FormControl>
                    <Input {...field} type="email" dir="ltr" autoComplete="email" />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="grid gap-4 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="trialDays"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('create.trialDays')}</FormLabel>
                    <FormControl>
                      <Input
                        {...field}
                        type="number"
                        min={1}
                        max={365}
                        dir="ltr"
                        // `z.coerce.number()` laisse l'entrée non typée : c'est
                        // le schéma qui convertit, pas le champ, et un `<input>`
                        // ne porte de toute façon qu'une chaîne.
                        value={field.value === undefined ? '' : String(field.value)}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="plan"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('create.plan')}</FormLabel>
                    <FormControl>
                      <Input {...field} value={field.value ?? ''} autoComplete="off" />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={onDone} disabled={pending}>
                {tCommon('cancel')}
              </Button>
              <Button type="submit" disabled={pending} className="gap-1.5">
                {pending ? <Loader2 className="size-4 animate-spin" /> : null}
                {t('create.submit')}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}

/**
 * Les identifiants, montrés une fois.
 *
 * Le dialogue ne se ferme PAS en cliquant à côté : perdre ce mot de passe d'un
 * clic distrait obligerait à repasser par une réinitialisation, et le client
 * attend au bout du fil.
 */
function CreatedPanel({ created, onClose }: { created: CreatedTenantResult; onClose: () => void }) {
  const t = useTranslations('platform');

  return (
    <Dialog open onOpenChange={() => undefined}>
      <DialogContent className="sm:max-w-lg" showCloseButton={false}>
        <DialogHeader>
          <DialogTitle>{t('create.doneTitle')}</DialogTitle>
          <DialogDescription>{t('create.doneBody', { name: created.name })}</DialogDescription>
        </DialogHeader>

        <dl className="flex flex-col gap-3 text-sm">
          <CopyRow label={t('create.adminEmail')} value={created.adminEmail} />
          <CopyRow label={t('create.title')} value={created.temporaryPassword} mono />
        </dl>

        <p className="text-muted-foreground text-xs">
          {t('create.seeded', {
            cities: created.citiesCreated,
            districts: created.districtsCreated,
          })}
        </p>

        <DialogFooter>
          <Button onClick={onClose}>{t('create.close')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function CopyRow({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  const t = useTranslations('platform');
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
    } catch {
      // Presse-papiers refusé (contexte non sécurisé, permission) : la valeur
      // reste sélectionnable à la main, ce qui est le vrai filet de sécurité.
      toast.error(t('errors.failed'));
    }
  };

  return (
    <div className="bg-muted/50 flex items-center justify-between gap-3 rounded-md border px-3 py-2">
      <div className="min-w-0">
        <dt className="text-muted-foreground text-xs">{label}</dt>
        <dd className={`truncate ${mono ? 'font-mono' : ''}`} dir="ltr">
          {value}
        </dd>
      </div>

      <Button type="button" variant="ghost" size="sm" className="gap-1.5" onClick={copy}>
        {copied ? <Check className="size-4" /> : <Copy className="size-4" />}
        <span className="hidden sm:inline">{copied ? t('create.copied') : t('create.copy')}</span>
      </Button>
    </div>
  );
}
