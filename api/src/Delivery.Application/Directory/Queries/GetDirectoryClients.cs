using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Directory.Queries;

/// <summary>`GET /directory/clients` : le vendeur se voit seul, l'agent voit son agence.</summary>
public sealed record GetDirectoryClientsQuery : IRequest<IReadOnlyList<DirectoryClientDto>>;

public sealed record DirectoryClientDto(
    string Id, string Name, string Type, string? TaxId, string Phone, string Email, string CityId, string? AgencyId, bool IsActive, string BillingType, bool IsPro, DateTime CreatedAt);

internal sealed class GetDirectoryClientsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetDirectoryClientsQuery, IReadOnlyList<DirectoryClientDto>>
{
    public async Task<IReadOnlyList<DirectoryClientDto>> Handle(GetDirectoryClientsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Clients.AsNoTracking().Where(c => c.DeletedAt == null);
        if (!caller.IsSuperAdmin())
        {
            if (caller.ClientId is { } clientId) query = query.Where(c => c.Id == clientId);
            else if (caller.AgencyId is { } agencyId) query = query.Where(c => c.AgencyId == agencyId);
        }
        var rows = await query.OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Type, c.TaxId, c.Phone, c.Email, c.CityId, c.AgencyId, c.IsActive, c.BillingType, c.IsPro, c.CreatedAt })
            .ToListAsync(cancellationToken);
        return rows.Select(c => new DirectoryClientDto(
            c.Id, c.Name, Money.ToWire(c.Type), c.TaxId, c.Phone, c.Email, c.CityId, c.AgencyId, c.IsActive, Money.ToWire(c.BillingType), c.IsPro, c.CreatedAt)).ToList();
    }
}
