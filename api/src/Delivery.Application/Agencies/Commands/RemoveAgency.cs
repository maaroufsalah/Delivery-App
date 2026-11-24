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

namespace Delivery.Application.Agencies.Commands;

/// <summary>`DELETE /agencies/:id` : le siège ne se supprime pas.</summary>
public sealed record RemoveAgencyCommand(string Id) : IRequest<IdResult>;

internal sealed class RemoveAgencyHandler(IAppDbContext db, IClock clock) : IRequestHandler<RemoveAgencyCommand, IdResult>
{
    public async Task<IdResult> Handle(RemoveAgencyCommand request, CancellationToken cancellationToken)
    {
        var id = request.Id;
        var agency = await db.Agencies.FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Agence introuvable.");
        if (id == await AgencyRows.HeadquartersIdAsync(db, cancellationToken)) throw BusinessException.Conflict("Le siège ne se supprime pas.", new { field = "id" });
        References.AssertDeletable($"l’agence « {agency.Name} »",
            References.Of("users", await db.Users.CountAsync(u => u.AgencyId == id && u.DeletedAt == null, cancellationToken)),
            References.Of("clients", await db.Clients.CountAsync(c => c.AgencyId == id && c.DeletedAt == null, cancellationToken)),
            References.Of("couriers", await db.Couriers.CountAsync(c => c.AgencyId == id && c.DeletedAt == null, cancellationToken)),
            References.Of("parcels", await db.Parcels.CountAsync(p => p.AgencyId == id, cancellationToken)));
        agency.DeletedAt = clock.UtcNow;
        agency.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return new IdResult(id);
    }
}
