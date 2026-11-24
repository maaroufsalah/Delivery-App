using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Users.Dtos;
using Delivery.Application.Zones.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Zones.Commands;

/// <summary>`DELETE /zones/:id`.</summary>
public sealed record RemoveZoneCommand(string Id) : IRequest<IdResult>;

internal sealed class RemoveZoneHandler(IAppDbContext db, IClock clock) : IRequestHandler<RemoveZoneCommand, IdResult>
{
    public async Task<IdResult> Handle(RemoveZoneCommand request, CancellationToken cancellationToken)
    {
        var id = request.Id;
        var zone = await db.Zones.FirstOrDefaultAsync(z => z.Id == id && z.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Zone introuvable.");
        References.AssertDeletable($"la zone « {zone.Name} »",
            References.Of("cities", await db.Cities.CountAsync(c => c.ZoneId == id && c.DeletedAt == null, cancellationToken)),
            References.Of("zoneRates", await db.ZoneRates.CountAsync(r => r.FromZoneId == id || r.ToZoneId == id, cancellationToken)),
            References.Of("clientDiscounts", await db.ClientDiscounts.CountAsync(d => d.ZoneIds.Contains(id), cancellationToken)));
        zone.DeletedAt = clock.UtcNow;
        zone.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return new IdResult(id);
    }
}
