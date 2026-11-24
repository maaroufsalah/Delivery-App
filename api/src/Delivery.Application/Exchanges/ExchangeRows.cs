using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Exchanges.Dtos;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Exchanges;

/// <summary>Ligne EF d'un échange (`INCLUDE` d'`ExchangesService`).</summary>
public sealed class ExchangeRow
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ExchangeStatus Status { get; set; }
    public string OriginalParcelId { get; set; } = string.Empty;
    public string OriginalParcelCode { get; set; } = string.Empty;
    public string? NewParcelId { get; set; }
    public string? NewParcelCode { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ReturnedProduct { get; set; } = string.Empty;
    public string NewProduct { get; set; } = string.Empty;
    public string OldDestination { get; set; } = string.Empty;
    public string NewDestination { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Fees { get; set; }
    public string? CourierId { get; set; }
    public string? CourierFirstName { get; set; }
    public string? CourierLastName { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? Note { get; set; }
}

public sealed class ExchangesProfile : Profile
{
    public ExchangesProfile()
    {
        CreateProjection<Exchange, ExchangeRow>()
            .ForMember(d => d.OriginalParcelCode, o => o.MapFrom(s => s.OriginalParcel.Code))
            .ForMember(d => d.NewParcelCode, o => o.MapFrom(s => s.NewParcel != null ? s.NewParcel.Code : null))
            .ForMember(d => d.ClientName, o => o.MapFrom(s => s.OriginalParcel.Client.Name))
            .ForMember(d => d.CourierFirstName, o => o.MapFrom(s => s.Courier != null ? s.Courier.FirstName : null))
            .ForMember(d => d.CourierLastName, o => o.MapFrom(s => s.Courier != null ? s.Courier.LastName : null));
    }
}

public static class ExchangeRows
{
    /// <summary>`scoped` : un échange se voit par son colis d'origine.</summary>
    public static IQueryable<Exchange> Scoped(IQueryable<Exchange> exchanges, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return exchanges;
        if (caller.ClientId is { } clientId) return exchanges.Where(x => x.OriginalParcel.ClientId == clientId);
        if (caller.CourierId is { } courierId) return exchanges.Where(x => x.OriginalParcel.CourierId == courierId);
        if (caller.AgencyId is { } agencyId) return exchanges.Where(x => x.OriginalParcel.AgencyId == agencyId);
        return exchanges.Where(x => x.Id == Scope.DenyAllId);
    }

    public static T Fill<T>(T dto, ExchangeRow row)
        where T : ExchangeListItemDto
    {
        dto.Id = row.Id;
        dto.Code = row.Code;
        dto.Status = Money.ToWire(row.Status);
        dto.OriginalParcelId = row.OriginalParcelId;
        dto.OriginalParcelCode = row.OriginalParcelCode;
        dto.NewParcelId = row.NewParcelId;
        dto.NewParcelCode = row.NewParcelCode;
        dto.ClientName = row.ClientName;
        dto.ReturnedProduct = row.ReturnedProduct;
        dto.NewProduct = row.NewProduct;
        dto.OldDestination = row.OldDestination;
        dto.NewDestination = row.NewDestination;
        dto.Amount = Money.ToCents(row.Amount);
        dto.Fees = Money.ToCents(row.Fees);
        dto.CourierId = row.CourierId;
        dto.CourierName = row.CourierFirstName is null ? null : $"{row.CourierFirstName} {row.CourierLastName}";
        dto.RequestedAt = row.RequestedAt;
        dto.PickedUpAt = row.PickedUpAt;
        dto.DeliveredAt = row.DeliveredAt;
        dto.ClosedAt = row.ClosedAt;
        dto.CancelledAt = row.CancelledAt;
        return dto;
    }

    public static async Task<ExchangeDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, IMapper mapper, string code, CancellationToken cancellationToken)
    {
        var row = await mapper.ProjectTo<ExchangeRow>(Scoped(db.Exchanges.AsNoTracking(), caller).Where(x => x.Code == code)).FirstOrDefaultAsync(cancellationToken)
                  ?? throw BusinessException.NotFound($"Échange introuvable : {code}");
        var events = await db.ExchangeEvents.AsNoTracking().Where(e => e.ExchangeId == row.Id).OrderBy(e => e.CreatedAt)
            .Select(e => new { e.Id, e.Status, e.Comment, e.AuthorName, e.CreatedAt }).ToListAsync(cancellationToken);
        var agency = caller.ClientId is null;
        var detail = Fill(new ExchangeDetailDto(), row);
        detail.Note = row.Note;
        detail.Events = events.Select(e => new ExchangeEventDto(e.Id, Money.ToWire(e.Status), e.Comment, e.AuthorName, e.CreatedAt)).ToList();
        detail.Pickupable = agency && row.Status == ExchangeStatus.NOUVEAU_COLIS_CREE;
        detail.Deliverable = agency && row.Status == ExchangeStatus.ANCIEN_RECUPERE;
        detail.Closable = agency && row.Status == ExchangeStatus.NOUVEAU_LIVRE;
        detail.Cancellable = agency && ParcelAdmin.CanCancelExchange(row.Status);
        return detail;
    }

    /// <summary>`authorName` d'`ExchangesService` : sans espace rogné, « Système » sans auteur.</summary>
    public static async Task<string> AuthorNameAsync(IAppDbContext db, string? userId, CancellationToken cancellationToken)
    {
        if (userId is null) return "Système";
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.FirstName, u.LastName }).FirstOrDefaultAsync(cancellationToken);
        return user is null ? "Système" : $"{user.FirstName} {user.LastName}";
    }

    public static void AssertAgency(ICurrentUser caller, string message)
    {
        if (caller.ClientId is not null) throw BusinessException.Forbidden(message, new { reason = "exchange-is-agency-only" });
    }
}
