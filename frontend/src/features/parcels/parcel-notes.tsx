'use client';

import { addParcelNoteSchema, type AddParcelNoteInput, type ParcelNote } from '@delivery/shared';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2, Paperclip, Send } from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useTransition } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'sonner';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Textarea } from '@/components/ui/textarea';
import { addParcelNoteAction } from '@/lib/actions/parcels';

/**
 * Notes internes — DOMAIN.md §1.11.
 *
 * Section SÉPARÉE de la timeline : le legacy mélangeait les deux dans
 * `colis_trakings`, ce qui rendait l'historique de statut illisible. Une note
 * ne change aucun statut et ne sort jamais vers le suivi public.
 */
export function ParcelNotes({ code, notes }: { code: string; notes: ParcelNote[] }) {
  const t = useTranslations('parcels.detail.notes');
  const tErrors = useTranslations('parcels.errors');
  const format = useFormatter();
  const [pending, startTransition] = useTransition();

  const form = useForm<AddParcelNoteInput>({
    resolver: zodResolver(addParcelNoteSchema),
    defaultValues: { body: '' },
  });

  function onSubmit(values: AddParcelNoteInput) {
    startTransition(async () => {
      const result = await addParcelNoteAction(code, values);
      if (result.ok) {
        form.reset({ body: '' });
        toast.success(t('added'));
      } else {
        toast.error(tErrors(result.error === 'invalid' ? 'invalid' : 'unexpected'));
      }
    });
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('title')}</CardTitle>
        <CardDescription>{t('description')}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-5">
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-3">
            <FormField
              control={form.control}
              name="body"
              render={({ field }) => (
                <FormItem>
                  <FormLabel className="sr-only">{t('placeholder')}</FormLabel>
                  <FormControl>
                    <Textarea {...field} rows={3} placeholder={t('placeholder')} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <div className="flex justify-end">
              <Button type="submit" size="sm" className="gap-1.5" disabled={pending}>
                {pending ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  <Send className="flip-rtl size-4" />
                )}
                {t('add')}
              </Button>
            </div>
          </form>
        </Form>

        {notes.length === 0 ? (
          <p className="text-muted-foreground text-sm">{t('empty')}</p>
        ) : (
          <ul className="flex flex-col gap-3">
            {notes.map((note) => (
              <li key={note.id} className="bg-muted/50 rounded-lg border p-3">
                <div className="text-muted-foreground flex flex-wrap items-baseline justify-between gap-2 text-xs">
                  <span className="font-medium">{note.authorName}</span>
                  <time dateTime={note.createdAt} className="tabular-nums">
                    {format.dateTime(new Date(note.createdAt), 'stamp')}
                  </time>
                </div>
                <p className="mt-1.5 text-sm whitespace-pre-line">{note.body}</p>
                {note.attachmentName ? (
                  <span className="text-muted-foreground mt-2 inline-flex max-w-full items-center gap-1 text-xs">
                    <Paperclip className="size-3 shrink-0" aria-hidden />
                    <span className="truncate">{note.attachmentName}</span>
                  </span>
                ) : null}
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
