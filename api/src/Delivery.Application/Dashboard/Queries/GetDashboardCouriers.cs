using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using MediatR;

namespace Delivery.Application.Dashboard;

/// <summary>`GET /dashboard/couriers` : performance des livreurs, classement paginé trié en base.</summary>
public sealed record GetDashboardCouriersQuery(DashboardFilters Filters, string? Page, string? PageSize, string? Sort) : IRequest<Paginated<DashboardCourierRowDto>>, IDashboardRequest;

public sealed class GetDashboardCouriersValidator : DashboardValidator<GetDashboardCouriersQuery>
{
    public GetDashboardCouriersValidator()
    {
        Paged(x => x.Page, x => x.PageSize);
        OneOfRule(x => x.Sort, "sort", DashboardOptions.CourierSorts);
    }
}

internal sealed class GetDashboardCouriersHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetDashboardCouriersQuery, Paginated<DashboardCourierRowDto>>
{
    public async Task<Paginated<DashboardCourierRowDto>> Handle(GetDashboardCouriersQuery request, CancellationToken cancellationToken)
    {
        var ctx = await DashboardScope.ResolveAsync(db, caller, clock, request.Filters, cancellationToken);
        return await DashboardRankings.CouriersAsync(db, ctx, DashboardPaging.Value(request.Page, 1), DashboardPaging.Value(request.PageSize, 10), request.Sort ?? "delivered",
            cancellationToken);
    }
}
