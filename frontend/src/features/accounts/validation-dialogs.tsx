'use client';

import type { Agency } from '@delivery/shared';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { SearchableSelect } from '@/components/searchable-select';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';

/**
 * Valider / Refuser une demande d'inscription — C13 bloc 2.
 *
 * UN composant pour les quatre emplacements : liste et fiche, clients et
 * livreurs. Ils ne diffèrent que par l'action passée en paramètre — écrire
 * quatre dialogues aurait garanti que le motif finisse obligatoire à trois
 * endroits sur quatre.
 */

export interface ValidationResult {
  ok: boolean;
  error?: string;
  /** Champ visé par le refus de l'API. `agencyId` = il en faut une. */
  field?: string;
}

/**
 * « Valider ».
 *
 * L'agence n'est demandée QUE si la fiche n'en a pas — et c'est l'API qui le
 * dit, pas l'écran : elle refuse une première fois avec `field: 'agencyId'`, le
 * sélecteur s'ouvre, et le second appel passe. L'écran n'a donc pas à
 * reproduire la règle « une inscription publique n'a pas d'agence », qui
 * vivrait alors à deux endroits.
 */
export function ValidateDialog({
  label,
  agencies,
  onConfirm,
  onClose,
}: {
  label: string;
  agencies: Agency[];
  onConfirm: (agencyId?: string) => Promise<ValidationResult>;
  onClose: () => void;
}) {
  const t = useTranslations('accounts.validation');
  const [pending, startTransition] = useTransition();
  const [agencyId, setAgencyId] = useState('');
  const [needsAgency, setNeedsAgency] = useState(false);

  const run = () => {
    startTransition(async () => {
      const result = await onConfirm(agencyId || undefined);

      if (result.ok) {
        toast.success(t('validated'));
        onClose();
        return;
      }

      if (result.field === 'agencyId') {
        setNeedsAgency(true);
        // Pas un toast d'erreur : rien n'a échoué du point de vue de la
        // personne, il manque juste une information qu'on lui demande ici.
        return;
      }

      toast.error(result.error ?? t('failed'));
    });
  };

  return (
    <Dialog open onOpenChange={(next) => (next ? null : onClose())}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('validateTitle')}</DialogTitle>
          <DialogDescription>{t('validateHint', { label })}</DialogDescription>
        </DialogHeader>

        {needsAgency ? (
          <div className="flex flex-col gap-2">
            <Label htmlFor="validate-agency">{t('agency')}</Label>
            <SearchableSelect
              options={agencies.map((agency) => ({ value: agency.id, label: agency.name }))}
              value={agencyId}
              onValueChange={setAgencyId}
              placeholder={t('agencyPlaceholder')}
              id="validate-agency"
              className="w-full"
            />
            <p className="text-muted-foreground text-xs">{t('agencyHint')}</p>
          </div>
        ) : null}

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={pending}>
            {t('cancel')}
          </Button>
          <Button onClick={run} disabled={pending || (needsAgency && !agencyId)}>
            {t('validate')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/**
 * « Refuser ».
 *
 * Le motif est obligatoire, et le dire ici plutôt qu'en message d'erreur : le
 * bouton reste désactivé tant que le champ est vide. La personne rappellera, et
 * l'agent qui décroche lira ce texte — c'est la seule chose qu'il aura.
 */
export function RejectDialog({
  label,
  onConfirm,
  onClose,
}: {
  label: string;
  onConfirm: (reason: string) => Promise<ValidationResult>;
  onClose: () => void;
}) {
  const t = useTranslations('accounts.validation');
  const [pending, startTransition] = useTransition();
  const [reason, setReason] = useState('');

  const run = () => {
    startTransition(async () => {
      const result = await onConfirm(reason.trim());

      if (result.ok) {
        toast.success(t('rejected'));
        onClose();
        return;
      }

      toast.error(result.error ?? t('failed'));
    });
  };

  return (
    <Dialog open onOpenChange={(next) => (next ? null : onClose())}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('rejectTitle')}</DialogTitle>
          <DialogDescription>{t('rejectHint', { label })}</DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-2">
          <Label htmlFor="reject-reason">{t('reason')}</Label>
          <Textarea
            id="reject-reason"
            autoFocus
            rows={3}
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            placeholder={t('reasonPlaceholder')}
          />
          <p className="text-muted-foreground text-xs">{t('reasonHint')}</p>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={pending}>
            {t('cancel')}
          </Button>
          <Button
            variant="destructive"
            onClick={run}
            disabled={pending || reason.trim().length === 0}
          >
            {t('reject')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
