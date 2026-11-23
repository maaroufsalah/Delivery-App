namespace Delivery.Domain.Enums;

public enum ChequeStatus
{
    RECU_LIVREUR,
    REMIS_ADMIN,
    RECU_ADMIN,
    TRANSMIS_VENDEUR,
    RECU_VENDEUR,
    REJETE,
    ANNULE,
    EN_ATTENTE_ENCAISSEMENT,
    DEPOSE_BANQUE,
    ENCAISSE,
}
