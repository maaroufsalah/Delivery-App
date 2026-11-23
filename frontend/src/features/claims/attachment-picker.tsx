'use client';

import type { ClaimAttachmentPayload } from '@delivery/shared';
import { Paperclip, X } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useRef, useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';

/**
 * Sélecteur de pièces jointes — trois photos d'un colis écrasé, pas une.
 *
 * Les fichiers sont encodés en base64 dans le navigateur et voyagent dans le
 * corps JSON de la Server Action (D-022) : accepter du `multipart` imposerait
 * `multer` côté API pour quelques centaines de kilo-octets.
 *
 * Rien n'est envoyé ici : le composant ne fait que composer la charge utile que
 * le formulaire postera. Tant que l'utilisateur n'a pas validé, aucun octet
 * n'a quitté son poste.
 */

const ACCEPTED = ['image/jpeg', 'image/png', 'image/webp', 'application/pdf'] as const;
const MAX_BYTES = 4 * 1024 * 1024;
const MAX_FILES = 5;

export function AttachmentPicker({
  value,
  onChange,
  disabled,
  max = MAX_FILES,
  hint,
}: {
  value: ClaimAttachmentPayload[];
  onChange: (next: ClaimAttachmentPayload[]) => void;
  disabled?: boolean;
  /** Réutilisé par la petite caisse, qui n'accepte qu'UN justificatif. */
  max?: number;
  /** Remplace l'aide par défaut, qui parle de cinq fichiers. */
  hint?: string;
}) {
  const t = useTranslations('claims.attachments');
  const inputRef = useRef<HTMLInputElement>(null);
  const [reading, setReading] = useState(false);

  const add = async (files: FileList) => {
    if (value.length + files.length > max) {
      toast.error(t('tooMany', { count: max }));
      return;
    }

    setReading(true);
    const next: ClaimAttachmentPayload[] = [];

    for (const file of Array.from(files)) {
      if (!(ACCEPTED as readonly string[]).includes(file.type)) {
        toast.error(t('unsupported', { name: file.name }));
        continue;
      }

      if (file.size > MAX_BYTES) {
        toast.error(t('tooLarge', { name: file.name }));
        continue;
      }

      const bytes = new Uint8Array(await file.arrayBuffer());
      // Par tranches : `String.fromCharCode(...bytes)` sur une photo de
      // plusieurs mégaoctets dépasse la taille maximale d'une liste d'arguments.
      let binary = '';
      for (let index = 0; index < bytes.length; index += 8192) {
        binary += String.fromCharCode(...bytes.subarray(index, index + 8192));
      }

      next.push({
        filename: file.name.slice(0, 200),
        mimeType: file.type as ClaimAttachmentPayload['mimeType'],
        content: btoa(binary),
      });
    }

    setReading(false);
    if (inputRef.current) inputRef.current.value = '';
    if (next.length > 0) onChange([...value, ...next]);
  };

  return (
    <div className="flex flex-col gap-2">
      <input
        ref={inputRef}
        type="file"
        multiple={max > 1}
        className="hidden"
        accept={ACCEPTED.join(',')}
        onChange={(event) => {
          if (event.target.files && event.target.files.length > 0) void add(event.target.files);
        }}
      />

      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={disabled || reading || value.length >= max}
        onClick={() => inputRef.current?.click()}
        className="gap-1.5 self-start"
      >
        <Paperclip className="size-4" aria-hidden />
        {t('add')}
      </Button>

      {value.length > 0 ? (
        <ul className="flex flex-col gap-1">
          {value.map((file, index) => (
            <li
              key={`${file.filename}-${index}`}
              className="bg-muted/40 flex items-center gap-2 rounded-md px-2 py-1 text-sm"
            >
              <Paperclip className="text-muted-foreground size-3.5 shrink-0" aria-hidden />
              <span className="truncate">{file.filename}</span>
              <button
                type="button"
                disabled={disabled}
                onClick={() => onChange(value.filter((_, position) => position !== index))}
                aria-label={t('remove', { name: file.filename })}
                className="hover:text-destructive ms-auto shrink-0 rounded p-0.5"
              >
                <X className="size-3.5" aria-hidden />
              </button>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-muted-foreground text-xs">{hint ?? t('hint')}</p>
      )}
    </div>
  );
}
