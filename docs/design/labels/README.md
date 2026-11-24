# Étiquettes : Speedy contre .NET

Même colis, mêmes données, deux moteurs :

- `speedy-*.pdf` : rendus par les gabarits React-PDF de Speedy (`apps/api/src/common/pdf`), lus sans
  modification, avec leurs propres dépendances.
- `dotnet-*.pdf` : rendus par `LabelDocuments.cs` (QuestPDF), via le test `DocumentSnapshots`
  (`DOCUMENT_SNAPSHOTS=<dossier> DOCUMENT_SAMPLE=<exemple.json> dotnet test --filter DocumentSnapshots`).
- `*-pN.png` : la page N à 144 ppp, pour la comparaison à l'œil.

Écarts mesurés (positions des images lues dans les PDF) :

| Gabarit | Écart |
|---|---|
| Classique 10×10 | identique (QR et code-barres aux mêmes coordonnées) |
| Classique A4 | ≤ 0,6 pt |
| À la marque 10×10 et « A4 » | x identiques ; y à 1–3 pt : React-PDF rétrécit légèrement les codes-barres (flex-shrink, 20,6 pt au lieu de 22) quand le contenu est serré |
| Pages | QuestPDF arrondit au quart de point (283,46 → 283,5 ; 841,89 → 842) |

Reproduits de Speedy : Code 128 toujours en jeu B, 10 modules de marge ; QR niveau M en mode octet ;
pointillés de découpe en A4 ; l'étiquette « A4 » à la marque sort sur une page carrée de 595,28 pt ;
logo noirci sur la bande claire (`logoForLightBackground`).

## Documents de gestion

| Document | Écart |
|---|---|
| Bon de réception d'entreposage | identique (blocs, colonnes, cadre arrondi, pied à 20 pt du bas) |
| Feuille de route (livraison, ramassage) | identique, sauf une adresse très longue : React-PDF rétrécit la colonne du code (flex-shrink) et garde l'adresse sur une ligne ; .NET garde les colonnes et passe à la ligne |
| Situation du stock livreur | identique (récapitulatif, sections, blocs de 100 lignes, « Page n / N ») |
| Bon de retour BL (BRB-) | identique (Client Pro, livreur, état « Reçu par &lt;nom court&gt; », référence BL, deux signatures) |
| Bon de retour colis (RL-) | identique (vendeurs, cartons, statut, « Créé par &lt;nom court&gt; — auteur ») |
| Bon de retour client (BRT-) | identique (motif, frais, total des frais, « Reçu par » et signature) |
| Bon de ramassage | identique, sauf une ligne « client corrigé » : chez Speedy le code chevauche le nom et les frais passent à la ligne (même effet flex-shrink) ; .NET garde les colonnes |

Le texte arabe garde sa police ; dans ces tableaux, React-PDF le pose à gauche : .NET aussi.
