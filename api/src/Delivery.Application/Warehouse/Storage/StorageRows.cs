using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Warehouse.Storage;

/// <summary>`StorageRequestListItem`.</summary>
public class StorageRequestListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string AgencyName { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public int TotalQuantity { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record StorageRequestItemDto(string Id, string ProductId, string ProductName, string? ProductReference, int Quantity);

/// <summary>`StorageRequestDetail`.</summary>
public sealed class StorageRequestDetailDto : StorageRequestListItemDto
{
    [JsonPropertyOrder(1)]
    public string? Note { get; set; }

    [JsonPropertyOrder(1)]
    public string? RefusalReason { get; set; }

    [JsonPropertyOrder(1)]
    public bool Decidable { get; set; }

    [JsonPropertyOrder(1)]
    public List<StorageRequestItemDto> Items { get; set; } = [];
}

/// <summary>`storageListQuerySchema` une fois lu.</summary>
public sealed record StorageListFilter(ListQuery Paging, string Order, IReadOnlyList<string>? ClientId, IReadOnlyList<string>? AgencyId, IReadOnlyList<string>? Status);

public static class StorageRows
{
    public const string Prefix = "ENT";

    internal sealed record Row(string Id, string Code, StorageRequestStatus Status, string ClientId, string ClientName, string AgencyId, string AgencyName, int LineCount, int TotalQuantity,
        DateTime RequestedAt, DateTime? DecidedAt, string? DecidedFirst, string? DecidedLast, DateTime CreatedAt, string? Note, string? RefusalReason);

    internal static IQueryable<Row> Project(IQueryable<StorageRequest> requests) => requests.Select(r => new Row(r.Id, r.Code, r.Status, r.ClientId, r.Client.Name, r.AgencyId, r.Agency.Name,
        r.Items.Count, r.Items.Sum(i => i.Quantity), r.RequestedAt, r.DecidedAt, r.DecidedBy != null ? r.DecidedBy.FirstName : null, r.DecidedBy != null ? r.DecidedBy.LastName : null,
        r.CreatedAt, r.Note, r.RefusalReason));

    internal static T Fill<T>(T dto, Row r)
        where T : StorageRequestListItemDto
    {
        dto.Id = r.Id;
        dto.Code = r.Code;
        dto.Status = Money.ToWire(r.Status);
        dto.ClientId = r.ClientId;
        dto.ClientName = r.ClientName;
        dto.AgencyId = r.AgencyId;
        dto.AgencyName = r.AgencyName;
        dto.LineCount = r.LineCount;
        dto.TotalQuantity = r.TotalQuantity;
        dto.RequestedAt = r.RequestedAt;
        dto.DecidedAt = r.DecidedAt;
        dto.DecidedByName = r.DecidedFirst is null ? null : $"{r.DecidedFirst} {r.DecidedLast}";
        dto.CreatedAt = r.CreatedAt;
        return dto;
    }

    internal static IQueryable<StorageRequest> Filter(IQueryable<StorageRequest> requests, ICurrentUser caller, StorageListFilter filter)
    {
        var where = WarehouseScope.Storage(requests, caller);
        if (filter.ClientId is { Count: > 0 } clients && !WarehouseScope.IsMerchant(caller)) where = where.Where(r => clients.Contains(r.ClientId));
        if (filter.AgencyId is { Count: > 0 } agencies && caller.MayFilterBy(caller.AgencyId)) where = where.Where(r => agencies.Contains(r.AgencyId));
        if (filter.Status is { Count: > 0 } statuses)
        {
            var wanted = statuses.Select(s => s == "pending" ? StorageRequestStatus.PENDING : s == "validated" ? StorageRequestStatus.VALIDATED : StorageRequestStatus.REFUSED).ToList();
            where = where.Where(r => wanted.Contains(r.Status));
        }
        if (!string.IsNullOrEmpty(filter.Paging.Search))
        {
            var pattern = Like.Contains(filter.Paging.Search);
            where = where.Where(r => EF.Functions.ILike(r.Code, pattern) || EF.Functions.ILike(r.Client.Name, pattern));
        }
        return filter.Order == "asc" ? where.OrderBy(r => r.RequestedAt) : where.OrderByDescending(r => r.RequestedAt);
    }

    public static async Task<StorageRequestDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var row = await Project(WarehouseScope.Storage(db.StorageRequests.AsNoTracking(), caller).Where(r => r.Code == code)).FirstOrDefaultAsync(cancellationToken)
                  ?? throw BusinessException.NotFound($"Demande introuvable : {code}");
        var detail = Fill(new StorageRequestDetailDto(), row);
        detail.Note = row.Note;
        detail.RefusalReason = row.RefusalReason;
        detail.Items = await db.StorageRequestItems.AsNoTracking().Where(i => i.RequestId == row.Id).OrderBy(i => i.CreatedAt)
            .Select(i => new StorageRequestItemDto(i.Id, i.ProductId, i.Product.Name, i.Product.Reference, i.Quantity)).ToListAsync(cancellationToken);
        detail.Decidable = row.Status == StorageRequestStatus.PENDING && detail.Items.Count > 0;
        return detail;
    }
}
