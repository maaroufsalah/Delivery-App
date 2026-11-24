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
