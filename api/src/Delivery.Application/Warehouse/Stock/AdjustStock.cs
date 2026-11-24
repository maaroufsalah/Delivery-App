using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Warehouse.Stock;

/// <summary>`POST /stock/adjust` (`stockAdjustSchema`) : un ajustement motivé, par le transporteur.</summary>
public sealed record AdjustStockCommand : IRequest<StockDetailDto>
{
    public string? ProductId { get; init; }
    public string? AgencyId { get; init; }
    public double? Quantity { get; init; }
    public string? Reason { get; init => field = Zod.Trim(value); }
}

public sealed class AdjustStockValidator : ZodValidator<AdjustStockCommand>
{
    public AdjustStockValidator()
    {
        RuleFor(x => x.ProductId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.Quantity).Required("number").Int().WithMessage("not-integer");
        RuleFor(x => x.Reason).Required().MinLength(1, "required").MaxLength(255);
    }

    protected override void Refine(AdjustStockCommand value, Action<string, string> issue)
    {
        if (value.Quantity == 0) issue(ValidationPaths.Raw + "quantity", "zero-movement");
    }
}

internal sealed class AdjustStockHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AdjustStockCommand, StockDetailDto>
{
    public async Task<StockDetailDto> Handle(AdjustStockCommand input, CancellationToken cancellationToken)
    {
        if (WarehouseScope.IsMerchant(caller)) throw BusinessException.Forbidden("L’ajustement du stock appartient au transporteur.");
        if (!await WarehouseScope.Products(db.Products, caller).AnyAsync(p => p.Id == input.ProductId && p.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("Produit introuvable.");
        if (!await db.Agencies.AnyAsync(a => a.Id == input.AgencyId && a.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("Agence introuvable.");
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await StockMoves.MoveAsync(db, input.ProductId!, input.AgencyId!, (int)input.Quantity!.Value, StockMovementKind.ADJUST, input.Reason, null, null, caller.UserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        var id = await db.StockItems.Where(s => s.ProductId == input.ProductId && s.AgencyId == input.AgencyId).Select(s => s.Id).FirstAsync(cancellationToken);
        return await StockRows.DetailAsync(db, caller, id, cancellationToken);
    }
}
