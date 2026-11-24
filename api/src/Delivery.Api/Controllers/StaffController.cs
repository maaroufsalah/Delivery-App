using Delivery.Api.Auth;
using Delivery.Application.Staff;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("staff/me")]
[Guards(Guard.Staff)]
public sealed class StaffController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet("alerts")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Alerts([FromQuery] string? agencyId) => Ok(new GetStaffAlertsQuery(agencyId));
}
