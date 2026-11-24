using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Claims;

/// <summary>`types` : le référentiel des types, actifs ou non, par ordre puis libellé.</summary>
public sealed record ListClaimTypesQuery : IRequest<IReadOnlyList<ClaimTypeDto>>;

internal sealed class ListClaimTypesHandler(IAppDbContext db) : IRequestHandler<ListClaimTypesQuery, IReadOnlyList<ClaimTypeDto>>
{
    public async Task<IReadOnlyList<ClaimTypeDto>> Handle(ListClaimTypesQuery input, CancellationToken cancellationToken)
    {
        var rows = await db.ClaimTypes.AsNoTracking().Where(t => t.DeletedAt == null).OrderBy(t => t.SortOrder).ThenBy(t => t.Label).ToListAsync(cancellationToken);
        return rows.Select(ClaimTypeFields.ToDto).ToList();
    }
}

internal static class ClaimTypeFields
{
    public static ClaimTypeDto ToDto(Domain.Entities.ClaimType t) => new(t.Id, t.Code, t.Label, Money.ToWire(t.SubjectType), t.SortOrder, t.IsActive);
}
