using Delivery.Application.Common.Interfaces;
using MediatR;

namespace Delivery.Application.Dashboard;

/// <summary>`GET /dashboard/clients` : top clients par volume, COD ou taux de réussite, et le plus actif.</summary>
public sealed record GetDashboardClientsQuery(DashboardFilters Filters, string? Page, string? PageSize, string? Rank) : IRequest<DashboardClientsDto>, IDashboardRequest;

public sealed class GetDashboardClientsValidator : DashboardValidator<GetDashboardClientsQuery>
{
    public GetDashboardClientsValidator()
    {
        Paged(x => x.Page, x => x.PageSize);
        OneOfRule(x => x.Rank, "rank", DashboardOptions.ClientRanks);
    }
}

internal sealed class GetDashboardClientsHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetDashboardClientsQuery, DashboardClientsDto>
{
    public async Task<DashboardClientsDto> Handle(GetDashboardClientsQuery request, CancellationToken cancellationToken)
    {
        var ctx = await DashboardScope.ResolveAsync(db, caller, clock, request.Filters, cancellationToken);
        return await DashboardRankings.ClientsAsync(db, ctx, DashboardPaging.Value(request.Page, 1), DashboardPaging.Value(request.PageSize, 10), request.Rank ?? "volume",
            cancellationToken);
    }
}
