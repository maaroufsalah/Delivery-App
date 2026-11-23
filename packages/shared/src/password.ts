/**
 * LA borne de longueur d'un mot de passe — une seule, pour tout le produit.
 *
 * C13 laissait deux politiques derrière lui : huit caractères à l'inscription
 * publique, dix partout ailleurs (création par un administrateur, mot de passe
 * redéfini, « Mon profil »). L'écart n'était pas une règle de sécurité, c'était
 * un accident de calendrier — et il se payait sur le dos de l'inscrit : le mot
 * de passe qu'on venait de lui laisser choisir à huit caractères lui était
 * refusé le jour où il voulait le redonner depuis son profil. Une politique qui
 * refuse de reproduire ce qu'elle a elle-même accepté n'est pas une politique,
 * c'est un piège.
 *
 * **Huit, donc** : la borne du geste le plus permissif l'emporte, parce que
 * l'autre sens invaliderait des mots de passe déjà en base — c'est-à-dire des
 * comptes qui se connectent aujourd'hui.
 *
 * **Aucune autre règle de complexité ici.** Ni chiffre, ni majuscule, ni
 * caractère spécial exigés : le durcissement est **B-15**, il reste ouvert au
 * BACKLOG, et il se décidera d'un bloc — borne, complexité et message — plutôt
 * qu'en ajoutant une contrainte à la fois sous des écrans qui ne l'annoncent
 * pas.
 *
 * Le nombre ne se recopie nulle part : les schémas zod l'importent, et les
 * catalogues de traduction le reçoivent en paramètre `{min}`.
 */
export const PASSWORD_MIN_LENGTH = 8;
