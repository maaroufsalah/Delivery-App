using Delivery.Api.Auth;
using Delivery.Application.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>`DashboardController` : le tableau de bord de pilotage, réservé au personnel.</summary>
[Route("dashboard")]
[Guards(Guard.NotMerchant, Guard.Staff)]
public sealed class DashboardController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Get([FromQuery] DashboardQueryParams query) => Ok(new GetDashboardQuery(query.Filters));

    [HttpGet("series")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Series([FromQuery] DashboardQueryParams query) => Ok(new GetDashboardSeriesQuery(query.Filters, query.Range));

    [HttpGet("couriers")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Couriers([FromQuery] DashboardQueryParams query) =>
        Ok(new GetDashboardCouriersQuery(query.Filters, query.Page, query.PageSize, query.Sort));

    [HttpGet("cities")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Cities([FromQuery] DashboardQueryParams query) => Ok(new GetDashboardCitiesQuery(query.Filters, query.Page, query.PageSize, query.Sort));

    [HttpGet("clients")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Clients([FromQuery] DashboardQueryParams query) => Ok(new GetDashboardClientsQuery(query.Filters, query.Page, query.PageSize, query.Rank));

    [HttpGet("clients/{id}")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> ClientDetail(string id, [FromQuery] DashboardQueryParams query) => Ok(new GetDashboardClientDetailQuery(id, query.Filters));

    [HttpGet("finance")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Finance([FromQuery] DashboardQueryParams query) => Ok(new GetDashboardFinanceQuery(query.Filters));

    [HttpGet("routes")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Routes([FromQuery] DashboardQueryParams query) => Ok(new GetDashboardRoutesQuery(query.Filters));

    [HttpGet("delays")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Delays([FromQuery] DashboardQueryParams query) => Ok(new GetDashboardDelaysQuery(query.Filters));

    [HttpGet("alerts")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Alerts([FromQuery] DashboardQueryParams query) => Ok(new GetDashboardAlertsQuery(query.Filters));

    [HttpGet("export")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Export([FromQuery] DashboardQueryParams query) =>
        FileOf(new ExportDashboardQuery(query.Filters, query.Section, query.Format, query.Sort));
}
