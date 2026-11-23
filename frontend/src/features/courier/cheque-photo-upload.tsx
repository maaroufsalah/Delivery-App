'use client';

import { Camera, Check, Loader2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useRef, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { attachChequePhotoAction } from '@/lib/actions/finance';

/** Deux mégaoctets : une photo de chèque, pas un scan d'archive. */
const MAX_BYTES = 2 * 1024 * 1024;

const ACCEPTED = ['image/jpeg', 'image/png', 'image/webp'];

/**
 * La photo du chèque, prise depuis le téléphone — C11 bloc 7, D-068.
 *
 * **`capture="environment"`** ouvre l'appareil photo arrière directement, sans
 * passer par la galerie. C'est l'attribut qui fait la différence entre « je
 * photographie mon chèque » et « je photographie mon chèque, je le retrouve
 * dans mes fichiers, je le sélectionne » — trois gestes debout dans une agence.
 * Sur un ordinateur, l'attribut est ignoré et le sélecteur de fichiers s'ouvre :
 * la même page sert les deux.
 *
 * La taille est vérifiée AVANT l'envoi. Un téléphone récent produit des photos
 * de huit mégaoctets, et les envoyer en base64 sur une 3G d'agence est le genre
 * d'attente qui fait abandonner — autant le dire tout de suite.
 */
export function ChequePhotoUpload({
  code,
  hasPhoto,
}: {
  code: string;
  /** Une photo est déjà attachée : le bouton propose de la REMPLACER. */
  hasPhoto: boolean;
}) {
  const t = useTranslations('courier.cheque');
  const input = useRef<HTMLInputElement>(null);
  const [done, setDone] = useState(hasPhoto);
  const [pending, startTransition] = useTransition();

  const send = (file: File) => {
    if (!ACCEPTED.includes(file.type)) {
      toast.error(t('wrongType'));
      return;
    }

    if (file.size > MAX_BYTES) {
      toast.error(t('tooLarge'));
      return;
    }

    startTransition(async () => {
      const buffer = await file.arrayBuffer();
      // `btoa` sur une chaîne binaire : la conversion se fait par tranches,
      // parce qu'un `String.fromCharCode(...)` sur deux mégaoctets d'un coup
      // dépasse la taille d'appel maximale et lève un RangeError.
      const bytes = new Uint8Array(buffer);
      let binary = '';
      for (let i = 0; i < bytes.length; i += 8192) {
        binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
      }

      const result = await attachChequePhotoAction(code, {
        proof: { name: file.name, mimeType: file.type, content: btoa(binary) },
      });

      if (result.ok) {
        setDone(true);
        toast.success(t('sent'));
      } else {
        toast.error(result.error);
      }
    });
  };

  return (
    <>
      <input
        ref={input}
        type="file"
        accept={ACCEPTED.join(',')}
        capture="environment"
        className="hidden"
        onChange={(event) => {
          const file = event.target.files?.[0];
          if (file) send(file);
          // Remis à zéro : reprendre la MÊME photo doit relancer l'envoi, et
          // un `input file` ne déclenche pas `change` sur une valeur identique.
          event.target.value = '';
        }}
      />

      <Button
        type="button"
        size="sm"
        variant="outline"
        className="min-h-11 w-full gap-1.5"
        disabled={pending}
        onClick={() => input.current?.click()}
      >
        {pending ? (
          <Loader2 className="size-4 animate-spin" aria-hidden />
        ) : done ? (
          <Check className="size-4" aria-hidden />
        ) : (
          <Camera className="size-4" aria-hidden />
        )}
        {done ? t('replace') : t('take')}
      </Button>
    </>
  );
}
