'use client';

import { CLAIM_SUBJECT_TYPES, type ClaimType, type ClaimTypeInput } from '@delivery/shared';
import { Loader2, Plus, Save, Trash2 } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
import { deleteClaimTypeAction, saveClaimTypeAction } from '@/lib/actions/claims';

/**
 * Référentiel des types de réclamation — D-019.
 *
 * Les six types seedés sont un POINT DE DÉPART : chaque transporteur nomme ses
 * motifs comme il les vit. Le legacy les avait déjà en table, mais y accrochait
 * un `varchar` CSV de statuts atteignables (§4 dette n°15) — celui-là n'est pas
 * repris, le parcours v2 est le même pour tous.
 *
 * `subjectType` porte la seule règle structurante : un type « colis » exige un
 * code colis à l'ouverture, un type « général » n'en demande pas.
 */

interface Draft {
  id: string | null;
  code: string;
  label: string;
  subjectType: ClaimTypeInput['subjectType'];
  sortOrder: number;
  isActive: boolean;
}

function draftOf(type: ClaimType): Draft {
  return {
    id: type.id,
    code: type.code,
    label: type.label,
    subjectType: type.subjectType,
    sortOrder: type.sortOrder,
    isActive: type.isActive,
  };
}

export function ClaimTypesForm({ types }: { types: ClaimType[] }) {
  const t = useTranslations('settings.claims');
  const tCommon = useTranslations('common');
  const [rows, setRows] = useState<Draft[]>(types.map(draftOf));
  const [pending, startTransition] = useTransition();

  const patch = (index: number, changes: Partial<Draft>) => {
    setRows((current) =>
      current.map((row, position) => (position === index ? { ...row, ...changes } : row)),
    );
  };

  const save = (index: number) => {
    const row = rows[index];
    if (!row) return;

    startTransition(async () => {
      const result = await saveClaimTypeAction(row.id, {
        code: row.code,
        label: row.label,
        subjectType: row.subjectType,
        sortOrder: row.sortOrder,
        isActive: row.isActive,
      });

      if (!result.ok) {
        toast.error(result.error);
        return;
      }

      patch(index, { id: result.data.id });
      toast.success(tCommon('save'));
    });
  };

  const remove = (index: number) => {
    const row = rows[index];
    if (!row) return;

    // Jamais créé en base : la ligne n'existe que dans cet écran.
    if (!row.id) {
      setRows((current) => current.filter((_, position) => position !== index));
      return;
    }

    startTransition(async () => {
      const result = await deleteClaimTypeAction(row.id as string);

      if (!result.ok) {
        // L'API refuse le retrait d'un type encore cité : elle dit par combien
        // de réclamations, et l'écran propose la désactivation à la place.
        toast.error(result.error);
        return;
      }

      setRows((current) => current.filter((_, position) => position !== index));
    });
  };

  return (
    <div className="flex flex-col gap-3">
      {rows.map((row, index) => (
        <Card key={row.id ?? `draft-${index}`}>
          <CardContent className="flex flex-col gap-3 pt-5 md:flex-row md:items-end">
            <div className="flex flex-1 flex-col gap-1.5">
              <Label htmlFor={`claim-type-label-${index}`}>{t('fields.label')}</Label>
              <Input
                id={`claim-type-label-${index}`}
                value={row.label}
                onChange={(event) => patch(index, { label: event.target.value })}
                disabled={pending}
              />
            </div>

            <div className="flex flex-col gap-1.5 md:w-44">
              <Label htmlFor={`claim-type-code-${index}`}>{t('fields.code')}</Label>
              <Input
                id={`claim-type-code-${index}`}
                value={row.code}
                onChange={(event) => patch(index, { code: event.target.value })}
                // Le code est la clé STABLE : on ne le change pas sous des
                // dossiers déjà ouverts.
                disabled={pending || row.id !== null}
                className="font-mono"
              />
            </div>

            <div className="flex flex-col gap-1.5 md:w-48">
              <Label htmlFor={`claim-type-subject-${index}`}>{t('fields.subjectType')}</Label>
              <Select
                value={row.subjectType}
                onValueChange={(value) =>
                  patch(index, { subjectType: value as Draft['subjectType'] })
                }
                disabled={pending}
              >
                <SelectTrigger id={`claim-type-subject-${index}`}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {CLAIM_SUBJECT_TYPES.map((subject) => (
                    <SelectItem key={subject} value={subject}>
                      {t(`subject.${subject}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="flex items-center gap-2 md:pb-2.5">
              <Switch
                id={`claim-type-active-${index}`}
                checked={row.isActive}
                onCheckedChange={(value) => patch(index, { isActive: value })}
                disabled={pending}
              />
              <Label htmlFor={`claim-type-active-${index}`} className="text-sm">
                {tCommon('active')}
              </Label>
            </div>

            <div className="flex gap-2 md:pb-0.5">
              <Button size="sm" onClick={() => save(index)} disabled={pending} className="gap-1.5">
                {pending ? (
                  <Loader2 className="size-4 animate-spin" aria-hidden />
                ) : (
                  <Save className="size-4" aria-hidden />
                )}
                {tCommon('save')}
              </Button>
              <Button
                size="sm"
                variant="ghost"
                onClick={() => remove(index)}
                disabled={pending}
                aria-label={tCommon('delete')}
              >
                <Trash2 className="size-4" aria-hidden />
              </Button>
            </div>
          </CardContent>
        </Card>
      ))}

      <Button
        variant="outline"
        className="gap-1.5 self-start"
        disabled={pending}
        onClick={() =>
          setRows((current) => [
            ...current,
            {
              id: null,
              code: '',
              label: '',
              subjectType: 'parcel',
              sortOrder: current.length,
              isActive: true,
            },
          ])
        }
      >
        <Plus className="size-4" aria-hidden />
        {t('add')}
      </Button>
    </div>
  );
}
