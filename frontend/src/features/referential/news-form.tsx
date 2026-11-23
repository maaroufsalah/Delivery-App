'use client';

import {
  ANNOUNCEMENT_AUDIENCES,
  type AnnouncementDetail,
  type AnnouncementInput,
  announcementSchema,
} from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { ImageIcon, Loader2, X } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useRef, useState, useTransition } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { RichText } from '@/features/referential/rich-text';
import { useRouter } from '@/i18n/navigation';
import { saveAnnouncementAction } from '@/lib/actions/announcements';

type ImageState =
  /** Rien n'est demandé : l'image en place est conservée. */
  | { kind: 'keep' }
  /** Elle est retirée. */
  | { kind: 'clear' }
  /** Elle est remplacée. */
  | { kind: 'set'; filename: string; mimeType: string; content: string };

const ACCEPTED = ['image/jpeg', 'image/png', 'image/webp'] as const;

/**
 * Rédaction d'une actualité.
 *
 * L'aperçu est à droite, en direct : le corps n'est pas du HTML mais un
 * sous-ensemble volontairement pauvre (paragraphes, gras, listes, liens), et un
 * rédacteur doit voir tout de suite ce que sa syntaxe produit — sans quoi il
 * publie en aveugle et corrige après coup.
 */
export function NewsForm({ announcement }: { announcement?: AnnouncementDetail }) {
  const t = useTranslations('referential.news');
  const tCommon = useTranslations('common');
  const router = useRouter();
  const fileRef = useRef<HTMLInputElement>(null);
  const [image, setImage] = useState<ImageState>({ kind: 'keep' });
  const [pending, startTransition] = useTransition();

  const form = useForm<AnnouncementInput>({
    resolver: zodResolver(announcementSchema),
    defaultValues: {
      title: announcement?.title ?? '',
      intro: announcement?.intro ?? '',
      body: announcement?.body ?? '',
      audience: announcement?.audience ?? 'all',
      isPublished: announcement?.isPublished ?? false,
      publishedAt: announcement?.publishedAt?.slice(0, 10) ?? '',
    },
    mode: 'onBlur',
  });

  const body = form.watch('body') ?? '';

  const pick = async (file: File) => {
    if (!(ACCEPTED as readonly string[]).includes(file.type)) {
      toast.error(t('image.unsupported'));
      return;
    }

    if (file.size > 4 * 1024 * 1024) {
      toast.error(t('image.tooLarge'));
      return;
    }

    const bytes = new Uint8Array(await file.arrayBuffer());
    let binary = '';
    for (let index = 0; index < bytes.length; index += 8192) {
      binary += String.fromCharCode(...bytes.subarray(index, index + 8192));
    }

    setImage({
      kind: 'set',
      filename: file.name.slice(0, 200),
      mimeType: file.type,
      content: btoa(binary),
    });
  };

  const submit = form.handleSubmit((values) => {
    startTransition(async () => {
      const result = await saveAnnouncementAction(announcement?.id ?? null, {
        ...values,
        // `undefined` conserve, `null` retire, un objet remplace : les trois
        // cas doivent rester distincts jusqu'à l'API.
        image:
          image.kind === 'keep'
            ? undefined
            : image.kind === 'clear'
              ? null
              : { filename: image.filename, mimeType: image.mimeType, content: image.content },
        // Une date de jour devient un instant : l'API trie et filtre dessus.
        publishedAt: values.publishedAt ? new Date(values.publishedAt).toISOString() : '',
      });

      if (!result.ok) {
        toast.error(result.error);
        return;
      }

      toast.success(t('saved'));
      router.push(`/news/${encodeURIComponent(result.data.id)}`);
    });
  });

  return (
    <Form {...form}>
      <form onSubmit={submit} className="flex flex-col gap-4 lg:grid lg:grid-cols-3 lg:items-start">
        <div className="flex flex-col gap-4 lg:col-span-2">
          <Card>
            <CardContent className="flex flex-col gap-4 pt-6">
              <FormField
                control={form.control}
                name="title"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('fields.title')}</FormLabel>
                    <FormControl>
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="intro"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('fields.intro')}</FormLabel>
                    <FormControl>
                      <Textarea {...field} value={field.value ?? ''} rows={2} />
                    </FormControl>
                    <FormDescription>{t('fields.introHint')}</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="body"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>{t('fields.body')}</FormLabel>
                    <FormControl>
                      <Textarea {...field} rows={14} className="font-mono text-sm" />
                    </FormControl>
                    <FormDescription>{t('fields.bodyHint')}</FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="text-base">{t('preview')}</CardTitle>
            </CardHeader>
            <CardContent>
              {body.trim() === '' ? (
                <p className="text-muted-foreground text-sm">{t('previewEmpty')}</p>
              ) : (
                <RichText body={body} />
              )}
            </CardContent>
          </Card>
        </div>

        <Card className="lg:sticky lg:top-20">
          <CardContent className="flex flex-col gap-4 pt-6">
            <FormField
              control={form.control}
              name="audience"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.audience')}</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {ANNOUNCEMENT_AUDIENCES.map((audience) => (
                        <SelectItem key={audience} value={audience}>
                          {t(`audience.${audience}`)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="publishedAt"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t('fields.date')}</FormLabel>
                  <FormControl>
                    <Input {...field} value={field.value ?? ''} type="date" />
                  </FormControl>
                  <FormDescription>{t('fields.dateHint')}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="isPublished"
              render={({ field }) => (
                <FormItem className="flex flex-row items-center gap-2">
                  <FormControl>
                    <Switch checked={field.value ?? false} onCheckedChange={field.onChange} />
                  </FormControl>
                  <FormLabel className="!mt-0">{t('fields.published')}</FormLabel>
                </FormItem>
              )}
            />

            <div className="flex flex-col gap-2 border-t pt-4">
              <Label>{t('fields.image')}</Label>
              <input
                ref={fileRef}
                type="file"
                className="hidden"
                accept={ACCEPTED.join(',')}
                onChange={(event) => {
                  const file = event.target.files?.[0];
                  if (file) void pick(file);
                }}
              />

              <p className="text-muted-foreground text-xs">
                {image.kind === 'set'
                  ? image.filename
                  : image.kind === 'clear'
                    ? t('image.removed')
                    : (announcement?.imageName ?? t('image.none'))}
              </p>

              <div className="flex gap-2">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={pending}
                  onClick={() => fileRef.current?.click()}
                  className="gap-1.5"
                >
                  <ImageIcon className="size-4" aria-hidden />
                  {t('image.choose')}
                </Button>

                {announcement?.hasImage || image.kind === 'set' ? (
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    disabled={pending}
                    onClick={() => {
                      setImage({ kind: 'clear' });
                      if (fileRef.current) fileRef.current.value = '';
                    }}
                    className="gap-1.5"
                  >
                    <X className="size-4" aria-hidden />
                    {t('image.remove')}
                  </Button>
                ) : null}
              </div>
            </div>

            <div className="flex flex-col gap-2 border-t pt-4">
              <Button type="submit" disabled={pending} className="gap-1.5">
                {pending ? <Loader2 className="size-4 animate-spin" aria-hidden /> : null}
                {tCommon('save')}
              </Button>
              <Button
                type="button"
                variant="outline"
                disabled={pending}
                onClick={() => router.back()}
              >
                {tCommon('cancel')}
              </Button>
            </div>
          </CardContent>
        </Card>
      </form>
    </Form>
  );
}
