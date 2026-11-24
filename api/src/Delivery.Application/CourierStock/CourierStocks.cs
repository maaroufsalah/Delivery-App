using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.CourierStock;

/// <summary>`CourierStockService` : le stock des livreurs, calculé à la volée (lecture seule, D-222).</summary>
public static class CourierStocks
{
    public static readonly string[] Sorts = ["courierName", "agencyName", "inHand", "pickedUp", "outForDelivery", "awaiting", "failedOrReturning", "deliveredUnremitted",
        "collectedUnremittedInCents", "toCollectInCents", "lastMovementAt"];

    public static readonly string[] ParcelSorts = ["code", "recipientName", "destinationCityName", "clientName", "codInCents", "statusId", "routeCode", "assignedAt", "lastMovementAt"];

    private static readonly int[] CandidateStatusIds = [18, 13, 7, 21, 24, .. StatusCatalog.CourierReturnStatusIds];
    private static readonly string[] AssignmentPrefixes = ["Circuit ", "Transféré de "];
    private static readonly CompareInfo French = CultureInfo.GetCultureInfo("fr").CompareInfo;

    public sealed record StockParcel(CourierStockParcelDto View, string CourierId, string CourierName, string CourierPhone, string? AgencyId, string? AgencyName);

    private sealed record RouteRef(string Id, string Code, string CourierId, RouteKind Kind, RouteStatus Status, DateTime? ExitDate, DateTime CreatedAt);

    /// <summary>`courierWhere` : l'administrateur voit tout (ou les agences demandées), l'agent son agence, les autres rien.</summary>
    private static IQueryable<Courier> Couriers(IAppDbContext db, ICurrentUser caller, IReadOnlyList<string>? agencyIds)
    {
        var couriers = db.Couriers.AsNoTracking();
        if (caller.IsSuperAdmin()) return agencyIds is null ? couriers : couriers.Where(c => c.AgencyId != null && agencyIds.Contains(c.AgencyId));
        if (caller.CourierId is not null || caller.ClientId is not null || caller.AgencyId is null) return couriers.Where(_ => false);
        return couriers.Where(c => c.AgencyId == caller.AgencyId);
    }

    public static async Task<List<StockParcel>> ParcelsAsync(IAppDbContext db, ICurrentUser caller, IReadOnlyList<string>? agencyIds, string? courierId, CancellationToken cancellationToken)
    {
        var couriers = Couriers(db, caller, agencyIds).Select(c => c.Id);
        var parcels = db.Parcels.AsNoTracking().Where(p => p.DeletedAt == null && p.CourierId != null && couriers.Contains(p.CourierId));
        if (courierId is not null) parcels = parcels.Where(p => p.CourierId == courierId);
        var candidates = await parcels.Where(p => CandidateStatusIds.Contains(p.StatusId)
                                                  || (p.StatusId == 16 && p.RouteLinks.Any(l => l.Route.Status != RouteStatus.CLOSED))
                                                  || (p.StatusId == 22 && p.RouteLinks.Any(l => l.Route.Kind == RouteKind.RETURN && l.Route.Status != RouteStatus.CLOSED))
                                                  || (p.StatusId == CourierPay.Delivered && !p.ReimbursedByCourier))
            .OrderBy(p => p.Code)
            .Select(p => new
            {
                p.Id, p.Code, p.RecipientName, p.RecipientPhone, p.StatusId, p.StatusAt, p.CourierId, p.ReimbursedByCourier, p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid, p.ClientId,
                Client = p.Client.Name, p.DestinationCityId, City = p.DestinationCity.Name, p.Courier!.FirstName, p.Courier.LastName, p.Courier.Phone, p.Courier.AgencyId,
                Agency = p.Courier.Agency != null ? p.Courier.Agency.Name : null,
                Routes = p.RouteLinks.Select(l => new RouteRef(l.Route.Id, l.Route.Code, l.Route.CourierId, l.Route.Kind, l.Route.Status, l.Route.ExitDate, l.Route.CreatedAt)).ToList(),
            }).ToListAsync(cancellationToken);
        if (candidates.Count == 0) return [];
        var ids = candidates.Select(c => c.Id).ToList();
        var events = (await db.ParcelEvents.AsNoTracking().Where(e => ids.Contains(e.ParcelId)).OrderBy(e => e.CreatedAt).Select(e => new { e.ParcelId, e.StatusId, e.Comment, e.CreatedAt })
            .ToListAsync(cancellationToken)).GroupBy(e => e.ParcelId).ToDictionary(g => g.Key, g => g.ToList());
        var result = new List<StockParcel>();
        foreach (var parcel in candidates)
        {
            var mine = parcel.CourierId!;
            var own = events.GetValueOrDefault(parcel.Id) ?? [];
            var block = Domain.Parcels.CourierStock.Classify(parcel.StatusId, mine, parcel.ReimbursedByCourier,
                parcel.Routes.Select(r => new CourierStockRouteLink(r.CourierId, r.Kind.ToString(), r.Status.ToString())).ToList(), own.Select(e => e.StatusId).ToList(), mine);
            if (block is null) continue;
            var route = parcel.Routes.Where(r => r.CourierId == mine).OrderByDescending(r => r.Status != RouteStatus.CLOSED).ThenByDescending(r => r.CreatedAt).FirstOrDefault();
            // Bug Speedy corrigé : `collected()` ignorait « déjà payé » ; le COD du stock est le dû (`codToCollect`).
            var cod = Billing.CodToCollect(Money.ToCents(parcel.CodAmount), Money.ToCents(parcel.DeliveryFee), parcel.FeeIncluded, parcel.AlreadyPaid);
            var codes = parcel.Routes.Where(r => r.CourierId == mine).Select(r => r.Code).ToList();
            DateTime? assigned = null;
            for (var i = own.Count - 1; i >= 0 && assigned is null; i--)
            {
                var comment = own[i].Comment ?? string.Empty;
                if (!AssignmentPrefixes.Any(prefix => comment.StartsWith(prefix, StringComparison.Ordinal))) continue;
                if (codes.Any(code => AssignsTo(comment, code))) assigned = own[i].CreatedAt;
            }
            assigned ??= route is null ? null : route.ExitDate ?? route.CreatedAt;
            result.Add(new StockParcel(new CourierStockParcelDto(parcel.Id, parcel.Code, parcel.RecipientName, parcel.RecipientPhone, parcel.DestinationCityId, parcel.City, parcel.ClientId,
                    parcel.Client, cod, parcel.StatusId, block, route?.Id, route?.Code, assigned, own.Count > 0 ? own[^1].CreatedAt : parcel.StatusAt),
                mine, $"{parcel.FirstName} {parcel.LastName}".Trim(), parcel.Phone, parcel.AgencyId, parcel.Agency));
        }
        return result;
    }

    private static bool AssignsTo(string comment, string code)
    {
        if (!comment.StartsWith("Transféré de ", StringComparison.Ordinal)) return comment.Contains(code, StringComparison.Ordinal);
        var arrival = comment.LastIndexOf(" à ", StringComparison.Ordinal);
        return arrival >= 0 && comment[arrival..].Contains($"({code})", StringComparison.Ordinal);
    }

    public static async Task<(List<CourierStockRowDto> Rows, CourierStockCounters Totals)> SnapshotAsync(IAppDbContext db, ICurrentUser caller, CourierStockListFilter filter,
        CancellationToken cancellationToken)
    {
        var agencyIds = filter.AgencyId is not null && caller.MayFilterBy(caller.AgencyId) ? filter.AgencyId : null;
        var parcels = await ParcelsAsync(db, caller, agencyIds, null, cancellationToken);
        var byCourier = new Dictionary<string, CourierStockRowDto>();
        foreach (var parcel in parcels)
        {
            if (!byCourier.TryGetValue(parcel.CourierId, out var row))
            {
                row = new CourierStockRowDto { CourierId = parcel.CourierId, CourierName = parcel.CourierName, CourierPhone = parcel.CourierPhone, AgencyId = parcel.AgencyId, AgencyName = parcel.AgencyName };
                byCourier[parcel.CourierId] = row;
            }
            Domain.Parcels.CourierStock.Add(row, parcel.View.Block, parcel.View.StatusId, parcel.View.CodInCents, parcel.View.LastMovementAt);
        }
        if (filter.All == "true")
        {
            var couriers = await Couriers(db, caller, agencyIds).Where(c => c.DeletedAt == null && c.IsActive && c.RejectedAt == null)
                .Select(c => new { c.Id, c.FirstName, c.LastName, c.Phone, c.AgencyId, Agency = c.Agency != null ? c.Agency.Name : null }).ToListAsync(cancellationToken);
            foreach (var c in couriers.Where(c => !byCourier.ContainsKey(c.Id)))
            {
                byCourier[c.Id] = new CourierStockRowDto { CourierId = c.Id, CourierName = $"{c.FirstName} {c.LastName}".Trim(), CourierPhone = c.Phone, AgencyId = c.AgencyId, AgencyName = c.Agency };
            }
        }
        var needle = Normalize(filter.Search ?? string.Empty);
        var rows = byCourier.Values.Where(r => needle.Length == 0 || Normalize(r.CourierName).Contains(needle, StringComparison.Ordinal) || Normalize(r.CourierPhone).Contains(needle, StringComparison.Ordinal))
            .ToList();
        rows.Sort((a, b) => Compare(RowValue(a, filter.Sort), RowValue(b, filter.Sort), filter.Order) is var c && c != 0 ? c : Compare(a.CourierName, b.CourierName, "asc"));
        var totals = new CourierStockCounters();
        foreach (var row in rows) Domain.Parcels.CourierStock.Sum(totals, row);
        return (rows, totals);
    }

    public static async Task<(CourierStockCourierDto Courier, CourierStockCounters Counters, List<CourierStockStatusLineDto> ByStatus, List<StockParcel> Parcels, CourierStockOptionsDto Options)>
        SelectionAsync(IAppDbContext db, ICurrentUser caller, string courierId, CourierStockParcelFilter filter, CancellationToken cancellationToken)
    {
        var courier = await Couriers(db, caller, null).Where(c => c.Id == courierId)
                          .Select(c => new CourierStockCourierDto(c.Id, (c.FirstName + " " + c.LastName).Trim(), c.Phone, c.AgencyId, c.Agency != null ? c.Agency.Name : null)).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Livreur introuvable.");
        var all = await ParcelsAsync(db, caller, null, courierId, cancellationToken);
        var parcels = all.Where(p => Matches(p.View, filter)).ToList();
        parcels.Sort((a, b) => Compare(ParcelValue(a.View, filter.Sort), ParcelValue(b.View, filter.Sort), filter.Order) is var c && c != 0 ? c : Compare(a.View.Code, b.View.Code, "asc"));
        var counters = new CourierStockCounters();
        foreach (var p in parcels) Domain.Parcels.CourierStock.Add(counters, p.View.Block, p.View.StatusId, p.View.CodInCents, p.View.LastMovementAt);
        var byStatus = parcels.GroupBy(p => p.View.StatusId).Select(g => new CourierStockStatusLineDto(g.Key, g.Count(), g.Sum(p => p.View.CodInCents))).OrderBy(l => l.StatusId).ToList();
        return (courier, counters, byStatus, parcels, OptionsOf(all.Select(p => p.View).ToList()));
    }

    private static CourierStockOptionsDto OptionsOf(IReadOnlyList<CourierStockParcelDto> parcels)
    {
        List<CourierStockOptionDto> Unique(IEnumerable<(string? Value, string? Label)> pairs)
        {
            var seen = new Dictionary<string, string>();
            foreach (var (value, label) in pairs) if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(label)) seen[value] = label;
            return seen.Select(kv => new CourierStockOptionDto(kv.Key, kv.Value)).OrderBy(o => o.Label, StringComparer.Create(CultureInfo.GetCultureInfo("fr"), false)).ToList();
        }
        return new CourierStockOptionsDto(parcels.Select(p => p.StatusId).Distinct().Order().ToList(), Unique(parcels.Select(p => ((string?)p.ClientId, (string?)p.ClientName))),
            Unique(parcels.Select(p => ((string?)p.DestinationCityId, (string?)p.DestinationCityName))), Unique(parcels.Select(p => (p.RouteId, p.RouteCode))));
    }

    private static bool Matches(CourierStockParcelDto parcel, CourierStockParcelFilter filter)
    {
        if (filter.StatusId is { } status && parcel.StatusId != status) return false;
        if (filter.ClientId is { } clients && !clients.Contains(parcel.ClientId)) return false;
        if (filter.CityId is { } cities && !cities.Contains(parcel.DestinationCityId)) return false;
        if (filter.RouteId is { } routes && !(parcel.RouteId is not null && routes.Contains(parcel.RouteId))) return false;
        var needle = Normalize(filter.Search ?? string.Empty);
        if (needle.Length == 0) return true;
        var digits = new string(needle.Where(char.IsAsciiDigit).ToArray());
        return Normalize(parcel.Code).Contains(needle, StringComparison.Ordinal) || Normalize(parcel.RecipientName).Contains(needle, StringComparison.Ordinal)
               || parcel.RecipientPhone.Contains(needle, StringComparison.Ordinal)
               || (digits.Length >= 3 && new string(parcel.RecipientPhone.Where(char.IsAsciiDigit).ToArray()).Contains(digits, StringComparison.Ordinal));
    }

    private static object? RowValue(CourierStockRowDto row, string sort) => sort switch
    {
        "courierName" => row.CourierName,
        "agencyName" => row.AgencyName,
        "inHand" => row.InHand,
        "pickedUp" => row.PickedUp,
        "outForDelivery" => row.OutForDelivery,
        "awaiting" => row.Awaiting,
        "failedOrReturning" => row.FailedOrReturning,
        "deliveredUnremitted" => row.DeliveredUnremitted,
        "collectedUnremittedInCents" => row.CollectedUnremittedInCents,
        "toCollectInCents" => row.ToCollectInCents,
        _ => row.LastMovementAt,
    };

    private static object? ParcelValue(CourierStockParcelDto parcel, string sort) => sort switch
    {
        "code" => parcel.Code,
        "recipientName" => parcel.RecipientName,
        "destinationCityName" => parcel.DestinationCityName,
        "clientName" => parcel.ClientName,
        "codInCents" => parcel.CodInCents,
        "statusId" => parcel.StatusId,
        "routeCode" => parcel.RouteCode,
        "assignedAt" => parcel.AssignedAt,
        _ => parcel.LastMovementAt,
    };

    /// <summary>`compare` : les vides en dernier quel que soit le sens ; le texte à la française, sans casse ni accent.</summary>
    private static int Compare(object? a, object? b, string order)
    {
        if (a is null && b is null) return 0;
        if (a is null) return 1;
        if (b is null) return -1;
        var result = (a, b) switch
        {
            (string x, string y) => French.Compare(x, y, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace),
            (IComparable x, _) => x.CompareTo(b),
            _ => 0,
        };
        return order == "asc" ? result : -result;
    }

    public static string Normalize(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.ToLowerInvariant(ch));
        }
        return builder.ToString().Trim();
    }

    /// <summary>Les paramètres de la liste, validés comme `courierStockListQuerySchema`.</summary>
    public static CourierStockListFilter ListFilter(ListQueryParams query, string[]? agencyId, string? all)
    {
        var paging = new ListQueryParams { Page = query.Page, PageSize = query.PageSize, Search = query.Search }.ToQueryWithDefault(25);
        var issues = new List<object>();
        var sort = query.Sort ?? "inHand";
        if (!Sorts.Contains(sort)) issues.Add(new { path = "sort", message = Common.Validation.Zod.InvalidOption(Sorts) });
        var order = query.Order ?? "desc";
        if (order is not ("asc" or "desc")) issues.Add(new { path = "order", message = "Invalid option: expected one of \"asc\"|\"desc\"" });
        if (all is not null and not ("true" or "false")) issues.Add(new { path = "all", message = "Invalid option: expected one of \"true\"|\"false\"" });
        if (issues.Count > 0) throw BusinessException.Validation("Saisie invalide.", new { issues });
        return new CourierStockListFilter(paging.Page, paging.PageSize, paging.Search, MultiValue.AsList(agencyId), all, sort, order);
    }

    /// <summary>Les paramètres du détail, validés comme `courierStockParcelQuerySchema`.</summary>
    public static CourierStockParcelFilter ParcelFilter(ListQueryParams query, string? statusId, string[]? clientId, string[]? cityId, string[]? routeId)
    {
        var paging = new ListQueryParams { Page = query.Page, PageSize = query.PageSize, Search = query.Search }.ToQueryWithDefault(25);
        var issues = new List<object>();
        var sort = query.Sort ?? "lastMovementAt";
        if (!ParcelSorts.Contains(sort)) issues.Add(new { path = "sort", message = Common.Validation.Zod.InvalidOption(ParcelSorts) });
        var order = query.Order ?? "desc";
        if (order is not ("asc" or "desc")) issues.Add(new { path = "order", message = "Invalid option: expected one of \"asc\"|\"desc\"" });
        int? status = null;
        if (statusId is not null)
        {
            var text = statusId.Trim();
            if (text.Length == 0) status = 0;
            else if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) issues.Add(new { path = "statusId", message = "Invalid input: expected number, received NaN" });
            else if (Math.Floor(value) != value) issues.Add(new { path = "statusId", message = "Invalid input: expected int, received number" });
            else status = (int)value;
        }
        if (issues.Count > 0) throw BusinessException.Validation("Saisie invalide.", new { issues });
        return new CourierStockParcelFilter(paging.Page, paging.PageSize, paging.Search, status, MultiValue.AsList(clientId), MultiValue.AsList(cityId), MultiValue.AsList(routeId), sort, order);
    }
}
