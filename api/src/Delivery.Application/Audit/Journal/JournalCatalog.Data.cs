namespace Delivery.Application.Audit.Journal;

/// <summary>Tables de `journal-catalog.ts` et `journal-labels.ts`, telles quelles (JSON).</summary>
public static partial class JournalCatalog
{
    private const string Json = """
        {
          "entities": {
            "Parcel": {
              "type": "parcel",
              "category": "parcels",
              "priority": 50,
              "fields": {
                "statusId": "parcel-status",
                "codAmount": "money",
                "deliveryFee": "money",
                "failureFee": "money",
                "otherFees": "money",
                "recipientName": "text",
                "recipientPhone": "text",
                "destinationCityId": "ref:city",
                "destinationDistrictName": "text",
                "deliveryAddress": "text",
                "originCityId": "ref:city",
                "pickupAddress": "text",
                "description": "text",
                "externalCode": "text",
                "courierId": "ref:courier",
                "agencyId": "ref:agency",
                "clientId": "ref:client",
                "expectedPayment": "enum",
                "billingState": "enum",
                "financialStatus": "enum",
                "deliveryMode": "enum",
                "fragile": "bool",
                "openOnDelivery": "bool",
                "blRequested": "bool",
                "alreadyPaid": "bool",
                "pickupWaived": "bool",
                "scheduledAt": "date"
              }
            },
            "DeliveryNote": {
              "type": null,
              "category": "notes",
              "priority": 80,
              "fields": {
                "status": "enum",
                "clientId": "ref:client",
                "agencyId": "ref:agency",
                "locationCityId": "ref:city"
              }
            },
            "Route": {
              "type": "route",
              "category": "routes",
              "priority": 80,
              "fields": {
                "status": "enum",
                "kind": "enum",
                "courierId": "ref:courier",
                "cityId": "ref:city",
                "exitDate": "date"
              }
            },
            "CashRemittance": {
              "type": "remittance",
              "category": "cash",
              "priority": 80,
              "fields": {
                "status": "enum",
                "amount": "money",
                "declaredAmount": "money",
                "totalCash": "money",
                "totalCheque": "money",
                "expectedCod": "money",
                "expectedNet": "money",
                "variance": "money",
                "varianceReason": "text",
                "courierId": "ref:courier",
                "agencyId": "ref:agency",
                "note": "text",
                "courierClosedAt": "date",
                "courierClosedById": "ref:user"
              }
            },
            "Invoice": {
              "type": "invoice",
              "category": "invoices",
              "priority": 80,
              "fields": {
                "status": "enum",
                "totalDue": "money",
                "totalCod": "money",
                "totalDeliveryFee": "money",
                "totalFailureFee": "money",
                "totalVat": "money",
                "clientId": "ref:client",
                "dueAt": "date",
                "periodStart": "date",
                "periodEnd": "date",
                "paidAt": "date",
                "note": "text"
              }
            },
            "CodPayout": {
              "type": "cod-payout",
              "category": "payouts",
              "priority": 80,
              "fields": {
                "status": "enum",
                "amount": "money",
                "method": "enum",
                "reference": "text",
                "clientId": "ref:client",
                "paidAt": "date",
                "note": "text"
              }
            },
            "TransferOrder": {
              "type": "transfer-order",
              "category": "payouts",
              "priority": 70,
              "fields": {
                "totalDue": "money",
                "totalCollected": "money",
                "totalFees": "money"
              }
            },
            "Cheque": {
              "type": "cheque",
              "category": "cheques",
              "priority": 80,
              "fields": {
                "status": "enum",
                "amount": "money",
                "number": "text",
                "bank": "text",
                "payerName": "text",
                "chequeDate": "date",
                "rejectionReason": "text",
                "note": "text"
              }
            },
            "Payment": {
              "type": "payment",
              "category": "invoices",
              "priority": 70,
              "fields": {
                "amount": "money",
                "method": "enum",
                "paidAt": "date",
                "reference": "text",
                "chequeNumber": "text",
                "clientId": "ref:client",
                "note": "text"
              }
            },
            "PettyCashMovement": {
              "type": "petty-cash",
              "category": "cash",
              "priority": 70,
              "fields": {
                "kind": "enum",
                "amount": "money",
                "label": "text",
                "category": "text",
                "occurredAt": "date",
                "buyerName": "text",
                "note": "text",
                "status": "enum",
                "refusalReason": "text"
              }
            },
            "PettyCashDay": {
              "type": "petty-cash",
              "category": "cash",
              "priority": 70,
              "fields": {
                "status": "enum",
                "openingBalance": "money",
                "closingBalance": "money",
                "reopenReason": "text"
              }
            },
            "User": {
              "type": "account",
              "category": "accounts",
              "priority": 40,
              "fields": {
                "firstName": "text",
                "lastName": "text",
                "email": "text",
                "username": "text",
                "phone": "text",
                "jobTitle": "text",
                "isActive": "bool",
                "status": "enum",
                "agencyId": "ref:agency",
                "mustChangePassword": "bool",
                "passwordHash": "secret"
              }
            },
            "Client": {
              "type": "client",
              "category": "accounts",
              "priority": 45,
              "fields": {
                "name": "text",
                "legalName": "text",
                "phone": "text",
                "email": "text",
                "address": "text",
                "cityId": "ref:city",
                "agencyId": "ref:agency",
                "isActive": "bool",
                "isPro": "bool",
                "billingType": "enum",
                "monthlyBilling": "bool",
                "vatRate": "number",
                "taxId": "text",
                "rejectionReason": "text",
                "notes": "text"
              }
            },
            "Courier": {
              "type": "courier",
              "category": "accounts",
              "priority": 45,
              "fields": {
                "firstName": "text",
                "lastName": "text",
                "phone": "text",
                "cityId": "ref:city",
                "agencyId": "ref:agency",
                "vehicleType": "enum",
                "isActive": "bool",
                "payModel": "enum",
                "settlementMode": "enum",
                "payPeriod": "enum",
                "fixedSalary": "money",
                "bankName": "text",
                "rib": "secret",
                "nationalId": "secret",
                "rejectionReason": "text"
              }
            },
            "ClientPickupAddress": {
              "type": "pickup-address",
              "category": "accounts",
              "priority": 40,
              "fields": {
                "label": "text",
                "address": "text",
                "cityId": "ref:city",
                "phone": "text",
                "isDefault": "bool"
              }
            },
            "PasswordResetRequest": {
              "type": "password-reset",
              "category": "accounts",
              "priority": 30,
              "fields": {}
            },
            "PricingRule": {
              "type": "pricing-rule",
              "category": "settings",
              "priority": 40,
              "fields": {
                "rateDistrict": "money",
                "rateAgency": "money",
                "rateRelayPoint": "money",
                "isEnabled": "bool"
              }
            },
            "Setting": {
              "type": "setting",
              "category": "settings",
              "priority": 40,
              "fields": {
                "value": "text"
              }
            },
            "Agency": {
              "type": "agency",
              "category": "settings",
              "priority": 40,
              "fields": {
                "name": "text",
                "phone": "text",
                "email": "text",
                "address": "text",
                "cityId": "ref:city",
                "isActive": "bool"
              }
            },
            "Claim": {
              "type": "claim",
              "category": "claims",
              "priority": 70,
              "fields": {
                "status": "enum",
                "subject": "text",
                "assigneeId": "ref:user",
                "dueAt": "date"
              }
            },
            "ReturnHandover": {
              "type": "return-handover",
              "category": "returns",
              "priority": 80,
              "fields": {
                "status": "enum",
                "courierId": "ref:courier",
                "note": "text",
                "cancelReason": "text"
              }
            },
            "ReturnSlip": {
              "type": "return-slip",
              "category": "returns",
              "priority": 80,
              "fields": {
                "status": "enum",
                "receivedByName": "text",
                "note": "text"
              }
            },
            "DeliverySlip": {
              "type": "delivery-slip",
              "category": "returns",
              "priority": 60,
              "fields": {
                "status": "enum"
              }
            },
            "Product": {
              "type": "product",
              "category": "warehouse",
              "priority": 40,
              "fields": {
                "name": "text",
                "reference": "text",
                "price": "money",
                "value": "money",
                "isActive": "bool",
                "internalStock": "number"
              }
            },
            "Order": {
              "type": "order",
              "category": "warehouse",
              "priority": 70,
              "fields": {
                "status": "enum",
                "recipientName": "text",
                "recipientPhone": "text",
                "deliveryAddress": "text",
                "destinationCityId": "ref:city",
                "totalAmount": "money"
              }
            },
            "ParcelImport": {
              "type": "parcel-import",
              "category": "parcels",
              "priority": 60,
              "fields": {
                "rowCount": "number",
                "okCount": "number",
                "koCount": "number"
              }
            }
          },
          "fallback": {
            "type": "other",
            "category": "other",
            "priority": 10,
            "fields": {
              "name": "text",
              "code": "text",
              "label": "text",
              "status": "enum",
              "amount": "money",
              "isActive": "bool"
            }
          },
          "companions": [
            "ParcelEvent",
            "Notification",
            "FieldChange",
            "ParcelFeeChange",
            "ChequeEvent",
            "ClaimEvent",
            "ReturnHandoverEvent",
            "DeliverySlipEvent",
            "CourierBalanceEntry",
            "PushToken"
          ],
          "technical": [
            "id",
            "companyId",
            "createdAt",
            "updatedAt",
            "statusAt",
            "lastLoginAt",
            "tokensValidFrom",
            "courierCommission",
            "courierCommissionAt",
            "courierCommissionSource",
            "courierCommissionCourierId",
            "currentCityId",
            "latitude",
            "longitude",
            "validatedAt",
            "validatedBy",
            "validatedById",
            "issuedAt",
            "issuedById",
            "paidById",
            "confirmedAt",
            "confirmedById",
            "pickedUpAt",
            "pickedUpBy",
            "createdById",
            "invitedById"
          ],
          "objectLabels": {
            "parcel": "Colis",
            "pickup-note": "Bon de ramassage",
            "dropoff-note": "Bon de dépôt",
            "route": "Circuit",
            "remittance": "Bon de caisse",
            "invoice": "Facture",
            "cod-payout": "Versement",
            "transfer-order": "Ordre de virement",
            "cheque": "Chèque",
            "payment": "Paiement",
            "petty-cash": "Petite caisse",
            "account": "Compte",
            "client": "E-commerçant",
            "courier": "Livreur",
            "pickup-address": "Adresse de ramassage",
            "password-reset": "Mot de passe oublié",
            "pricing-rule": "Tarif",
            "setting": "Paramètre",
            "agency": "Agence",
            "claim": "Réclamation",
            "return-handover": "Bon de retour livreur",
            "return-slip": "Bon de retour client",
            "delivery-slip": "BL client",
            "product": "Produit",
            "order": "Commande",
            "parcel-import": "Import de colis",
            "other": "Autre"
          },
          "actionLabels": {
            "create": "Création",
            "update": "Modification",
            "delete": "Suppression",
            "status": "Changement de statut",
            "amount": "Changement de montant",
            "validate": "Validation",
            "issue": "Émission",
            "pay": "Paiement",
            "close": "Clôture",
            "cancel": "Annulation",
            "login": "Connexion réussie",
            "login-failed": "Connexion échouée",
            "client-change": "Changement de client"
          },
          "fieldLabels": {
            "statusId": "Statut",
            "status": "Statut",
            "codAmount": "Montant à encaisser",
            "deliveryFee": "Frais de livraison",
            "failureFee": "Frais d’échec",
            "otherFees": "Autres frais",
            "recipientName": "Destinataire",
            "recipientPhone": "Téléphone du destinataire",
            "destinationCityId": "Ville de destination",
            "destinationDistrictName": "Quartier",
            "deliveryAddress": "Adresse de livraison",
            "originCityId": "Ville de départ",
            "pickupAddress": "Adresse de ramassage",
            "description": "Désignation",
            "externalCode": "Référence",
            "courierId": "Livreur",
            "agencyId": "Agence",
            "clientId": "E-commerçant",
            "expectedPayment": "Paiement attendu",
            "billingState": "Facturation",
            "financialStatus": "Statut financier",
            "deliveryMode": "Mode de livraison",
            "fragile": "Fragile",
            "openOnDelivery": "Ouverture à la livraison",
            "blRequested": "BL à récupérer",
            "alreadyPaid": "Déjà payé",
            "pickupWaived": "Ramassage offert",
            "scheduledAt": "Date prévue",
            "locationCityId": "Ville du geste",
            "kind": "Type",
            "cityId": "Ville",
            "exitDate": "Date de sortie",
            "amount": "Montant",
            "declaredAmount": "Montant déclaré",
            "totalCash": "Espèces",
            "totalCheque": "Chèques",
            "expectedCod": "Encaissements attendus",
            "expectedNet": "Net attendu",
            "variance": "Écart",
            "varianceReason": "Motif de l’écart",
            "courierClosedAt": "Caisse clôturée par le livreur le",
            "courierClosedById": "Caisse clôturée par",
            "note": "Note",
            "totalDue": "Total dû",
            "totalCod": "Total encaissé",
            "totalDeliveryFee": "Total des frais",
            "totalFailureFee": "Total des frais d’échec",
            "totalVat": "TVA",
            "dueAt": "Échéance",
            "periodStart": "Début de période",
            "periodEnd": "Fin de période",
            "paidAt": "Payé le",
            "method": "Mode de paiement",
            "reference": "Référence",
            "totalCollected": "Total encaissé",
            "totalFees": "Total des frais",
            "number": "Numéro",
            "bank": "Banque",
            "payerName": "Tireur",
            "chequeDate": "Date du chèque",
            "rejectionReason": "Motif du rejet",
            "chequeNumber": "Numéro de chèque",
            "label": "Libellé",
            "category": "Catégorie",
            "occurredAt": "Date",
            "buyerName": "Acheteur",
            "refusalReason": "Motif du refus",
            "openingBalance": "Solde d’ouverture",
            "closingBalance": "Solde de clôture",
            "reopenReason": "Motif de la correction",
            "firstName": "Prénom",
            "lastName": "Nom",
            "email": "E-mail",
            "username": "Nom d’utilisateur",
            "phone": "Téléphone",
            "jobTitle": "Fonction",
            "isActive": "Actif",
            "mustChangePassword": "Mot de passe à changer",
            "password": "Mot de passe",
            "name": "Nom",
            "legalName": "Raison sociale",
            "address": "Adresse",
            "isPro": "Client Pro",
            "billingType": "Type de facturation",
            "monthlyBilling": "Facturation mensuelle",
            "vatRate": "Taux de TVA (centièmes de %)",
            "taxId": "Identifiant fiscal",
            "notes": "Notes",
            "vehicleType": "Véhicule",
            "payModel": "Rémunération",
            "settlementMode": "Règlement",
            "payPeriod": "Périodicité de paie",
            "fixedSalary": "Salaire fixe",
            "bankName": "Banque",
            "rib": "RIB",
            "nationalId": "CIN",
            "isDefault": "Par défaut",
            "rateDistrict": "Tarif quartier",
            "rateAgency": "Tarif agence",
            "rateRelayPoint": "Tarif point relais",
            "isEnabled": "Activé",
            "value": "Valeur",
            "subject": "Objet",
            "assigneeId": "Responsable",
            "cancelReason": "Motif d’annulation",
            "receivedByName": "Reçu par",
            "price": "Prix",
            "internalStock": "Stock interne",
            "totalAmount": "Montant total",
            "rowCount": "Lignes",
            "okCount": "Lignes importées",
            "koCount": "Lignes refusées",
            "code": "Code"
          },
          "enumValueLabels": {
            "active": "Actif",
            "agency": "Agence",
            "annule": "Annulé",
            "at-remittance": "À la remise",
            "awaiting-receipt": "En attente de réception",
            "awaiting-validation": "À valider",
            "cancelled": "Annulé",
            "car": "Voiture",
            "cash": "Espèces",
            "changed": "modifié",
            "check": "Chèque",
            "cheque": "Chèque",
            "closed": "Clôturé",
            "collected": "Récupéré",
            "collecting": "En collecte",
            "commission": "À la commission",
            "confirmed": "Confirmé",
            "daily": "Quotidienne",
            "delivered": "Remis",
            "delivering": "En livraison",
            "delivery": "Livraison",
            "depose-banque": "Déposé en banque",
            "dispatched": "Parti",
            "district": "Quartier",
            "draft": "Brouillon",
            "en-attente-encaissement": "En attente d’encaissement",
            "encaisse": "Encaissé",
            "expense": "Dépense",
            "facture": "Facturé",
            "handed-over": "Remis",
            "hybrid": "Fixe + courses",
            "in-progress": "En cours",
            "invoiced": "Facturé",
            "issued": "Émise",
            "monthly": "Mensuelle",
            "motorbike": "Moto",
            "non-encaisse": "Non encaissé",
            "normal": "Normal",
            "open": "Ouvert",
            "paid": "Payé",
            "parcel-created": "Colis créé",
            "pending": "En attente",
            "picked-up": "Ramassé",
            "picked-up-by-courier": "Ramassé par le livreur",
            "pickup": "Ramassage",
            "professional": "Professionnel",
            "received": "Reçu",
            "recu-admin": "Reçu par %shortName%",
            "recu-livreur": "Reçu par le livreur",
            "recu-vendeur": "Reçu par le vendeur",
            "refused": "Refusé",
            "reimbursed": "Remboursé",
            "rejected": "Rejeté",
            "rejete": "Rejeté",
            "relay-point": "Point relais",
            "rembourse": "Remboursé",
            "remis-admin": "Remis à %shortName%",
            "remis-livreur": "Remis par le livreur",
            "resolved": "Résolue",
            "return": "Retour",
            "return-requested": "Retour demandé",
            "returned": "Retourné",
            "salary": "Fixe",
            "supply": "Approvisionnement",
            "to-invoice": "À facturer",
            "transfer": "Virement",
            "transmis-vendeur": "Transmis au vendeur",
            "validated": "Validé",
            "van": "Fourgon",
            "weekly": "Hebdomadaire"
          },
          "roleLabels": {
            "admin": "Admin",
            "staff": "Personnel",
            "merchant": "E-commerçant",
            "courier": "Livreur",
            "platform": "Nocido",
            "other": "Autre"
          },
          "sourceLabels": {
            "web": "Web",
            "mobile": "Application mobile",
            "import": "Import Excel",
            "auto": "Automatique"
          }
        }
        """;
}
