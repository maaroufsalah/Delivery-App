using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels;
using Delivery.Application.Warehouse.Stock;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Warehouse.Orders;

/// <summary>`POST /orders/:code/parcel` (`orderToParcelSchema`) : le colis de la commande, le stock sort.</summary>
public sealed record CreateOrderParcelCommand : IRequest<OrderDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public bool? PickupAtSender { get; init; } = true;
    public bool? FeeIncluded { get; init; } = true;
    public bool? Fragile { get; init; } = false;
    public bool? OpenOnDelivery { get; init; } = false;
}

public sealed class CreateOrderParcelValidator : ZodValidator<CreateOrderParcelCommand>;

internal sealed class CreateOrderParcelHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<CreateOrderParcelCommand, OrderDetailDto>
{
    public async Task<OrderDetailDto> Handle(CreateOrderParcelCommand input, CancellationToken cancellationToken)
    {
        var order = await WarehouseScope.Orders(db.Orders, caller).Include(o => o.Lines).Include(o => o.Client).FirstOrDefaultAsync(o => o.Code == input.Code, cancellationToken)
                    ?? throw BusinessException.NotFound($"Commande introuvable : {input.Code}");
        if (order.Status == OrderStatus.PARCEL_CREATED || order.ParcelId is not null) throw BusinessException.Conflict("Le colis de cette commande existe déjà.", new { reason = "order-has-parcel" });
        if (order.Status != OrderStatus.CONFIRMED) throw BusinessException.Conflict("Confirmez la commande avant de créer le colis.", new { reason = "order-not-confirmed" });
        if (order.Lines.Count == 0) throw BusinessException.Validation("Une commande sans ligne ne donne pas de colis.");
        var originCityId = order.Client.CityId;
        var agencyId = await ResolveAgencyAsync(db, order.AgencyId, originCityId, cancellationToken);
        var hasDistrict = !string.IsNullOrEmpty(order.DestinationDistrictId) || !string.IsNullOrEmpty(order.DestinationDistrictName);
        var quote = await ParcelPricing.QuoteAsync(db, originCityId, order.DestinationCityId, hasDistrict ? "district" : "agency", order.ClientId, cancellationToken);
        var now = clock.UtcNow;
        var statusId = input.PickupAtSender == false ? 2 : 1;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var parcelCode = await ParcelCodes.ReserveAsync(db, originCityId, order.DestinationCityId, now, cancellationToken);
        await ParcelLookup.AssertExternalCodeFreeAsync(db.Parcels, order.ClientId, order.ExternalRef, null, cancellationToken);
        var parcel = new Parcel
        {
            Code = parcelCode,
            ExternalCode = order.ExternalRef,
            ClientId = order.ClientId,
            AgencyId = agencyId,
            StatusId = statusId,
            StatusAt = now,
            OriginCityId = originCityId,
            DestinationCityId = order.DestinationCityId,
            DestinationDistrictId = order.DestinationDistrictId,
            DestinationDistrictName = order.DestinationDistrictName,
            DeliveryMode = hasDistrict ? DeliveryMode.DISTRICT : DeliveryMode.AGENCY,
            CurrentCityId = originCityId,
            Description = $"Commande {order.Code}",
            RecipientName = order.RecipientName,
            RecipientPhone = order.RecipientPhone,
            PickupAddress = string.Empty,
            DeliveryAddress = order.DeliveryAddress,
            CodAmount = order.TotalAmount,
            DeliveryFee = Money.FromCents(quote.Fee),
            FeeIncluded = true,
            AlreadyPaid = false,
            OpenOnDelivery = input.OpenOnDelivery ?? false,
            Fragile = input.Fragile ?? false,
        };
        db.Parcels.Add(parcel);
        foreach (var line in order.Lines) db.ParcelItems.Add(new ParcelItem { ParcelId = parcel.Id, ProductId = line.ProductId, Quantity = line.Quantity });
        db.ParcelEvents.Add(new ParcelEvent { ParcelId = parcel.Id, StatusId = statusId, Comment = $"Créé depuis la commande {order.Code}", AuthorId = caller.UserId, CityId = originCityId, CreatedAt = now });
        await db.SaveChangesAsync(cancellationToken);
        foreach (var line in order.Lines)
        {
            await StockMoves.MoveAsync(db, line.ProductId, agencyId, -line.Quantity, StockMovementKind.OUT, null, null, parcel.Id, caller.UserId, cancellationToken);
        }
        var marked = await db.Orders.Where(o => o.Id == order.Id && o.Status == OrderStatus.CONFIRMED && o.ParcelId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.PARCEL_CREATED).SetProperty(o => o.ParcelId, parcel.Id), cancellationToken);
        if (marked == 0) throw BusinessException.Conflict("Le colis de cette commande vient d’être créé.", new { reason = "order-has-parcel" });
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return await OrderRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }

    private static async Task<string> ResolveAgencyAsync(IAppDbContext db, string? agencyId, string cityId, CancellationToken cancellationToken)
    {
        if (agencyId is not null && await db.Agencies.AnyAsync(a => a.Id == agencyId && a.DeletedAt == null, cancellationToken)) return agencyId;
        return await db.Agencies.AsNoTracking().Where(a => a.CityId == cityId && a.DeletedAt == null).OrderBy(a => a.CreatedAt).Select(a => a.Id).FirstOrDefaultAsync(cancellationToken)
               ?? await db.Agencies.AsNoTracking().Where(a => a.DeletedAt == null).OrderBy(a => a.CreatedAt).Select(a => a.Id).FirstOrDefaultAsync(cancellationToken)
               ?? throw BusinessException.Validation("Aucune agence disponible.");
    }
}
