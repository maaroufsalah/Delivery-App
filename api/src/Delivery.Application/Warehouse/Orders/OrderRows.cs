using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Warehouse.Orders;

/// <summary>`OrderListItem`.</summary>
public class OrderListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? ExternalRef { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string? AgencyId { get; set; }
    public string? AgencyName { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string DestinationCityId { get; set; } = string.Empty;
    public string DestinationCityName { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public long TotalAmount { get; set; }
    public string? ParcelId { get; set; }
    public string? ParcelCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record OrderLineDto(string Id, string ProductId, string ProductName, string? ProductReference, int Quantity, long UnitPrice);

/// <summary>`OrderDetail`.</summary>
public sealed class OrderDetailDto : OrderListItemDto
{
    [JsonPropertyOrder(1)]
    public string DeliveryAddress { get; set; } = string.Empty;

    [JsonPropertyOrder(1)]
    public string? DestinationDistrictId { get; set; }

    [JsonPropertyOrder(1)]
    public string? DestinationDistrictName { get; set; }

    [JsonPropertyOrder(1)]
    public string? Note { get; set; }

    [JsonPropertyOrder(1)]
    public bool ParcelCreatable { get; set; }

    [JsonPropertyOrder(1)]
    public List<OrderLineDto> Lines { get; set; } = [];
}

/// <summary>`orderListQuerySchema` une fois lu.</summary>
public sealed record OrderListFilter(ListQuery Paging, string Order, IReadOnlyList<string>? ClientId, IReadOnlyList<string>? AgencyId, IReadOnlyList<string>? Status, string? Source);

public static class OrderRows
{
    public const string Prefix = "CMD";

    internal sealed record Row(string Id, string Code, OrderStatus Status, OrderSource Source, string? ExternalRef, string ClientId, string ClientName, string? AgencyId, string? AgencyName,
        string RecipientName, string RecipientPhone, string DestinationCityId, string DestinationCityName, int LineCount, decimal TotalAmount, string? ParcelId, string? ParcelCode, DateTime CreatedAt,
        string DeliveryAddress, string? DestinationDistrictId, string? DestinationDistrictName, string? DistrictRefName, string? Note);

    internal static IQueryable<Row> Project(IQueryable<Order> orders) => orders.Select(o => new Row(o.Id, o.Code, o.Status, o.Source, o.ExternalRef, o.ClientId, o.Client.Name, o.AgencyId,
        o.Agency != null ? o.Agency.Name : null, o.RecipientName, o.RecipientPhone, o.DestinationCityId, o.DestinationCity.Name, o.Lines.Count, o.TotalAmount, o.ParcelId,
        o.Parcel != null ? o.Parcel.Code : null, o.CreatedAt, o.DeliveryAddress, o.DestinationDistrictId, o.DestinationDistrictName,
        o.DestinationDistrict != null ? o.DestinationDistrict.Name : null, o.Note));

    internal static T Fill<T>(T dto, Row r)
        where T : OrderListItemDto
    {
        dto.Id = r.Id;
        dto.Code = r.Code;
        dto.Status = Money.ToWire(r.Status);
        dto.Source = Money.ToWire(r.Source);
        dto.ExternalRef = r.ExternalRef;
        dto.ClientId = r.ClientId;
        dto.ClientName = r.ClientName;
        dto.AgencyId = r.AgencyId;
        dto.AgencyName = r.AgencyName;
        dto.RecipientName = r.RecipientName;
        dto.RecipientPhone = r.RecipientPhone;
        dto.DestinationCityId = r.DestinationCityId;
        dto.DestinationCityName = r.DestinationCityName;
        dto.LineCount = r.LineCount;
        dto.TotalAmount = Money.ToCents(r.TotalAmount);
        dto.ParcelId = r.ParcelId;
        dto.ParcelCode = r.ParcelCode;
        dto.CreatedAt = r.CreatedAt;
        return dto;
    }

    internal static IQueryable<Order> Filter(IQueryable<Order> orders, ICurrentUser caller, OrderListFilter filter)
    {
        var where = WarehouseScope.Orders(orders, caller);
        if (filter.ClientId is { Count: > 0 } clients && !WarehouseScope.IsMerchant(caller)) where = where.Where(o => clients.Contains(o.ClientId));
        if (filter.AgencyId is { Count: > 0 } agencies && caller.MayFilterBy(caller.AgencyId)) where = where.Where(o => o.AgencyId != null && agencies.Contains(o.AgencyId));
        if (filter.Status is { Count: > 0 } statuses)
        {
            var wanted = statuses.Select(s => s switch
            {
                "confirmed" => OrderStatus.CONFIRMED,
                "parcel-created" => OrderStatus.PARCEL_CREATED,
                "cancelled" => OrderStatus.CANCELLED,
                _ => OrderStatus.DRAFT,
            }).ToList();
            where = where.Where(o => wanted.Contains(o.Status));
        }
        if (!string.IsNullOrEmpty(filter.Source))
        {
            var source = filter.Source == "excel" ? OrderSource.EXCEL : OrderSource.MANUAL;
            where = where.Where(o => o.Source == source);
        }
        if (!string.IsNullOrEmpty(filter.Paging.Search))
        {
            var pattern = Like.Contains(filter.Paging.Search);
            where = where.Where(o => EF.Functions.ILike(o.Code, pattern) || EF.Functions.ILike(o.ExternalRef!, pattern) || EF.Functions.ILike(o.RecipientName, pattern));
        }
        return filter.Order == "asc" ? where.OrderBy(o => o.CreatedAt) : where.OrderByDescending(o => o.CreatedAt);
    }

    public static async Task<OrderDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var row = await Project(WarehouseScope.Orders(db.Orders.AsNoTracking(), caller).Where(o => o.Code == code)).FirstOrDefaultAsync(cancellationToken)
                  ?? throw BusinessException.NotFound($"Commande introuvable : {code}");
        var detail = Fill(new OrderDetailDto(), row);
        detail.DeliveryAddress = row.DeliveryAddress;
        detail.DestinationDistrictId = row.DestinationDistrictId;
        detail.DestinationDistrictName = ParcelViews.DistrictLabel(row.DestinationDistrictName, row.DistrictRefName);
        detail.Note = row.Note;
        detail.Lines = (await db.OrderLines.AsNoTracking().Where(l => l.OrderId == row.Id).OrderBy(l => l.CreatedAt)
            .Select(l => new { l.Id, l.ProductId, l.Product.Name, l.Product.Reference, l.Quantity, l.UnitPrice }).ToListAsync(cancellationToken))
            .Select(l => new OrderLineDto(l.Id, l.ProductId, l.Name, l.Reference, l.Quantity, Money.ToCents(l.UnitPrice))).ToList();
        detail.ParcelCreatable = row.Status == OrderStatus.CONFIRMED && detail.Lines.Count > 0;
        return detail;
    }
}
