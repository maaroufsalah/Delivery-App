'use client';

import { EXPECTED_PAYMENTS, type ExpectedPayment } from '@delivery/shared';
import { Banknote, FileText } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { cn } from '@/lib/utils';

/**
 * « PAIEMENT ATTENDU : ESPÈCES / CHÈQUE » — recette SpeedyTrans 3, point 3.
 *
 * Posée comme la question BL : pour un client Pro seulement, Espèces si rien
 * n'est touché. Deux boutons plutôt qu'une liste déroulante : il n'y a que
 * deux réponses, et on les voit toutes les deux d'un coup d'œil. Une
 * INFORMATION pour le livreur, imprimée sur l'étiquette — l'encaissement se
 * saisit comme avant.
 */
export function ExpectedPaymentField({
  value,
  onChange,
  disabled = false,
}: {
  value: ExpectedPayment | undefined;
  onChange: (value: ExpectedPayment) => void;
  disabled?: boolean;
}) {
  const t = useTranslations('parcels.expectedPayment');
  const current = value ?? 'cash';

  return (
    <fieldset className="flex flex-col gap-2" disabled={disabled}>
      <legend className="text-sm font-medium">{t('label')}</legend>
      <p className="text-muted-foreground text-xs">{t('hint')}</p>
      <div role="radiogroup" className="grid grid-cols-2 gap-2">
        {EXPECTED_PAYMENTS.map((option) => {
          const selected = option === current;
          const Icon = option === 'cheque' ? FileText : Banknote;

          return (
            <button
              key={option}
              type="button"
              role="radio"
              aria-checked={selected}
              onClick={() => onChange(option)}
              className={cn(
                'flex items-center gap-2 rounded-lg border px-3 py-2 text-start text-sm transition-colors',
                'focus-visible:ring-ring outline-none focus-visible:ring-2',
                selected ? 'border-primary bg-accent font-medium' : 'hover:bg-muted/60',
              )}
            >
              <Icon className="size-4 shrink-0" aria-hidden />
              {t(option)}
            </button>
          );
        })}
      </div>
    </fieldset>
  );
}
