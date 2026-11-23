'use client';

import {
  PERMISSION_MODULES,
  type PermissionKey,
  type PermissionModule,
  type UserPermissions,
} from '@delivery/shared';
import { Loader2, RotateCcw, ShieldCheck } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useMemo, useState, useTransition } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { resetUserPermissionsAction, updateUserPermissionsAction } from '@/lib/actions/accounts';
import { cn } from '@/lib/utils';

/**
 * Le bouton « Permissions » de la fiche Utilisateur — « Dernières
 * corrections », point 1 (modèle Wimo).
 *
 * Chaque case est le droit EFFECTIF du compte. La matrice des rôles reste la
 * base : cocher ce que le rôle ne donne pas pose un octroi, décocher ce qu'il
 * donne pose un retrait, et la mention à côté de la case dit laquelle des deux
 * origines la tient. L'API déduit les écarts par différence : l'écran poste
 * seulement ce qu'il affiche.
 */
export function UserPermissionsButton({
  initial,
  canManage,
}: {
  initial: UserPermissions;
  /** `accounts.write`. Sans lui, le modal se lit mais ne s'écrit pas. */
  canManage: boolean;
}) {
  const t = useTranslations('accounts.userPermissions');
  const [open, setOpen] = useState(false);

  return (
    <>
      <Button variant="outline" size="sm" className="gap-1.5" onClick={() => setOpen(true)}>
        <ShieldCheck className="size-4" aria-hidden />
        {t('button')}
      </Button>
      {open ? (
        <UserPermissionsDialog
          initial={initial}
          canManage={canManage}
          onClose={() => setOpen(false)}
        />
      ) : null}
    </>
  );
}

function UserPermissionsDialog({
  initial,
  canManage,
  onClose,
}: {
  initial: UserPermissions;
  canManage: boolean;
  onClose: () => void;
}) {
  const t = useTranslations('accounts.userPermissions');
  const tPerm = useTranslations('accounts.roles.permissions');
  const tModule = useTranslations('accounts.roles.modules');
  const [pending, startTransition] = useTransition();

  // La dernière réponse de l'API : c'est elle qui dit l'origine de chaque case.
  const [data, setData] = useState(initial);
  const [effective, setEffective] = useState(() => new Set(initial.effective));

  const readOnly = !canManage || data.locked;
  const fromRoles = useMemo(() => new Set(data.fromRoles), [data.fromRoles]);
  const saved = useMemo(() => new Set(data.effective), [data.effective]);

  const changes = useMemo(() => {
    let count = 0;
    for (const key of effective) if (!saved.has(key)) count += 1;
    for (const key of saved) if (!effective.has(key)) count += 1;
    return count;
  }, [effective, saved]);

  const groups = useMemo(
    () =>
      PERMISSION_MODULES.filter((module) => module !== 'platform')
        .map((module) => ({
          module,
          permissions: data.permissions.filter((permission) => permission.module === module),
        }))
        .filter((group) => group.permissions.length > 0),
    [data.permissions],
  );

  const toggle = (key: PermissionKey, checked: boolean) => {
    setEffective((current) => {
      const next = new Set(current);
      if (checked) next.add(key);
      else next.delete(key);
      return next;
    });
  };

  const apply = (next: UserPermissions, message: string) => {
    setData(next);
    setEffective(new Set(next.effective));
    toast.success(message);
  };

  const save = () => {
    startTransition(async () => {
      const result = await updateUserPermissionsAction(data.userId, {
        effective: [...effective],
      });
      if (result.ok) apply(result.data, t('saved'));
      else toast.error(result.error);
    });
  };

  const reset = () => {
    startTransition(async () => {
      const result = await resetUserPermissionsAction(data.userId);
      if (result.ok) apply(result.data, t('resetDone'));
      else toast.error(result.error);
    });
  };

  /** L'origine d'une case, telle qu'elle SERAIT enregistrée. */
  const originOf = (key: PermissionKey): 'role' | 'granted' | 'revoked' | null => {
    const byRole = fromRoles.has(key);
    const on = effective.has(key);
    if (on && byRole) return 'role';
    if (on && !byRole) return 'granted';
    if (!on && byRole) return 'revoked';
    return null;
  };

  const specificCount = data.grants.length + data.revokes.length;

  return (
    <Dialog open onOpenChange={(next) => (next ? null : onClose())}>
      <DialogContent className="flex max-h-[90dvh] flex-col sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t('title')}</DialogTitle>
          <DialogDescription>{data.locked ? t('locked') : t('hint')}</DialogDescription>
        </DialogHeader>

        <div className="-mx-6 min-h-0 flex-1 overflow-y-auto border-y">
          {groups.map((group) => (
            <fieldset key={group.module}>
              <legend className="text-muted-foreground bg-muted/50 w-full px-6 py-1.5 text-xs font-semibold tracking-wide uppercase">
                {tModule(group.module as PermissionModule)}
              </legend>

              {group.permissions.map((permission) => {
                const key = permission.key;
                const origin = originOf(key);
                const id = `perm-${key}`;

                return (
                  <div
                    key={key}
                    className="hover:bg-accent/40 flex items-center gap-3 border-t px-6 py-2 text-sm"
                  >
                    <Checkbox
                      id={id}
                      checked={effective.has(key)}
                      disabled={readOnly || pending}
                      onCheckedChange={(next) => toggle(key, next === true)}
                    />
                    <label htmlFor={id} className="min-w-0 flex-1 cursor-pointer">
                      {tPerm(key)}
                    </label>
                    {origin ? (
                      <span
                        className={cn(
                          'shrink-0 rounded-full px-2 py-0.5 text-xs',
                          origin === 'role' && 'text-muted-foreground bg-muted',
                          origin === 'granted' && 'bg-primary/10 text-primary',
                          origin === 'revoked' && 'bg-destructive/10 text-destructive',
                        )}
                      >
                        {t(`origin.${origin}`)}
                      </span>
                    ) : null}
                  </div>
                );
              })}
            </fieldset>
          ))}
        </div>

        <DialogFooter className="gap-2 sm:justify-between">
          {readOnly ? (
            <span />
          ) : (
            <Button
              variant="ghost"
              className="gap-1.5"
              onClick={reset}
              disabled={pending || specificCount === 0}
            >
              <RotateCcw className="size-4" aria-hidden />
              {t('reset')}
            </Button>
          )}
          <div className="flex items-center justify-end gap-2">
            <Button variant="outline" onClick={onClose} disabled={pending}>
              {t('close')}
            </Button>
            {readOnly ? null : (
              <Button onClick={save} disabled={pending || changes === 0} className="gap-1.5">
                {pending ? <Loader2 className="size-4 animate-spin" /> : null}
                {changes > 0 ? t('saveCount', { count: changes }) : t('save')}
              </Button>
            )}
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
