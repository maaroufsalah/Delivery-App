using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Claims;

/// <summary>`counters` : ouvertes, en cours, en retard (dans sa portée).</summary>
public sealed record GetClaimCountersQuery : IRequest<ClaimCountersDto>;

internal sealed class GetClaimCountersHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetClaimCountersQuery, ClaimCountersDto>
{
    public async Task<ClaimCountersDto> Handle(GetClaimCountersQuery input, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var claims = ClaimRows.Scoped(db.Claims.AsNoTracking(), caller);
        var open = await claims.CountAsync(c => c.Status == ClaimStatus.OPEN, cancellationToken);
        var inProgress = await claims.CountAsync(c => c.Status == ClaimStatus.IN_PROGRESS, cancellationToken);
        var late = await claims.CountAsync(c => (c.Status == ClaimStatus.OPEN || c.Status == ClaimStatus.IN_PROGRESS) && c.DueAt != null && c.DueAt < now, cancellationToken);
        return new ClaimCountersDto(open, inProgress, late);
    }
}
