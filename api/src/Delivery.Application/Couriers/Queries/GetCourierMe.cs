using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Couriers.Queries;

/// <summary>`CourierMe` : le profil du livreur connecté.</summary>
public sealed record CourierMeDto(string Id, string FirstName, string LastName, string Phone, string VehicleType, string CityId, string CityName, string? AgencyId, string? AgencyName,
    string PayModel, string SettlementMode);

/// <summary>`GET /courier/me` : agence (celle qui recevra ses retours), ville, régime de paie.</summary>
public sealed record GetCourierMeQuery : IRequest<CourierMeDto>;

internal sealed class GetCourierMeHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierMeQuery, CourierMeDto>
{
    public async Task<CourierMeDto> Handle(GetCourierMeQuery request, CancellationToken cancellationToken)
    {
        var courier = await db.Couriers.AsNoTracking().Where(c => c.Id == caller.CourierId && c.DeletedAt == null)
            .Select(c => new { c.Id, c.FirstName, c.LastName, c.Phone, c.VehicleType, c.CityId, CityName = c.City.Name, c.AgencyId, AgencyName = c.Agency != null ? c.Agency.Name : null, c.PayModel, c.SettlementMode })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound("Fiche livreur introuvable.");
        var agencyId = await db.AgencyForCourierAsync(courier.AgencyId, courier.CityId, cancellationToken);
        var agencyName = courier.AgencyName ?? (agencyId is null ? null : await db.Agencies.AsNoTracking().Where(a => a.Id == agencyId).Select(a => a.Name).FirstOrDefaultAsync(cancellationToken));
        return new CourierMeDto(courier.Id, courier.FirstName, courier.LastName, courier.Phone, Money.ToWire(courier.VehicleType), courier.CityId, courier.CityName, agencyId, agencyName,
            Money.ToWire(courier.PayModel), Money.ToWire(courier.SettlementMode));
    }
}
