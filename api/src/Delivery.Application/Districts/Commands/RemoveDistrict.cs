using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Districts.Commands;

/// <summary>`DELETE /districts/:id`.</summary>
public sealed record RemoveDistrictCommand(string Id) : IRequest<IdResult>;

internal sealed class RemoveDistrictHandler(IAppDbContext db, IClock clock) : IRequestHandler<RemoveDistrictCommand, IdResult>
{
    public async Task<IdResult> Handle(RemoveDistrictCommand request, CancellationToken cancellationToken)
    {
        var id = request.Id;
        var district = await db.Districts.FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Quartier introuvable.");
        References.AssertDeletable($"le quartier « {district.Name} »",
            References.Of("parcels", await db.Parcels.CountAsync(p => p.DestinationDistrictId == id, cancellationToken)),
            References.Of("pickupAddresses", await db.ClientPickupAddresses.CountAsync(a => a.DistrictId == id, cancellationToken)));
        district.DeletedAt = clock.UtcNow;
        district.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return new IdResult(id);
    }
}
