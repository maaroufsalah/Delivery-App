# Chantier « Report des factures négatives » (portage de Speedy 8b566dc, 0dc0628, 50c23bb)

## Règle

- Une facture client à net < 0 d'un vendeur ordinaire n'est jamais réglée : à l'émission elle passe `CARRIED_OVER`
  (« Reportée »), ses colis passent remboursés, et la prochaine facture émise du vendeur porte une ligne hors colis
  `CARRY_OVER` « Report FA-xxxx : −X MAD » (sans TVA, hors base TVA pro), `carriedIntoId` posé sous transaction
  (refus 409 `carry-over-already-claimed` si une émission concurrente l'a pris).
- Les reports s'additionnent ; une suivante encore négative est reportée à son tour.
- Exclus par type (`Domain/Finance/CarryOver.Applies`) : `isPro`, `isInternal`, régime `PROFESSIONAL`.
- Règlement refusé (409 `invoice-negative-carried-over`) sur une facture reportée ou émise à net < 0 d'un vendeur ordinaire.
- Règlement annulé (`cancelledAt`) : filtre global EF, hors de toutes les sommes et listes.
- En-tête du jour sans la facture reportée ; OV « factures » ne prend jamais une reportée ; OV « sélection » et
  candidats de la génération en masse retranchent les reports en attente.
- `PATCH /clients/:id/internal` (`finance.validate`, 403 sinon), journalisé par l'audit.
- État « Reportée sur FA-… » / « Reportée sur la prochaine facture » : écran, PDF, Excel ; mobile « Reportée sur votre
  prochaine facture ». Frontend, mobile et `packages/shared` : diff Speedy appliqué à l'identique.

## Rattrapage

`scripts/rattrapage-report-negatives.sql` (simulation par défaut, ROLLBACK ; `-v apply=1 -v expect=n:total`) : à lancer
après déploiement de la migration `CarryOverNegativeInvoices` et après un dump. Règlements annulés, jamais supprimés.
