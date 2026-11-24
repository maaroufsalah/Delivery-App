using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using MediatR;

namespace Delivery.Application.Dashboard;

/// <summary>`GET /dashboard/cities` : performance par ville de destination, classement paginé.</summary>
public sealed record GetDashboardCitiesQuery(DashboardFilters Filters, string? Page, string? PageSize, string? Sort) : IRequest<Paginated<DashboardCityRowDto>>, IDashboardRequest;

public sealed class GetDashboardCitiesValidator : DashboardValidator<GetDashboardCitiesQuery>
{
    public GetDashboardCitiesValidator()
    {
        Paged(x => x.Page, x => x.PageSize);
        OneOfRule(x => x.Sort, "sort", DashboardOptions.CitySorts);
    }
}

internal sealed class GetDashboardCitiesHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetDashboardCitiesQuery, Paginated<DashboardCityRowDto>>
{
    public async Task<Paginated<DashboardCityRowDto>> Handle(GetDashboardCitiesQuery request, CancellationToken cancellationToken)
    {
        var ctx = await DashboardScope.ResolveAsync(db, caller, clock, request.Filters, cancellationToken);
        return await DashboardRankings.CitiesAsync(db, ctx, DashboardPaging.Value(request.Page, 1), DashboardPaging.Value(request.PageSize, 10), request.Sort ?? "total",
            cancellationToken);
    }
}
