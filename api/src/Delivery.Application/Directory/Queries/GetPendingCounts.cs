using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Directory.Queries;

/// <summary>`GET /directory/pending-counts` : inscriptions en attente de validation.</summary>
public sealed record GetPendingCountsQuery : IRequest<PendingCountsDto>;

public sealed record PendingCountsDto(int Clients, int Couriers);

internal sealed class GetPendingCountsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetPendingCountsQuery, PendingCountsDto>
{
    public async Task<PendingCountsDto> Handle(GetPendingCountsQuery request, CancellationToken cancellationToken)
    {
        if (caller.ClientId is not null || caller.CourierId is not null) return new PendingCountsDto(0, 0);
        var clients = await db.Clients.CountAsync(c => c.DeletedAt == null && c.ValidatedAt == null && c.RejectedAt == null, cancellationToken);
        var couriers = await db.Couriers.CountAsync(c => c.DeletedAt == null && c.ValidatedAt == null && c.RejectedAt == null, cancellationToken);
        return new PendingCountsDto(clients, couriers);
    }
}
