import {
  COURIER_STATUS_FAMILIES,
  courierStatusColor,
  courierStatusMembers,
} from '@delivery/shared';
import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { Card, CardContent } from '@/components/ui/card';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('courier.statuses');
  return { title: t('title') };
}

/**
 * LES STATUTS DES COLIS — L2 bloc 9, maquette 14, le miroir de l'écran mobile.
 *
 * **Le même regroupement, par la même table.** `COURIER_STATUS_FAMILIES` vit
 * dans `packages/shared` et un test vérifie que sa partition reste exacte : les
 * deux supports montrent donc les mêmes neuf familles, dans le même ordre, avec
 * les mêmes couleurs. Une seconde table ici aurait divergé au premier statut
 * ajouté au référentiel — et la divergence ne se serait vue nulle part.
 *
 * **Déplié d'emblée, contrairement au mobile.** Sur un téléphone, neuf lignes
 * plus trente-quatre sous-lignes demandent de replier ; sur un écran large,
 * tout tient, et le tableau de correspondance est justement ce qu'on vient
 * chercher.
 */
export default async function CourierStatusesPage() {
  const [t, tFamily, tHint, tStatus] = await Promise.all([
    getTranslations('courier.statuses'),
    getTranslations('courier.statusFamily'),
    /*
     * Les explications dans leur propre objet — recette 4, point 3. Elles
     * vivaient sous `statusFamily` en `"new.hint"` : next-intl lit le point
     * comme une imbrication, et l'écran affichait la clé brute.
     */
    getTranslations('courier.statusFamilyHint'),
    getTranslations('status'),
  ]);

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold tracking-tight">{t('title')}</h1>
        <p className="text-muted-foreground text-sm">{t('hint')}</p>
      </header>

      <ul className="flex flex-col gap-2">
        {COURIER_STATUS_FAMILIES.map((family) => {
          const color = courierStatusColor(family);

          return (
            <li key={family.key}>
              <Card>
                <CardContent className="flex flex-col gap-2 p-4">
                  <div className="flex items-start gap-3">
                    {/* La pastille de la maquette. Sa couleur vient du
                        référentiel, et un test garantit qu'elle se voit sur
                        les deux fonds. */}
                    <span
                      className="mt-1 size-3.5 shrink-0 rounded-full"
                      style={{ backgroundColor: color }}
                      aria-hidden
                    />

                    <span className="min-w-0 flex-1">
                      <span className="block text-sm font-semibold">{tFamily(family.key)}</span>
                      <span className="text-muted-foreground block text-sm">
                        {tHint(family.key)}
                      </span>
                    </span>
                  </div>

                  {/*
                    LES MEMBRES, et c'est tout l'intérêt de l'écran : le badge
                    d'une fiche colis porte le libellé PRÉCIS du référentiel —
                    « injoignable », « hors zone » — et ceci est le seul endroit
                    qui dit à quelle famille il appartient.
                  */}
                  <div className="flex flex-wrap items-center gap-1.5 ps-6.5">
                    <span className="text-muted-foreground text-xs">{t('covers')}</span>
                    {courierStatusMembers(family).map((member) => (
                      <span
                        key={member.id}
                        className="rounded-full border px-2 py-0.5 text-xs"
                        style={{ borderColor: member.color, color: member.color }}
                      >
                        {tStatus(member.key)}
                      </span>
                    ))}
                  </div>
                </CardContent>
              </Card>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
