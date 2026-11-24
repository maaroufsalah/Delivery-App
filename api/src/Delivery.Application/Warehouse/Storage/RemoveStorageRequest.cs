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

/// <summary>`DELETE /storage-requests/:code` : seulement en attente.</summary>
public sealed record RemoveStorageRequestCommand(string Code) : IRequest<StorageCodeDto>;

public sealed record StorageCodeDto(string Code);

internal sealed class RemoveStorageRequestHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveStorageRequestCommand, StorageCodeDto>
{
    public async Task<StorageCodeDto> Handle(RemoveStorageRequestCommand request, CancellationToken cancellationToken)
    {
        var row = await WarehouseScope.Storage(db.StorageRequests, caller).Include(r => r.Items).FirstOrDefaultAsync(r => r.Code == request.Code, cancellationToken)
                  ?? throw BusinessException.NotFound($"Demande introuvable : {request.Code}");
        if (row.Status != StorageRequestStatus.PENDING) throw BusinessException.Conflict("Une demande tranchée ne se supprime pas.");
        db.StorageRequestItems.RemoveRange(row.Items);
        db.StorageRequests.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return new StorageCodeDto(request.Code);
    }
}
