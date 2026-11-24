using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using MediatR;

namespace Delivery.Application.Dashboard;

/// <summary>`GET /dashboard/series` : livrés, retours, en cours, échecs sur 7 j, 30 j (par jour) ou 12 mois (par mois), jusqu'à la fin de la période.</summary>
public sealed record GetDashboardSeriesQuery(DashboardFilters Filters, string? Range) : IRequest<DashboardSeriesDto>, IDashboardRequest;

public sealed class GetDashboardSeriesValidator : DashboardValidator<GetDashboardSeriesQuery>
{
    public GetDashboardSeriesValidator()
    {
        OneOfRule(x => x.Range, "range", DashboardOptions.SeriesRanges);
    }
}

internal sealed class GetDashboardSeriesHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetDashboardSeriesQuery, DashboardSeriesDto>
{
    public async Task<DashboardSeriesDto> Handle(GetDashboardSeriesQuery request, CancellationToken cancellationToken)
    {
        var ctx = await DashboardScope.ResolveAsync(db, caller, clock, request.Filters, cancellationToken);
        var range = request.Range ?? "30d";
        var to = ctx.Win.To;
        var (from, granularity) = range switch
        {
            "7d" => (Periods.AddDays(to, -6), "day"),
            "30d" => (Periods.AddDays(to, -29), "day"),
            _ => (Periods.ShiftMonths(Periods.FirstOfMonth(to), -11), "month"),
        };
        var sub = new PeriodWindow("custom", from, to, granularity, Periods.ZonedDayStart(from, ctx.Zone), Periods.ZonedDayStart(Periods.AddDays(to, 1), ctx.Zone));
        var points = await DashboardSql.PointsForAsync(db, ctx.Population.Within(sub.Start, sub.End), sub, ctx.Zone, cancellationToken);
        return new DashboardSeriesDto(range, granularity, from, to, sub.Start, sub.End, points);
    }
}
