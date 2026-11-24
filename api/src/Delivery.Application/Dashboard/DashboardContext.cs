using System.Globalization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Dashboard;

/// <summary>`ParcelPopulation` : la portée, les filtres et (facultatif) la fenêtre de création des colis.</summary>
public sealed record ParcelPopulation(bool DenyAll, string? AgencyId, string? ClientId, string? CourierId, string? DestinationCityId, DateTime? Start, DateTime? End)
{
    /// <summary>La même population, sans borne de date (« l'état », pas la période).</summary>
    public ParcelPopulation Unbounded() => this with { Start = null, End = null };

    public ParcelPopulation Within(DateTime start, DateTime end) => this with { Start = start, End = end };
}

/// <summary>`DashboardContext` : fenêtre, fenêtre précédente et populations.</summary>
public sealed record DashboardContext(
    TimeZoneInfo Zone, PeriodWindow Win, PeriodWindow? Prev, ParcelPopulation Base, ParcelPopulation Population, ParcelPopulation? PreviousPopulation, bool SuperAdmin,
    string? ScopeAgencyId)
{
    /// <summary>`isSuperAdmin(scope) ? pop.agencyId : (scope.agencyId ?? pop.agencyId)` (circuits, grand livre).</summary>
    public string? CourierAgency => SuperAdmin ? Population.AgencyId : ScopeAgencyId ?? Population.AgencyId;

    /// <summary>`pop.agencyId`, écrasé par l'agence de l'agent (règlements, OV, RF-).</summary>
    public string? OwnAgency => !SuperAdmin && ScopeAgencyId is not null ? ScopeAgencyId : Population.AgencyId;

    public DashboardWindowDto WindowDto() => DashboardScope.WindowOf(Win);
}

/// <summary>`DashboardService.resolve` / `where` : la portée de l'appelant et la fenêtre demandée.</summary>
public static class DashboardScope
{
    public static async Task<DashboardContext> ResolveAsync(IAppDbContext db, ICurrentUser caller, IClock clock, DashboardFilters raw, CancellationToken cancellationToken)
    {
        var filters = raw.Clean();
        var superAdmin = caller.IsSuperAdmin();
        var population = PopulationOf(caller, filters, superAdmin);
        var timeZone = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => c.Timezone).FirstOrDefaultAsync(cancellationToken);
        var zone = Periods.Zone(string.IsNullOrEmpty(timeZone) ? Periods.DefaultTimeZone : timeZone);
        var firstDataAt = await Parcels(db, population).OrderBy(p => p.CreatedAt).Select(p => (DateTime?)p.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var win = Periods.Resolve(filters.Period!, JsDay(filters.From, "from"), JsDay(filters.To, "to"), zone, firstDataAt, clock.UtcNow);
        var prev = Periods.Previous(win, zone);
        return new DashboardContext(zone, win, prev, population, population.Within(win.Start, win.End), prev is null ? null : population.Within(prev.Start, prev.End), superAdmin,
            caller.AgencyId);
    }

    /// <summary>`where` puis `populationOf` : `scopeWhere`, puis les filtres que la portée autorise.</summary>
    private static ParcelPopulation PopulationOf(ICurrentUser caller, DashboardFilters filters, bool superAdmin)
    {
        string? agency = null, client = null, courier = null;
        if (!superAdmin)
        {
            if (caller.ClientId is { } ownClient) client = ownClient;
            else if (caller.CourierId is { } ownCourier) courier = ownCourier;
            else if (caller.AgencyId is { } ownAgency) agency = ownAgency;
            else return new ParcelPopulation(true, null, null, null, null, null, null);
        }

        if (filters.AgencyId is not null && (superAdmin || filters.AgencyId == caller.AgencyId)) agency = filters.AgencyId;
        if (filters.CourierId is not null && caller.MayFilterBy(caller.CourierId)) courier = filters.CourierId;
        if (filters.ClientId is not null && caller.MayFilterBy(caller.ClientId)) client = filters.ClientId;
        return new ParcelPopulation(false, agency, client, courier, filters.CityId, null, null);
    }

    /// <summary>La population en LINQ (comptages, derniers colis).</summary>
    public static IQueryable<Parcel> Parcels(IAppDbContext db, ParcelPopulation pop)
    {
        var parcels = db.Parcels.AsNoTracking();
        if (pop.DenyAll) return parcels.Where(p => p.Id == Scope.DenyAllId);
        parcels = parcels.Where(p => p.DeletedAt == null);
        if (pop.AgencyId is { } agency) parcels = parcels.Where(p => p.AgencyId == agency);
        if (pop.ClientId is { } client) parcels = parcels.Where(p => p.ClientId == client);
        if (pop.CourierId is { } courier) parcels = parcels.Where(p => p.CourierId == courier);
        if (pop.DestinationCityId is { } city) parcels = parcels.Where(p => p.DestinationCityId == city);
        if (pop.Start is { } start) parcels = parcels.Where(p => p.CreatedAt >= start);
        if (pop.End is { } end) parcels = parcels.Where(p => p.CreatedAt < end);
        return parcels;
    }

    public static DashboardWindowDto WindowOf(PeriodWindow win) => new(win.Period, win.From, win.To, win.Granularity, win.Start, win.End);

    /// <summary>`counts` : les familles du tableau de bord sur des paires (statut, nombre).</summary>
    public static DashboardCountsDto Counts(IReadOnlyCollection<(int StatusId, int Count)> rows)
    {
        int SumOf(IReadOnlyCollection<int> ids) => rows.Where(r => ids.Contains(r.StatusId)).Sum(r => r.Count);
        var delivered = SumOf([DashboardSql.Delivered]);
        var returned = SumOf(Domain.Parcels.StatusCatalog.DashboardReturnStatusIds);
        var closedOrReturned = delivered + returned;
        return new DashboardCountsDto(rows.Sum(r => r.Count), delivered, SumOf(Domain.Parcels.StatusCatalog.DashboardInProgressStatusIds),
            SumOf(Domain.Parcels.StatusCatalog.DashboardInTransitStatusIds), returned, SumOf(Domain.Parcels.StatusCatalog.DashboardFailedStatusIds),
            SumOf(Domain.Parcels.StatusCatalog.DashboardClosedStatusIds), Ratio(delivered, closedOrReturned), Ratio(returned, closedOrReturned));
    }

    /// <summary>`ratio` : <c>null</c> sur un dénominateur nul.</summary>
    public static double? Ratio(double numerator, double denominator) => denominator == 0 ? null : numerator / denominator;

    /// <summary>
    /// Speedy lit `AAAA-MM-JJ` avec `Date.UTC`, qui « déborde » sans erreur (`2026-02-30` → `2026-03-02`) ;
    /// .NET refuserait la date. On la normalise de la même façon avant de résoudre la fenêtre.
    /// </summary>
    private static string? JsDay(string? day, string path)
    {
        if (day is null) return null;
        try
        {
            var year = int.Parse(day[..4], CultureInfo.InvariantCulture);
            var month = int.Parse(day[5..7], CultureInfo.InvariantCulture);
            var date = int.Parse(day[8..10], CultureInfo.InvariantCulture);
            return new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(month - 1).AddDays(date - 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path, message = "invalid-date" } } });
        }
    }
}
