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

/// <summary>`orderSchema` : une commande de produits stockés, à transformer en colis.</summary>
public sealed record CreateOrderCommand : IRequest<OrderDetailDto>
{
    public static readonly string[] Sources = ["manual", "excel"];

    public sealed record Line(string? ProductId, double? Quantity, double? UnitPrice);

    public string? ClientId { get; init; }
    public string? AgencyId { get; init; }
    public string? Source { get; init; } = "manual";
    public string? ExternalRef { get; init => field = Zod.Trim(value); }
    public string? RecipientName { get; init => field = Zod.Trim(value); }
    public string? RecipientPhone { get; init => field = value is null ? null : Phones.Normalize(value); }
    public string? DestinationCityId { get; init; }
    public string? DestinationDistrictId { get; init; }
    public string? DestinationDistrictName { get; init => field = Zod.Trim(value); }
    public string? DeliveryAddress { get; init => field = Zod.Trim(value); }
    public string? Note { get; init => field = Zod.Trim(value); }
    public IReadOnlyList<Line>? Lines { get; init; }
}

public sealed class CreateOrderValidator : ZodValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Must(v => v is null || v.Length >= 1).WithMessage(Zod.InvalidInput);
        RuleFor(x => x.Source).OneOf(CreateOrderCommand.Sources);
        RuleFor(x => x.ExternalRef).MaxLength(64);
        RuleFor(x => x.RecipientName).Required().MinLength(1, "required").MaxLength(120);
        RuleFor(x => x.RecipientPhone).Required().Pattern(Phones.Pattern, "invalid-phone");
        RuleFor(x => x.DestinationCityId).Required().MinLength(1, "required");
        RuleFor(x => x.DestinationDistrictId).Must(v => v is null || v.Length >= 1).WithMessage(Zod.InvalidInput);
        RuleFor(x => x.DestinationDistrictName).MaxLength(ParcelInputs.DistrictNameMax);
        RuleFor(x => x.DeliveryAddress).Required().MinLength(1, "required").MaxLength(255);
        RuleFor(x => x.Note).MaxLength(500);
        RuleFor(x => x.Lines).Required("array").Custom((lines, context) =>
        {
            if (lines is null) return;
            if (lines.Count < 1) context.AddFailure(ValidationPaths.Raw + "lines", "required");
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line.ProductId)) context.AddFailure(ValidationPaths.Raw + $"lines.{i}.productId", line.ProductId is null ? Zod.Expected("string") : "required");
                if (line.Quantity is not { } q) context.AddFailure(ValidationPaths.Raw + $"lines.{i}.quantity", Zod.Expected("number"));
                else if (Math.Floor(q) != q) context.AddFailure(ValidationPaths.Raw + $"lines.{i}.quantity", "not-integer");
                else if (q < 1) context.AddFailure(ValidationPaths.Raw + $"lines.{i}.quantity", "too-small");
                else if (q > 10_000) context.AddFailure(ValidationPaths.Raw + $"lines.{i}.quantity", "too-large");
                if (line.UnitPrice is < 0) context.AddFailure(ValidationPaths.Raw + $"lines.{i}.unitPrice", "negative-amount");
                else if (line.UnitPrice is > 1_000_000) context.AddFailure(ValidationPaths.Raw + $"lines.{i}.unitPrice", "amount-too-large");
            }
        });
    }
}

internal sealed class CreateOrderHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<CreateOrderCommand, OrderDetailDto>
{
    public async Task<OrderDetailDto> Handle(CreateOrderCommand input, CancellationToken cancellationToken)
    {
        var clientId = WarehouseScope.IsMerchant(caller) ? caller.ClientId! : input.ClientId!;
        var ids = input.Lines!.Select(l => l.ProductId!).Distinct().ToList();
        var prices = await db.Products.AsNoTracking().Where(p => p.ClientId == clientId && ids.Contains(p.Id) && p.DeletedAt == null)
            .Select(p => new { p.Id, p.Price }).ToDictionaryAsync(p => p.Id, p => Money.ToCents(p.Price), cancellationToken);
        if (prices.Count != ids.Count) throw BusinessException.Validation("Produit inconnu, ou appartenant à un autre e-commerçant.", new { field = "lines" });
        var lines = input.Lines!.Select(l => (l.ProductId!, Quantity: (int)l.Quantity!.Value, UnitPrice: l.UnitPrice is { } price ? Money.MadToCents(price) : prices.GetValueOrDefault(l.ProductId!))).ToList();
        var externalRef = string.IsNullOrEmpty(input.ExternalRef) ? null : input.ExternalRef;
        if (externalRef is not null)
        {
            var existing = await db.Orders.AsNoTracking().Where(o => o.ClientId == clientId && o.ExternalRef == externalRef).Select(o => o.Code).FirstOrDefaultAsync(cancellationToken);
            if (existing is not null) throw BusinessException.Conflict($"La commande « {externalRef} » existe déjà ({existing}).", new { reason = "order-duplicate", code = existing });
        }
        var total = lines.Sum(l => l.UnitPrice * l.Quantity);
        var district = await ParcelInputs.ResolveDistrictAsync(db, input.DestinationCityId!, input.DestinationDistrictId, input.DestinationDistrictName, cancellationToken);
        var now = clock.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var code = await DocumentCodes.ReserveAsync(db, OrderRows.Prefix, now, cancellationToken);
        var order = new Order
        {
            Code = code,
            ClientId = clientId,
            AgencyId = string.IsNullOrEmpty(input.AgencyId) ? null : input.AgencyId,
            Source = input.Source == "excel" ? OrderSource.EXCEL : OrderSource.MANUAL,
            ExternalRef = externalRef,
            RecipientName = input.RecipientName!,
            RecipientPhone = input.RecipientPhone!,
            DestinationCityId = input.DestinationCityId!,
            DestinationDistrictId = district.DistrictId,
            DestinationDistrictName = district.DistrictName,
            DeliveryAddress = input.DeliveryAddress!,
            Note = string.IsNullOrEmpty(input.Note) ? null : input.Note,
            TotalAmount = Money.FromCents(total),
            CreatedById = caller.UserId,
        };
        db.Orders.Add(order);
        foreach (var (productId, quantity, unitPrice) in lines) db.OrderLines.Add(new OrderLine { OrderId = order.Id, ProductId = productId, Quantity = quantity, UnitPrice = Money.FromCents(unitPrice) });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await OrderRows.DetailAsync(db, caller, code, cancellationToken);
    }
}
