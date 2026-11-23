using System.Text.RegularExpressions;
using AutoMapper;
using Delivery.Application.Cities.Dtos;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Cities.Commands;

/// <summary>`DELETE /cities/:id` : suppression douce, refusée tant qu'une fiche la référence.</summary>
public sealed record RemoveCityCommand(string Id) : IRequest<Users.Dtos.IdResult>;

internal sealed class RemoveCityHandler(IAppDbContext db, IClock clock) : IRequestHandler<RemoveCityCommand, Users.Dtos.IdResult>
{
    public async Task<Users.Dtos.IdResult> Handle(RemoveCityCommand request, CancellationToken cancellationToken)
    {
        var id = request.Id;
        var city = await db.Cities.FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Ville introuvable.");
        var parcels = (await CityRows.ParcelCountsAsync(db, [id], cancellationToken)).GetValueOrDefault(id);
        References.AssertDeletable($"la ville « {city.Name} »",
            References.Of("parcels", parcels),
            References.Of("districts", await db.Districts.CountAsync(d => d.CityId == id && d.DeletedAt == null, cancellationToken)),
            References.Of("agencies", await db.Agencies.CountAsync(a => a.CityId == id && a.DeletedAt == null, cancellationToken)),
            References.Of("clients", await db.Clients.CountAsync(c => c.CityId == id && c.DeletedAt == null, cancellationToken)),
            References.Of("couriers", await db.Couriers.CountAsync(c => c.CityId == id && c.DeletedAt == null, cancellationToken)),
            References.Of("pricingRules", await db.PricingRules.CountAsync(r => r.DeletedAt == null && (r.OriginCityId == id || r.DestinationCityId == id), cancellationToken)),
            References.Of("clientDiscounts", await db.ClientDiscountCities.CountAsync(d => d.CityId == id, cancellationToken)));
        city.DeletedAt = clock.UtcNow;
        city.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return new Users.Dtos.IdResult(id);
    }
}
