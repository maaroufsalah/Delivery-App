using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Warehouse.Stock;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Warehouse.Storage;

/// <summary>`storageRequestSchema` : un dépôt de marchandise dans une agence.</summary>
public sealed record CreateStorageRequestCommand : IRequest<StorageRequestDetailDto>
{
    public sealed record Item(string? ProductId, double? Quantity);

    public string? ClientId { get; init; }
    public string? AgencyId { get; init; }
    public string? Note { get; init => field = Zod.Trim(value); }
    public IReadOnlyList<Item>? Items { get; init; }
}

public sealed class CreateStorageRequestValidator : ZodValidator<CreateStorageRequestCommand>
{
    public CreateStorageRequestValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.Note).MaxLength(500);
        RuleFor(x => x.Items).Required("array").Custom((items, context) =>
        {
            if (items is null) return;
            if (items.Count < 1) context.AddFailure(ValidationPaths.Raw + "items", "required");
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (string.IsNullOrEmpty(item.ProductId)) context.AddFailure(ValidationPaths.Raw + $"items.{i}.productId", item.ProductId is null ? Zod.Expected("string") : "required");
                if (item.Quantity is not { } q) context.AddFailure(ValidationPaths.Raw + $"items.{i}.quantity", Zod.Expected("number"));
                else if (Math.Floor(q) != q) context.AddFailure(ValidationPaths.Raw + $"items.{i}.quantity", "not-integer");
                else if (q < 1) context.AddFailure(ValidationPaths.Raw + $"items.{i}.quantity", "too-small");
                else if (q > 100_000) context.AddFailure(ValidationPaths.Raw + $"items.{i}.quantity", "too-large");
            }
        });
    }
}

internal sealed class CreateStorageRequestHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<CreateStorageRequestCommand, StorageRequestDetailDto>
{
    public async Task<StorageRequestDetailDto> Handle(CreateStorageRequestCommand input, CancellationToken cancellationToken)
    {
        var clientId = WarehouseScope.IsMerchant(caller) ? caller.ClientId! : input.ClientId!;
        if (!await db.Clients.AnyAsync(c => c.Id == clientId && c.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("E-commerçant introuvable.");
        if (!await db.Agencies.AnyAsync(a => a.Id == input.AgencyId && a.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("Agence introuvable.");
        var productIds = input.Items!.Select(i => i.ProductId!).Distinct().ToList();
        var found = await db.Products.CountAsync(p => p.ClientId == clientId && productIds.Contains(p.Id) && p.DeletedAt == null, cancellationToken);
        if (found != productIds.Count) throw BusinessException.Validation("Produit inconnu, ou appartenant à un autre e-commerçant.", new { field = "items" });
        var now = clock.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var code = await DocumentCodes.ReserveAsync(db, StorageRows.Prefix, now, cancellationToken);
        var request = new StorageRequest
        {
            Code = code,
            ClientId = clientId,
            AgencyId = input.AgencyId!,
            RequestedAt = now,
            Note = string.IsNullOrEmpty(input.Note) ? null : input.Note,
            CreatedById = caller.UserId,
        };
        db.StorageRequests.Add(request);
        foreach (var item in input.Items!) db.StorageRequestItems.Add(new StorageRequestItem { RequestId = request.Id, ProductId = item.ProductId!, Quantity = (int)item.Quantity!.Value });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await StorageRows.DetailAsync(db, caller, code, cancellationToken);
    }
}
