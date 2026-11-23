'use client';

import {
  PERMISSION_MODULES,
  type PermissionKey,
  type PermissionModule,
  type RoleMatrix,
  TENANT_ROLES,
  type UserRole,
} from '@delivery/shared';
import { Loader2, ShieldCheck } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useMemo, useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { updateRoleMatrixAction } from '@/lib/actions/accounts';
import { cn } from '@/lib/utils';

type Granted = Record<UserRole, PermissionKey[]>;

/** Comparaison rôle par rôle, pour compter ce qui a bougé depuis le chargement. */
function countChanges(initial: Granted, current: Granted): number {
  let changes = 0;

  for (const role of TENANT_ROLES) {
    const before = new Set(initial[role] ?? []);
    const after = new Set(current[role] ?? []);

    for (const key of before) if (!after.has(key)) changes += 1;
    for (const key of after) if (!before.has(key)) changes += 1;
  }

  return changes;
}

/**
 * Matrice rôle × permission — DOMAIN.md §4 dette n°13.
 *
 * Ce n'est ni une liste ni une grille de saisie : ni `DataTable` ni
 * `MatrixGrid` n'exprimaient une case à cocher à l'intersection de deux axes
 * de nature différente. C'est donc une grille CSS, et non un `<table>`
 * (CLAUDE.md §2) : chaque case porte son propre libellé accessible
 * « permission — rôle », ce que la lecture d'un tableau ne donnerait pas mieux.
 *
 * L'écran poste la matrice ENTIÈRE, jamais des deltas : deux administrateurs
 * qui éditent en même temps aboutiraient sinon à un état dépendant de l'ordre
 * des requêtes.
 */
export function RolesScreen({ matrix }: { matrix: RoleMatrix }) {
  const t = useTranslations('accounts.roles');
  const tPerm = useTranslations('accounts.roles.permissions');
  const tModule = useTranslations('accounts.roles.modules');
  const tRole = useTranslations('accounts.roles.names');

  const [granted, setGranted] = useState<Granted>(matrix.granted);
  const [saving, setSaving] = useState(false);

  const changes = countChanges(matrix.granted, granted);

  /**
   * Le catalogue vient de l'API : il fait foi sur ce qui existe réellement.
   *
   * Le module `platform` en est RETIRÉ (C8, D-048) : il administre les tenants,
   * pas la société. L'afficher ici proposerait au propriétaire d'une société de
   * s'accorder l'accès à la console des autres — que l'API refuserait, mais une
   * case à cocher qui ne coche rien est un mensonge.
   */
  const groups = useMemo(() => {
    return PERMISSION_MODULES.filter((module) => module !== 'platform')
      .map((module) => ({
        module,
        permissions: matrix.permissions.filter((permission) => permission.module === module),
      }))
      .filter((group) => group.permissions.length > 0);
  }, [matrix.permissions]);

  const toggle = (role: UserRole, key: PermissionKey, checked: boolean) => {
    setGranted((current) => {
      const keys = new Set(current[role] ?? []);
      if (checked) keys.add(key);
      else keys.delete(key);
      return { ...current, [role]: [...keys] };
    });
  };

  const save = async () => {
    setSaving(true);
    const result = await updateRoleMatrixAction({ granted });
    setSaving(false);

    if (result.ok) {
      // La réponse porte la matrice telle qu'elle a été ÉCRITE — super-admin
      // complété compris. L'afficher évite de laisser croire à un état que la
      // base n'a pas retenu.
      setGranted(result.data.granted);
      toast.success(t('saved'));
      return;
    }

    toast.error(result.error);
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="bg-muted/40 text-muted-foreground flex items-start gap-2 rounded-lg border p-3 text-sm">
        <ShieldCheck className="mt-0.5 size-4 shrink-0" />
        <p>{t('superAdminHint')}</p>
      </div>

      {/* Le défilement horizontal est assumé : 4 rôles et un libellé lisible ne
          tiennent pas dans 375 px, et masquer une colonne rendrait la matrice
          fausse — une case non vue est un droit non vérifié. */}
      <div className="bg-card overflow-x-auto rounded-lg border">
        <div className="min-w-xl">
          <div
            className="bg-primary text-primary-foreground sticky top-0 z-10 grid items-center gap-2 px-3 py-2 text-xs font-semibold"
            style={{
              gridTemplateColumns: `minmax(12rem, 1fr) repeat(${TENANT_ROLES.length}, 6rem)`,
            }}
          >
            <span>{t('title')}</span>
            {TENANT_ROLES.map((role) => (
              <span key={role} className="text-center leading-tight">
                {tRole(role)}
              </span>
            ))}
          </div>

          {groups.map((group) => (
            <fieldset key={group.module} className="border-t">
              <legend className="sr-only">{tModule(group.module as PermissionModule)}</legend>

              <p className="text-muted-foreground bg-muted/50 px-3 py-1.5 text-xs font-semibold tracking-wide uppercase">
                {tModule(group.module as PermissionModule)}
              </p>

              {group.permissions.map((permission) => (
                <div
                  key={permission.key}
                  className="hover:bg-accent/40 grid items-center gap-2 border-t px-3 py-2 text-sm"
                  style={{
                    gridTemplateColumns: `minmax(12rem, 1fr) repeat(${TENANT_ROLES.length}, 6rem)`,
                  }}
                >
                  <span>{tPerm(permission.key)}</span>

                  {TENANT_ROLES.map((role) => {
                    // Le super-administrateur garde tout : l'API recompose sa
                    // ligne quoi qu'on poste, l'écran ne fait donc pas semblant
                    // de la laisser modifiable.
                    const locked = role === 'SUPER_ADMIN';
                    const checked = locked || (granted[role] ?? []).includes(permission.key);

                    return (
                      <div key={role} className="flex justify-center">
                        <Checkbox
                          checked={checked}
                          disabled={locked || saving}
                          aria-label={`${tPerm(permission.key)}, ${tRole(role)}`}
                          onCheckedChange={(next) => toggle(role, permission.key, next === true)}
                        />
                      </div>
                    );
                  })}
                </div>
              ))}
            </fieldset>
          ))}
        </div>
      </div>

      {/* Barre d'enregistrement collée au bas : sur un téléphone, un bouton
          « enregistrer » situé après 18 lignes de cases n'existe pas. */}
      <div className="bg-background/95 sticky bottom-0 flex items-center justify-between gap-3 border-t py-3 backdrop-blur">
        <span
          className={cn('text-sm', changes > 0 ? 'text-foreground' : 'text-muted-foreground')}
          aria-live="polite"
        >
          {changes > 0 ? t('dirty', { count: changes }) : null}
        </span>

        <Button onClick={save} disabled={saving || changes === 0} className="gap-1.5">
          {saving ? <Loader2 className="size-4 animate-spin" /> : null}
          {t('save')}
        </Button>
      </div>
    </div>
  );
}
