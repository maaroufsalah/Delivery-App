using Delivery.Application.Common.Interfaces;
using MediatR;

namespace Delivery.Application.Claims;

/// <summary>`detail` : le fil complet ; les notes internes et les transitions restent au personnel.</summary>
public sealed record GetClaimQuery(string Code) : IRequest<ClaimDetailDto>;

internal sealed class GetClaimHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetClaimQuery, ClaimDetailDto>
{
    public Task<ClaimDetailDto> Handle(GetClaimQuery input, CancellationToken cancellationToken) => ClaimRows.DetailAsync(db, caller, clock, input.Code, cancellationToken);
}
