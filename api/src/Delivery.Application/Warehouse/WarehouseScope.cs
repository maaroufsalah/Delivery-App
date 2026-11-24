using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Domain.Entities;

namespace Delivery.Application.Warehouse;

/// <summary>
/// `warehouse-scope.ts` : le super-admin et le personnel voient tout l'entrepôt (l'agence ne
/// borne pas, comme Speedy), le vendeur ses produits, le livreur rien.
/// </summary>
public static class WarehouseScope
{
    public const int DefaultLowStockThreshold = 5;

    public static bool IsMerchant(ICurrentUser caller) => !caller.IsSuperAdmin() && caller.ClientId is not null;

    public static bool DeniesAll(ICurrentUser caller) => !caller.IsSuperAdmin() && caller.ClientId is null && caller.CourierId is not null;

    public static IQueryable<Product> Products(IQueryable<Product> products, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return products;
        if (caller.ClientId is { } clientId) return products.Where(p => p.ClientId == clientId);
        if (caller.CourierId is not null) return products.Where(p => p.Id == Scope.DenyAllId);
        return products;
    }

    public static IQueryable<StockItem> Stock(IQueryable<StockItem> items, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return items;
        if (caller.ClientId is { } clientId) return items.Where(i => i.ClientId == clientId);
        if (caller.CourierId is not null) return items.Where(i => i.Id == Scope.DenyAllId);
        return items;
    }

    public static IQueryable<StorageRequest> Storage(IQueryable<StorageRequest> requests, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return requests;
        if (caller.ClientId is { } clientId) return requests.Where(r => r.ClientId == clientId);
        if (caller.CourierId is not null) return requests.Where(r => r.Id == Scope.DenyAllId);
        return requests;
    }

    public static IQueryable<Order> Orders(IQueryable<Order> orders, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return orders;
        if (caller.ClientId is { } clientId) return orders.Where(o => o.ClientId == clientId);
        if (caller.CourierId is not null) return orders.Where(o => o.Id == Scope.DenyAllId);
        return orders;
    }

    /// <summary>`lowStockThreshold` : seuil d'alerte, par agence si la clé y est surchargée.</summary>
    public static async Task<int> LowStockThresholdAsync(IAppDbContext db, string? agencyId, CancellationToken cancellationToken)
    {
        var value = await db.SettingAsync(SettingKeys.LowStockThreshold, cancellationToken, agencyId);
        return value is { ValueKind: JsonValueKind.Number } number && number.TryGetDouble(out var d) && double.IsFinite(d) && d >= 0 ? (int)Math.Floor(d) : DefaultLowStockThreshold;
    }
}
