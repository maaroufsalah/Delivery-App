using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Warehouse;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Merchant.Queries;

public sealed record MerchantActivityItemDto(string? ParcelCode, int StatusId, DateTime At);

/// <summary>`MerchantSummary` : l'accueil du vendeur.</summary>
public sealed record MerchantSummaryDto(int CreatedToday, int DeliveredToday, int InTransit, bool IsPro, int ParcelsTotal, IReadOnlyDictionary<string, int> Parcels,
    int UnpaidInvoices, long UnpaidAmount, int OpenClaims, int LowStockLines, IReadOnlyList<MerchantActivityItemDto> Activity, long CodToReceive, string BillingType);

/// <summary>`GET /merchant/me/summary` : les chiffres du jour et ce qui lui reviendra.</summary>
public sealed record GetMerchantSummaryQuery : IRequest<MerchantSummaryDto>;

internal sealed class GetMerchantSummaryHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetMerchantSummaryQuery, MerchantSummaryDto>
{
    /// <summary>`MERCHANT_PARCEL_BUCKETS`, dans leur ordre.</summary>
    private static readonly (string Key, int[] Ids)[] Buckets =
    [
        ("awaiting", [1, 2, 38]),
        ("pickedUp", [3, 31]),
        ("inTransit", [7, 12, 17]),
        ("outForDelivery", [13, 16]),
        ("delivered", [4]),
        ("returned", [5, 20, 21, 22, 23, 24]),
        ("cancelled", [19, 27]),
    ];

    /// <summary>`OPEN_STATUS_IDS` : tous les statuts non terminaux.</summary>
    private static readonly int[] OpenStatusIds = Enumerable.Range(1, 40).Where(id => StatusCatalog.Find(id) is { Group: not "terminal" }).ToArray();

    public async Task<MerchantSummaryDto> Handle(GetMerchantSummaryQuery request, CancellationToken cancellationToken)
    {
        var clientId = caller.ClientId ?? throw BusinessException.Forbidden("Cet espace est réservé aux e-commerçants.", new { reason = "not-a-merchant" });
        var mine = db.Parcels.AsNoTracking().Where(p => p.ClientId == clientId && p.DeletedAt == null);
        var zone = Periods.Zone(await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => c.Timezone).FirstOrDefaultAsync(cancellationToken));
        var startOfToday = Periods.ZonedDayStart(Periods.LocalDay(clock.UtcNow, zone), zone);
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => new { c.BillingType, c.IsPro, c.VatRate }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound("Fiche client introuvable.");
        var vatRate = await IntSettingAsync(SettingKeys.VatRate, 0, cancellationToken);
        var createdToday = await mine.CountAsync(p => p.CreatedAt >= startOfToday, cancellationToken);
        var deliveredToday = await mine.CountAsync(p => p.StatusId == Billing.DeliveredStatusId && p.StatusAt >= startOfToday, cancellationToken);
        var inTransit = await mine.CountAsync(p => OpenStatusIds.Contains(p.StatusId), cancellationToken);
        var owed = await mine.Where(p => p.StatusId == Billing.DeliveredStatusId && p.BillingState != BillingState.REIMBURSED)
            .Select(p => new { p.CodAmount, p.DeliveryFee, p.FailureFee, p.FeeIncluded, p.AlreadyPaid, p.StatusId }).ToListAsync(cancellationToken);
        var parcelsTotal = await mine.CountAsync(cancellationToken);
        var byStatus = await mine.GroupBy(p => p.StatusId).Select(g => new { StatusId = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var invoices = await db.Invoices.AsNoTracking().Where(i => i.ClientId == clientId && i.Status == InvoiceStatus.ISSUED).Select(i => i.TotalDue).ToListAsync(cancellationToken);
        var openClaims = await db.Claims.AsNoTracking().CountAsync(c => c.ClientId == clientId && (c.Status == ClaimStatus.OPEN || c.Status == ClaimStatus.IN_PROGRESS), cancellationToken);
        var lowStockLines = await LowStockLinesAsync(clientId, cancellationToken);
        var activity = await db.ParcelEvents.AsNoTracking().Where(e => e.Parcel.ClientId == clientId && e.Parcel.DeletedAt == null)
            .OrderByDescending(e => e.CreatedAt).Take(10).Select(e => new MerchantActivityItemDto(e.Parcel.Code, e.StatusId, e.CreatedAt)).ToListAsync(cancellationToken);

        var billingType = client.BillingType == BillingType.PROFESSIONAL ? "professional" : "normal";
        var proRate = client.IsPro ? Billing.ProVatRateFor(true, client.VatRate, await IntSettingAsync(SettingKeys.ProVatRate, Billing.DefaultProVatRate, cancellationToken)) : null;
        var billables = owed.Select(p => new BillableParcel(Money.ToCents(p.CodAmount), Money.ToCents(p.DeliveryFee), Money.ToCentsOrNull(p.FailureFee), 0, p.FeeIncluded, p.AlreadyPaid, p.StatusId)).ToList();
        var codToReceive = billables.Sum(p => Billing.NetDueFor(billingType, p, Billing.DeliveredStatusId, proRate is null ? vatRate : 0))
                           - (proRate is null ? 0 : Billing.VatOnTotal(billables.Sum(p => Billing.FeeCharged(p)), proRate.Value));
        var counts = Buckets.ToDictionary(b => b.Key, b => byStatus.Where(r => b.Ids.Contains(r.StatusId)).Sum(r => r.Count));
        return new MerchantSummaryDto(createdToday, deliveredToday, inTransit, client.IsPro, parcelsTotal, counts, invoices.Count, invoices.Sum(t => Money.ToCents(t)),
            openClaims, lowStockLines, activity, Math.Max(0, codToReceive), billingType);
    }

    private async Task<int> IntSettingAsync(string key, int fallback, CancellationToken cancellationToken) =>
        await db.SettingAsync(key, cancellationToken) is { ValueKind: JsonValueKind.Number } value && value.TryGetDouble(out var number) ? (int)number : fallback;

    private async Task<int> LowStockLinesAsync(string clientId, CancellationToken cancellationToken)
    {
        var rows = await db.StockItems.AsNoTracking().Where(s => s.Product.ClientId == clientId).Select(s => new { s.AgencyId, s.Quantity }).ToListAsync(cancellationToken);
        var thresholds = new Dictionary<string, int>();
        foreach (var agencyId in rows.Select(r => r.AgencyId).Distinct()) thresholds[agencyId] = await WarehouseScope.LowStockThresholdAsync(db, agencyId, cancellationToken);
        return rows.Count(r => r.Quantity <= thresholds.GetValueOrDefault(r.AgencyId));
    }
}
