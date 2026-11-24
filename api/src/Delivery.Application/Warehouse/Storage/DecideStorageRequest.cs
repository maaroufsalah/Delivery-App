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

/// <summary>`POST /storage-requests/:code/decision` : valider (entrée en stock) ou refuser (motif).</summary>
public sealed record DecideStorageRequestCommand : IRequest<StorageRequestDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public bool? Approve { get; init; }
    public string? RefusalReason { get; init => field = Zod.Trim(value); }
}

public sealed class DecideStorageRequestValidator : ZodValidator<DecideStorageRequestCommand>
{
    public DecideStorageRequestValidator()
    {
        RuleFor(x => x.Approve).Required("boolean");
        RuleFor(x => x.RefusalReason).MaxLength(255);
    }

    protected override void Refine(DecideStorageRequestCommand value, Action<string, string> issue)
    {
        if (value.Approve != true && string.IsNullOrWhiteSpace(value.RefusalReason)) issue(ValidationPaths.Raw + "refusalReason", "required");
    }
}

internal sealed class DecideStorageRequestHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<DecideStorageRequestCommand, StorageRequestDetailDto>
{
    public async Task<StorageRequestDetailDto> Handle(DecideStorageRequestCommand input, CancellationToken cancellationToken)
    {
        var request = await WarehouseScope.Storage(db.StorageRequests, caller).Include(r => r.Items).FirstOrDefaultAsync(r => r.Code == input.Code, cancellationToken)
                      ?? throw BusinessException.NotFound($"Demande introuvable : {input.Code}");
        if (request.Status != StorageRequestStatus.PENDING) throw BusinessException.Conflict("Cette demande est déjà tranchée.", new { reason = "storage-already-decided" });
        if (request.Items.Count == 0) throw BusinessException.Validation("Une demande sans ligne ne se valide pas.");
        if (WarehouseScope.IsMerchant(caller)) throw BusinessException.Forbidden("La validation appartient au transporteur.");
        if (!caller.IsSuperAdmin() && caller.AgencyId is { } own && own != request.AgencyId
            && !await db.UserAgencies.AnyAsync(l => l.UserId == caller.UserId && l.AgencyId == request.AgencyId, cancellationToken))
        {
            throw BusinessException.Forbidden("Cette agence n'est pas dans votre périmètre.");
        }
        var approve = input.Approve == true;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (approve)
        {
            foreach (var item in request.Items)
            {
                await StockMoves.MoveAsync(db, item.ProductId, request.AgencyId, item.Quantity, StockMovementKind.IN, null, request.Id, null, caller.UserId, cancellationToken);
            }
        }
        request.Status = approve ? StorageRequestStatus.VALIDATED : StorageRequestStatus.REFUSED;
        request.DecidedAt = clock.UtcNow;
        request.DecidedById = caller.UserId;
        request.RefusalReason = approve ? null : input.RefusalReason;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await StorageRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
