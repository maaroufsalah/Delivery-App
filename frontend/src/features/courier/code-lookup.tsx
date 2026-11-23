'use client';

import { Search } from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useRouter } from '@/i18n/navigation';

/**
 * « SAISIR UN CODE » — ce que le web met à la place du scanner (L1 bloc 2).
 *
 * Sur le téléphone, la tuile la plus utilisée de l'accueil ouvre la caméra.
 * Sur un poste d'agence, il n'y a pas de caméra utile : demander la permission
 * mènerait à un refus, et un refus à une tuile morte. Le geste équivalent est
 * de taper le code qu'on a sous les yeux, sur l'étiquette.
 *
 * **Entrée suffit.** Le bouton existe pour qui navigue à la souris, mais le
 * cas courant est une saisie au clavier suivie d'Entrée — c'est le geste d'un
 * agent qui a le colis dans une main.
 *
 * **Aucune validation ici.** Le code part tel quel vers la fiche, et c'est la
 * fiche qui dit « ce colis ne vous est pas confié » si l'API le refuse. Juger
 * du format côté navigateur ferait une seconde règle à tenir d'accord avec
 * celle du serveur, pour refuser plus tôt ce qui sera refusé de toute façon.
 */
export function CodeLookup({
  label,
  placeholder,
  submit,
}: {
  label: string;
  placeholder: string;
  submit: string;
}) {
  const router = useRouter();
  const [code, setCode] = useState('');

  const open = () => {
    const trimmed = code.trim();
    if (!trimmed) return;

    router.push(`/me/parcels/${encodeURIComponent(trimmed)}`);
  };

  return (
    <form
      className="bg-card flex min-h-20 flex-col justify-center gap-2 rounded-xl border p-4"
      onSubmit={(event) => {
        event.preventDefault();
        open();
      }}
    >
      <label htmlFor="courier-code" className="text-sm font-medium">
        {label}
      </label>

      <div className="flex gap-2">
        <Input
          id="courier-code"
          value={code}
          onChange={(event) => setCode(event.target.value)}
          placeholder={placeholder}
          autoComplete="off"
          // Les codes sont en majuscules sur l'étiquette : les imposer évite
          // une recherche vide sur une saisie minuscule.
          className="font-mono uppercase"
          dir="ltr"
        />
        <Button type="submit" size="icon" aria-label={submit} disabled={!code.trim()}>
          <Search className="size-4" aria-hidden />
        </Button>
      </div>
    </form>
  );
}
