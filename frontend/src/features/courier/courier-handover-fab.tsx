'use client';

import { Plus } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { CourierHandoverWizard } from '@/features/courier/courier-handover-wizard';

/**
 * LE BOUTON FLOTTANT « + CRÉER UN BON DE RETOUR » — chantier « Bon de retour
 * livreur », point 1.
 *
 * **En bas à droite, et il flotte.** C'est la demande, et elle est juste : le
 * livreur ouvre cet onglet pour faire UNE chose, et il la fait d'une main, en
 * tenant un carton de l'autre. Un bouton dans l'en-tête l'obligerait à remonter
 * une liste qui défile.
 *
 * **`end-4` et non `right-4`** : en arabe l'interface se retourne, et le pouce
 * aussi (CLAUDE.md §5). Le bouton passe à gauche sans que personne y pense.
 *
 * Il ne s'affiche QUE sur l'onglet des bons : ailleurs, il proposerait une
 * action sans rapport avec ce qu'on regarde.
 */
export function CourierHandoverFab() {
  const t = useTranslations('courier.returns');
  const [creating, setCreating] = useState(false);

  return (
    <>
      <Button
        size="lg"
        className="fixed end-4 bottom-20 z-30 gap-1.5 rounded-full shadow-lg sm:bottom-6"
        onClick={() => setCreating(true)}
      >
        <Plus className="size-5" aria-hidden />
        {t('newHandover')}
      </Button>

      {creating ? <CourierHandoverWizard onDone={() => setCreating(false)} /> : null}
    </>
  );
}
