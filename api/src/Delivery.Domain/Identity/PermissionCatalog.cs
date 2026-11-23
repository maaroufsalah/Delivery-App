namespace Delivery.Domain.Identity;

/// <summary>Catalogue des permissions de `@delivery/shared/permissions.ts`, dans le même ordre.</summary>
public static class PermissionCatalog
{
    public static readonly IReadOnlyList<string> Modules =
    [
        "platform",
        "parcels",
        "routes",
        "notes",
        "returns",
        "warehouse",
        "finance",
        "pettycash",
        "claims",
        "announcements",
        "directory",
        "accounts",
        "settings",
        "audit",
    ];

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new("platform.read", "platform", "Consulter les tenants"),
        new("platform.write", "platform", "Créer et administrer les tenants"),
        new("platform.impersonate", "platform", "Se connecter en tant qu’un tenant"),
        new("parcels.read", "parcels", "Consulter les colis"),
        new("parcels.write", "parcels", "Créer et modifier les colis"),
        new("parcels.status", "parcels", "Changer le statut d’un colis"),
        new("parcels.dispute", "parcels", "Contester un statut terminal"),
        new("parcels.correctStatus", "parcels", "Corriger librement le statut d’un colis"),
        new("parcels.editFees", "parcels", "Modifier les frais d’un colis"),
        new("routes.read", "routes", "Consulter les circuits"),
        new("routes.write", "routes", "Créer et modifier les circuits"),
        new("routes.confirm", "routes", "Confirmer un colis en tournée"),
        new("routes.scan", "routes", "Ouvrir sa tournée en scannant"),
        new("routes.close", "routes", "Clôturer un circuit"),
        new("routes.stock", "routes", "Consulter et télécharger le stock des livreurs"),
        new("notes.read", "notes", "Consulter les bons"),
        new("notes.write", "notes", "Valider les bons"),
        new("notes.request", "notes", "Demander un ramassage"),
        new("returns.read", "returns", "Consulter les retours"),
        new("returns.write", "returns", "Composer les remises et les bons de retour"),
        new("returns.confirm", "returns", "Confirmer une remise, clôturer un bon"),
        new("warehouse.read", "warehouse", "Consulter produits, stock et commandes"),
        new("warehouse.write", "warehouse", "Gérer produits, stock et commandes"),
        new("warehouse.validate", "warehouse", "Valider une demande d’entreposage"),
        new("finance.read", "finance", "Consulter la facturation"),
        new("finance.write", "finance", "Composer factures et retours de fonds"),
        new("finance.validate", "finance", "Émettre une facture"),
        new("finance.pay", "finance", "Enregistrer un règlement"),
        new("finance.confirm", "finance", "Confirmer un retour de fonds"),
        new("pettycash.read", "pettycash", "Consulter la petite caisse"),
        new("pettycash.write", "pettycash", "Saisir, modifier et supprimer les mouvements de petite caisse"),
        new("pettycash.courier", "pettycash", "Saisir ses dépenses de petite caisse (livreur)"),
        new("claims.read", "claims", "Consulter les réclamations"),
        new("claims.write", "claims", "Ouvrir une réclamation et y répondre"),
        new("claims.handle", "claims", "Traiter, assigner et clôturer une réclamation"),
        new("announcements.write", "announcements", "Publier des actualités"),
        new("directory.read", "directory", "Consulter le référentiel"),
        new("directory.write", "directory", "Gérer le référentiel"),
        new("accounts.read", "accounts", "Consulter les comptes"),
        new("accounts.write", "accounts", "Gérer les comptes"),
        new("settings.read", "settings", "Consulter la configuration"),
        new("settings.write", "settings", "Modifier la configuration"),
        new("audit.read", "audit", "Consulter le journal des modifications"),
    ];

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultRolePermissions =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["PLATFORM_ADMIN"] =
            [
                "platform.read",
                "platform.write",
                "platform.impersonate",
            ],
            ["SUPER_ADMIN"] =
            [
                "parcels.read",
                "parcels.write",
                "parcels.status",
                "parcels.dispute",
                "parcels.correctStatus",
                "parcels.editFees",
                "routes.read",
                "routes.write",
                "routes.confirm",
                "routes.scan",
                "routes.close",
                "routes.stock",
                "notes.read",
                "notes.write",
                "notes.request",
                "returns.read",
                "returns.write",
                "returns.confirm",
                "warehouse.read",
                "warehouse.write",
                "warehouse.validate",
                "finance.read",
                "finance.write",
                "finance.validate",
                "finance.pay",
                "finance.confirm",
                "pettycash.read",
                "pettycash.write",
                "pettycash.courier",
                "claims.read",
                "claims.write",
                "claims.handle",
                "announcements.write",
                "directory.read",
                "directory.write",
                "accounts.read",
                "accounts.write",
                "settings.read",
                "settings.write",
                "audit.read",
            ],
            ["STAFF"] =
            [
                "parcels.read",
                "parcels.write",
                "parcels.status",
                "parcels.dispute",
                "parcels.editFees",
                "routes.read",
                "routes.write",
                "routes.confirm",
                "routes.close",
                "routes.stock",
                "notes.read",
                "notes.write",
                "notes.request",
                "returns.read",
                "returns.write",
                "returns.confirm",
                "warehouse.read",
                "warehouse.write",
                "warehouse.validate",
                "finance.read",
                "finance.write",
                "finance.confirm",
                "claims.read",
                "claims.write",
                "claims.handle",
                "announcements.write",
                "directory.read",
                "directory.write",
                "accounts.read",
                "settings.read",
            ],
            ["MERCHANT"] =
            [
                "parcels.read",
                "parcels.write",
                "notes.read",
                "notes.request",
                "returns.read",
                "warehouse.read",
                "warehouse.write",
                "finance.read",
                "claims.read",
                "claims.write",
                "directory.read",
            ],
            ["COURIER"] =
            [
                "parcels.read",
                "parcels.status",
                "claims.read",
                "claims.write",
                "routes.read",
                "routes.scan",
                "routes.confirm",
                "returns.read",
                "returns.write",
                "finance.read",
                "pettycash.courier",
            ],
        };

    private static readonly HashSet<string> Keys = All.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);

    public static bool IsPermissionKey(string? value) => value is not null && Keys.Contains(value);

    public static int IndexOf(string key)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (All[i].Key == key) return i;
        }
        return int.MaxValue;
    }
}

public sealed record PermissionDefinition(string Key, string Module, string Label);
